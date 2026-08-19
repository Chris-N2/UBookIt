using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The one Core function every surface asks "can this service ever be fulfilled
/// as configured" (service-booking spec, "Structural unfulfillability is
/// answerable from Core").
/// <para>
/// Exercised with <b>no host at all</b> — no store, no clock, no HTTP context —
/// which is the property that made lifting it out of the Web project worth doing:
/// the delivery API can now publish the same answer instead of deriving a second
/// one that would be free to disagree.
/// </para>
/// </summary>
public class ServiceFulfillabilityTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Res(
        int id,
        string type,
        string open = "09:00",
        int granularity = 30,
        int min = 30,
        int max = 480)
        => Resource.Create(
            type,
            $"Resource {id}",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly(open, "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    /// <summary>
    /// A role's pool, built by hand rather than resolved through a store — which
    /// is exactly what the function's purity buys, and what a test of it should
    /// therefore demonstrate.
    /// </summary>
    private static RoleCandidates Pool(ServiceRole role, params Resource[] resources)
        => new(
            role,
            [.. resources.Select(resource => new ServiceCandidate(
                resource,
                new DurationRange(
                    resource.Availability.Constraints.MinDuration,
                    resource.Availability.Constraints.MaxDuration)))]);

    [Fact]
    public void A_shortfall_is_reported_as_permanent()
    {
        // Two therapists needed and one exists: no assignment can fill both slots,
        // on any date, whatever the calendar says.
        var pools = new[] { Pool(ServiceRole.Create(Therapist, null, count: 2).Value, Res(2, Therapist)) };

        Assert.True(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void A_role_with_no_eligible_resource_is_reported_as_permanent()
    {
        // Subsumed by the shortfall question rather than asked separately: a slot
        // with no candidates has no saturating assignment. Stated as its own test
        // because it is the case a reader most expects to see, and the one an
        // implementation is most likely to special-case twice.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room)),
            Pool(new ServiceRole(Therapist, 1)),
        };

        Assert.True(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void A_permanent_grid_misalignment_is_reported_as_permanent()
    {
        // 09:00 stepped by 30 against 09:15 stepped by 20: the two grids never
        // meet, on any day. Both pools are perfectly healthy, so nothing else in
        // the product would say why the service is permanently empty.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 120)),
            Pool(new ServiceRole(Therapist, 1), Res(2, Therapist, open: "09:15", granularity: 20, min: 20, max: 120)),
        };

        // Non-vacuity: the OTHER two questions are silent here, so this test is
        // about the misalignment and not about a pool or a length.
        Assert.Null(PoolSufficiency.FindShortfall(pools));
        Assert.NotEmpty(ServiceFulfillability.CommonLengthMinutes(pools));

        Assert.True(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void No_common_length_is_reported_as_permanent()
    {
        // The room can only be booked for 30 minutes and the therapist only for
        // 20. Each role is healthy, their grids coincide, and no length exists
        // that both can provide — the question ⑩ found by mutation, and the one
        // that lived in the Web project until this function was lifted.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 30)),
            Pool(new ServiceRole(Therapist, 1), Res(2, Therapist, granularity: 20, min: 20, max: 20)),
        };

        Assert.Null(PoolSufficiency.FindShortfall(pools));
        Assert.Null(StartAlignment.FindMisalignment(pools));

        Assert.Empty(ServiceFulfillability.CommonLengthMinutes(pools));
        Assert.True(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void A_well_configured_service_is_not_reported_as_permanent()
    {
        // The pair that makes every assertion above non-vacuous: a predicate that
        // always answered "permanent" would pass them all.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room)),
            Pool(new ServiceRole(Therapist, 1), Res(2, Therapist)),
        };

        Assert.False(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void The_function_answers_about_configuration_only_never_about_the_calendar()
    {
        // One-directional, and this is the direction that matters: the three
        // questions consult no booking calendar, so a fully booked service is
        // NOT structurally unfulfillable. Reporting it as permanent would tell a
        // consumer to stop asking about a service that is bookable next week.
        //
        // Demonstrated by the shape of the call rather than only asserted: the
        // pools carry no bookings because there is nowhere in them to put any.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room)),
            Pool(new ServiceRole(Therapist, 1), Res(2, Therapist)),
        };

        Assert.False(ServiceFulfillability.IsPermanentlyUnfulfillable(pools));
    }

    [Fact]
    public void The_common_lengths_are_every_length_all_roles_can_provide()
    {
        // The lifted computation itself, stated so a change to it has to be
        // deliberate: the intersection of the roles' grids, not either role's own
        // range.
        var pools = new[]
        {
            Pool(new ServiceRole(ResourceTypes.Room, 1), Res(1, ResourceTypes.Room, granularity: 30, min: 30, max: 60)),
            Pool(new ServiceRole(Therapist, 1), Res(2, Therapist, granularity: 30, min: 30, max: 120)),
        };

        Assert.Equal([30, 60], ServiceFulfillability.CommonLengthMinutes(pools));
    }

    [Fact]
    public void The_reason_code_names_nothing_about_the_configuration()
    {
        // The code travels to an anonymous response, so it may not carry the role,
        // the resource type, the capability, the count, or a pool size. A constant
        // has no room for any of them — asserted anyway, because the tempting
        // "improvement" is to make it richer.
        var code = ServiceFulfillability.NotFulfillableCode;

        Assert.Equal("service-not-fulfillable", code);
        Assert.DoesNotContain(Therapist, code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("room", code, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(code, "0123456789".Select(c => c.ToString()).Where(code.Contains));
    }
}
