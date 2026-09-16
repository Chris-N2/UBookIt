using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

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
/// Stands in for the service booking service and refuses to be used — every endpoint but move
/// goes nowhere near it, and move's own tests supply a recording one.
/// </summary>
public sealed class UnusedServiceBookingService : IServiceBookingService
{
    private static InvalidOperationException Unexpected([System.Runtime.CompilerServices.CallerMemberName] string member = "")
        => new($"The endpoint reached {member} on the service booking service; it should not.");

    public Task<ServiceResolution> ResolveAsync(
        ServiceRole role, ServiceDuration duration, CancellationToken cancellationToken = default) => throw Unexpected();

    public Task<DomainResult<IReadOnlyList<RoleCandidates>>> ResolveCandidatesAsync(
        Guid serviceId, CancellationToken cancellationToken = default) => throw Unexpected();

    public Task<DomainResult<IReadOnlyList<ServiceBookableStart>>> GetBookableStartsAsync(
        Guid serviceId, DateOnly fromDate, DateOnly toDate, Guid? pinnedResourceId = null,
        CancellationToken cancellationToken = default) => throw Unexpected();

    public Task<DomainResult<Booking>> PlaceAsync(
        ServiceBookingRequest request, CancellationToken cancellationToken = default) => throw Unexpected();

    public Task<DomainResult<Booking>> MoveAsync(
        Guid bookingId, DateTimeOffset newStart, TimeSpan newLength, CancellationToken cancellationToken = default)
        => throw Unexpected();
}

/// <summary>
/// In-memory IBookingStore honouring the atomic placement contract via a lock:
/// the conflict check and the write happen as one critical section.
/// </summary>
public sealed class InMemoryBookingStore : IBookingStore
{
    // WHERE THIS DOUBLE STILL DIFFERS FROM THE SQL STORE, stated rather than left to be found.
    //
    // `EraseBookerAsync` mutates the stored aggregate in place while `UpdateAsync` replaces the
    // entry with a rebuilt one, so a caller holding a reference sees an erasure and not a
    // status change. Harmless — nothing holds one across a write — but it means the stale-copy
    // interleaving that three review rounds turned on CANNOT be staged through this double at
    // all, which is why those guarantees live in the integration suite against a real database.
    //
    // `PlaceAsync` also stores the caller's live instance rather than a copy, so a test that
    // mutates a placed aggregate mutates the store with no write. Nothing relies on that today,
    // and it is recorded because "nothing relies on it" is luck rather than construction.

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Booking> _bookings = [];

    /// <summary>How many batched claim reads have been made against this store.</summary>
    public int BatchClaimReads { get; private set; }

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
            // Counted so a test can assert the cost rather than assume it: service
            // availability composes one batched read over every role's pool, and a
            // composition that slipped into reading per candidate — or per length —
            // would still return the right answer.
            BatchClaimReads++;

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

            // Mirrors the unique index in the real store. Without it this fake would accept
            // two bookings sharing a reference, and every test of the retry would pass here
            // while the behaviour it claims to cover was never exercised — a fake that is
            // more permissive than the thing it stands in for tests nothing.
            if (_bookings.Values.Any(existing => existing.Reference == booking.Reference))
            {
                return Task.FromResult(DomainResult<Booking>.Failure(
                    FailureCodes.ReferenceTaken, "That booking reference is already in use."));
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

    /// <summary>
    /// How many updates this store has been asked to persist.
    /// </summary>
    /// <remarks>
    /// Counted because this store <b>cannot otherwise show that an update happened</b>: it
    /// keeps the same instance the caller mutated, so re-reading a cancelled booking reports
    /// <c>Cancelled</c> whether or not anything was ever persisted. Removing the
    /// <c>UpdateAsync</c> call from cancellation left the whole observation suite green until
    /// this existed.
    /// <para>
    /// That instance-sharing is convenient and has now caused three separate false negatives,
    /// so where a test needs to know that a write <i>occurred</i> rather than that a value
    /// <i>looks right</i>, it asks this.
    /// </para>
    /// </remarks>
    public int UpdateCount { get; private set; }

    /// <summary>
    /// Persists a status change — and, unlike the aggregate-swap this used to be, ONLY that.
    /// </summary>
    /// <remarks>
    /// It replaced the whole stored aggregate, which made this double more permissive than the
    /// thing it stands in for: writing back a pre-erasure copy restored the person, so erasure
    /// was reversible through the package's own test store while the SQL one refused it. A fake
    /// that is more permissive than the real implementation tests nothing — and here it would
    /// have hidden the exact defect two reviews spent themselves finding.
    /// </remarks>
    public Task UpdateAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            UpdateCount++;

            if (!_bookings.TryGetValue(booking.Id, out var stored))
            {
                throw new InvalidOperationException(
                    $"No booking exists with id {booking.Id}; nothing was updated.");
            }

            // The status, rebuilt onto whatever booker is stored — never the caller's copy of
            // it. Mirrors the SQL store writing one column.
            _bookings[booking.Id] = Rebuild(stored, booking.Status);
            return Task.CompletedTask;
        }
    }

    /// <summary>The ids of every stored booking, for a test that has to find one it did not place.</summary>
    public IReadOnlyList<Guid> Ids()
    {
        lock (_gate)
        {
            return [.. _bookings.Keys];
        }
    }

    /// <summary>How many move writes this store has been asked for.</summary>
    public int MoveCount { get; private set; }

    /// <summary>
    /// The fourth narrow write: the interval, and nothing else, under the same critical section
    /// as placement, with the conflict check excluding the booking's own claims and the status
    /// condition inside the write.
    /// </summary>
    /// <remarks>
    /// Rebuilds the stored entry rather than mutating the caller's aggregate, so — as for
    /// <c>UpdateAsync</c> — a stale aggregate's status or booker cannot reach storage through
    /// this path. That is what lets the Core-level "stale aggregate leaves status and booker
    /// alone" scenario mean something against this double.
    /// </remarks>
    public Task<DomainResult> MoveAsync(
        Guid bookingId,
        BookingInterval newInterval,
        IReadOnlyCollection<BookingStatus> permittedFrom,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            MoveCount++;

            if (!_bookings.TryGetValue(bookingId, out var stored))
            {
                return Task.FromResult(DomainResult.Failure(
                    FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}."));
            }

            var resourceIds = stored.Claims.Select(c => c.ResourceId).ToHashSet();

            var conflicts = _bookings.Values.Any(existing =>
                existing.Id != bookingId
                && existing.IsBlocking
                && existing.Interval.Overlaps(newInterval)
                && existing.Claims.Any(c => resourceIds.Contains(c.ResourceId)));

            if (conflicts)
            {
                return Task.FromResult(DomainResult.Failure(
                    FailureCodes.Conflict, "The requested interval conflicts with an existing booking."));
            }

            // The status predicate, evaluated against what is STORED inside the same critical
            // section — never against the caller's copy. Mirrors the SQL statement's WHERE.
            if (!permittedFrom.Contains(stored.Status))
            {
                return Task.FromResult(DomainResult.Failure(
                    FailureCodes.InvalidStatusTransition, $"A {stored.Status} booking cannot be moved."));
            }

            _bookings[bookingId] = Rebuild(stored, stored.Status, newInterval);
            return Task.FromResult(DomainResult.Success());
        }
    }

    /// <summary>How many times the due-for-erasure read was asked for.</summary>
    /// <remarks>
    /// Counted so a test can assert that retention, when it is switched off, issues <b>no query
    /// at all</b> — rather than issuing one and discarding the answer. "Nothing was erased" is
    /// satisfied by both, and only one of them is the requirement.
    /// </remarks>
    public int DueForErasureReads { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(
        DateTimeOffset cutoffUtc, int take, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            DueForErasureReads++;

            // Mirrors the SQL predicate exactly, ordering included. The ordering is not cosmetic
            // here: the sweep takes the head of this set repeatedly and relies on each erasure
            // removing a booking from it, so a double that returned an arbitrary top-n could make
            // a non-terminating sweep look like a terminating one.
            //
            // No status filter, matching the port. A double that quietly filtered would make the
            // status-blindness tests pass without the production query being status-blind.
            IReadOnlyList<Guid> due = _bookings.Values
                .Where(b => b.Interval.EndUtc < cutoffUtc && !b.Booker.IsErased)
                .OrderBy(b => b.Interval.EndUtc)
                .ThenBy(b => b.Id)
                .Take(take)
                .Select(b => b.Id)
                .ToList();

            return Task.FromResult(due);
        }
    }

    /// <summary>How many times the booker-erasure write was asked for.</summary>
    public int EraseCount { get; private set; }

    /// <inheritdoc />
    public Task<bool> EraseBookerAsync(
        Guid bookingId, DateTimeOffset erasedUtc, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            EraseCount++;

            if (!_bookings.TryGetValue(bookingId, out var stored))
            {
                return Task.FromResult(false);
            }

            // Absorbing, exactly as the port requires: an already-erased booking keeps the
            // first instant. `Booking.EraseBooker` is itself a no-op on an erased booker, so
            // this is the domain's own rule rather than a second copy of it.
            stored.EraseBooker(erasedUtc);
            return Task.FromResult(true);
        }
    }

    /// <summary>The stored booking with a different status and/or interval, and everything else untouched.</summary>
    private static Booking Rebuild(Booking stored, BookingStatus status, BookingInterval? interval = null)
        => Booking.Rehydrate(
            stored.Id,
            stored.Reference,
            interval ?? stored.Interval,
            stored.Booker,
            stored.Claims,
            status,
            stored.CreatedUtc,
            stored.Service).Value;
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

    /// <summary>
    /// A room open 08:00–18:00 on <see cref="BaseDate"/>'s day of week (and only
    /// that day), offered for booking on its own.
    /// <para>
    /// <b>The domain default is the opposite.</b> A resource withholds direct
    /// booking unless told otherwise; this helper grants it because it stands for
    /// "an ordinary resource a visitor can book", which is what the placement,
    /// availability and delivery suites are about. A test that means to exercise
    /// the permission itself builds its own resource and says so explicitly —
    /// see <c>DirectBookingTests</c>.
    /// </para>
    /// </summary>
    public static Resource Room(AvailabilityConfiguration? availability = null)
        => Resource.Create(
            directlyBookable: true,
            type: ResourceTypes.Room,
            displayName: "Meeting Room A",
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
        => ServiceBookingWith(services, resources, nowUtc).Services;

    /// <summary>
    /// The same graph, with the collaborators a test needs in order to put
    /// bookings on a resource's calendar — for the properties that are about what
    /// does <em>not</em> change when it fills up.
    /// </summary>
    public static (ServiceBookingService Services, BookingService Bookings, InMemoryBookingStore Store)
        ServiceBookingWith(
            InMemoryServiceStore services, InMemoryResourceStore resources, DateTimeOffset? nowUtc = null)
    {
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(nowUtc ?? Now);
        var bookings = new BookingService(resources, bookingStore, time, Settings);

        return (
            new ServiceBookingService(
                services,
                resources,
                bookingStore,
                new AvailabilityService(resources, bookingStore, time, Settings),
                bookings,
                Settings),
            bookings,
            bookingStore);
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

/// <summary>
/// An <see cref="IEmailSender"/> for tests that only need the flows to be constructible.
/// </summary>
/// <remarks>
/// <b>Defaults to a host that CANNOT send</b>, which is the default install: no SMTP host and no
/// pickup directory. A stub defaulting to "can send" would put every existing flow test on the
/// sending branch of the privacy notice, which is the branch most sites never see.
/// </remarks>
public sealed class TestEmailSender(bool canSend = false) : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public bool CanSendRequiredEmail() => canSend;

    public Task SendAsync(EmailMessage message, string emailType)
        => SendAsync(message, emailType, false, null);

    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification)
        => SendAsync(message, emailType, enableNotification, null);

    public Task SendAsync(
        EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
    {
        Sent.Add(message);

        return Task.CompletedTask;
    }
}
