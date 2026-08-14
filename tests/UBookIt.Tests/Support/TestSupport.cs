using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;

namespace UBookIt.Tests.Support;

/// <summary>A TimeProvider pinned to a fixed instant.</summary>
public sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => nowUtc;
}

/// <summary>
/// Doubles for both resource ports, mirroring <see cref="InMemoryServiceStore"/>.
/// The management side exists so the DTO → mapper → controller seam can be
/// exercised without a database; store-level semantics that this double cannot
/// honour (delete's in-use rule) throw rather than pretending.
/// </summary>
public sealed class InMemoryResourceStore : IResourceStore, IResourceManagementStore
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

    public Task<IReadOnlyList<Resource>> ListByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        // Unpaged by contract — a truncated candidate pool would silently change
        // the answer rather than fail, so the double must not clamp either.
        IReadOnlyList<Resource> items = _resources.Values
            .Where(r => string.Equals(r.Type, type, StringComparison.Ordinal))
            .OrderBy(r => r.Id)
            .ToList();

        return Task.FromResult(items);
    }

    public Task<DomainResult<Resource>> CreateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        _resources[resource.Id] = resource;
        return Task.FromResult(DomainResult<Resource>.Success(resource));
    }

    /// <summary>Full replace, as the SQL store does — never a merge.</summary>
    public Task<DomainResult<Resource>> UpdateAsync(Resource resource, CancellationToken cancellationToken = default)
    {
        if (!_resources.ContainsKey(resource.Id))
        {
            return Task.FromResult(DomainResult<Resource>.Failure(
                FailureCodes.ResourceNotFound, $"No resource exists with id {resource.Id}."));
        }

        _resources[resource.Id] = resource;
        return Task.FromResult(DomainResult<Resource>.Success(resource));
    }

    public Task<DomainResult> DeleteAsync(Guid resourceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This double cannot honour the in-use delete rule.");

    public Task<IReadOnlyList<ResourceTypeUsage>> ListTypesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ResourceTypeUsage> types = _resources.Values
            .GroupBy(r => r.Type)
            .Select(g => new ResourceTypeUsage(g.Key, g.Count()))
            .OrderBy(t => t.Type, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(types);
    }

    public Task<IReadOnlyList<CapabilityUsage>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CapabilityUsage> usage = _resources.Values
            .SelectMany(r => r.Capabilities.Keys)
            .GroupBy(key => key, StringComparer.Ordinal)
            .Select(g => new CapabilityUsage(g.Key, g.Count()))
            .OrderBy(u => u.Key, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(usage);
    }
}

/// <summary>In-memory service store implementing both the read and management ports.</summary>
public sealed class InMemoryServiceStore : IServiceStore, IServiceManagementStore
{
    private readonly Dictionary<Guid, Service> _services = [];

    public InMemoryServiceStore Add(Service service)
    {
        _services[service.Id] = service;
        return this;
    }

    public Task<Service?> GetAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_services.GetValueOrDefault(serviceId));

    public Task<ServicePage> ListAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 500);

        var total = _services.Count;
        IReadOnlyList<Service> items = _services.Values
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(new ServicePage(items, total));
    }

    public Task<DomainResult<Service>> CreateAsync(Service service, CancellationToken cancellationToken = default)
    {
        _services[service.Id] = service;
        return Task.FromResult(DomainResult<Service>.Success(service));
    }

    public Task<DomainResult<Service>> UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        if (!_services.ContainsKey(service.Id))
        {
            return Task.FromResult(DomainResult<Service>.Failure(
                FailureCodes.ServiceNotFound, $"No service exists with id {service.Id}."));
        }

        _services[service.Id] = service;
        return Task.FromResult(DomainResult<Service>.Success(service));
    }

    public Task<DomainResult> DeleteAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => Task.FromResult(_services.Remove(serviceId)
            ? DomainResult.Success()
            : DomainResult.Failure(FailureCodes.ServiceNotFound, $"No service exists with id {serviceId}."));
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

    public Task<IReadOnlyList<ClaimInfo>> GetClaimsAsync(
        IReadOnlyCollection<Guid> resourceIds, DateTimeOffset fromUtc, DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        var wanted = resourceIds.ToHashSet();

        lock (_gate)
        {
            IReadOnlyList<ClaimInfo> claims = _bookings.Values
                .Where(b => b.Interval.Overlaps(fromUtc, toUtc))
                .SelectMany(b => b.Claims
                    .Where(c => wanted.Contains(c.ResourceId))
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

/// <summary>
/// Reading a single-role service's candidate pool out of the per-role
/// resolution result. Asserts the service really does have one role, so a test
/// written for one role cannot quietly pass by inspecting the first of several.
/// </summary>
public static class ResolutionResultExtensions
{
    public static IReadOnlyList<ServiceCandidate> SingleRolePool(
        this DomainResult<IReadOnlyList<RoleCandidates>> result)
    {
        Assert.True(result.Succeeded, "resolution failed");

        return Assert.Single(result.Value).Candidates;
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

    /// <summary>
    /// A fully wired <see cref="ServiceBookingService"/> over in-memory stores.
    /// Shared so every caller exercises the real collaborator graph — a double
    /// standing in for resolution would let a test agree with itself about which
    /// resources a configuration resolves to.
    /// </summary>
    public static ServiceBookingService ServiceBooking(
        InMemoryServiceStore services, InMemoryResourceStore resources, DateTimeOffset? nowUtc = null)
    {
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(nowUtc ?? Now);

        return new ServiceBookingService(
            services,
            resources,
            bookingStore,
            new AvailabilityService(resources, bookingStore, time, Settings),
            new BookingService(resources, bookingStore, time, Settings),
            Settings);
    }

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
