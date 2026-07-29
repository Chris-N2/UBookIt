using UBookIt.Core.Availability;
using UBookIt.Core.Common;
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
    private static readonly DateOnly Date = TestData.BaseDate;

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

    [Fact]
    public void Build_with_slots_reports_times_and_min_duration()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);
        IReadOnlyList<Slot> slots =
        [
            new Slot(TestData.Utc(Date, "09:00"), TimeSpan.FromMinutes(30)),
            new Slot(TestData.Utc(Date, "09:30"), TimeSpan.FromMinutes(30)),
        ];

        var model = BookingFormBuilder.Build(room, Date, today, slots, TestData.London);

        Assert.True(model.HasTimes);
        Assert.Equal(2, model.Times.Count);
        Assert.Equal(30, model.DurationMinutes);           // resource min duration
        Assert.Equal(today, model.MinDate);
        Assert.Equal(today.AddDays(90), model.MaxDate);    // default horizon
    }

    [Fact]
    public void Build_with_no_slots_reports_the_no_times_state()
    {
        var room = TestData.Room();
        var today = BookingFormBuilder.TodayIn(TestData.Now, TestData.London);

        var model = BookingFormBuilder.Build(room, Date, today, [], TestData.London);

        Assert.False(model.HasTimes);
        Assert.Empty(model.Times);
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
            SelectedTimeIso = TestData.Utc(Date, "09:00").ToString("O"),
            Name = "Ada",
            Email = "not-an-email",
            Phone = "01234",
            Errors = [new BookingError("Please enter a valid email address.", BookingFieldIds.Email)],
        };

        var model = BookingFormBuilder.Build(room, Date, today, [], TestData.London, failed);

        Assert.Equal("Ada", model.Name);
        Assert.Equal("not-an-email", model.Email);
        Assert.Equal("01234", model.Phone);
        Assert.Equal(failed.SelectedTimeIso, model.SelectedTimeIso);
        Assert.True(model.HasErrors);
        Assert.Equal("Please enter a valid email address.", model.ErrorFor(BookingFieldIds.Email));
    }
}
