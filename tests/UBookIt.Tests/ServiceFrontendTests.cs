using System.Text.RegularExpressions;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The service booking flow, the catalogue, and the dispatcher in front of them:
/// everything the default front-end decides before a view is chosen.
/// <para>
/// The flows are exercised over the <b>real</b> Core collaborator graph rather
/// than a double standing in for resolution. A double would let a test agree with
/// itself about which resources a configuration resolves to, which is precisely
/// the thing the multi-role cases are about.
/// </para>
/// </summary>
public class ServiceFrontendTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Res(
        int id,
        string type,
        string name,
        string open = "09:00",
        string close = "17:00",
        int granularity = 30,
        int min = 30,
        int max = 480,
        params string[] capabilities)
        => Resource.Create(
            type,
            name,
            directlyBookable: true,
            capabilities: capabilities,
            availability: TestData.Config(
                TestData.Weekly(open, close, Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    private static Service Svc(string name, ServiceDuration? duration, params ServiceRole[] roles)
        => Service.Create(name, duration, roles, id: Id(900)).Value;

    private sealed record Harness(
        ServiceBookingFlow Flow,
        ServiceBookingService Core,
        BookingService Bookings,
        Service Service,
        InMemoryResourceStore Resources,
        InMemoryServiceStore ServiceStore);

    private static Harness Build(Service service, params Resource[] resources)
    {
        var serviceStore = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore();

        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var (core, bookings, _) = TestData.ServiceBookingWith(serviceStore, resourceStore);

        return new Harness(
            new ServiceBookingFlow(serviceStore, core, TestData.Settings, new FixedTimeProvider(TestData.Now)),
            core,
            bookings,
            service,
            resourceStore,
            serviceStore);
    }

    /// <summary>A room and a therapist, both able to fulfil the massage all day.</summary>
    private static Harness Massage()
        => Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"),
            Res(2, Therapist, "Jane"));

    private static BookingFlowInput On(DateOnly date, int? minutes = null, string? token = null)
        => new() { Date = date, DurationMinutes = minutes, FlowToken = token };

    private static async Task<IReadOnlyList<RoleCandidates>> PoolsOf(Harness harness)
    {
        var resolved = await harness.Core.ResolveCandidatesAsync(harness.Service.Id);
        Assert.True(resolved.Succeeded);
        return resolved.Value;
    }

    // ---------------------------------------------------------------------
    // 2.2 — written for SEVERAL resources, including the confirmation.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_multi_role_confirmation_names_every_resolved_resource()
    {
        // The failure this guards is a `.First()` on the resolved set: it passes
        // every single-role test and fails only the multi-role case the service
        // flow exists to serve. So the covering assertion is that BOTH names are
        // present — a count, or either one alone, is a different booking from the
        // one that exists.
        var harness = Massage();

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));
        Assert.Equal(2, placed.Value.Claims.Count);

        var confirmation = ServiceBookingFormBuilder.BuildConfirmation(
            placed.Value, harness.Service.Name, await NamesOf(harness, placed.Value), TestData.London);

        Assert.Equal(2, confirmation.ResourceNames.Count);
        Assert.Contains("Treatment Room", confirmation.ResourceNames);
        Assert.Contains("Jane", confirmation.ResourceNames);

        // The model carries THIS booking's reference. The rendering suite proves the view
        // prints Model.Reference rather than the Guid; it renders a fixture and so can say
        // nothing about whether the model was filled from the booking. Substituting a constant
        // here — every visitor on both flows shown the same reference — passed 1791 tests.
        Assert.Equal(placed.Value.Reference.Display, confirmation.Reference);
    }

    [Fact]
    public async Task A_single_role_confirmation_names_its_one_resource()
    {
        // The pair that makes the assertion above non-vacuous in the other
        // direction: the single-role case is the resolved set too, not a special
        // case of it, so it reports one name rather than none or a count.
        var harness = Build(
            Svc("Haircut", null, new ServiceRole(Therapist, 1)),
            Res(2, Therapist, "Jane"));

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);

        var confirmation = ServiceBookingFormBuilder.BuildConfirmation(
            placed.Value, harness.Service.Name, await NamesOf(harness, placed.Value), TestData.London);

        Assert.Equal("Jane", Assert.Single(confirmation.ResourceNames));
    }

    [Fact]
    public async Task A_claim_whose_name_cannot_be_read_is_reported_rather_than_dropped()
    {
        // Dropping it would quietly turn a two-resource booking into a
        // one-resource confirmation — the very shape the requirement forbids —
        // and would do so without any code saying "first".
        var harness = Massage();

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded);

        var confirmation = ServiceBookingFormBuilder.BuildConfirmation(
            placed.Value, harness.Service.Name, new Dictionary<Guid, string>(), TestData.London);

        Assert.Equal(placed.Value.Claims.Count, confirmation.ResourceNames.Count);
    }

    private static async Task<Dictionary<Guid, string>> NamesOf(Harness harness, Booking booking)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var claim in booking.Claims)
        {
            var resource = await harness.Resources.GetAsync(claim.ResourceId);
            names[claim.ResourceId] = resource!.DisplayName;
        }

        return names;
    }

    // ---------------------------------------------------------------------
    // 3 — refusals. The covering test is the PAIR, proved to differ.
    // ---------------------------------------------------------------------

    /// <summary>A service needing a therapist where none exists — never fulfillable.</summary>
    private static Harness Unfulfillable()
        => Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"));

    /// <summary>
    /// The deterministic sentence, which exactly one surface is allowed to say.
    /// Held as a constant so every test below asks the same question of it.
    /// </summary>
    private const string PermanentClaim = "not currently available for booking";

    [Fact]
    public async Task Every_placement_refusal_of_a_fulfillable_service_invites_another_time()
    {
        // The pair this test used to compare was the wrong pair, and QA proved it
        // live: it compared two MESSAGES and never asked what page they land on,
        // so it could not see a perfectly bookable service being told it was "not
        // currently available for booking" above its own nine bookable times.
        //
        // The real rule is that a PLACEMENT failure is an answer about one
        // instant, whatever its code — Core says so where it raises
        // `service-unavailable` — so every one of them must invite another time.
        // Both codes a fulfillable service can produce are therefore exercised
        // here, and the assertion is made of each.
        var busy = Massage();

        // Every resource able to fulfil the service is taken at 09:00, but the
        // service is fulfillable in general.
        await OccupyAsync(busy, Id(2), "09:00", 60);

        var raced = await busy.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = busy.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.Equal(FailureCodes.Conflict, raced.Failures[0].Code);

        // The case QA found. A fulfillable service, asked for a start outside its
        // opening hours: every candidate's own rules refuse, so no assignment can
        // reach the conflict check and Core reports `service-unavailable` — for a
        // service that is bookable all day.
        var outOfHours = await Massage().Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = Id(900),
            Start = TestData.Utc(Date, "03:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.Equal(FailureCodes.ServiceUnavailable, outOfHours.Failures[0].Code);

        foreach (var failure in new[] { raced, outOfHours })
        {
            var message = Rendered(failure.Failures);

            Assert.DoesNotContain(BookingMessages.Fallback, message, StringComparison.Ordinal);

            // Invites another time...
            Assert.Contains("choose another", message, StringComparison.OrdinalIgnoreCase);

            // ...and never makes the claim only the configuration-time check may.
            Assert.DoesNotContain(PermanentClaim, message, StringComparison.OrdinalIgnoreCase);

            // It is about the chosen start, so it points at the time list — where
            // the visitor can act on it.
            Assert.Equal(
                BookingFieldIds.Times,
                BookingMessages.ForFailures(failure.Failures).First().FieldId);
        }
    }

    [Fact]
    public async Task The_permanent_claim_is_made_only_by_the_configuration_time_check()
    {
        // The other half of the pair, and the one that keeps the first honest: the
        // deterministic wording must still exist somewhere, or "no message says
        // it" would be satisfied by never saying it at all.
        var outcome = await Unfulfillable().Flow.BuildAsync(Id(900), On(Date));

        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);

        // And it is the refusal page — which offers no form and no times — that
        // carries it, not a message on a form.
        Assert.Null(outcome.Form);

        var page = RepoFiles.Read(
            "src/UBookIt.Web/Views/Shared/Components/BookingFlow/ServiceUnavailable.cshtml");

        Assert.Contains(PermanentClaim, page, StringComparison.Ordinal);
    }

    [Fact]
    public void No_placement_failure_can_produce_the_permanent_claim()
    {
        // Stated over EVERY code the map knows rather than over the two a fixture
        // happens to produce. The defect QA found was a code nobody had thought to
        // provoke, so enumerating the map is the assertion that scales.
        var codes = typeof(FailureCodes)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);

        foreach (var code in codes)
        {
            Assert.DoesNotContain(PermanentClaim, BookingMessages.ForCode(code), StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(PermanentClaim, BookingMessages.Fallback, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failed_submission_against_an_unfulfillable_service_redraws_no_form()
    {
        // What makes the two shapes safe: a stale POST against a service that can
        // never be fulfilled must not come back as a form carrying an inviting
        // message. The configuration-time refusal is decided before the failed
        // submission is consulted, so the redraw is the refusal page.
        //
        // Named by a test rather than left to be inferred from the order of two
        // statements in a method, which is the kind of thing a later edit reorders
        // without noticing.
        var outcome = await Unfulfillable().Flow.BuildAsync(
            Id(900),
            new BookingFlowInput
            {
                Date = Date,
                Failed = new FailedSubmission
                {
                    Date = Date,
                    DurationMinutes = 60,
                    Errors = [new BookingError("That time is not available for this service. Please choose another.", BookingFieldIds.Times)],
                },
            });

        Assert.Null(outcome.Form);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable!.Reason);
    }

    [Fact]
    public void The_refusals_are_mapped_from_the_code_and_never_from_the_message_text()
    {
        // Two failures carrying the SAME code and wildly different domain text
        // render identically. That is the property that keeps the backoffice
        // diagnostic out of the visitor's page: the map has no way to read it.
        var plain = Rendered([new DomainFailure(FailureCodes.ServiceUnavailable, "This service cannot be booked at that time.")]);

        var diagnostic = Rendered(
        [
            new DomainFailure(
                FailureCodes.ServiceUnavailable,
                "This service needs 3 distinct resources for 'therapist' with cert-x at that time, "
                + "and only 1 can provide it then."),
        ]);

        Assert.Equal(plain, diagnostic);
    }

    [Fact]
    public async Task A_deterministic_refusal_names_no_role_type_capability_or_count()
    {
        // The tempting implementation pipes the pool-sufficiency shortfall text
        // straight through, and it reads plausibly — which is why this needs a
        // test rather than a review.
        //
        // A role needing two therapists with a capability, where one such
        // therapist exists: the shortfall is real, so the domain produces its
        // richest message and the assertion below is not vacuous.
        var harness = Build(
            Svc(
                "Couples massage",
                null,
                ServiceRole.Create(Therapist, ["cert-x"], count: 2).Value),
            Res(2, Therapist, "Jane", capabilities: "cert-x"));

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(placed.Succeeded);

        var failure = placed.Failures[0];
        Assert.Equal(FailureCodes.ServiceUnavailable, failure.Code);

        // Non-vacuity: the domain really is offering the configuration detail
        // here. Were it not, the assertions below would pass against a fixture
        // that had nothing to leak.
        Assert.Contains(Therapist, failure.Message, StringComparison.Ordinal);
        Assert.Contains("cert-x", failure.Message, StringComparison.Ordinal);
        Assert.Contains("2", failure.Message, StringComparison.Ordinal);

        var visitorFacing = Rendered(placed.Failures);

        Assert.DoesNotContain(Therapist, visitorFacing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cert-x", visitorFacing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resource", visitorFacing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capabilit", visitorFacing, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(visitorFacing, "0123456789".ToCharArray().Select(c => c.ToString()).Where(visitorFacing.Contains));
    }

    [Fact]
    public async Task The_backoffice_shortfall_report_is_unchanged()
    {
        // The visitor-facing rule is enforced at the point of rendering, never by
        // suppressing the diagnostic at its source. The person who can fix the
        // configuration still gets the role, its requirement and the arithmetic.
        var harness = Build(
            Svc("Couples massage", null, ServiceRole.Create(Therapist, ["cert-x"], count: 2).Value),
            Res(2, Therapist, "Jane", capabilities: "cert-x"));

        var shortfall = PoolSufficiency.FindShortfall(await PoolsOf(harness));

        Assert.NotNull(shortfall);
        Assert.Equal(2, shortfall.Required);
        Assert.Equal(1, shortfall.Eligible);
        Assert.Equal(Therapist, Assert.Single(shortfall.Roles).Role.ResourceType);
        Assert.Contains("cert-x", Assert.Single(shortfall.Roles).Role.RequiredCapabilities.Keys);
    }

    [Fact]
    public void The_unavailable_model_has_nowhere_to_carry_a_diagnostic()
    {
        // Structural, and deliberately so: the disclosure rule is easiest to break
        // by adding a member to the model and rendering it. This fails the moment
        // one appears, rather than when someone happens to write a message.
        var members = typeof(ServiceUnavailableModel)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .Order()
            .ToArray();

        Assert.Equal(["Reason", "ServiceName"], members);
    }

    // ---------------------------------------------------------------------
    // 3 (render time) — the same distinction, before a form is offered.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task An_unfulfillable_service_is_refused_before_a_form_is_offered()
    {
        var outcome = await Unfulfillable().Flow.BuildAsync(Id(900), On(Date));

        Assert.Null(outcome.Form);
        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);
    }

    [Fact]
    public async Task A_busy_service_still_offers_its_form_and_says_the_date_is_empty()
    {
        // The other half of the pair, at render time. A fulfillable service whose
        // resources are all taken must NOT reach the deterministic page: the
        // visitor is invited to choose another date, because another date can help.
        var harness = Massage();
        await OccupyAsync(harness, Id(2), "09:00", 480);

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.Null(outcome.Unavailable);
        Assert.NotNull(outcome.Form);
        Assert.False(outcome.Form.HasTimes);

        // And the form is still drawn, so there is a date control to change.
        Assert.NotEmpty(outcome.Form.DurationOptions);
        Assert.True(outcome.Form.MaxDate > outcome.Form.MinDate);
    }

    [Fact]
    public async Task A_service_whose_roles_share_one_resource_is_deterministically_refused()
    {
        // Two roles of one type, told apart by capability, drawing on a single
        // resource that satisfies both: each role has a candidate, and they cannot
        // be filled at once. Every start would be empty, forever, and "no times
        // available" would send the visitor back tomorrow.
        var harness = Build(
            Svc(
                "Joint session",
                null,
                ServiceRole.Create(Therapist, ["cert-x"]).Value,
                ServiceRole.Create(Therapist, ["cert-y"]).Value),
            Res(2, Therapist, "Jane", capabilities: ["cert-x", "cert-y"]));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);
    }

    [Fact]
    public async Task A_service_whose_start_grids_can_never_coincide_is_deterministically_refused()
    {
        // Grids anchored at each resource's opening time and stepped by its own
        // granularity: 09:00/30 against 09:15/20 never meet, on any day. Both
        // roles report perfectly healthy pools, and nothing else in the product
        // would say why the service is permanently empty.
        var harness = Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", open: "09:00", granularity: 30, min: 30, max: 120),
            Res(2, Therapist, "Jane", open: "09:15", granularity: 20, min: 20, max: 120));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);
    }

    [Fact]
    public async Task A_service_no_single_length_can_satisfy_is_deterministically_refused()
    {
        // Each role is healthy on its own and their start grids do coincide, so
        // neither the sufficiency check nor the alignment check has anything to
        // say — but the room can only be booked for 30 minutes and the therapist
        // only for 20, so no length exists that both can provide. Core answers
        // with no starts, on every date, forever.
        //
        // Found by mutation: without the length clause this configuration renders
        // "no times are available, please choose another date" for all eternity,
        // which is exactly what the requirement exists to prevent.
        var harness = Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", granularity: 30, min: 30, max: 30),
            Res(2, Therapist, "Jane", granularity: 20, min: 20, max: 20));

        // Non-vacuity: the other two deterministic checks really are silent here,
        // so this test cannot be passing for the wrong reason.
        var pools = await PoolsOf(harness);
        Assert.Null(PoolSufficiency.FindShortfall(pools));
        Assert.Null(StartAlignment.FindMisalignment(pools));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.Null(outcome.Form);
        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);
    }

    [Fact]
    public async Task A_fault_is_not_reported_as_a_permanent_answer()
    {
        // A service that could not be read is a fault; "try again later" is honest
        // for it and wrong for the other. Ordering is load-bearing.
        var outcome = await Massage().Flow.BuildAsync(Id(404), On(Date));

        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.Unknown, outcome.Unavailable.Reason);
    }

    [Fact]
    public void A_fulfillable_service_is_not_refused()
    {
        // The pair that makes every deterministic assertion above non-vacuous: a
        // predicate that always answered "not fulfillable" would pass them all.
        var pools = FulfillablePools();

        Assert.False(ServiceUnavailableModel.IsUnavailable(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            pools,
            zoneResolved: true,
            out var model));

        Assert.Null(model);
    }

    private static IReadOnlyList<RoleCandidates> FulfillablePools()
    {
        var harness = Massage();
        return PoolsOf(harness).GetAwaiter().GetResult();
    }

    // ---------------------------------------------------------------------
    // 2 — the flow itself.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Choosing_a_date_reveals_the_services_times()
    {
        var outcome = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.HasTimes);
        Assert.Equal(60, outcome.Form.DurationMinutes);
        Assert.Equal("Massage", outcome.Form.ServiceName);
    }

    [Fact]
    public async Task The_times_offered_are_the_services_not_one_resources()
    {
        // Rooms are free all day; the therapist only until 11:00. Starts at which
        // a room is free but no therapist is must not be offered — a flow reading
        // one role's availability would offer them.
        var harness = Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", open: "09:00", close: "17:00"),
            Res(2, Therapist, "Jane", open: "09:00", close: "11:00"));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.HasTimes);

        var offered = outcome.Form.Times.Select(t => t.Label).ToList();

        Assert.Contains("09:00", offered);

        // The room is free then and the therapist is not.
        Assert.DoesNotContain("11:00", offered);
        Assert.DoesNotContain("14:00", offered);
    }

    [Fact]
    public async Task An_empty_date_says_so_explicitly_rather_than_rendering_a_blank_list()
    {
        var harness = Massage();
        await OccupyAsync(harness, Id(1), "09:00", 480);

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.False(outcome.Form.HasTimes);

        // Nothing at all is available, so this is the plain empty state rather
        // than the "not this long" explanation.
        Assert.Null(outcome.Form.LongestAvailableMinutes);
        Assert.False(outcome.Form.LengthIsTheProblem);
    }

    [Fact]
    public async Task A_length_with_no_times_explains_the_longest_that_is_available()
    {
        var harness = Build(
            Svc("Massage", null, new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", open: "09:00", close: "10:00", max: 60),
            Res(2, Therapist, "Jane", open: "09:00", close: "10:00", max: 60));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.HasTimes);
        Assert.Equal(60, outcome.Form.LongestAvailableMinutes);
    }

    // --- 2.3 length control ---

    [Fact]
    public async Task A_variable_service_offers_the_lengths_every_role_can_provide()
    {
        // The room tops out at 60 minutes and the therapist at 120: the offered
        // set is the intersection, not either role's own range.
        var harness = Build(
            Svc("Massage", ServiceDuration.Variable(Mins(30), Mins(240)).Value,
                new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", min: 30, max: 60),
            Res(2, Therapist, "Jane", min: 30, max: 120));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.NotNull(outcome.Form);
        Assert.Equal([30, 60], outcome.Form.DurationOptions);
        Assert.False(outcome.Form.LengthIsFixed);
    }

    [Fact]
    public async Task A_fixed_service_states_its_length_rather_than_offering_a_choice()
    {
        var harness = Build(
            Svc("Massage", ServiceDuration.Fixed(Mins(60)).Value,
                new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"),
            Res(2, Therapist, "Jane"));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date));

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.LengthIsFixed);
        Assert.Equal(60, outcome.Form.DurationMinutes);
        Assert.Equal([60], outcome.Form.DurationOptions);
    }

    [Fact]
    public async Task A_hand_edited_length_still_renders_a_usable_form()
    {
        // Rendering only. This must NOT be read as permission to substitute a
        // length when placing: submissions carry their length to Core unchanged.
        var outcome = await Massage().Flow.BuildAsync(Id(900), On(Date, 37));

        Assert.NotNull(outcome.Form);
        Assert.Contains(outcome.Form.DurationMinutes, outcome.Form.DurationOptions);
    }

    [Fact]
    public async Task An_unpermitted_submitted_length_is_rejected_and_places_nothing()
    {
        // The flow never substitutes on the write path. The surface controller
        // needs a host, so this asserts the Core call it makes verbatim.
        var harness = Build(
            Svc("Massage", ServiceDuration.Fixed(Mins(60)).Value, new ServiceRole(Therapist, 1)),
            Res(2, Therapist, "Jane"));

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(90),
            Booker = TestData.Booker(),
        });

        Assert.False(placed.Succeeded);
        Assert.DoesNotContain(
            Rendered(placed.Failures), BookingMessages.Fallback, StringComparison.Ordinal);
    }

    // --- 2.5 a pin can only be submitted where a choice was offered ---

    [Fact]
    public void Only_the_who_control_can_put_a_pin_into_the_front_ends_markup()
    {
        // ⑩'s scan asserted an absence: no control, no hidden field, no code path
        // (design D7). The absence is now conditional rather than total, so the
        // scan is narrowed to the same shape one step in: the ONLY markup that can
        // carry a pin is the who control and the hidden field beside it, and both
        // are guarded by the model saying a choice was offered or made.
        //
        // Still scoped to the default front-end. The delivery API deliberately
        // accepts a pin from any caller (design D8), so a scan over the whole
        // assembly would assert the wrong thing.
        var carriers = RepoFiles
            .Paths("src/UBookIt.Web/Views", "*.cshtml")
            .Where(path => File.ReadLines(path).Any(Mentions))
            .Select(Path.GetFileName)
            .ToList();

        // Exactly one view emits the field, and it is the service flow's — not the
        // resource flow's, and not the shared partials, whose GET form renders the
        // control itself under its own guard (asserted below).
        Assert.Equal(["Service.cshtml"], carriers);

        var service = RepoFiles.Read("src/UBookIt.Web/Views/Shared/Components/BookingFlow/Service.cshtml");

        // Guarded on the visitor's own choice, so a service offering none emits no
        // field at all rather than an empty one somebody could later make settable
        // without thinking about which role's resources belong in the list.
        Assert.Contains("@if (Model.ChosenResourceId is { } chosenResource)", service, StringComparison.Ordinal);

        var dateAndLength = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_DateAndLength.cshtml");

        // And the control that produces the choice is guarded on the service
        // offering one. Both halves, or a form could offer a control whose value
        // nothing carries — or carry a value no control produced.
        Assert.Contains("@if (Model.OffersResourceChoice)", dateAndLength, StringComparison.Ordinal);
        Assert.Contains($"name=\"@BookingKeys.ResourceQuery\"", dateAndLength, StringComparison.Ordinal);

        // Non-vacuity, kept from ⑩: the identifier really is one this scan would
        // find. The delivery API sets it, in the same assembly, and the predicate
        // says so.
        Assert.Contains(
            RepoFiles.Paths("src/UBookIt.Web/Mapping", "*.cs"),
            path => File.ReadLines(path).Any(Mentions));

        // A mention inside a comment is how a decision is recorded, so only code
        // counts — and the test has to be able to tell them apart.
        static bool Mentions(string line)
        {
            var code = line.TrimStart();

            return !code.StartsWith("//", StringComparison.Ordinal)
                && !code.StartsWith("///", StringComparison.Ordinal)
                && !code.StartsWith("@*", StringComparison.Ordinal)
                && !code.StartsWith('*')
                && code.Contains("PinnedResourceId", StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_service_with_no_selectable_role_offers_no_choice_at_all()
    {
        // The runtime half of the scan above: the markup is guarded on this
        // model, so the guard is only as good as what the flow puts in it.
        var outcome = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.False(outcome.Form.OffersResourceChoice);
        Assert.Empty(outcome.Form.ResourceChoices);
        Assert.Null(outcome.Form.ChosenResourceId);

        // And a hand-edited URL naming an eligible resource changes nothing: with
        // no role marked selectable there is no choice for this flow to honour.
        var pinned = await Massage().Flow.BuildAsync(
            Id(900),
            new BookingFlowInput { Date = Date, DurationMinutes = 60, ChosenResourceId = Id(2) });

        Assert.NotNull(pinned.Form);
        Assert.Null(pinned.Form.ChosenResourceId);
        Assert.False(pinned.Form.OffersResourceChoice);

        // Non-vacuity: Id(2) really is an eligible resource for this service, so
        // the assertion is about the flag rather than about an unknown id.
        var pools = await PoolsOf(Massage());
        Assert.Contains(pools.SelectMany(p => p.Candidates), c => c.ResourceId == Id(2));
    }

    // ---------------------------------------------------------------------
    // 8 — the who control: what a visitor is offered, and what it does.
    // ---------------------------------------------------------------------

    /// <summary>
    /// A massage whose therapist role a visitor may choose from: two therapists,
    /// one room. The room role is deliberately NOT selectable — the honest list of
    /// pinnable resources would otherwise include it, which is a choice nobody
    /// wants offered beside the one they do (design D1).
    /// </summary>
    private static Harness Choosable(params Resource[] extra)
        => Build(
            Svc(
                "Massage",
                null,
                new ServiceRole(ResourceTypes.Room, 1),
                new ServiceRole(Therapist, 1) { VisitorSelectable = true }),
            [.. new[] { Res(1, ResourceTypes.Room, "Treatment Room"), Res(2, Therapist, "Jane"), Res(3, Therapist, "Sam") }
                .Concat(extra)]);

    private static BookingFlowInput Choosing(Guid? who, int? minutes = 60)
        => new() { Date = Date, DurationMinutes = minutes, ChosenResourceId = who };

    [Fact]
    public async Task A_service_offering_a_choice_offers_each_eligible_resource_by_name()
    {
        var outcome = await Choosable().Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.OffersResourceChoice);

        // The selectable role's pool, and only that role's — the room is not among
        // the choices even though a pin naming it would be perfectly eligible.
        Assert.Equal(["Jane", "Sam"], outcome.Form.ResourceChoices.Select(c => c.Name));
        Assert.DoesNotContain(outcome.Form.ResourceChoices, c => c.Id == Id(1));

        // Defaulting to no particular choice.
        Assert.Null(outcome.Form.ChosenResourceId);
    }

    [Fact]
    public async Task The_choices_are_ordered_for_reading_by_name()
    {
        // Display order is by name with the id as the tiebreak, so it is total and
        // stable. Presentation only: pool order stays ascending by resource id,
        // which three changes draw determinism guarantees from — so the fixture
        // names them in the opposite order to their ids, and the assertion would
        // fail if the list were merely the pool.
        var harness = Build(
            Svc("Massage", null, new ServiceRole(Therapist, 1) { VisitorSelectable = true }),
            Res(2, Therapist, "Zoe"),
            Res(3, Therapist, "Adam"));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.Equal(["Adam", "Zoe"], outcome.Form!.ResourceChoices.Select(c => c.Name));

        // Non-vacuity: pool order really is the other way round, so this is the
        // display rule and not a coincidence.
        var pools = await PoolsOf(harness);
        Assert.Equal([Id(2), Id(3)], pools.Single().Candidates.Select(c => c.ResourceId));
    }

    [Fact]
    public async Task Choosing_a_person_narrows_the_times()
    {
        // Jane is booked all morning; Sam is free all day. The times shown for
        // Jane are the times Jane can actually be assigned, not the service's.
        var harness = Choosable();
        await OccupyAsync(harness, Id(2), "09:00", 180);

        var jane = await harness.Flow.BuildAsync(Id(900), Choosing(Id(2)));
        var anyone = await harness.Flow.BuildAsync(Id(900), Choosing(null));

        Assert.NotNull(jane.Form);
        Assert.True(jane.Form.HasTimes);
        Assert.DoesNotContain("09:00", jane.Form.Times.Select(t => t.Label));

        // And the service itself is bookable then, on Sam — which is exactly the
        // promise a dropped pin would have made.
        Assert.Contains("09:00", anyone.Form!.Times.Select(t => t.Label));

        // The choice is reflected back, so the control redraws on the visitor's
        // own answer rather than resetting under them.
        Assert.Equal(Id(2), jane.Form.ChosenResourceId);
    }

    [Fact]
    public async Task Choosing_nobody_offers_the_services_own_times()
    {
        // "Any" leaves the flow behaving exactly as it does for a service that
        // offers no choice at all — asserted against that very service, so the
        // claim is a comparison rather than a restatement of the implementation.
        var choosable = await Choosable().Flow.BuildAsync(Id(900), Choosing(null));
        var plain = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.Equal(
            plain.Form!.Times.Select(t => t.Label),
            choosable.Form!.Times.Select(t => t.Label));
    }

    [Fact]
    public async Task A_person_with_no_times_that_day_is_still_offered()
    {
        // The list is the role's pool, unfiltered by date (design D10). Filtering
        // it would make a name vanish from under the cursor as the visitor changes
        // the date, and would answer with the control a question the times already
        // answer directly.
        var harness = Choosable();
        await OccupyAsync(harness, Id(2), "09:00", 480);

        var outcome = await harness.Flow.BuildAsync(Id(900), Choosing(Id(2)));

        Assert.NotNull(outcome.Form);
        Assert.Contains(outcome.Form.ResourceChoices, c => c.Id == Id(2));

        // And the start list is what reports the emptiness — the form is still
        // drawn, so there is a date control to change and a choice to revise.
        Assert.False(outcome.Form.HasTimes);
        Assert.Null(outcome.Unavailable);
    }

    [Fact]
    public async Task A_stale_choice_in_a_link_resets_to_any_and_says_so()
    {
        // A bookmarked URL can name someone this service can no longer be
        // fulfilled by. Falling back silently would quietly make it a different
        // booking; refusing to render would punish a visitor for a change they had
        // no part in. Nothing is committed at GET, so the honest answer is to reset
        // and say so (design D11).
        var harness = Choosable();

        var outcome = await harness.Flow.BuildAsync(Id(900), Choosing(Id(404)));

        Assert.NotNull(outcome.Form);
        Assert.Null(outcome.Form.ChosenResourceId);
        Assert.True(outcome.Form.ResourceChoiceWasReset);

        // Still a usable form, with the choice offered again.
        Assert.True(outcome.Form.OffersResourceChoice);
        Assert.True(outcome.Form.HasTimes);

        // Non-vacuity: an offered resource is NOT reported as reset, so the flag
        // means "your choice was dropped" rather than "a choice was supplied".
        var honoured = await harness.Flow.BuildAsync(Id(900), Choosing(Id(2)));
        Assert.False(honoured.Form!.ResourceChoiceWasReset);
        Assert.Equal(Id(2), honoured.Form.ChosenResourceId);
    }

    [Fact]
    public async Task A_choice_whose_role_is_no_longer_selectable_is_reset_and_said_so()
    {
        // The third of design D11's three causes, and the one QA found silently
        // dropping its message: the resource still exists and is still perfectly
        // eligible, and the editor has simply turned the picker off. A link
        // carrying it must not go on pinning a resource the site no longer offers
        // — and must not do so QUIETLY, which is the half this test used to miss.
        var outcome = await Massage().Flow.BuildAsync(Id(900), Choosing(Id(2)));

        Assert.NotNull(outcome.Form);
        Assert.Null(outcome.Form.ChosenResourceId);
        Assert.False(outcome.Form.OffersResourceChoice);

        // Says so. Without this the visitor gets a form that will book anyone,
        // with nothing on the page reporting that their choice was dropped.
        Assert.True(outcome.Form.ResourceChoiceWasReset);

        // Non-vacuity: a request naming NOBODY is not a reset, so the flag means
        // "your choice was dropped" rather than "this service offers no choice".
        var unchosen = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));
        Assert.False(unchosen.Form!.ResourceChoiceWasReset);
    }

    [Fact]
    public void The_reset_notice_is_rendered_even_where_no_control_remains()
    {
        // The markup half, because no C# test renders Razor and the defect was
        // exactly a message put where it could never appear: the notice lived
        // inside the control's own branch, so for the flag-cleared cause — the one
        // with no control left — it was structurally unreachable.
        var markup = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_DateAndLength.cshtml");

        // Matched on the CONDITION rather than the whole line. This asserted the
        // exact `@if` text until that branch legitimately gained a second reason to
        // render — an error against the choice, which also arrives when no control
        // remains — and a test pinned to one spelling fails on a correct change.
        //
        // The behavioural guarantee now has a better home: `UBookIt.Tests.Rendering`
        // renders this partial and requires the reset flag to change the output in
        // every state that can express it, which is what "the notice is reachable
        // for all three causes" actually means. This stays as the cheap structural
        // half — the branch exists outside the control's own guard.
        Assert.Contains("@if (!Model.OffersResourceChoice", markup, StringComparison.Ordinal);

        // And both branches say it, or the guard above would merely move the gap.
        Assert.Equal(2, Regex.Matches(markup, @"id=""ubookit-who-reset""").Count);
    }

    [Fact]
    public void A_refused_pin_is_named_on_the_page_the_visitor_sees()
    {
        // QA's mutation: with the naming inside the surface controller, forcing the
        // generic wording left all 762 tests green, because the only covering
        // assertion was a tautology over the message helper. The decision now lives
        // where it can be attacked without a host, and this exercises THAT.
        var failures = new[]
        {
            new DomainFailure(
                FailureCodes.PinnedResourceUnavailable,
                "Resource 00000000-0000-0000-0000-000000000002 could not be booked for this service at that time.",
                nameof(ServiceBookingRequest.PinnedResourceId)),
        };

        var named = BookingMessages.ForFailures(failures, "Jane").Single();

        Assert.Contains("Jane", named.Message, StringComparison.Ordinal);
        Assert.Equal(BookingFieldIds.Resource, named.FieldId);

        // Never the domain's own text, which names a raw id.
        Assert.DoesNotContain("00000000", named.Message, StringComparison.Ordinal);

        // And it discloses nothing else about the configuration (design D12).
        foreach (var forbidden in new[] { Therapist, "capabilit", "role" })
        {
            Assert.DoesNotContain(forbidden, named.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void An_unreadable_name_still_says_what_happened()
    {
        // The fallback, and the pair that keeps the assertion above honest: a
        // rule that always used the generic wording would pass a test that only
        // checked this one.
        var failures = new[] { new DomainFailure(FailureCodes.PinnedResourceUnavailable, "x") };

        var generic = BookingMessages.ForFailures(failures, chosenResourceName: null).Single();
        var named = BookingMessages.ForFailures(failures, "Jane").Single();

        Assert.NotEqual(generic.Message, named.Message);

        // Still says what happened rather than "no times available", and still
        // offers both ways forward.
        Assert.DoesNotContain("no times", generic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("another time", generic.Message, StringComparison.OrdinalIgnoreCase);

        // A name is used ONLY for the code it belongs to: every other failure
        // renders identically whether or not a resource was chosen.
        foreach (var code in AllFailureCodes().Where(c => c != FailureCodes.PinnedResourceUnavailable))
        {
            Assert.Equal(
                BookingMessages.ForFailures([new DomainFailure(code, "x")], null).Single().Message,
                BookingMessages.ForFailures([new DomainFailure(code, "x")], "Jane").Single().Message);
        }
    }

    [Fact]
    public void The_surface_controller_wires_both_halves_of_the_choice_into_a_failed_submission()
    {
        // The controller needs an Umbraco host, which is why the decisions it used
        // to make were moved out of it. What is left is wiring, and QA showed both
        // lines could be cut with the whole suite green: the name never reaching
        // the message map (so a refused pin loses the visitor's own choice from
        // its wording), and the choice never reaching TempData (so the redraw
        // silently drops it on an author-named single-service site, where that is
        // the only carrier).
        //
        // Asserted over the shipped source, which is the technique this project
        // already uses for the Post-Redirect-Get query string — see
        // `Contact_details_never_appear_in_a_URL`. It is a weaker net than a
        // behavioural test and it is the one available without a host harness.
        var source = RepoFiles.Read("src/UBookIt.Web/Rendering/ServiceBookingSurfaceController.cs");

        // The name is looked up and handed to the map, rather than the map being
        // called without it.
        Assert.Contains(
            "BookingMessages.ForFailures(\n                failures, await ChosenResourceNameAsync(form, failures))",
            source.Replace("\r\n", "\n"),
            StringComparison.Ordinal);

        // And the visitor's choice is carried back for the redraw.
        Assert.Contains("ChosenResourceId = form.PinnedResourceId", source, StringComparison.Ordinal);

        // Non-vacuity: the members these lines name really exist, so a rename that
        // silently broke the wiring could not leave this test passing.
        Assert.Contains("ChosenResourceNameAsync", source, StringComparison.Ordinal);
        Assert.Contains(
            nameof(FailedSubmission.ChosenResourceId),
            typeof(FailedSubmission).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task A_failed_submission_redraws_the_visitors_own_choice()
    {
        // The choice survives POST → redirect → GET by two independent carriers,
        // and QA showed either could be disabled with the whole suite green. This
        // covers the one that is the ONLY live carrier for an author-named service
        // flow — the single-service site the design explicitly supports, where
        // there is no subject token and so no query string to ride on.
        var harness = Choosable();

        var outcome = await harness.Flow.BuildAsync(
            Id(900),
            new BookingFlowInput
            {
                Date = Date,
                Failed = new FailedSubmission
                {
                    Date = Date,
                    DurationMinutes = 60,
                    ChosenResourceId = Id(3),
                    Errors = [new BookingError("…", BookingFieldIds.Resource)],
                },
            });

        Assert.NotNull(outcome.Form);
        Assert.Equal(Id(3), outcome.Form.ChosenResourceId);
    }

    [Fact]
    public void The_redirect_after_a_submission_carries_the_chosen_resource()
    {
        // The other carrier, for the flow reached through the catalogue: the
        // redraw must land on a URL that agrees with the page it draws, which is
        // why the choice is in the URL at all.
        var query = BookingFlowLink
            .For(BookingSubject.Service(Id(900)), new DateOnly(2026, 8, 20), 90, Id(3))
            .ToUriComponent();

        Assert.Contains("ubWho=" + Id(3), query, StringComparison.OrdinalIgnoreCase);

        // And a flow where nobody was chosen produces exactly the query string it
        // produced before this change — no empty parameter.
        Assert.DoesNotContain(
            "ubWho",
            BookingFlowLink.For(BookingSubject.Service(Id(900)), new DateOnly(2026, 8, 20), 90)
                .ToUriComponent(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_selectable_role_of_count_two_says_what_a_visitor_chooses()
    {
        // "This one, plus N−1 chosen for you" (design D5). The control has to say
        // so, or a picker implies a choice and then books resources the visitor
        // never chose.
        var harness = Build(
            Svc("Couples massage", null,
                ServiceRole.Create(Therapist, null, count: 2, visitorSelectable: true).Value),
            Res(2, Therapist, "Jane"),
            Res(3, Therapist, "Sam"));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);
        Assert.Equal(2, outcome.Form.ResourceChoiceCount);

        // And the wording lives in the shared partial, guarded on the count — the
        // markup is where the obligation is discharged, and no C# test renders it.
        var markup = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_DateAndLength.cshtml");

        Assert.Contains("Model.ResourceChoiceCount > 1", markup, StringComparison.Ordinal);
        Assert.Contains("You choose one of them", markup, StringComparison.Ordinal);

        // The ordinary case says nothing about several, or the sentence would be
        // noise on every single-resource service.
        var single = await Choosable().Flow.BuildAsync(Id(900), On(Date, 60));
        Assert.Equal(1, single.Form!.ResourceChoiceCount);
    }

    [Fact]
    public async Task Only_the_selectable_roles_pool_is_offered_where_two_roles_share_a_type()
    {
        // Design D1's decisive clause, and the one with no other covering test: a
        // service-level flag naming the role by TYPE KEY was rejected because two
        // roles may name one type and be told apart only by their capabilities.
        //
        // Here both roles are `therapist`; only the cert-x one is selectable, and
        // its pool is a strict subset of the other's. A flag resolved by type
        // would offer the union — including Sam, who cannot fill the role the
        // visitor is choosing for.
        var harness = Build(
            Svc(
                "Joint session",
                null,
                ServiceRole.Create(Therapist, ["cert-x"], visitorSelectable: true).Value,
                ServiceRole.Create(Therapist, null).Value),
            Res(2, Therapist, "Jane", capabilities: "cert-x"),
            Res(3, Therapist, "Sam"),
            Res(4, Therapist, "Ada", capabilities: "cert-x"));

        var outcome = await harness.Flow.BuildAsync(Id(900), On(Date, 60));

        Assert.NotNull(outcome.Form);

        // Only the cert-x therapists, and Sam — eligible for the OTHER role, and
        // pinnable at placement — is not among the choices.
        Assert.Equal(["Ada", "Jane"], outcome.Form.ResourceChoices.Select(c => c.Name));

        // Non-vacuity: Sam really is a candidate of this service, so his absence
        // is the flag's doing rather than the pool's.
        var pools = await PoolsOf(harness);
        Assert.Contains(pools.SelectMany(p => p.Candidates), c => c.ResourceId == Id(3));
    }

    [Fact]
    public async Task A_pin_is_honoured_for_a_role_that_is_not_visitor_selectable()
    {
        // Design D8, stated as a test because it is the clause a later change is
        // most likely to "fix" into an access control. Placement is UNCHANGED: the
        // flag decides what is OFFERED, never what placement accepts, and a
        // booking pinning an eligible resource on an unflagged role is perfectly
        // deliverable — nothing about it is wrong.
        //
        // It is not concealment either: every role's pool is already computable
        // from the public reads, so gating placement would buy nothing.
        var harness = Massage();

        Assert.All(harness.Service.Roles, role => Assert.False(role.VisitorSelectable));

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
            PinnedResourceId = Id(2),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));
        Assert.Contains(placed.Value.Claims, claim => claim.ResourceId == Id(2));

        // And the availability query answers the same question the same way: a pin
        // on an unflagged role narrows the times rather than being refused.
        var starts = await harness.Core.GetBookableStartsAsync(Id(900), Date, Date, Id(2));

        Assert.True(starts.Succeeded);
        Assert.NotEmpty(starts.Value);
    }

    [Fact]
    public async Task The_choice_is_honoured_at_placement_and_reported_on_the_confirmation()
    {
        var harness = Choosable();

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
            PinnedResourceId = Id(3),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));
        Assert.Contains(placed.Value.Claims, claim => claim.ResourceId == Id(3));

        var confirmation = ServiceBookingFormBuilder.BuildConfirmation(
            placed.Value, harness.Service.Name, await NamesOf(harness, placed.Value), TestData.London);

        // The person they chose, named back to them — and the room they were given
        // beside them, because the confirmation describes the booking that exists.
        Assert.Contains("Sam", confirmation.ResourceNames);
        Assert.Contains("Treatment Room", confirmation.ResourceNames);
        Assert.DoesNotContain("Jane", confirmation.ResourceNames);
    }

    [Fact]
    public async Task A_busy_choice_is_refused_by_name_rather_than_reported_as_no_availability()
    {
        // The two facts have different next steps: one is answered by choosing
        // another time or another person, the other by choosing another date. The
        // page must not collapse one into the other — which is the whole reason
        // `pinned-resource-unavailable` exists.
        var harness = Choosable();
        await OccupyAsync(harness, Id(2), "09:00", 60);

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
            PinnedResourceId = Id(2),
        });

        Assert.False(placed.Succeeded);

        // Non-vacuity: Sam is free at that time, so this is a refused CHOICE and
        // not an exhausted service — an unpinned request succeeds.
        Assert.Equal(FailureCodes.PinnedResourceUnavailable, placed.Failures[0].Code);

        var rendered = Rendered(placed.Failures);

        Assert.DoesNotContain(BookingMessages.Fallback, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("no times", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PermanentClaim, rendered, StringComparison.OrdinalIgnoreCase);

        // Named, because the visitor named them. The message the flow renders is
        // built from the resource's own display name.
        Assert.Contains("Jane", BookingMessages.PinnedUnavailable("Jane"), StringComparison.Ordinal);

        // And it offers both ways forward: another time, or letting the service
        // choose. One of the two alone would send the visitor down a single path
        // when the other may suit them better.
        Assert.Contains("another time", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pick for you", rendered, StringComparison.OrdinalIgnoreCase);

        // It points at the control that made the choice, where "anyone" is one
        // keystroke away — not at the time list, which offers only half the way
        // forward.
        Assert.Equal(
            BookingFieldIds.Resource,
            BookingMessages.ForFailures(placed.Failures).First().FieldId);
    }

    [Fact]
    public void A_refused_choice_names_the_resource_and_nothing_else()
    {
        // The narrow permission (design D12): a refusal about a resource the
        // visitor themselves chose may name it, and nothing more. None of the five
        // prohibited facts — role, resource type, required capability, count, pool
        // size — may appear.
        var message = BookingMessages.PinnedUnavailable("Jane");

        Assert.Contains("Jane", message, StringComparison.Ordinal);
        Assert.DoesNotContain(Therapist, message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("capabilit", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("role", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(message, "0123456789".Select(c => c.ToString()).Where(message.Contains));
    }

    [Fact]
    public async Task A_deterministic_refusal_names_nothing_even_where_a_choice_was_offered()
    {
        // The other side of D12, and the one it would be easiest to get wrong: a
        // service that offers a picker and then becomes permanently unfulfillable
        // still discloses nothing — including any resource the visitor had chosen.
        var harness = Build(
            Svc(
                "Massage",
                null,
                new ServiceRole(ResourceTypes.Room, 1),
                new ServiceRole(Therapist, 1) { VisitorSelectable = true }),
            Res(2, Therapist, "Jane"));

        var outcome = await harness.Flow.BuildAsync(Id(900), Choosing(Id(2)));

        Assert.Null(outcome.Form);
        Assert.NotNull(outcome.Unavailable);
        Assert.Equal(ServiceUnavailableReason.NotFulfillable, outcome.Unavailable.Reason);

        // The model has nowhere to carry a resource, which is what makes the claim
        // structural rather than a property of today's wording.
        Assert.Equal("Massage", outcome.Unavailable.ServiceName);
    }

    [Fact]
    public void Only_a_failure_about_the_visitors_own_choice_points_at_the_choice_control()
    {
        // Enumerated over every code the map knows, rather than over the two a
        // fixture happens to produce — ⑩'s technique, applied to the new control.
        // A code wrongly landed here would put "choose someone else" on a failure
        // that has nothing to do with who.
        var expected = new[] { FailureCodes.PinnedResourceUnavailable, FailureCodes.ResourceNotEligible };

        foreach (var code in AllFailureCodes())
        {
            var field = BookingMessages.ForFailures([new DomainFailure(code, "x")]).Single().FieldId;

            Assert.Equal(expected.Contains(code), field == BookingFieldIds.Resource);
        }
    }

    [Fact]
    public void No_failure_message_claims_the_service_has_no_availability()
    {
        // The no-availability page's sentence is the start list's to say, and only
        // when the start list is empty. A failure message asserting it would send a
        // visitor to change the date when the date is not the problem — the exact
        // collapse the pin refusal exists to prevent, stated over the whole map so
        // a code nobody thought to provoke cannot reintroduce it.
        foreach (var code in AllFailureCodes())
        {
            Assert.DoesNotContain("no times", BookingMessages.ForCode(code), StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("no times", BookingMessages.Fallback, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("no times", BookingMessages.PinnedUnavailable("Jane"), StringComparison.OrdinalIgnoreCase);

        // Non-vacuity: the sentence really is one the front end says, on the page
        // that is entitled to say it.
        Assert.Contains(
            "No times are available",
            RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_Times.cshtml"),
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> AllFailureCodes()
    {
        var codes = typeof(FailureCodes)
            .GetFields()
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);
        return codes;
    }

    // ---------------------------------------------------------------------
    // 4 — catalogue and entry points.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Services_and_directly_bookable_resources_are_listed_together()
    {
        var resources = new InMemoryResourceStore()
            .Add(Res(1, ResourceTypes.Room, "Meeting Room A"))
            .Add(Res(2, Therapist, "Jane"));

        var services = new InMemoryServiceStore()
            .Add(Svc("Massage", null, new ServiceRole(Therapist, 1)))
            .Add(Service.Create("Haircut", null, [new ServiceRole(Therapist, 1)], id: Id(901)).Value);

        var catalogue = await new BookingCatalogue(services, resources).BuildAsync();

        Assert.Equal(4, catalogue.Entries.Count);
        Assert.Contains(catalogue.Entries, e => e.Name == "Massage" && e.Subject.Kind == BookableKind.Service);
        Assert.Contains(catalogue.Entries, e => e.Name == "Meeting Room A" && e.Subject.Kind == BookableKind.Resource);

        // Presented alike: one ordering across both kinds, not services then
        // resources. A visitor cannot tell which is which from the list's shape.
        Assert.Equal(
            ["Haircut", "Jane", "Massage", "Meeting Room A"],
            catalogue.Entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public async Task A_resource_that_withholds_direct_booking_is_not_offered()
    {
        // Both kinds are needed or the test cannot fail: a catalogue that listed
        // nothing at all would satisfy the absence on its own.
        var withholding = Resource.Create(
            ResourceTypes.Room,
            "Service-only Room",
            directlyBookable: false,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(3)).Value;

        var resources = new InMemoryResourceStore()
            .Add(Res(1, ResourceTypes.Room, "Meeting Room A"))
            .Add(withholding);

        var catalogue = await new BookingCatalogue(new InMemoryServiceStore(), resources).BuildAsync();

        Assert.Equal("Meeting Room A", Assert.Single(catalogue.Entries).Name);
    }

    // --- 4.3 the dispatcher ---

    [Fact]
    public void No_id_anywhere_means_the_catalogue()
        => Assert.Null(FlowEntry.Resolve(null, null, null).Subject);

    [Fact]
    public void A_query_subject_selects_its_own_flow()
    {
        var service = FlowEntry.Resolve(null, null, BookingSubject.Service(Id(900)));
        Assert.Equal(BookableKind.Service, service.Subject!.Value.Kind);

        var resource = FlowEntry.Resolve(null, null, BookingSubject.Resource(Id(1)));
        Assert.Equal(BookableKind.Resource, resource.Subject!.Value.Kind);
    }

    [Fact]
    public void An_explicitly_supplied_id_beats_the_query()
    {
        // What makes "a site with one service" work: the author names it, and no
        // catalogue exists to be traversed — nor can a stale URL redirect the
        // visitor away from the thing the author placed.
        var entry = FlowEntry.Resolve(Id(900), null, BookingSubject.Resource(Id(1)));

        Assert.Equal(BookingSubject.Service(Id(900)), entry.Subject);
        Assert.True(entry.NamedByAuthor);

        // And an author-named subject puts nothing in the URL, which is what keeps
        // the existing component's Post-Redirect-Get target unchanged.
        Assert.Null(entry.Token);
    }

    [Fact]
    public void A_subject_reached_through_the_catalogue_carries_its_token()
    {
        var entry = FlowEntry.Resolve(null, null, BookingSubject.Service(Id(900)));

        Assert.False(entry.NamedByAuthor);
        Assert.Equal(BookingSubject.Service(Id(900)).Token, entry.Token);
    }

    [Fact]
    public void A_subject_token_round_trips_and_a_malformed_one_is_no_subject_at_all()
    {
        foreach (var subject in new[] { BookingSubject.Service(Id(900)), BookingSubject.Resource(Id(1)) })
        {
            Assert.True(BookingSubject.TryParse(subject.Token, out var parsed));
            Assert.Equal(subject, parsed);
        }

        foreach (var malformed in new[] { null, "", "s", "s:", "x:" + Id(1), Id(1).ToString(), "s:not-a-guid" })
        {
            Assert.False(BookingSubject.TryParse(malformed, out _));
        }
    }

    // --- 4.5 the entry point does not change what follows ---

    [Fact]
    public async Task Reaching_a_service_directly_and_through_the_catalogue_give_the_same_step()
    {
        var direct = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));
        var viaCatalogue = await Massage().Flow.BuildAsync(
            Id(900), On(Date, 60, BookingSubject.Service(Id(900)).Token));

        Assert.NotNull(direct.Form);
        Assert.NotNull(viaCatalogue.Form);

        // Same steps and controls from that point on. The flow token is not a
        // control — it is how the URL remembers the answer the author gave
        // another way — and is the one thing permitted to differ.
        Assert.Equal(direct.Form.ServiceId, viaCatalogue.Form.ServiceId);
        Assert.Equal(direct.Form.SelectedDate, viaCatalogue.Form.SelectedDate);
        Assert.Equal(direct.Form.DurationMinutes, viaCatalogue.Form.DurationMinutes);
        Assert.Equal(direct.Form.DurationOptions, viaCatalogue.Form.DurationOptions);
        Assert.Equal(direct.Form.LengthIsFixed, viaCatalogue.Form.LengthIsFixed);
        Assert.Equal(
            direct.Form.Times.Select(t => t.InstantIso),
            viaCatalogue.Form.Times.Select(t => t.InstantIso));

        Assert.Null(direct.Form.FlowToken);
        Assert.NotNull(viaCatalogue.Form.FlowToken);
    }

    // --- 4.4 the existing component is unchanged ---

    [Fact]
    public async Task The_resource_flow_still_answers_as_it_did_when_invoked_with_an_id()
    {
        // Someone may already have the Booking component in a template. Its guts
        // moved; its answers did not.
        // One instance, not two: TestData.Room() mints a fresh id each call, and
        // asking the flow about a resource the store never saw would prove only
        // that an unknown id is a fault.
        var room = TestData.Room();
        var resources = new InMemoryResourceStore().Add(room);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, store, time, TestData.Settings),
            TestData.Settings,
            time);

        var outcome = await flow.BuildAsync(room.Id, On(Date, 60));

        Assert.Null(outcome.Unavailable);
        Assert.NotNull(outcome.Form);
        Assert.True(outcome.Form.HasTimes);

        // Nothing in the URL, so nothing in the redirect: this is what makes the
        // extraction invisible to a site already using the component.
        Assert.Null(outcome.Form.FlowToken);

        // And a resource's length stays a choice, even when the grid holds one
        // value — rendering it as settled text would change this flow.
        Assert.False(outcome.Form.LengthIsFixed);
    }

    [Fact]
    public async Task A_withholding_resource_still_reaches_the_permanent_answer_through_the_flow()
    {
        var withholding = Resource.Create(
            ResourceTypes.Room,
            "Service-only Room",
            directlyBookable: false,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(3)).Value;

        var resources = new InMemoryResourceStore().Add(withholding);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        var flow = new ResourceBookingFlow(
            resources,
            new AvailabilityService(resources, store, time, TestData.Settings),
            TestData.Settings,
            time);

        var outcome = await flow.BuildAsync(Id(3), On(Date));

        Assert.Null(outcome.Form);
        Assert.Equal(BookingUnavailableReason.NotOfferedIndividually, outcome.Unavailable!.Reason);
    }

    // ---------------------------------------------------------------------
    // 5 — state in the URL, and what must never be in it.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_step_is_linkable_the_same_choices_come_back()
    {
        var first = await Massage().Flow.BuildAsync(Id(900), On(Date, 60));
        var reopened = await Massage().Flow.BuildAsync(
            Id(900), On(first.Form!.SelectedDate, first.Form.DurationMinutes));

        Assert.Equal(first.Form.SelectedDate, reopened.Form!.SelectedDate);
        Assert.Equal(first.Form.DurationMinutes, reopened.Form.DurationMinutes);
        Assert.Equal(
            first.Form.Times.Select(t => t.InstantIso), reopened.Form.Times.Select(t => t.InstantIso));
    }

    [Fact]
    public void A_submitted_subject_that_disagrees_with_what_was_booked_is_discarded()
    {
        // The subject is a POST field, so a hand-made submission can name a
        // service while booking a resource. Honouring it would place the booking
        // and then redirect into the other flow, which reads a different TempData
        // key — losing the confirmation for a booking that really happened.
        Assert.Null(BookingSubject.Agreeing(
            BookingSubject.Service(Id(900)).Token, BookingSubject.Resource(Id(1))));

        // Same id, different kind: the pair that a comparison on the Guid alone
        // would wave through.
        Assert.Null(BookingSubject.Agreeing(
            BookingSubject.Service(Id(1)).Token, BookingSubject.Resource(Id(1))));

        // And the honest case still travels, or the guard would be a silent
        // removal of the flow state it exists to protect.
        Assert.Equal(
            BookingSubject.Resource(Id(1)),
            BookingSubject.Agreeing(BookingSubject.Resource(Id(1)).Token, BookingSubject.Resource(Id(1))));

        // An absent subject is not a disagreement — it is the component-invoked
        // flow, which has no URL state and must keep its bare redirect.
        Assert.Null(BookingSubject.Agreeing(null, BookingSubject.Resource(Id(1))));
    }

    [Fact]
    public void The_redirect_after_a_submission_carries_the_whole_step_not_just_the_subject()
    {
        // QA found the redraw landing on a URL that disagreed with the page it
        // drew: the form showed the submitted date while the address bar named
        // another, because only the subject survived the redirect. The step's
        // choices are in the URL precisely so that cannot happen.
        var query = BookingFlowLink.For(BookingSubject.Service(Id(900)), new DateOnly(2026, 8, 20), 90)
            .ToUriComponent();

        Assert.Contains("ubBook=s%3A" + Id(900), query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ubDate=2026-08-20", query, StringComparison.Ordinal);
        Assert.Contains("ubMins=90", query, StringComparison.Ordinal);

        // A length of zero is what an omitted field binds to; carrying it would
        // put a value in the URL no control could have produced.
        Assert.DoesNotContain(
            "ubMins",
            BookingFlowLink.For(BookingSubject.Resource(Id(1)), new DateOnly(2026, 8, 20), 0).ToUriComponent(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Contact_details_never_appear_in_a_URL()
    {
        // The place a later change would most plausibly leak them is the GET
        // forms and the Post-Redirect-Get target, so both are asserted over the
        // shipped source rather than over one rendered page.
        //
        // EVERY GET form the package renders, found by scanning rather than by a
        // list — a hardcoded pair would be blind to a third one added later, which
        // is exactly the change that would introduce this leak.
        var getForms = RepoFiles
            .Paths("src/UBookIt.Web/Views", "*.cshtml")
            .Where(path => File.ReadAllText(path).Contains("method=\"get\"", StringComparison.Ordinal))
            .ToList();

        // Non-vacuity: the scan must actually be finding the forms it claims to
        // check, or an empty result would satisfy every assertion below.
        Assert.Equal(2, getForms.Count);

        foreach (var path in getForms)
        {
            var markup = File.ReadAllText(path);

            foreach (var field in new[] { "\"Name\"", "\"Email\"", "\"Phone\"" })
            {
                Assert.DoesNotContain(field, markup, StringComparison.Ordinal);
            }
        }

        // And the redirect. Every query string either controller produces is built
        // by one function, so the rule is a property of that function rather than
        // of two call sites happening to agree — assert BOTH halves, or a second
        // builder added beside it would be invisible.
        foreach (var controller in new[]
        {
            "src/UBookIt.Web/Rendering/BookingSurfaceController.cs",
            "src/UBookIt.Web/Rendering/ServiceBookingSurfaceController.cs",
        })
        {
            var source = RepoFiles.Read(controller);

            Assert.Contains("BookingFlowLink.For(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("QueryString.Create(", source, StringComparison.Ordinal);
        }

        var link = RepoFiles.Read("src/UBookIt.Web/Rendering/BookingSubject.cs");
        var keys = Regex.Matches(link, @"BookingKeys\.(\w+)").Select(m => m.Groups[1].Value).Distinct().Order();

        Assert.Equal(["DateQuery", "DurationQuery", "ResourceQuery", "SubjectQuery"], keys.ToArray());
    }

    [Fact]
    public void The_only_flow_state_in_the_URL_is_what_is_booked_the_date_the_length_and_the_chosen_resource()
    {
        // The enumeration is the point: it is what stops flow state accreting in
        // the URL unnoticed. It grew by exactly one when the who control arrived,
        // and a resource id in a URL discloses nothing a public read does not
        // already carry.
        var keys = typeof(BookingKeys)
            .GetFields()
            .Where(f => f.IsLiteral && f.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .Order()
            .ToArray();

        Assert.Equal(["ubBook", "ubDate", "ubMins", "ubWho"], keys);
    }

    // ---------------------------------------------------------------------
    // 6 — one accessibility bar, met by both flows and the catalogue.
    // ---------------------------------------------------------------------

    [Fact]
    public void Both_flows_render_the_same_accessibility_critical_markup()
    {
        // The bar is stated once in the spec precisely so there is one place to
        // satisfy it. This asserts the structure that makes that true: the clauses
        // live in the shared partials, and both flows' views include them. A test
        // that checked one flow's markup would leave the bar met by whichever flow
        // was reviewed most recently.
        var partials = new[] { "_ErrorSummary", "_DateAndLength", "_Times", "_YourDetails" };

        foreach (var view in new[]
        {
            "src/UBookIt.Web/Views/Shared/Components/Booking/Default.cshtml",
            "src/UBookIt.Web/Views/Shared/Components/BookingFlow/Service.cshtml",
        })
        {
            var markup = RepoFiles.Read(view);

            foreach (var partial in partials)
            {
                Assert.Contains(
                    $"~/Views/Shared/UBookIt/{partial}.cshtml", markup, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void No_view_uses_a_tag_helper_this_package_never_registers()
    {
        // The assertion above names a PATH, and a path appears in the file whether
        // the construct around it renders or is emitted as literal text. QA proved
        // the gap by restoring the original defect — `<partial name="~/…" />` in
        // place of `@await Html.PartialAsync(…)` — with all 690 tests green.
        //
        // The cause is structural and permanent: `UBookIt.Web` has no
        // `_ViewImports.cshtml`, so `Microsoft.AspNetCore.Mvc.TagHelpers` is never
        // added and EVERY tag helper degrades to literal text on the page rather
        // than to an error. Nothing else in the suite can see that, because the C#
        // tests render no Razor at all.
        //
        // So the construct is banned outright rather than the one instance fixed.
        //
        // This does NOT make the accessibility requirement testable — only
        // rendering Razor would, and that is a host-test harness this project has
        // never had. It is deferred deliberately and recorded as an obligation in
        // the change's tasks.md §9.3, not left as an aspiration in a comment. What
        // this closes is the one defect class that has now bitten twice.
        var offenders = new List<string>();

        foreach (var path in RepoFiles.Paths("src/UBookIt.Web/Views", "*.cshtml"))
        {
            var markup = File.ReadAllText(path);
            var name = Path.GetFileName(path);

            if (markup.Contains("<partial", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add($"{name}: <partial> tag helper (use @await Html.PartialAsync)");
            }

            if (Regex.IsMatch(markup, @"\sasp-[a-z-]+\s*="))
            {
                offenders.Add($"{name}: asp-* tag helper attribute");
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_shared_partials_are_all_reached_from_a_flow()
    {
        // Directory-driven rather than a hardcoded list, so a partial added later
        // cannot sit unreferenced while the suite reports the seam intact.
        var flows = new[]
        {
            RepoFiles.Read("src/UBookIt.Web/Views/Shared/Components/Booking/Default.cshtml"),
            RepoFiles.Read("src/UBookIt.Web/Views/Shared/Components/BookingFlow/Service.cshtml"),
        };

        // The one partial in this folder whose consumer is NOT a flow, named rather
        // than filtered by a pattern. `_Styles.cshtml` is rendered by the consuming
        // SITE's layout, into the document head — a flow cannot reach the head at all,
        // which is the whole reason it is a partial the site calls rather than markup a
        // ViewComponent emits.
        //
        // The rule this exempts it from is a real one: a flow partial nothing
        // references is dead code wearing a live seam. That reasoning does not reach a
        // partial with a different consumer, so exempting it does not weaken the rule
        // for anything the rule is about.
        //
        // It is covered instead by the emission rule in the rendering suite, which
        // asserts exactly one view emits package styling, that it is this one, and that
        // it names the shipped asset — a stronger check than "some flow mentions it".
        const string EmittedBySiteLayout = "_Styles";

        foreach (var path in RepoFiles.Paths("src/UBookIt.Web/Views/Shared/UBookIt", "*.cshtml"))
        {
            var name = Path.GetFileNameWithoutExtension(path);

            if (name == EmittedBySiteLayout)
            {
                continue;
            }

            Assert.Contains(flows, flow => flow.Contains($"UBookIt/{name}.cshtml", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_flow_partials_the_seam_rule_covers_are_not_reduced_to_nothing()
    {
        // The vacuity guard for the exemption above. Naming one file to skip is one
        // edit away from skipping the folder, and the loop would then pass by checking
        // nothing while reporting the seam intact — the exact shape this suite has been
        // bitten by before.
        var covered = RepoFiles
            .Paths("src/UBookIt.Web/Views/Shared/UBookIt", "*.cshtml")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != "_Styles")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["_DateAndLength", "_ErrorSummary", "_Times", "_YourDetails"], covered);
    }

    [Fact]
    public void The_shared_partials_carry_the_clauses_the_bar_names()
    {
        var times = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_Times.cshtml");

        // Start times are a grouped set of radio controls in a fieldset with a
        // legend naming the group, each with an associated label.
        Assert.Contains("<fieldset", times, StringComparison.Ordinal);
        Assert.Contains("<legend>", times, StringComparison.Ordinal);
        Assert.Contains("type=\"radio\"", times, StringComparison.Ordinal);
        Assert.Contains("<label for=", times, StringComparison.Ordinal);

        var details = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_YourDetails.cshtml");

        // Required inputs indicated in text, not by colour or placeholder alone,
        // and error text associated with its control.
        Assert.Contains("(required)", details, StringComparison.Ordinal);
        Assert.Contains("aria-describedby", details, StringComparison.Ordinal);
        Assert.Contains("aria-invalid", details, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder=", details, StringComparison.Ordinal);

        var summary = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_ErrorSummary.cshtml");

        // The summary lists each problem in text and links to the offending field.
        Assert.Contains("role=\"alert\"", summary, StringComparison.Ordinal);
        Assert.Contains("href=\"#@error.FieldId\"", summary, StringComparison.Ordinal);

        var dateAndLength = RepoFiles.Read("src/UBookIt.Web/Views/Shared/UBookIt/_DateAndLength.cshtml");

        Assert.Contains("<label for=\"ubookit-date\">", dateAndLength, StringComparison.Ordinal);
        Assert.Contains("<label for=\"@BookingFieldIds.Duration\">", dateAndLength, StringComparison.Ordinal);
    }

    [Fact]
    public void The_catalogue_is_a_grouped_set_with_a_legend_naming_what_is_chosen()
    {
        var markup = RepoFiles.Read("src/UBookIt.Web/Views/Shared/Components/BookingFlow/Catalogue.cshtml");

        Assert.Contains("<fieldset", markup, StringComparison.Ordinal);
        Assert.Contains("<legend>What would you like to book?</legend>", markup, StringComparison.Ordinal);
        Assert.Contains("type=\"radio\"", markup, StringComparison.Ordinal);
        Assert.Contains("<label for=\"@id\">@entry.Name</label>", markup, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------

    private static string Rendered(IReadOnlyList<DomainFailure> failures)
        => string.Join(" ", BookingMessages.ForFailures(failures).Select(e => e.Message));

    /// <summary>Books a resource solid over an interval, so it cannot fulfil anything there.</summary>
    private static async Task OccupyAsync(Harness harness, Guid resourceId, string start, int minutes)
    {
        var placed = await harness.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = resourceId,
            Start = TestData.Utc(Date, start),
            Duration = Mins(minutes),
            Booker = TestData.Booker(),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));
    }
}
