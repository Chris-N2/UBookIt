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
/// Every route that answers "when can this be booked" accounts for closures: fixed-duration
/// slots, bookable starts, the pure projection, service availability over a candidate pool,
/// and placement validation.
/// </summary>
/// <remarks>
/// <b>Each route is exercised through its own entry point.</b> They share a free-time
/// computation today, but that is an implementation fact and the spec's guarantee is about
/// the answers callers get — a future route that computed its own windows would have to
/// fail one of these.
/// </remarks>
public class SiteClosureAvailabilityTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    /// <summary>
    /// A resource as hydration produces it: <paramref name="closures"/> are the closures that
    /// APPLY to it, and <paramref name="optOuts"/> the ones it is exempt from. The store
    /// never puts an opted-out closure in the first list, so a test for opting out passes the
    /// id in the second and leaves it out of the first — exactly what the mapper does.
    /// </summary>
    private static Resource Room(
        int id, IEnumerable<SiteClosure>? closures = null, IEnumerable<Guid>? optOuts = null)
        => Resource.Create(
            ResourceTypes.Room,
            $"Resource {id}",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(30), minDuration: Mins(30), maxDuration: Mins(480)).Value,
                closures: closures),
            id: Id(id),
            closureOptOuts: optOuts).Value;

    // ---- projections ----

    [Fact]
    public async Task Slot_projection_offers_nothing_on_a_closure_date()
    {
        var closed = Room(1, [TestData.Closure(Date, "Bank holiday")]);
        var (_, availability, _) = TestData.Services(closed);

        var slots = await availability.GetSlotsAsync(closed.Id, Date, Date, Mins(60));

        Assert.True(slots.Succeeded);
        Assert.Empty(slots.Value);
    }

    [Fact]
    public async Task Bookable_starts_omit_a_closure_date_and_keep_its_neighbours()
    {
        // Open every day, so the dates either side are ordinary open days and an empty
        // result on the closure date cannot be the whole range being closed.
        var resource = Resource.Create(
            ResourceTypes.Room,
            "Every day",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Enum.GetValues<DayOfWeek>()),
                closures: [TestData.Closure(Date, "Bank holiday")]),
            id: Id(2)).Value;

        var (_, availability, _) = TestData.Services(resource);

        var starts = await availability.GetBookableStartsAsync(
            resource.Id, Date.AddDays(-1), Date.AddDays(1));

        Assert.True(starts.Succeeded);
        Assert.DoesNotContain(starts.Value, s => DateOnly.FromDateTime(s.StartUtc.UtcDateTime) == Date);
        Assert.Contains(starts.Value, s => DateOnly.FromDateTime(s.StartUtc.UtcDateTime) == Date.AddDays(-1));
        Assert.Contains(starts.Value, s => DateOnly.FromDateTime(s.StartUtc.UtcDateTime) == Date.AddDays(1));
    }

    /// <summary>
    /// The pure projection takes no closure argument — the resource it is handed carries
    /// them. This is the guarantee that let the frozen signature stay frozen.
    /// </summary>
    [Fact]
    public void The_pure_projection_accounts_for_the_resources_closures()
    {
        var closed = Room(3, [TestData.Closure(Date, "Bank holiday")]);
        var open = Room(4);
        var (_, availability, _) = TestData.Services(open);

        var closedResult = availability.ProjectBookableStarts(closed, [], Date, Date);
        var openResult = availability.ProjectBookableStarts(open, [], Date, Date);

        Assert.True(closedResult.Succeeded);
        Assert.Empty(closedResult.Value);

        // The same call on a resource without the closure answers, so the empty result
        // above is the closure and not the projection refusing the range.
        Assert.NotEmpty(openResult.Value);
    }

    // ---- service availability over a pool ----

    private static (ServiceBookingService Services, IBookingService Bookings) Wire(
        Service service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var availability = new AvailabilityService(resourceStore, bookingStore, time, settings);
        var bookings = new BookingService(resourceStore, bookingStore, time, settings);

        return (
            new ServiceBookingService(serviceStore, resourceStore, bookingStore, availability, bookings, settings),
            bookings);
    }

    [Fact]
    public async Task A_service_offers_nothing_when_every_candidate_is_closed()
    {
        var closure = TestData.Closure(Date, "Bank holiday");
        var service = Service.Create("Consultation", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value;
        var (services, _) = Wire(service, Room(5, [closure]), Room(6, [closure]));

        var result = await services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task A_service_still_offers_the_date_when_one_candidate_has_opted_out()
    {
        // The opted-out resource is hydrated WITHOUT the closure, which is what opting out
        // produces — so the pool has one candidate that can fill the role.
        var closure = TestData.Closure(Date, "Bank holiday");
        var service = Service.Create("Consultation", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value;
        var (services, _) = Wire(service, Room(7, [closure]), Room(8, optOuts: [closure.Id]));

        var result = await services.GetBookableStartsAsync(service.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Value);
    }

    // ---- placement ----

    [Fact]
    public async Task Placement_on_a_closure_date_is_refused_as_outside_open_hours()
    {
        var closed = Room(9, [TestData.Closure(Date, "Christmas Day")]);
        var (bookings, _, _) = TestData.Services(closed);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = closed.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.OutsideOpenHours, Assert.Single(result.Failures).Code);
    }

    /// <summary>
    /// Placement is reachable anonymously, so the refusal must not disclose why the date is
    /// shut. Asserted over the whole failure — code, message and field — rather than by
    /// checking the message alone, because the next leak will be in whichever member nobody
    /// looked at.
    /// </summary>
    [Fact]
    public async Task The_refusal_names_neither_the_closure_nor_its_label()
    {
        var closed = Room(10, [TestData.Closure(Date, "Directors' away day")]);
        var (bookings, _, _) = TestData.Services(closed);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = closed.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var failure = Assert.Single(result.Failures);
        var rendered = string.Join(" | ", failure.Code, failure.Message, failure.Field ?? string.Empty);

        Assert.DoesNotContain("away day", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("closure", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("closed", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("holiday", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Placement_succeeds_on_a_date_the_resource_has_opted_out_of()
    {
        // Opted out: it records the exemption, and hydration therefore hands it no closure.
        var closure = TestData.Closure(Date, "Christmas Day");
        var open = Room(11, optOuts: [closure.Id]);
        var (bookings, _, _) = TestData.Services(open);

        var result = await bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = open.Id,
            Start = TestData.Utc(Date, "10:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        Assert.True(result.Succeeded, string.Join(", ", result.Failures.Select(f => f.Code)));
    }
}
