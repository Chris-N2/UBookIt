using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// An operator places a booking for a <em>service</em> on a booker's behalf (service-booking
/// spec, "Placing a booking on a booker's behalf is the service booking service's entry point").
/// <para>
/// <b>Everything here goes through the service booking service</b>, never through the booking
/// service beneath it. That is the whole point of the entry point: a rule that lives on this
/// layer is invisible to the one below, and `move-booking` shipped exactly that defect — a
/// service booking moved to a length its service forbids, past 1644 unit tests, caught only by
/// a live probe.
/// </para>
/// </summary>
public class ServicePlaceOnBehalfTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A therapist open 08:00–18:00 on the fixture's day, needing a day's notice and bookable
    /// at most 90 days out — the two rules an operator is exempt from, stated so a test can
    /// show they are.
    /// </summary>
    private static Resource Therapy(int id = 1, int maxMinutes = 240) => Resource.Create(
        Therapist,
        $"Therapist {id}",
        directlyBookable: false,
        availability: TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(15),
                minDuration: TimeSpan.FromMinutes(15),
                maxDuration: TimeSpan.FromMinutes(maxMinutes),
                leadTime: TimeSpan.FromHours(24),
                horizonDays: 90).Value),
        id: Id(id)).Value;

    private static Service Treatment(int minMinutes = 45, int maxMinutes = 120)
        => Service.Create(
            "Treatment",
            ServiceDuration.Variable(
                TimeSpan.FromMinutes(minMinutes), TimeSpan.FromMinutes(maxMinutes)).Value,
            [ServiceRole.Create(Therapist, null).Value]).Value;

    private static (ServiceBookingService Services, BookingService Bookings) Wire(
        Service? service, DateTimeOffset? nowUtc = null, bool autoConfirm = true,
        params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore();
        if (service is not null)
        {
            serviceStore.Add(service);
        }

        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(nowUtc ?? TestData.Now);
        var settings = new SiteBookingSettings
        {
            TimeZoneId = TestData.LondonZoneId,
            AutoConfirm = autoConfirm,
        };

        var bookings = new BookingService(resourceStore, bookingStore, time, settings);

        return (
            new ServiceBookingService(
                serviceStore,
                resourceStore,
                bookingStore,
                new AvailabilityService(resourceStore, bookingStore, time, settings),
                bookings,
                settings),
            bookings);
    }

    private static ServiceBookingRequest Request(
        Service service, string start = "10:00", int minutes = 60)
        => new()
        {
            ServiceId = service.Id,
            Start = TestData.Utc(Date, start),
            Duration = TimeSpan.FromMinutes(minutes),
            Booker = TestData.Booker(),
        };

    [Fact]
    public async Task Spec_scenario_an_operator_places_a_service_booking_for_a_booker()
    {
        var service = Treatment();
        var (services, _) = Wire(service, resources: Therapy());

        var result = await services.PlaceOnBehalfAsync(Request(service));

        Assert.True(result.Succeeded);
        Assert.Equal(Id(1), Assert.Single(result.Value.Claims).ResourceId);
        Assert.Equal(service.Id, result.Value.Service?.ServiceId);
    }

    // ------------------------------------------------------------------
    // The rule that lives one layer up
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_a_services_length_rules_bind_an_operator()
    {
        // The `move-booking` defect, in its placement form: the resource admits 15–240, the
        // service sells 45–120, and only this layer knows the second.
        var service = Treatment(minMinutes: 45, maxMinutes: 120);
        var (services, _) = Wire(service, resources: Therapy());

        var result = await services.PlaceOnBehalfAsync(Request(service, minutes: 30));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.DurationTooShort);
    }

    [Fact]
    public async Task Spec_scenario_a_claimed_resources_ceiling_still_binds()
    {
        // The service would sell 240; this therapist's own maximum is 90.
        var service = Treatment(minMinutes: 45, maxMinutes: 240);
        var (services, _) = Wire(service, resources: Therapy(maxMinutes: 90));

        var result = await services.PlaceOnBehalfAsync(Request(service, minutes: 120));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.DurationTooLong);
    }

    [Fact]
    public async Task The_booking_service_alone_does_not_apply_the_services_length_rule()
    {
        // States WHY the entry point exists rather than assuming it. If this ever starts
        // failing, the split has been removed and the entry point is no longer load-bearing —
        // which is a thing to notice deliberately, not to discover through a bug.
        var service = Treatment(minMinutes: 45, maxMinutes: 120);
        var (_, bookings) = Wire(service, resources: Therapy());

        var direct = await bookings.PlaceOnBehalfAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(30),
            Booker = TestData.Booker(),
        });

        Assert.True(direct.Succeeded);
    }

    // ------------------------------------------------------------------
    // Operator terms must survive resolution, not only reach the pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_lead_time_is_waived_for_a_service_placement_too()
    {
        // End-to-end proof that the terms reach the pipeline THROUGH this layer: resolution,
        // shortlisting and the advisory free-claims read must none of them apply a rule the
        // pipeline was told to waive.
        //
        // It does NOT guard the failure classification — the first attempt succeeds, so the
        // rule check is never consulted. That claim was made and measured false; the guard
        // that does catch it is the conflict test below.
        var service = Treatment();
        var (services, _) = Wire(
            service, nowUtc: TestData.Utc(Date, "09:00"), resources: Therapy());

        var result = await services.PlaceOnBehalfAsync(Request(service, start: "10:00"));

        Assert.True(
            result.Succeeded,
            "An operator's service placement one hour out was refused: "
            + string.Join(", ", result.Failures.Select(f => f.Code)));
    }

    [Fact]
    public async Task The_horizon_is_waived_for_a_service_placement_too()
    {
        var service = Treatment();
        var (services, _) = Wire(service, resources: Therapy());
        var far = Date.AddDays(7 * 20); // same weekday, well past the 90-day horizon

        var result = await services.PlaceOnBehalfAsync(new ServiceBookingRequest
        {
            ServiceId = service.Id,
            Start = TestData.Utc(far, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });

        Assert.True(
            result.Succeeded,
            "An operator's service placement beyond the horizon was refused: "
            + string.Join(", ", result.Failures.Select(f => f.Code)));
    }

    [Fact]
    public async Task A_busy_candidate_inside_lead_time_is_reported_as_a_conflict()
    {
        // THE GUARD ON THE RULE CHECK'S TERMS, and it is measured rather than argued: with the
        // classification asked on a visitor's terms this returns `service-unavailable`
        // ("This service cannot be booked at that time"), because `lead-time` counts as a
        // deterministic refusal and condemns the only candidate. The therapist is not
        // unbookable — they are BUSY — and an operator told otherwise stops trying instead of
        // offering another time.
        var service = Treatment();
        var (services, bookings) = Wire(
            service, nowUtc: TestData.Utc(Date, "09:00"), resources: Therapy());

        // Occupy the only therapist at the requested interval. Placed on a booker's behalf so
        // the fixture itself is not subject to the lead time it is about to exercise.
        var occupied = await bookings.PlaceOnBehalfAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        });
        Assert.True(occupied.Succeeded);

        var result = await services.PlaceOnBehalfAsync(Request(service, start: "10:00"));

        Assert.False(result.Succeeded);
        Assert.Equal(
            FailureCodes.Conflict,
            Assert.Single(result.Failures).Code);
    }

    [Fact]
    public async Task A_visitor_is_still_refused_where_an_operator_is_not()
    {
        // The pair that makes the two above specific to the terms rather than to the fixture.
        var service = Treatment();
        var (services, _) = Wire(
            service, nowUtc: TestData.Utc(Date, "09:00"), resources: Therapy());

        var visitor = await services.PlaceAsync(Request(service, start: "10:00"));

        Assert.False(visitor.Succeeded);
    }

    // ------------------------------------------------------------------
    // Everything else is unchanged
    // ------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_a_direct_resource_is_delegated_untouched()
    {
        var (services, bookings) = Wire(service: null, resources: Therapy());
        var request = new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "10:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        };

        var viaService = await services.PlaceOnBehalfAsync(request);

        Assert.True(viaService.Succeeded);
        // Delegated, therefore subject to the booking service's rules alone — including the
        // waived direct-bookability, which this resource withholds.
        Assert.Null(viaService.Value.Service);
        Assert.NotNull(bookings);
    }

    [Fact]
    public async Task Spec_scenario_an_unfulfillable_service_is_reported_as_it_always_was()
    {
        var service = Treatment();
        var (services, _) = Wire(service); // no resources at all

        var operatorResult = await services.PlaceOnBehalfAsync(Request(service));
        var visitorResult = await services.PlaceAsync(Request(service));

        Assert.False(operatorResult.Succeeded);
        Assert.Equal(
            visitorResult.Failures.Select(f => f.Code),
            operatorResult.Failures.Select(f => f.Code));
    }

    [Fact]
    public async Task Spec_scenario_a_visitors_service_placement_is_unchanged()
    {
        var service = Treatment();
        var (services, _) = Wire(service, resources: Therapy());

        var visitor = await services.PlaceAsync(Request(service, minutes: 30));

        Assert.False(visitor.Succeeded);
        Assert.Contains(visitor.Failures, f => f.Code == FailureCodes.DurationTooShort);
    }

    [Fact]
    public async Task An_operators_service_booking_is_confirmed_under_approval()
    {
        var service = Treatment();
        var (services, _) = Wire(service, autoConfirm: false, resources: Therapy());

        var result = await services.PlaceOnBehalfAsync(Request(service));

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Confirmed, result.Value.Status);
    }

    [Fact]
    public async Task A_visitors_service_booking_still_waits_under_approval()
    {
        var service = Treatment();
        var (services, _) = Wire(service, autoConfirm: false, resources: Therapy());

        var result = await services.PlaceAsync(Request(service));

        Assert.True(result.Succeeded);
        Assert.Equal(BookingStatus.Requested, result.Value.Status);
    }

    [Fact]
    public async Task An_operator_placement_needs_a_terms_aware_booking_service_and_says_so()
    {
        // The seam fails loudly rather than running the visitor's rules: a fallback would
        // reintroduce the exact defect it exists to prevent, wearing the look of robustness.
        var service = Treatment();
        var resourceStore = new InMemoryResourceStore().Add(Therapy());
        var serviceStore = new InMemoryServiceStore();
        serviceStore.Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);

        var services = new ServiceBookingService(
            serviceStore,
            resourceStore,
            bookingStore,
            new AvailabilityService(resourceStore, bookingStore, time, TestData.Settings),
            new VisitorOnlyBookingService(
                new BookingService(resourceStore, bookingStore, time, TestData.Settings)),
            TestData.Settings);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => services.PlaceOnBehalfAsync(Request(service)));

        // ...and a visitor's placement through the same service is unaffected.
        Assert.True((await services.PlaceAsync(Request(service))).Succeeded);
    }

    /// <summary>
    /// An <see cref="IBookingService"/> that does not offer the internal terms-aware rule
    /// check — what a host substituting its own implementation looks like from here.
    /// </summary>
    private sealed class VisitorOnlyBookingService(IBookingService inner) : IBookingService
    {
        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceOnBehalfAsync(
            BookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceOnBehalfAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => inner.PlaceAsync(request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceForServiceAsync(
            ServiceAttribution service,
            MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default)
            => inner.PlaceForServiceAsync(service, request, cancellationToken);

        public Task<DomainResult<Booking>> PlaceForServiceOnBehalfAsync(
            ServiceAttribution service,
            MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default)
            => inner.PlaceForServiceOnBehalfAsync(service, request, cancellationToken);

        public DomainResult CheckPlacementRules(
            Resource resource, DateTimeOffset start, TimeSpan duration)
            => inner.CheckPlacementRules(resource, start, duration);

        public Task<DomainResult<Booking>> MoveAsync(
            Guid bookingId,
            DateTimeOffset newStart,
            TimeSpan newLength,
            CancellationToken cancellationToken = default)
            => inner.MoveAsync(bookingId, newStart, newLength, cancellationToken);

        public Task<DomainResult<Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> CancelAsVisitorAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.CancelAsVisitorAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> ConfirmAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.ConfirmAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> DeclineAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.DeclineAsync(bookingId, cancellationToken);

        public Task<DomainResult<Booking>> EraseBookerAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
            => inner.EraseBookerAsync(bookingId, cancellationToken);
    }
}
