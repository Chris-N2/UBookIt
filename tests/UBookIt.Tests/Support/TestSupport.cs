using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Tests.Support;

/// <summary>A TimeProvider pinned to a fixed instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => nowUtc;
}

public sealed class InMemoryResourceStore : IResourceStore
{
    private readonly Dictionary<Guid, Resource> _resources = [];

    public InMemoryResourceStore Add(Resource resource)
    {
        _resources[resource.Id] = resource;
        return this;
    }

    public Task<Resource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_resources.GetValueOrDefault(resourceId));

    public Task<ResourcePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 500);

        var total = _resources.Count;
        IReadOnlyList<Resource> items = _resources.Values
            .OrderBy(r => r.DisplayName).ThenBy(r => r.Id)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(new ResourcePage(items, total));
    }
}

/// <summary>
/// In-memory IBookingStore honouring the atomic placement contract via a lock:
/// the conflict check and the write happen as one critical section.
/// </summary>
public sealed class InMemoryBookingStore : IBookingStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Booking> _bookings = [];

    public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        Guid resourceId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<ClaimInfo> claims = _bookings.Values
                .Where(b => b.Interval.Overlaps(fromUtc, toUtc))
                .SelectMany(b => b.Claims
                    .Where(c => c.ResourceId == resourceId)
                    .Select(c => new ClaimInfo(c.ResourceId, b.Id, b.Interval, b.Status)))
                .ToList();

            return Task.FromResult(claims);
        }
    }

    public Task<DomainResult<Booking>> PlaceAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var conflicts = _bookings.Values.Any(existing =>
                existing.IsBlocking
                && existing.Interval.Overlaps(booking.Interval)
                && existing.Claims.Any(c => booking.Claims.Any(nc => nc.ResourceId == c.ResourceId)));

            if (conflicts)
            {
                return Task.FromResult(DomainResult<Booking>.Failure(
                    FailureCodes.Conflict, "The requested interval conflicts with an existing booking."));
            }

            _bookings[booking.Id] = booking;
            return Task.FromResult(DomainResult<Booking>.Success(booking));
        }
    }

    public Task<Booking?> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_bookings.GetValueOrDefault(bookingId));
        }
    }

    public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _bookings[booking.Id] = booking;
            return Task.CompletedTask;
        }
    }
}

public static class TestData
{
    public const string LondonZoneId = "Europe/London";

    public static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById(LondonZoneId);

    public static SiteBookingSettings Settings { get; } = new() { TimeZoneId = LondonZoneId };

    /// <summary>A Tuesday in September 2026 (BST) used as the default booking date.</summary>
    public static readonly DateOnly BaseDate = new(2026, 9, 15);

    /// <summary>Fixed "now" well before <see cref="BaseDate"/> and within horizon of it.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    public static DayWindow Win(string start, string end)
        => DayWindow.Create(TimeOnly.Parse(start), TimeOnly.Parse(end)).Value;

    /// <summary>Open the given days with a single window.</summary>
    public static WeeklyOpenHours Weekly(string start, string end, params DayOfWeek[] days)
        => WeeklyOpenHours.Create(days.Select(d => (d, Win(start, end)))).Value;

    public static AvailabilityConfiguration Config(
        WeeklyOpenHours openHours,
        IEnumerable<DateException>? exceptions = null,
        BookingConstraints? constraints = null)
        => AvailabilityConfiguration.Create(openHours, exceptions, constraints).Value;

    /// <summary>A room open 08:00–18:00 on <see cref="BaseDate"/>'s day of week (and only that day).</summary>
    public static Resource Room(AvailabilityConfiguration? availability = null)
        => Resource.Create(
            ResourceTypes.Room,
            "Meeting Room A",
            availability: availability ?? Config(Weekly("08:00", "18:00", BaseDate.DayOfWeek))).Value;

    public static Booker Booker(Guid? memberKey = null)
        => UBookIt.Core.Bookings.Booker.Create(memberKey, "Test Person", "test@example.com").Value;

    /// <summary>UTC instant for a wall-clock time on a date in the London zone.</summary>
    public static DateTimeOffset Utc(DateOnly date, string time)
        => WallClockMapper.ToUtc(date, TimeOnly.Parse(time), London);

    public static (BookingService Bookings, AvailabilityService Availability, InMemoryBookingStore Store)
        Services(Resource resource, DateTimeOffset? nowUtc = null)
    {
        var resources = new InMemoryResourceStore().Add(resource);
        var store = new InMemoryBookingStore();
        var time = new FixedTimeProvider(nowUtc ?? Now);

        return (
            new BookingService(resources, store, time, Settings),
            new AvailabilityService(resources, store, time, Settings),
            store);
    }
}
