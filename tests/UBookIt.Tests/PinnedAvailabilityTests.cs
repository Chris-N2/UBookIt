using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The service bookable-starts query asked <em>conditionally</em>: given this
/// resource, when can the service be booked (service-booking spec, "Availability
/// conditional on a pinned resource").
/// <para>
/// The pinned answer is a new guarantee beside the unpinned one, never a
/// replacement for it, so both halves are asserted throughout: what pinning
/// removes, and what an unpinned query still says.
/// </para>
/// </summary>
public class PinnedAvailabilityTests
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

    private sealed record Harness(ServiceBookingService Core, BookingService Bookings, Service Service);

    private static Harness Build(Service service, params Resource[] resources)
    {
        var services = new InMemoryServiceStore().Add(service);
        var resourceStore = new InMemoryResourceStore();

        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var (core, bookings, _) = TestData.ServiceBookingWith(services, resourceStore);

        return new Harness(core, bookings, service);
    }

    private static Service Svc(params ServiceRole[] roles)
        => Service.Create("Massage", null, roles, id: Id(900)).Value;

    private static async Task<IReadOnlyList<ServiceBookableStart>> StartsAsync(
        Harness harness, Guid? pinned = null)
    {
        var result = await harness.Core.GetBookableStartsAsync(harness.Service.Id, Date, Date, pinned);

        Assert.True(result.Succeeded, string.Join("; ", result.Failures.Select(f => f.Code)));
        return result.Value;
    }

    /// <summary>
    /// Two answers compared by what they SAY rather than by reference. The runs
    /// are a list on a record, so record equality compares them by reference and
    /// two identical answers are unequal — which would make an "unchanged"
    /// assertion fail for the right result and, worse, could be "fixed" by
    /// weakening it to a count.
    /// </summary>
    private static IEnumerable<(DateTimeOffset Start, TimeSpan Min, TimeSpan Max, TimeSpan Step)> Shape(
        IEnumerable<ServiceBookableStart> starts)
        => starts.SelectMany(start => start.Runs.Select(run => (start.StartUtc, run.Min, run.Max, run.Step)));

    /// <summary>Books a resource solid over an interval, so it can fulfil nothing there.</summary>
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

    // ---------------------------------------------------------------------
    // 3.4 — the single-slot fast path (design D4). The commonest configuration
    // in the product, and the one an implementation threading only the general
    // path would silently ignore.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_single_role_count_one_service_honours_the_pin()
    {
        // One role, count 1: `FeasibleRuns` short-circuits here and never consults
        // the assignment at all, so the pin has to narrow the union itself. Jane
        // is booked solid all day and Sam is free throughout — an implementation
        // that dropped the pin on this path would offer Sam's whole day for a
        // request that names Jane, and every multi-role test would still pass.
        var harness = Build(Svc(new ServiceRole(Therapist, 1)), Res(2, Therapist, "Jane"), Res(3, Therapist, "Sam"));

        await OccupyAsync(harness, Id(2), "09:00", 480);

        Assert.Empty(await StartsAsync(harness, Id(2)));

        // Non-vacuity, and the half that makes the emptiness mean something: Sam
        // is free all day, so the service itself has plenty of starts and the
        // unpinned query says so.
        Assert.NotEmpty(await StartsAsync(harness));
        Assert.NotEmpty(await StartsAsync(harness, Id(3)));
    }

    [Fact]
    public async Task A_single_role_pin_narrows_to_the_hours_that_resource_works()
    {
        // The same path with nothing booked at all, so the narrowing cannot come
        // from a claims filter: Jane works mornings and Sam afternoons, and each
        // pin sees only its own half of the day.
        var harness = Build(
            Svc(new ServiceRole(Therapist, 1)),
            Res(2, Therapist, "Jane", open: "09:00", close: "12:00"),
            Res(3, Therapist, "Sam", open: "13:00", close: "17:00"));

        var jane = await StartsAsync(harness, Id(2));
        var sam = await StartsAsync(harness, Id(3));
        var anyone = await StartsAsync(harness);

        Assert.NotEmpty(jane);
        Assert.NotEmpty(sam);

        Assert.All(jane, start => Assert.True(start.StartUtc < TestData.Utc(Date, "12:00")));
        Assert.All(sam, start => Assert.True(start.StartUtc >= TestData.Utc(Date, "13:00")));

        // Together they are the service's own answer: neither pin invented a start
        // and neither lost one.
        Assert.Equal(
            anyone.Select(s => s.StartUtc).Order(),
            jane.Concat(sam).Select(s => s.StartUtc).Order());
    }

    // ---------------------------------------------------------------------
    // 3.5 — the general path, and what pinning may never do.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_pinned_query_offers_only_starts_that_resource_can_serve()
    {
        // Two roles, so this runs the assignment rather than the fast path. Jane
        // is claimed all morning; the room and Sam are free all day.
        var harness = Build(
            Svc(new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"),
            Res(2, Therapist, "Jane"),
            Res(3, Therapist, "Sam"));

        await OccupyAsync(harness, Id(2), "09:00", 180);

        var pinned = await StartsAsync(harness, Id(2));

        Assert.NotEmpty(pinned);
        Assert.All(pinned, start => Assert.True(start.StartUtc >= TestData.Utc(Date, "12:00")));

        // The service itself is bookable through the morning, on Sam — which is
        // exactly the promise a dropped pin would have made.
        var unpinned = await StartsAsync(harness);
        Assert.Contains(unpinned, start => start.StartUtc == TestData.Utc(Date, "09:00"));
    }

    [Fact]
    public async Task The_pinned_answer_never_exceeds_the_unpinned_one()
    {
        // Pinning constrains the assignment and can only remove possibilities, so
        // a pinned query offering a start the unpinned query does not is a fault
        // rather than a richer answer. Asserted over every start AND every length,
        // because a run is where an extra length would hide.
        var harness = Build(
            Svc(new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room", granularity: 30, min: 30, max: 120),
            Res(2, Therapist, "Jane", granularity: 30, min: 30, max: 90),
            Res(3, Therapist, "Sam", granularity: 60, min: 60, max: 120));

        await OccupyAsync(harness, Id(2), "10:00", 60);

        var unpinned = await StartsAsync(harness);

        foreach (var pin in new[] { Id(1), Id(2), Id(3) })
        {
            var pinned = await StartsAsync(harness, pin);

            Assert.NotEmpty(pinned);

            foreach (var start in pinned)
            {
                var same = Assert.Single(unpinned, s => s.StartUtc == start.StartUtc);

                foreach (var length in start.Runs.SelectMany(run => run.Lengths()))
                {
                    Assert.True(
                        same.Admits(length),
                        $"pinning {pin} offered {length} at {start.StartUtc}, which the service does not offer");
                }
            }
        }
    }

    [Fact]
    public async Task An_unpinned_query_is_unchanged()
    {
        // The guarantee beside the new one: supplying no pin leaves the query's
        // result exactly what it was. Asserted by comparing the parameter's two
        // spellings — omitted, and explicitly null — over a pool where pinning
        // would visibly differ.
        var harness = Build(
            Svc(new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"),
            Res(2, Therapist, "Jane", close: "12:00"),
            Res(3, Therapist, "Sam", open: "12:00"));

        var omitted = await harness.Core.GetBookableStartsAsync(harness.Service.Id, Date, Date);
        var explicitNull = await StartsAsync(harness, pinned: null);

        Assert.True(omitted.Succeeded);
        Assert.Equal(Shape(omitted.Value), Shape(explicitNull));

        // And it names no resource, then or now: the response carries starts and
        // runs, and there is nowhere in it for an id to appear.
        Assert.NotEmpty(explicitNull);
        Assert.All(explicitNull, start => Assert.NotEmpty(start.Runs));

        // Non-vacuity: a pin really would change this answer.
        Assert.NotEqual(explicitNull.Count, (await StartsAsync(harness, Id(2))).Count);
    }

    [Fact]
    public async Task A_start_offered_by_a_pinned_query_places_with_the_same_pin()
    {
        // The claim the whole feature rests on: the availability answer is
        // honoured at placement when the same pin is supplied. Both are asked of
        // the same graph, so a divergence here is the drift the shared computation
        // exists to prevent.
        var harness = Build(
            Svc(new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(1, ResourceTypes.Room, "Treatment Room"),
            Res(2, Therapist, "Jane", close: "12:00"),
            Res(3, Therapist, "Sam"));

        var starts = await StartsAsync(harness, Id(2));
        var first = starts[0];

        var placed = await harness.Core.PlaceAsync(new ServiceBookingRequest
        {
            ServiceId = harness.Service.Id,
            Start = first.StartUtc,
            Duration = first.Runs[0].Min,
            Booker = TestData.Booker(),
            PinnedResourceId = Id(2),
        });

        Assert.True(placed.Succeeded, string.Join("; ", placed.Failures.Select(f => f.Code)));
        Assert.Contains(placed.Value.Claims, claim => claim.ResourceId == Id(2));
    }

    [Fact]
    public async Task A_resource_eligible_for_two_overlapping_roles_is_pinned_into_either()
    {
        // Two `therapist` roles told apart by capability, and Jane satisfies both.
        // The pin names the BOOKING rather than a role, so the assignment chooses
        // which slot she fills and the other is filled from the remaining
        // candidates — a pin bound to one role would answer with half the starts.
        var harness = Build(
            Svc(
                ServiceRole.Create(Therapist, ["cert-x"]).Value,
                ServiceRole.Create(Therapist, null).Value),
            Res(2, Therapist, "Jane", capabilities: "cert-x"),
            Res(3, Therapist, "Sam"));

        var pinned = await StartsAsync(harness, Id(2));
        var unpinned = await StartsAsync(harness);

        // Jane is the only cert-x therapist, so every assignment contains her —
        // pinning her removes nothing at all.
        Assert.Equal(Shape(unpinned), Shape(pinned));
        Assert.NotEmpty(pinned);

        // And Sam, who can only fill the unrestricted role, is pinnable too.
        Assert.Equal(Shape(unpinned), Shape(await StartsAsync(harness, Id(3))));
    }

    [Fact]
    public async Task A_pinned_query_over_a_role_of_count_two_still_needs_the_others()
    {
        // "This one, plus N−1 chosen for you" (design D5): pinning Jane into a
        // role of count 2 requires a second therapist beside her, so a day on
        // which she is the only one free offers nothing.
        var harness = Build(
            Svc(ServiceRole.Create(Therapist, null, count: 2).Value),
            Res(2, Therapist, "Jane"),
            Res(3, Therapist, "Sam"));

        Assert.NotEmpty(await StartsAsync(harness, Id(2)));

        await OccupyAsync(harness, Id(3), "09:00", 480);

        Assert.Empty(await StartsAsync(harness, Id(2)));

        // And without the pin the answer is the same, because the shortage is real
        // rather than a property of the pin.
        Assert.Empty(await StartsAsync(harness));
    }

    // ---------------------------------------------------------------------
    // 3.6 — a pin outside every pool (design D7), and what is reported first.
    // ---------------------------------------------------------------------

    private static async Task<string> FailureCodeAsync(
        Harness harness, Guid? pinned, DateOnly? from = null, DateOnly? to = null, Guid? serviceId = null)
    {
        var result = await harness.Core.GetBookableStartsAsync(
            serviceId ?? harness.Service.Id, from ?? Date, to ?? Date, pinned);

        Assert.False(result.Succeeded);
        return result.Failures[0].Code;
    }

    [Fact]
    public async Task A_pin_naming_a_resource_of_the_wrong_type_is_rejected()
    {
        var harness = Build(
            Svc(new ServiceRole(Therapist, 1)),
            Res(2, Therapist, "Jane"),
            Res(1, ResourceTypes.Room, "Treatment Room"));

        // Rejected rather than ignored: answering "here is when this service is
        // available" while discarding the resource the caller named would send
        // them to submit a start they had been told was good.
        Assert.Equal(FailureCodes.ResourceNotEligible, await FailureCodeAsync(harness, Id(1)));
    }

    [Fact]
    public async Task A_pin_naming_a_resource_excluded_by_capabilities_is_rejected()
    {
        var harness = Build(
            Svc(ServiceRole.Create(Therapist, ["cert-x"]).Value),
            Res(2, Therapist, "Jane", capabilities: "cert-x"),
            Res(3, Therapist, "Sam"));

        Assert.Equal(FailureCodes.ResourceNotEligible, await FailureCodeAsync(harness, Id(3)));

        // Non-vacuity: the qualifying therapist is accepted, so the rejection is
        // about the capability rather than about pinning at all.
        Assert.NotEmpty(await StartsAsync(harness, Id(2)));
    }

    [Fact]
    public async Task A_pin_naming_an_unknown_resource_is_rejected()
    {
        var harness = Build(Svc(new ServiceRole(Therapist, 1)), Res(2, Therapist, "Jane"));

        Assert.Equal(FailureCodes.ResourceNotEligible, await FailureCodeAsync(harness, Id(404)));
    }

    [Fact]
    public async Task A_pin_excluded_by_duration_is_rejected()
    {
        // The third eligibility filter, which is easy to forget: the service's
        // duration range admits nothing this resource can provide, so it is in no
        // candidate pool — exactly as if its type were wrong.
        var service = Service.Create(
            "Massage",
            ServiceDuration.Variable(Mins(90), Mins(120)).Value,
            [new ServiceRole(Therapist, 1)],
            id: Id(900)).Value;

        var harness = Build(
            service,
            Res(2, Therapist, "Jane", min: 30, max: 120),
            Res(3, Therapist, "Sam", min: 30, max: 60));

        Assert.Equal(FailureCodes.ResourceNotEligible, await FailureCodeAsync(harness, Id(3)));
        Assert.NotEmpty(await StartsAsync(harness, Id(2)));
    }

    [Fact]
    public async Task An_unknown_service_is_reported_before_the_pin()
    {
        // The service bounds whether the question can be asked at all, so it is
        // reported in preference — a caller told "that resource is not eligible"
        // for a service that does not exist would go looking for the wrong fault.
        var harness = Build(Svc(new ServiceRole(Therapist, 1)), Res(2, Therapist, "Jane"));

        Assert.Equal(
            FailureCodes.ServiceNotFound,
            await FailureCodeAsync(harness, Id(404), serviceId: Id(999)));
    }

    [Fact]
    public async Task An_invalid_range_is_reported_before_the_pin()
    {
        // Same reasoning, and the range is checked before anything is even loaded.
        var harness = Build(Svc(new ServiceRole(Therapist, 1)), Res(2, Therapist, "Jane"));

        Assert.Equal(
            FailureCodes.DateRangeInvalid,
            await FailureCodeAsync(harness, Id(404), from: Date.AddDays(1), to: Date));
    }

    [Fact]
    public async Task A_pin_is_rejected_even_where_some_role_has_no_candidates()
    {
        // Before the empty-pool guard, as on the placement path: a pin naming a
        // resource outside every pool is equally wrong whether some pool is empty
        // or merely lacks that resource, and the caller's own mistake is the more
        // useful thing to report. The alternative answers "no availability" for a
        // request that was malformed.
        var harness = Build(
            Svc(new ServiceRole(ResourceTypes.Room, 1), new ServiceRole(Therapist, 1)),
            Res(2, Therapist, "Jane"));

        Assert.Equal(FailureCodes.ResourceNotEligible, await FailureCodeAsync(harness, Id(404)));

        // Non-vacuity: without a pin this same service answers "no starts"
        // successfully, so the failure above is the pin's and not the empty pool's.
        var empty = await harness.Core.GetBookableStartsAsync(harness.Service.Id, Date, Date);
        Assert.True(empty.Succeeded);
        Assert.Empty(empty.Value);
    }
}
