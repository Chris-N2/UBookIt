using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Persistence.Responsibility;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// The responsibility assignment store against real SQL Server: round-trips, wholesale
/// replace, idempotent writes, the missing-subject refusal, the booking-facing union,
/// and assignments not outliving their subject.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ResponsibilityStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IResponsibilityStore Store(UBookIt.Persistence.UBookItDbContext context)
        => new SqlResponsibilityStore(context);

    private static ResponsibilityAssignment User(out Guid key)
        => new(ResponsibilityPartyKind.User, key = Guid.NewGuid());

    private static ResponsibilityAssignment Group(out Guid key)
        => new(ResponsibilityPartyKind.Group, key = Guid.NewGuid());

    [Fact]
    public async Task An_assignment_round_trips_for_a_resource()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var expected = new[] { User(out _), User(out _), Group(out _) };

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Resource, resourceId, expected, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var actual = await Store(read).GetAsync(ResponsibilitySubject.Resource, resourceId, Ct);

        Assert.Equal(expected.OrderBy(a => a.Kind).ThenBy(a => a.Key), actual.OrderBy(a => a.Kind).ThenBy(a => a.Key));
    }

    [Fact]
    public async Task A_service_takes_assignments_the_same_way()
    {
        fixture.EnsureAvailable();
        var serviceId = await SeedServiceAsync();
        var expected = User(out _);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Service, serviceId, [expected], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        Assert.Equal(expected, Assert.Single(
            await Store(read).GetAsync(ResponsibilitySubject.Service, serviceId, Ct)));
    }

    [Fact]
    public async Task Writing_replaces_the_set_wholesale()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var survivor = User(out _);

        await using (var context = fixture.CreateContext())
        {
            var store = Store(context);
            Assert.True((await store.ReplaceAsync(
                ResponsibilitySubject.Resource, resourceId, [User(out _), User(out _), Group(out _)], Ct)).Succeeded);
            Assert.True((await store.ReplaceAsync(
                ResponsibilitySubject.Resource, resourceId, [survivor], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        Assert.Equal(survivor, Assert.Single(
            await Store(read).GetAsync(ResponsibilitySubject.Resource, resourceId, Ct)));
    }

    [Fact]
    public async Task A_duplicate_in_the_written_set_is_stored_once()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var assignment = User(out _);

        await using (var context = fixture.CreateContext())
        {
            // The same assignment twice in one set: stored once, and not an error — the
            // compound primary key makes the duplicate impossible, the store's Distinct
            // makes it a non-event.
            Assert.True((await Store(context).ReplaceAsync(
                ResponsibilitySubject.Resource, resourceId, [assignment, assignment], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        Assert.Single(await Store(read).GetAsync(ResponsibilitySubject.Resource, resourceId, Ct));
    }

    [Fact]
    public async Task Assigning_to_a_missing_subject_is_rejected_with_nothing_stored()
    {
        fixture.EnsureAvailable();
        var missing = Guid.NewGuid();

        await using var context = fixture.CreateContext();
        var store = Store(context);

        var resource = await store.ReplaceAsync(ResponsibilitySubject.Resource, missing, [User(out _)], Ct);
        var service = await store.ReplaceAsync(ResponsibilitySubject.Service, missing, [User(out _)], Ct);

        Assert.False(resource.Succeeded);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(resource.Failures).Code);
        Assert.False(service.Succeeded);
        Assert.Equal(FailureCodes.ServiceNotFound, Assert.Single(service.Failures).Code);
        Assert.Empty(await store.GetAsync(ResponsibilitySubject.Resource, missing, Ct));
        Assert.Empty(await store.GetAsync(ResponsibilitySubject.Service, missing, Ct));
    }

    [Fact]
    public async Task A_booking_unions_its_service_and_every_claimed_resource()
    {
        fixture.EnsureAvailable();
        var firstResource = await Seed.EveryDayRoomAsync(fixture, Ct);
        var secondResource = await Seed.EveryDayRoomAsync(fixture, Ct);
        var serviceId = await SeedServiceAsync();
        var onFirst = User(out _);
        var onSecond = Group(out _);
        var onService = User(out _);

        await using (var context = fixture.CreateContext())
        {
            var store = Store(context);
            Assert.True((await store.ReplaceAsync(ResponsibilitySubject.Resource, firstResource, [onFirst], Ct)).Succeeded);
            Assert.True((await store.ReplaceAsync(ResponsibilitySubject.Resource, secondResource, [onSecond], Ct)).Succeeded);
            Assert.True((await store.ReplaceAsync(ResponsibilitySubject.Service, serviceId, [onService], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var assignments = await Store(read)
            .GetForBookingAsync(BookingFor([firstResource, secondResource], serviceId), Ct);

        Assert.Equal(3, assignments.Count);
        Assert.Contains(onFirst, assignments);
        Assert.Contains(onSecond, assignments);
        Assert.Contains(onService, assignments);
    }

    [Fact]
    public async Task One_party_on_service_and_resource_is_one_assignment()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);
        var serviceId = await SeedServiceAsync();
        var party = User(out _);

        await using (var context = fixture.CreateContext())
        {
            var store = Store(context);
            Assert.True((await store.ReplaceAsync(ResponsibilitySubject.Resource, resourceId, [party], Ct)).Succeeded);
            Assert.True((await store.ReplaceAsync(ResponsibilitySubject.Service, serviceId, [party], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        Assert.Equal(party, Assert.Single(
            await Store(read).GetForBookingAsync(BookingFor([resourceId], serviceId), Ct)));
    }

    /// <summary>
    /// The discriminator keeps the id spaces apart: a direct booking whose claimed
    /// resource id happens to equal a service's id must not pick up the service's
    /// assignments. Constructed deliberately — Guids do not collide by accident, but a
    /// predicate that ignored SubjectType would pass every other test in this file.
    /// </summary>
    [Fact]
    public async Task Subject_types_do_not_cross_match_on_a_shared_id()
    {
        fixture.EnsureAvailable();
        var sharedId = Guid.NewGuid();
        await Seed.EveryDayRoomAsync(fixture, Ct, id: sharedId);
        await SeedServiceAsync(id: sharedId);
        var onServiceOnly = User(out _);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Service, sharedId, [onServiceOnly], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var store = Store(read);

        // A direct booking claiming the resource with that id: the service's assignment
        // must not surface.
        Assert.Empty(await store.GetForBookingAsync(BookingFor([sharedId], serviceId: null), Ct));
        Assert.False(await store.HasAnyForBookingAsync(BookingFor([sharedId], serviceId: null), Ct));
    }

    [Fact]
    public async Task HasAny_answers_both_ways()
    {
        fixture.EnsureAvailable();
        var assigned = await Seed.EveryDayRoomAsync(fixture, Ct);
        var unassigned = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Resource, assigned, [User(out _)], Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var store = Store(read);

        Assert.True(await store.HasAnyForBookingAsync(BookingFor([assigned], null), Ct));
        Assert.False(await store.HasAnyForBookingAsync(BookingFor([unassigned], null), Ct));
    }

    [Fact]
    public async Task Deleting_a_resource_removes_its_assignments()
    {
        fixture.EnsureAvailable();
        var resourceId = await Seed.EveryDayRoomAsync(fixture, Ct);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Resource, resourceId, [User(out _), Group(out _)], Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlResourceManagementStore(context, new SqlSiteClosureStore(context)).DeleteAsync(resourceId, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        // Read straight through the store: a recycled id must inherit nobody, and the
        // store's own read is what any later subject with this id would see.
        Assert.Empty(await Store(read).GetAsync(ResponsibilitySubject.Resource, resourceId, Ct));
    }

    [Fact]
    public async Task Deleting_a_service_removes_its_assignments()
    {
        fixture.EnsureAvailable();
        var serviceId = await SeedServiceAsync();

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await Store(context)
                .ReplaceAsync(ResponsibilitySubject.Service, serviceId, [User(out _)], Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).DeleteAsync(serviceId, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        Assert.Empty(await Store(read).GetAsync(ResponsibilitySubject.Service, serviceId, Ct));
    }

    // ---- fixtures ----

    private async Task<Guid> SeedServiceAsync(Guid? id = null)
    {
        var service = Service.Create(
            "Consultation",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [new ServiceRole("room", 1)],
            id).Value;

        await using var context = fixture.CreateContext();

        Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        return service.Id;
    }

    /// <summary>
    /// A domain booking naming the given subjects. Never stored: the booking-facing reads
    /// query by the ids the booking carries, which is the whole point — sending happens
    /// after placement, from the domain object in hand.
    /// </summary>
    private static Booking BookingFor(Guid[] resourceIds, Guid? serviceId)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
                "UTC").Value,
            Booker.Create(null, "Integration Tester", "integration@example.com", null).Value,
            resourceIds.Select(id => new ResourceClaim(id)),
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            serviceId is { } sid ? new ServiceAttribution(sid, "Consultation") : null).Value;
}
