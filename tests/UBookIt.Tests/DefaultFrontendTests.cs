using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// Host-independent pieces of the default front-end: the failure-code → message
/// map, the view-model assembly (site-zone display + exact-instant values), and
/// failed-submission repopulation. The rendered markup, anti-forgery, and PRG
/// behaviour are verified live against the TestSite.
/// </summary>
public class DefaultFrontendTests
{

    /// <summary>The default install: no retention period configured, no policy link.</summary>
    /// <remarks>
    /// Named rather than inlined so that every existing test says which configuration it is
    /// building a form for. These tests are about times and lengths, not about the notice —
    /// but "the notice is absent from my reasoning" and "the notice is in its default state"
    /// are different claims, and only one of them is true here.
    /// </remarks>
    private static readonly PrivacyNoticeView NoRetention = new(null, null);
    private static readonly DateOnly Date = TestData.BaseDate;

    // --- which unavailable answer the flow gives (services spec / default-frontend) ---

    private static Resource Res(bool directlyBookable)
        => Resource.Create(
            ResourceTypes.Room,
            "Meeting Room A",
            directlyBookable: directlyBookable,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek))).Value;

    [Fact]
    public void Spec_scenario_a_withholding_resource_explains_itself()
    {
        // QA proved this decision could be reverted to the generic "try again
        // later" answer with the whole suite green — restoring exactly the
        // conflation this change exists to remove.
        Assert.True(BookingUnavailableModel.IsUnavailable(
            Res(directlyBookable: false), zoneResolved: true, out var model));

        Assert.NotNull(model);
        Assert.Equal(BookingUnavailableReason.NotOfferedIndividually, model.Reason);
    }

    [Fact]
    public void A_permitting_resource_renders_the_ordinary_flow()
    {
        // The pair that makes the assertion above non-vacuous: a function that
        // always returned the permanent answer would pass it.
        Assert.False(BookingUnavailableModel.IsUnavailable(
            Res(directlyBookable: true), zoneResolved: true, out var model));

        Assert.Null(model);
    }

    [Fact]
    public void A_fault_is_not_reported_as_a_permanent_answer()
    {
        // A resource that could not be read, or a zone that will not resolve, is
        // a fault — "try again later" is honest for it and wrong for the other.
        // The ordering is load-bearing: a null resource must not be inspected for
        // a permission it cannot have.
        static BookingUnavailableReason ReasonFor(Resource? resource, bool zoneResolved)
        {
            Assert.True(BookingUnavailableModel.IsUnavailable(resource, zoneResolved, out var model));
            return model.Reason;
        }

        Assert.Equal(BookingUnavailableReason.Unknown, ReasonFor(null, zoneResolved: true));
        Assert.Equal(
            BookingUnavailableReason.Unknown,
            ReasonFor(Res(directlyBookable: true), zoneResolved: false));

        // Including for a resource that ALSO withholds: the fault is reported as
        // a fault, not silently upgraded to the permanent answer.
        Assert.Equal(
            BookingUnavailableReason.Unknown,
            ReasonFor(Res(directlyBookable: false), zoneResolved: false));
    }

    [Fact]
    public void A_withdrawn_permission_does_not_fall_back_to_try_again()
    {
        // The form is never rendered for a resource that withholds the
        // permission — but a visitor holding a page from before it was withdrawn
        // can still submit one. The generic fallback would tell them to try
        // again, which cannot work, so this code needs a message of its own.
        var message = BookingMessages.ForCode(FailureCodes.ResourceNotDirectlyBookable);

        Assert.NotEqual(BookingMessages.Fallback, message);
        Assert.DoesNotContain("try again", message, StringComparison.OrdinalIgnoreCase);

        // And it says the thing that is actually true and actionable.
        Assert.Contains("on its own", message, StringComparison.OrdinalIgnoreCase);
    }

    // --- 6.1 message map ---

    [Theory]
    [InlineData(FailureCodes.Conflict)]
    [InlineData(FailureCodes.OutsideOpenHours)]
    [InlineData(FailureCodes.LeadTime)]
    [InlineData(FailureCodes.Horizon)]
    [InlineData(FailureCodes.Granularity)]
    [InlineData(FailureCodes.DurationTooShort)]
    [InlineData(FailureCodes.DurationTooLong)]
    [InlineData(FailureCodes.IntervalInvalid)]
    [InlineData(FailureCodes.EmailInvalid)]
    [InlineData(FailureCodes.NameRequired)]
    [InlineData(FailureCodes.ResourceNotFound)]
    public void Mapped_codes_yield_a_specific_message(string code)
    {
        var message = BookingMessages.ForCode(code);

        Assert.False(string.IsNullOrWhiteSpace(message));
        Assert.NotEqual(BookingMessages.Fallback, message);
    }

    [Fact]
    public void Conflict_reads_as_no_longer_available()
        => Assert.Contains("no longer available", BookingMessages.ForCode(FailureCodes.Conflict));

    [Fact]
    public void Unknown_code_falls_back_safely()
        => Assert.Equal(BookingMessages.Fallback, BookingMessages.ForCode("not-a-real-code"));

    [Fact]
    public void Duplicate_failure_codes_dedupe_to_one_message()
    {
        var errors = BookingMessages.ForFailures(
        [
            new DomainFailure(FailureCodes.Conflict, "x"),
            new DomainFailure(FailureCodes.Conflict, "y"),
        ]);

        Assert.Equal(BookingMessages.ForCode(FailureCodes.Conflict), Assert.Single(errors).Message);
    }

    [Fact]
    public void Failures_carry_the_offending_field_id()
    {
        var errors = BookingMessages.ForFailures(
        [
            new DomainFailure(FailureCodes.EmailInvalid, "x", "Email"),
            new DomainFailure(FailureCodes.NameRequired, "y", "Name"),
            new DomainFailure(FailureCodes.Conflict, "z"),            // no field → the time selection
        ]);

        Assert.Equal(BookingFieldIds.Email, errors.Single(e => e.Message == BookingMessages.ForCode(FailureCodes.EmailInvalid)).FieldId);
        Assert.Equal(BookingFieldIds.Name, errors.Single(e => e.Message == BookingMessages.ForCode(FailureCodes.NameRequired)).FieldId);
        Assert.Equal(BookingFieldIds.Times, errors.Single(e => e.Message == BookingMessages.ForCode(FailureCodes.Conflict)).FieldId);
    }

    // --- 6.2 view-model assembly ---

    [Fact]
    public void Time_option_shows_site_zone_wall_clock_and_round_trips_the_instant()
    {
        var startUtc = TestData.Utc(Date, "09:00"); // 09:00 London wall-clock

        var option = BookingFormBuilder.ToOption(startUtc, TestData.London);

        Assert.Equal("09:00", option.Label);
        Assert.Equal(startUtc, DateTimeOffset.Parse(option.InstantIso, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    private static IReadOnlyList<BookableStart> Starts(params (string Time, int MaxMinutes)[] entries)
        => entries
            .Select(e => new BookableStart(
                TestData.Utc(Date, e.Time), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(e.MaxMinutes)))
            .ToList();

    [Fact]
    public void Build_with_starts_reports_times_and_the_chosen_duration()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var model = BookingFormBuilder.Build(
            room, Date, today, Starts(("09:00", 60), ("09:30", 30)),
            TimeSpan.FromMinutes(30), TestData.London, NoRetention);

        Assert.True(model.HasTimes);
        Assert.Equal(2, model.Times.Count);
        Assert.Equal(30, model.DurationMinutes);           // resource min duration
        Assert.Equal(today, model.MinDate);
        Assert.Equal(today.AddDays(90), model.MaxDate);    // default horizon
    }

    [Fact]
    public void Build_with_no_starts_reports_the_no_times_state()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var model = BookingFormBuilder.Build(room, Date, today, [], TimeSpan.FromMinutes(30), TestData.London, NoRetention);

        Assert.False(model.HasTimes);
        Assert.Empty(model.Times);
        Assert.Null(model.LongestAvailableMinutes);
        Assert.False(model.LengthIsTheProblem);
    }

    // --- Visitor-chosen length ---

    [Fact]
    public void Duration_options_are_the_granularity_multiples_the_resource_permits()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(30),
                minDuration: TimeSpan.FromMinutes(30),
                maxDuration: TimeSpan.FromMinutes(120)).Value));

        Assert.Equal(new[] { 30, 60, 90, 120 }, BookingFormBuilder.DurationOptions(room).ToArray());
    }

    [Fact]
    public void An_absent_length_renders_the_resource_minimum()
    {
        var room = TestData.Room();

        Assert.Equal(
            room.Availability.Constraints.MinDuration,
            BookingFormBuilder.ResolveDisplayDuration(room, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(37)]      // not a granularity multiple
    [InlineData(100000)]  // beyond the resource maximum
    public void An_unpermitted_length_still_RENDERS_the_resource_minimum(int minutes)
    {
        // Rendering only: a hand-edited query parameter should still draw a
        // usable form. This must NOT be read as permission to substitute a
        // length when placing — see the placement tests below, and the comment
        // on ResolveDisplayDuration.
        var room = TestData.Room();

        Assert.Equal(
            room.Availability.Constraints.MinDuration,
            BookingFormBuilder.ResolveDisplayDuration(room, minutes));
    }

    [Fact]
    public void A_permitted_length_is_honoured_when_rendering()
    {
        var room = TestData.Room();

        Assert.Equal(TimeSpan.FromMinutes(60), BookingFormBuilder.ResolveDisplayDuration(room, 60));
    }

    // --- Placement must reject, never substitute (spec: "An unpermitted length
    // is rejected server-side"). The surface controller needs an Umbraco host,
    // so these assert the Core call it now makes verbatim: the submitted length
    // reaches placement unchanged and is refused.

    [Theory]
    [InlineData(37)]      // not a granularity multiple
    [InlineData(100000)]  // beyond the resource maximum
    [InlineData(15)]      // below the resource minimum
    public async Task An_unpermitted_submitted_length_is_rejected_and_places_nothing(int minutes)
    {
        var room = TestData.Room();
        var (bookings, _, store) = TestData.Services(room);

        var result = await bookings.PlaceAsync(new UBookIt.Core.Bookings.BookingRequest
        {
            ResourceId = room.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(minutes),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);

        // Nothing was written: no claim exists anywhere on that day.
        var claims = await store.GetClaimsAsync(
            room.Id, TestData.Utc(Date, "00:00"), TestData.Utc(Date, "00:00").AddDays(1));
        Assert.Empty(claims);

        // And the failure has to reach the visitor as a message, not a fallback.
        var errors = BookingMessages.ForFailures(result.Failures);
        Assert.NotEmpty(errors);
        Assert.DoesNotContain(errors, e => e.Message == BookingMessages.Fallback);
    }

    [Fact]
    public void A_missing_length_is_reported_against_the_length_control()
    {
        // A POST omitting the field binds to 0. Core can only call that
        // interval-invalid, which would point at the time list; the controller
        // reports it as a duration problem instead. Asserts the mapping the
        // controller relies on, since the controller itself needs a host.
        var errors = BookingMessages.ForFailures(
        [
            new UBookIt.Core.Common.DomainFailure(
                UBookIt.Core.Common.FailureCodes.DurationTooShort,
                "A booking length is required.",
                "DurationMinutes"),
        ]);

        Assert.Equal(BookingFieldIds.Duration, Assert.Single(errors).FieldId);
    }

    [Fact]
    public void A_duration_bound_failure_points_at_the_length_control()
    {
        var errors = BookingMessages.ForFailures(
        [
            new UBookIt.Core.Common.DomainFailure(
                UBookIt.Core.Common.FailureCodes.DurationTooLong, "too long"),
        ]);

        Assert.Equal(BookingFieldIds.Duration, Assert.Single(errors).FieldId);
    }

    [Fact]
    public void Only_starts_admitting_the_chosen_length_are_offered()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var model = BookingFormBuilder.Build(
            room, Date, today, Starts(("09:00", 120), ("11:00", 30)),
            TimeSpan.FromMinutes(60), TestData.London, NoRetention);

        var only = Assert.Single(model.Times);
        Assert.Equal(
            TestData.Utc(Date, "09:00"),
            DateTimeOffset.Parse(only.InstantIso, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    [Fact]
    public void An_unavailable_length_reports_the_longest_that_is_available()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var model = BookingFormBuilder.Build(
            room, Date, today, Starts(("09:00", 90), ("11:00", 30)),
            TimeSpan.FromMinutes(180), TestData.London, NoRetention);

        Assert.False(model.HasTimes);
        Assert.True(model.LengthIsTheProblem);
        Assert.Equal(90, model.LongestAvailableMinutes);
    }

    // --- 6.3 repopulation ---

    [Fact]
    public void Failed_submission_repopulates_input_and_errors()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);
        var failed = new FailedSubmission
        {
            Date = Date,
            DurationMinutes = 60,
            SelectedTimeIso = TestData.Utc(Date, "09:00").ToString("O"),
            Name = "Ada",
            Email = "not-an-email",
            Phone = "01234",
            Errors = [new BookingError("Please enter a valid email address.", BookingFieldIds.Email)],
        };

        var model = BookingFormBuilder.Build(
            room, Date, today, [], BookingFormBuilder.ResolveDisplayDuration(room, failed.DurationMinutes),
            TestData.London, NoRetention, failed);

        // The chosen length survives a failed submission alongside the details.
        Assert.Equal(60, model.DurationMinutes);
        Assert.Equal("Ada", model.Name);
        Assert.Equal("not-an-email", model.Email);
        Assert.Equal("01234", model.Phone);
        Assert.Equal(failed.SelectedTimeIso, model.SelectedTimeIso);
        Assert.True(model.HasErrors);
        Assert.Equal("Please enter a valid email address.", model.ErrorFor(BookingFieldIds.Email));
    }
}
