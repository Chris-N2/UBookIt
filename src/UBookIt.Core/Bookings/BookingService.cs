using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;

namespace UBookIt.Core.Bookings;

/// <summary>A request to place a booking. Start is an absolute instant; wall-clock rules are applied in the site zone.</summary>
public sealed record BookingRequest
{
    public required Guid ResourceId { get; init; }

    public required DateTimeOffset Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public required Booker Booker { get; init; }
}

/// <summary>
/// A request to place one booking claiming several resources at once — the
/// shape a multi-role service resolves to. All claims share the booking's single
/// interval, which is what the store's atomic contract is defined over.
/// </summary>
/// <remarks>
/// <b>This carries no service, deliberately.</b> Attributing a booking to a service is done
/// by calling <see cref="IBookingService.PlaceForServiceAsync"/>, for the same reason there
/// is no "this is a direct booking" flag on <see cref="BookingRequest"/>: a request field
/// would restate the call site in a form a caller can get wrong. A caller could then name a
/// service whose roles these resources do not satisfy, and the result is not a wrong answer
/// to a query — it is a wrong <i>fact</i>, stored permanently and indistinguishable later
/// from a real attribution.
/// </remarks>
public sealed record MultiClaimBookingRequest
{
    /// <summary>
    /// The resources to claim, one per role. Every one of them is validated
    /// against its own constraints: a booking is placed only where each
    /// resource would have accepted it on its own.
    /// </summary>
    public required IReadOnlyList<Guid> ResourceIds { get; init; }

    public required DateTimeOffset Start { get; init; }

    public required TimeSpan Duration { get; init; }

    public required Booker Booker { get; init; }
}

/// <summary>Places, confirms, declines and cancels bookings.</summary>
public interface IBookingService
{
    /// <summary>
    /// Runs the placement validation pipeline (bookings spec order) and, when
    /// valid, atomically places a booking via the store — <c>Confirmed</c> under
    /// <see cref="SiteBookingSettings.AutoConfirm"/>, <c>Requested</c> when the
    /// site requires approval.
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(BookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Places a booking for a named resource <b>on a booker's behalf</b> — an operator
    /// recording a booking somebody made by telephone or at a desk.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same pipeline, evaluated on <see cref="PlacementTerms.Operator"/>: a lead of zero
    /// (the start must not have passed) and no horizon, with the site's approval setting
    /// waived, so the booking is <c>Confirmed</c> whatever that setting says. Open hours,
    /// granularity, duration bounds and conflict bind an operator exactly as they bind a
    /// visitor — they are facts about what is physically bookable rather than policy about
    /// who is asking.
    /// </para>
    /// <para>
    /// <b>This overload does not apply the direct-bookability rule</b>, and that is the whole
    /// difference between it and <see cref="PlaceAsync(BookingRequest, CancellationToken)"/>.
    /// The permission exists so that a stranger cannot assemble a combination the business
    /// cannot deliver; an operator is the person trusted to make that judgement. The waiver is
    /// structural — this method does not contain the check — rather than a flag either method
    /// reads, for the reason the direct overload's own remarks give.
    /// </para>
    /// <para>
    /// <b>It knows resources and nothing about services.</b> A booking placed for a service
    /// has a length rule this layer cannot see, so an operator's single entry point is
    /// <c>IServiceBookingService</c>'s on-behalf placement, which applies it and then delegates
    /// here. A caller reaching this method directly gets the resources' rules alone.
    /// </para>
    /// </remarks>
    Task<DomainResult<Booking>> PlaceOnBehalfAsync(
        BookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same placement on a booker's behalf, recording the service it was placed for —
    /// the overload a service booking placed by an operator runs through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists rather than letting a caller hand <see cref="PlacementTerms"/> to the
    /// existing service overload, and the reason is not tidiness. A terms parameter on a
    /// published port is a parameter the anonymous delivery endpoint could pass
    /// <see cref="PlacementTerms.Operator"/> to — turning a visitor's request into an
    /// operator's by supplying an argument. Naming the operator's placements instead means
    /// the anonymous path cannot ask for operator terms at all, whatever it passes.
    /// </para>
    /// <para>
    /// As with the visitor's service overload, a null attribution throws rather than placing:
    /// a caller reaching here has asked for a service booking, and an unattributed one would
    /// be a booking that succeeded, looks ordinary, and is permanently wrong.
    /// </para>
    /// </remarks>
    Task<DomainResult<Booking>> PlaceForServiceOnBehalfAsync(
        ServiceAttribution service,
        MultiClaimBookingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same pipeline for a booking claiming several resources: every
    /// resource is validated against its own constraints, and one booking
    /// carrying a claim for each is placed through the store's all-or-nothing
    /// contract.
    /// <para>
    /// The single-resource overload delegates here rather than duplicating the
    /// rules, so direct placement and service placement cannot drift apart.
    /// </para>
    /// </summary>
    Task<DomainResult<Booking>> PlaceAsync(
        MultiClaimBookingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same placement, recording the service it was placed for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A distinct entry point rather than a field on the request</b>, so that naming a
    /// service is inseparable from actually placing through one. Which method you call is
    /// what the booking <i>is</i> — the discipline direct placement already follows.
    /// </para>
    /// <para>
    /// It takes the attribution whole, including the display name, because the snapshot has
    /// to be taken by the caller that has the service in hand at placement time. Reading
    /// the name here would be a second load of something the caller already holds, and
    /// reading it later would answer with the name the service has <i>now</i>.
    /// </para>
    /// <para>
    /// Placement behaviour is identical to the overload above in every respect. The only
    /// difference is the value recorded on a booking that succeeds.
    /// </para>
    /// </remarks>
    Task<DomainResult<Booking>> PlaceForServiceAsync(
        ServiceAttribution service,
        MultiClaimBookingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the placement rules that are properties of the request and one
    /// resource — everything the pipeline evaluates <em>before</em> the conflict
    /// check — and reports whether that resource would have refused the request
    /// regardless of who else holds it.
    /// <para>
    /// Exists so a caller that skips an attempt can still classify it the way an
    /// attempt would have. Service placement excludes candidates already claimed
    /// at the requested interval; whether such a candidate contributes a lost
    /// race or a deterministic refusal depends on rules the exclusion cannot
    /// see — a start off the resource's grid, outside its open hours, inside its
    /// lead time, or beyond its horizon. Answering that by re-implementing the
    /// rules would give two implementations free to disagree, so it is answered
    /// by the same code the pipeline runs.
    /// </para>
    /// <para>
    /// Touches no store and places nothing.
    /// </para>
    /// </summary>
    DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration);

    Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a <see cref="BookingStatus.Requested"/> booking.
    /// </summary>
    /// <remarks>
    /// Succeeds only from <c>Requested</c>; any other status fails with
    /// <see cref="FailureCodes.InvalidStatusTransition"/> and touches the store not at all.
    /// Fails with <see cref="FailureCodes.BookingNotFound"/> when no booking has the id.
    /// There is deliberately no general "set status" operation — one operation per verb, so
    /// the transitions the status machine refuses have no door to knock on.
    /// </remarks>
    Task<DomainResult<Booking>> ConfirmAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declines a <see cref="BookingStatus.Requested"/> booking.
    /// </summary>
    /// <remarks>
    /// Succeeds only from <c>Requested</c>, on the same terms as
    /// <see cref="ConfirmAsync"/>. A declined booking remains — row, interval, booker,
    /// service — but stops blocking, so the slot it held is immediately bookable again.
    /// </remarks>
    Task<DomainResult<Booking>> DeclineAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Erases a booking's booker contact details and member key, keeping the booking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anonymisation, not deletion.</b> The booking keeps its id, reference, interval,
    /// time zone, status, creation time, claims and service, and goes on blocking exactly the
    /// time it blocked before. Deleting it would return time the site had sold, and would
    /// destroy the site's own record of what happened — which is not what the person asking
    /// has asked for, and not something a site may concede on their behalf.
    /// </para>
    /// <para>
    /// <b>Idempotent.</b> Erasing an already-erased booking succeeds and changes nothing,
    /// including the recorded instant. This is deliberately the opposite of
    /// <see cref="CancelAsync"/>, which refuses a second attempt — see
    /// <see cref="Booking.EraseBooker"/> for why the two differ.
    /// </para>
    /// <para>
    /// Fails with <see cref="FailureCodes.BookingNotFound"/> when no booking has the id.
    /// </para>
    /// </remarks>
    Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a booking to a new start and length: a placement of the booking it already is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The new interval runs the placement pipeline against every resource the booking claims —
    /// the same rules, in the same order, with the same codes — under
    /// <see cref="PlacementTerms.Operator"/>: a lead of zero (the start must not have passed)
    /// and no horizon. Open hours, granularity, duration bounds and conflict bind exactly as they
    /// bind a visitor. See <see cref="PlacementTerms"/> for why.
    /// </para>
    /// <para>
    /// Permitted from <c>Requested</c> and <c>Confirmed</c> only, and the status is unchanged by
    /// it. Every claim is kept: a service booking moves with the resources it was assigned, and
    /// one that is busy at the new time produces <see cref="FailureCodes.Conflict"/> rather than
    /// a substitution. A move to the interval already held fails with
    /// <see cref="FailureCodes.IntervalUnchanged"/>; an unknown booking with
    /// <see cref="FailureCodes.BookingNotFound"/>.
    /// </para>
    /// <para>
    /// Reported through the observation port with the interval the booking held before, after
    /// the store has agreed and only on success.
    /// </para>
    /// </remarks>
    Task<DomainResult<Booking>> MoveAsync(
        Guid bookingId, DateTimeOffset newStart, TimeSpan newLength, CancellationToken cancellationToken = default);
}

public sealed class BookingService(
    IResourceStore resourceStore,
    IBookingStore bookingStore,
    TimeProvider timeProvider,
    SiteBookingSettings settings,
    IBookingObserver? observer = null,
    IBookingReferenceFactory? referenceFactory = null) : IBookingService, IPlacementRuleCheck
{
    /// <summary>
    /// Where a booking's quotable reference comes from. Never null.
    /// </summary>
    /// <remarks>
    /// Defaulted, and for a different reason from the observer above. Omitting an observer
    /// produces silence; omitting this produces a perfectly good random reference, exactly as
    /// the inline <c>Guid.NewGuid()</c> below produces a perfectly good id. There is no wrong
    /// fact to record either way, which is what separates both of these from
    /// <c>Booking.Create</c>'s service attribution — omitting <i>that</i> claimed a service
    /// booking had been placed directly.
    /// </remarks>
    private readonly IBookingReferenceFactory _referenceFactory =
        referenceFactory ?? new RandomBookingReferenceFactory();

    /// <summary>
    /// How many references placement will try before concluding the generator is broken.
    /// </summary>
    /// <remarks>
    /// Small on purpose. This is not a budget for bad luck — one collision in 27^8 is already
    /// remarkable — it is the number of attempts after which "unlucky" stops being the
    /// explanation and "returning the same value" starts.
    /// </remarks>
    private const int MaxReferenceAttempts = 5;

    /// <summary>
    /// Where placement and cancellation are reported. Never null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Defaulted, unlike <c>Booking.Create</c>'s attribution — and the difference is worth
    /// stating, because the last change made the opposite call.</b> Omitting a booking's
    /// service silently recorded a wrong <i>fact</i>: a service booking that claimed it was
    /// placed directly. Omitting an observer records nothing and claims nothing; it produces
    /// silence, which is exactly right for the several dozen tests that construct this
    /// service to exercise placement rules and have no interest in who is told.
    /// </para>
    /// <para>
    /// Production never omits it: the only construction is by the container, which supplies
    /// every registered dependency. The residual risk is a host that forgets to register one
    /// and gets silence — so a test asserts the composer registers it, rather than trusting
    /// that nobody will.
    /// </para>
    /// </remarks>
    private readonly IBookingObserver _observer = observer ?? new NullBookingObserver();

    /// <summary>
    /// Tells the observer, and lets nothing it does reach the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The booking is already stored by the time this runs.</b> An exception escaping here
    /// would report failure for a booking that exists — and a visitor told their booking
    /// failed books again, so somebody else's handler throwing would produce a double
    /// booking. That is worse than any notification is worth, which is why this is absolute
    /// rather than best-effort.
    /// </para>
    /// <para>
    /// <b>The catch is silent, and that is a consequence rather than a choice.</b> Core has
    /// no logging dependency because it has no dependencies at all, and acquiring one to
    /// report a third party's fault would trade the property this design is built on for a
    /// log line. The shipped Umbraco adapter logs — it has an <c>ILogger</c> and is where the
    /// diagnostics belong. An observer written by somebody else is theirs to instrument.
    /// </para>
    /// </remarks>
    private static async Task TellAsync(Func<Task> tell)
    {
        try
        {
            await tell().ConfigureAwait(false);
        }
        catch
        {
            // Deliberately swallowed. See the remarks above: the booking is committed, and
            // the caller's answer must not depend on what an observer does.
        }
    }

    public async Task<DomainResult<Booking>> PlaceAsync(
        BookingRequest request, CancellationToken cancellationToken = default)
    {
        // Direct booking, and the guard belongs here rather than in either caller:
        // the delivery endpoint and the no-JavaScript flow both arrive at this
        // overload, and the second of them never crosses an HTTP boundary at all,
        // so a transport-layer check would not see it (design D1).
        //
        // It is also why there is no "this is a direct booking" flag on the request.
        // Calling THIS overload is what being a direct booking means; service
        // placement composes its own MultiClaimBookingRequest and cannot reach the
        // check, whatever its resources permit. A flag would restate the call site
        // in a form a caller can get wrong.
        var resource = await resourceStore
            .GetAsync(request.ResourceId, cancellationToken)
            .ConfigureAwait(false);

        // Only a resource that exists AND withholds the permission is refused here.
        // A missing one falls through deliberately, so the pipeline below reports
        // `resource-not-found` as it always has rather than this rule inventing a
        // second opinion about a resource it could not read.
        if (resource is { DirectlyBookable: false })
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ResourceNotDirectlyBookable,
                $"{resource.DisplayName} is not offered for booking on its own.",
                nameof(BookingRequest.ResourceId));
        }

        // Ahead of the rule pipeline, not inside it: the request was never one this
        // resource accepts, so reporting `outside-open-hours` for it would send a
        // booker to look for a better time that does not exist.
        //
        // The cost is one extra read on the direct path, since the overload below
        // loads the resource again. Deliberate: sharing the load would mean either
        // threading a loaded aggregate through the multi-claim signature — visible
        // to every service placement, which has no use for it — or hoisting the
        // check down into that overload, which is exactly what must not happen.
        return await PlaceAsync(
            new MultiClaimBookingRequest
            {
                ResourceIds = [request.ResourceId],
                Start = request.Start,
                Duration = request.Duration,
                Booker = request.Booker,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<DomainResult<Booking>> PlaceOnBehalfAsync(
        BookingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A SIBLING of the direct overload above, deliberately not a caller of it.
        //
        // That overload carries the direct-bookability guard, and an operator is not subject
        // to it. Reaching the pipeline without passing through it is what makes the waiver
        // structural: there is no branch here reading a flag, there is a call that does not go
        // past the guard. The cost is the three lines of request composition below, duplicated.
        // Sharing them would mean hoisting the guard down into the shared path, which is
        // exactly what the direct overload's remarks forbid.
        //
        // It also means the visitor overload is untouched by this change, so "the visitor path
        // is unchanged" is a property of the diff rather than a claim about it.
        return PlaceAsync(
            new MultiClaimBookingRequest
            {
                ResourceIds = [request.ResourceId],
                Start = request.Start,
                Duration = request.Duration,
                Booker = request.Booker,
            },
            service: null,
            PlacementTerms.Operator,
            cancellationToken);
    }

    public Task<DomainResult<Booking>> PlaceForServiceOnBehalfAsync(
        ServiceAttribution service,
        MultiClaimBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);

        return PlaceAsync(request, service, PlacementTerms.Operator, cancellationToken);
    }

    public Task<DomainResult<Booking>> PlaceAsync(
        MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
        => PlaceAsync(request, service: null, operatorTerms: null, cancellationToken);

    public Task<DomainResult<Booking>> PlaceForServiceAsync(
        ServiceAttribution service,
        MultiClaimBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        // Throwing rather than placing. A caller reaching this overload with a null
        // attribution has asked for a service booking and would get an unattributed one —
        // a booking that succeeded, looks ordinary, and is silently wrong in the exact way
        // this whole change exists to prevent. There is no sensible fallback: the general
        // overload is what "no service" means, and it is one call away.
        ArgumentNullException.ThrowIfNull(service);

        return PlaceAsync(request, service, operatorTerms: null, cancellationToken);
    }

    /// <summary>
    /// The one multi-claim pipeline. Every public entry point delegates here and they differ
    /// only in what they pass for <paramref name="service"/> and
    /// <paramref name="operatorTerms"/> — duplicating the pipeline is how they would drift
    /// into placing different bookings for the same request.
    /// </summary>
    /// <param name="operatorTerms">
    /// The terms an operator is placing under, or <c>null</c> for a visitor's placement.
    /// <para>
    /// <b><c>null</c> is not a missing argument; it is what a visitor's terms look like from
    /// here.</b> A visitor's lead and horizon ARE each claimed resource's own configuration,
    /// so there is no single value to hand in — the loop below resolves them per resource, as
    /// it always has. An operator's are one value for the whole placement, which is why only
    /// that case has something to pass. The parameter carries no default deliberately: every
    /// call site states which kind of placement it is making, and none can acquire the wrong
    /// one by forgetting.
    /// </para>
    /// </param>
    private async Task<DomainResult<Booking>> PlaceAsync(
        MultiClaimBookingRequest request,
        ServiceAttribution? service,
        PlacementTerms? operatorTerms,
        CancellationToken cancellationToken)
    {
        var zoneResult = AvailabilityService.ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(zoneResult.Failures);
        }

        var zone = zoneResult.Value;

        if (request.ResourceIds.Count == 0)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.ClaimsInvalid, "A booking must claim at least one resource.");
        }

        // Loaded before the interval rules, in the order supplied, so that an
        // unknown resource is still reported as such rather than masked by
        // whatever else the request got wrong.
        var resources = new List<Resource>(request.ResourceIds.Count);

        foreach (var resourceId in request.ResourceIds)
        {
            var loaded = await resourceStore.GetAsync(resourceId, cancellationToken).ConfigureAwait(false);
            if (loaded is null)
            {
                return DomainResult<Booking>.Failure(
                    FailureCodes.ResourceNotFound, $"No resource exists with id {resourceId}.");
            }

            resources.Add(loaded);
        }

        var windowResult = ResolveWindow(zone, request.Start, request.Duration);
        if (!windowResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(windowResult.Failures);
        }

        var window = windowResult.Value;
        var interval = window.Interval;
        var failures = new List<DomainFailure>();

        // Rules 2–7 are properties of a resource, so they run once per claimed
        // resource: a booking is placed only where every one of them would have
        // accepted it alone. For a single-claim request this is exactly the
        // pipeline it has always been.
        foreach (var resource in resources)
        {
            failures.AddRange(ValidateAgainst(
                resource,
                window,
                request.Duration,
                operatorTerms ?? PlacementTerms.Visitor(resource.Availability.Constraints)));
        }

        if (failures.Count > 0)
        {
            return DomainResult<Booking>.Failure(OrderByPipeline(failures));
        }

        // Rule 8: conflict — checked atomically by the store across every claimed
        // resource (bookings spec, "Atomic placement contract").
        //
        // The loop is for reference collisions and nothing else: a booking is immutable, so a
        // taken reference cannot be swapped in place — a new booking has to be built. Every
        // other outcome, success or failure, leaves immediately, so a rejected placement is
        // never retried and the pipeline above never runs twice.
        // Read from the terms rather than from the entry point, so that the status decision
        // below stays the single place a new booking's status is chosen.
        var approvalApplies = operatorTerms?.ApprovalApplies ?? true;

        DomainResult<Booking> placed;
        var attempt = 0;

        while (true)
        {
            var booking = Booking.Create(
                Guid.NewGuid(),
                _referenceFactory.Next(),
                interval,
                request.Booker,
                [.. resources.Select(r => new ResourceClaim(r.Id))],
                // The one site that decides what a new booking IS, for the direct, the
                // service AND the operator path alike — all three funnel through here, so
                // they cannot disagree.
                //
                // An operator's terms waive approval (PlacementTerms.ApprovalApplies): the
                // person placing the booking reviewed it by placing it. A visitor's terms
                // never waive it, which is what the absence of operator terms means here. The
                // setting is read exactly once, and no caller chooses a status of its own.
                approvalApplies && !settings.AutoConfirm
                    ? BookingStatus.Requested
                    : BookingStatus.Confirmed,
                window.NowUtc,
                service);

            placed = await bookingStore.PlaceAsync(booking, cancellationToken).ConfigureAwait(false);

            if (placed.Succeeded || !placed.Failures.Any(f => f.Code == FailureCodes.ReferenceTaken))
            {
                break;
            }

            // Bounded, because an unbounded retry turns a broken generator into a hang. At 27^8
            // values a genuine collision is already a curiosity; several in a row is not bad
            // luck, it is a generator returning the same value — a bug, and reported as one
            // rather than as something the booker did wrong.
            if (++attempt >= MaxReferenceAttempts)
            {
                throw new InvalidOperationException(
                    $"Could not obtain an unused booking reference in {MaxReferenceAttempts} attempts. "
                    + $"The configured {nameof(IBookingReferenceFactory)} is returning values that are already in use.");
            }
        }

        // After the store agreed, and only then. Announcing before the commit would report a
        // booking that may not exist; announcing on failure would report one that does not.
        if (placed.Succeeded)
        {
            await TellAsync(() => _observer.BookingPlacedAsync(placed.Value, cancellationToken))
                .ConfigureAwait(false);
        }

        return placed;
    }

    public async Task<DomainResult<Booking>> CancelAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await bookingStore.GetBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        var transition = booking.Cancel();
        if (!transition.Succeeded)
        {
            return DomainResult<Booking>.Failure(transition.Failures);
        }

        await bookingStore.UpdateAsync(booking, cancellationToken).ConfigureAwait(false);

        // Reached only where the status machine allowed the transition AND the update was
        // written — which is what makes this unambiguous. Cancellation succeeds from
        // Requested or Confirmed and nowhere else, so being told at all means the booking has
        // just become cancelled, and a second attempt fails above and tells nobody.
        await TellAsync(() => _observer.BookingCancelledAsync(booking, cancellationToken))
            .ConfigureAwait(false);

        return DomainResult<Booking>.Success(booking);
    }

    public Task<DomainResult<Booking>> ConfirmAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
        => TransitionAsync(
            bookingId,
            booking => booking.Confirm(),
            booking => _observer.BookingConfirmedAsync(booking, cancellationToken),
            cancellationToken);

    public Task<DomainResult<Booking>> DeclineAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
        => TransitionAsync(
            bookingId,
            booking => booking.Decline(),
            booking => _observer.BookingDeclinedAsync(booking, cancellationToken),
            cancellationToken);

    /// <remarks>
    /// The same shape as <see cref="CancelAsync"/>, shared by confirm and decline: load,
    /// transition, store, observe. The observation runs only where the status machine allowed
    /// the transition AND the update was written — both transitions succeed only from
    /// <see cref="BookingStatus.Requested"/>, so being told at all means the booking has just
    /// become what its status says, and a second attempt fails above the store and tells
    /// nobody.
    /// </remarks>
    private async Task<DomainResult<Booking>> TransitionAsync(
        Guid bookingId,
        Func<Booking, DomainResult> transition,
        Func<Booking, Task> observe,
        CancellationToken cancellationToken)
    {
        var booking = await bookingStore.GetBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        var result = transition(booking);
        if (!result.Succeeded)
        {
            return DomainResult<Booking>.Failure(result.Failures);
        }

        await bookingStore.UpdateAsync(booking, cancellationToken).ConfigureAwait(false);

        await TellAsync(() => observe(booking)).ConfigureAwait(false);

        return DomainResult<Booking>.Success(booking);
    }

    /// <summary>The statuses a move is permitted from — handed to the store so the same set is checked inside the write.</summary>
    private static readonly BookingStatus[] MovableFrom = [BookingStatus.Requested, BookingStatus.Confirmed];

    public async Task<DomainResult<Booking>> MoveAsync(
        Guid bookingId, DateTimeOffset newStart, TimeSpan newLength, CancellationToken cancellationToken = default)
    {
        var booking = await bookingStore.GetBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        // The status rule first, and from the aggregate, so a cancelled booking is refused
        // before any resource is loaded or any rule run. The store checks it AGAIN inside the
        // write — see IBookingStore.MoveAsync — because this read can be stale by then.
        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Confirmed))
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.InvalidStatusTransition, $"A {booking.Status} booking cannot be moved.");
        }

        var zoneResult = AvailabilityService.ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(zoneResult.Failures);
        }

        // Rule 1 and the calendar window, exactly as placement resolves them.
        var windowResult = ResolveWindow(zoneResult.Value, newStart, newLength);
        if (!windowResult.Succeeded)
        {
            return DomainResult<Booking>.Failure(windowResult.Failures);
        }

        var window = windowResult.Value;

        // Rules 2–7 against EVERY claimed resource, on operator terms. The claims are the
        // booking's own — a move keeps them — so a resource that has since been deleted is a
        // genuine fault rather than a bad request, and is reported as such.
        var failures = new List<DomainFailure>();

        foreach (var claim in booking.Claims)
        {
            var resource = await resourceStore.GetAsync(claim.ResourceId, cancellationToken).ConfigureAwait(false);
            if (resource is null)
            {
                return DomainResult<Booking>.Failure(
                    FailureCodes.ResourceNotFound,
                    $"Booking {bookingId} claims resource {claim.ResourceId}, which no longer exists.");
            }

            failures.AddRange(ValidateAgainst(resource, window, newLength, PlacementTerms.Operator));
        }

        if (failures.Count > 0)
        {
            return DomainResult<Booking>.Failure(OrderByPipeline(failures));
        }

        // After the pipeline, so "unchanged" is only ever said about an interval the rules
        // accepted — an operator always hears the truth about the time they typed. Asked, not
        // applied: the aggregate changes only once the store has agreed, below.
        var permitted = booking.CanMoveTo(window.Interval);
        if (!permitted.Succeeded)
        {
            return DomainResult<Booking>.Failure(permitted.Failures);
        }

        // Rule 8: conflict — checked atomically by the store across every claimed resource,
        // excluding this booking's own claims, with the status re-checked inside the write.
        var stored = await bookingStore
            .MoveAsync(bookingId, window.Interval, MovableFrom, cancellationToken)
            .ConfigureAwait(false);

        if (!stored.Succeeded)
        {
            return DomainResult<Booking>.Failure(stored.Failures);
        }

        // The store has written it; now the aggregate says so too. CanMoveTo passed on this
        // same instance a moment ago and nothing here has changed it since, so this cannot
        // fail — and if it ever did, the answer is not "moved" and not "refused" but "the
        // domain and the store disagree", which is reported as exactly that.
        var previous = booking.Interval;
        var applied = booking.MoveTo(window.Interval);
        if (!applied.Succeeded)
        {
            throw new InvalidOperationException(
                $"Booking {bookingId} was moved in storage but the aggregate refused the same move: "
                + string.Join(", ", applied.Failures.Select(f => f.Code)));
        }

        // After the store agreed, and only then — carrying where the booking came from, which
        // is the one thing a report of a move cannot do without.
        await TellAsync(() => _observer.BookingMovedAsync(booking, previous, cancellationToken))
            .ConfigureAwait(false);

        return DomainResult<Booking>.Success(booking);
    }

    public async Task<DomainResult<Booking>> EraseBookerAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
    {
        // Straight to the store's erase, with no read-modify-write of an aggregate.
        //
        // Reading a booking, calling EraseBooker on it and writing the whole thing back is
        // what produced the last two defects: whatever else that aggregate carried was written
        // too, from a copy that could already be out of date. Erasure needs to say one thing —
        // "this booking is erased, as at this instant" — and saying only that removes the
        // entire class.
        //
        // The clock is still this service's, so the instant is comparable with the creation
        // time and is not read from ambient system time at the storage layer.
        var existed = await bookingStore
            .EraseBookerAsync(bookingId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        if (!existed)
        {
            return DomainResult<Booking>.Failure(
                FailureCodes.BookingNotFound, $"No booking exists with id {bookingId}.");
        }

        // Read back, and report what STORAGE holds rather than what this call intended. The
        // store absorbs an erasure onto a row that already records one, so a second caller's
        // instant is not the stored instant — and this value is published as the endpoint's
        // `erasedUtc` under a contract saying it is the FIRST erasure's. An answer about
        // stored state has to come from storage.
        var stored = await bookingStore.GetBookingAsync(bookingId, cancellationToken).ConfigureAwait(false);

        if (stored is null)
        {
            // The write matched a row and the read did not find one. Nothing in the package
            // deletes a booking, so this is unreachable — and it is answered rather than
            // asserted away because the honest report is "something is wrong", never "no such
            // booking". The caller's data HAS been erased; telling them it never existed would
            // be the one answer guaranteed to be false.
            throw new InvalidOperationException(
                $"Booking {bookingId} was erased but could not be read back.");
        }

        return DomainResult<Booking>.Success(stored);
    }

    public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return CheckPlacementRules(resource, start, duration, PlacementTerms.Visitor(resource.Availability.Constraints));
    }

    /// <inheritdoc />
    /// <summary>
    /// The same rules under explicit terms. Internal, so the terms a rule is evaluated on are
    /// decided by an operation in this assembly rather than chosen by a caller — the public
    /// member is the visitor's, and the operator's terms are reached only through an operator
    /// operation.
    /// </summary>
    public DomainResult CheckPlacementRules(
        Resource resource, DateTimeOffset start, TimeSpan duration, PlacementTerms terms)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(terms);

        var zoneResult = AvailabilityService.ResolveZone(settings);
        if (!zoneResult.Succeeded)
        {
            return DomainResult.Failure(zoneResult.Failures);
        }

        var windowResult = ResolveWindow(zoneResult.Value, start, duration);
        if (!windowResult.Succeeded)
        {
            return DomainResult.Failure(windowResult.Failures);
        }

        var failures = ValidateAgainst(resource, windowResult.Value, duration, terms);

        return failures.Count == 0 ? DomainResult.Success() : DomainResult.Failure(OrderByPipeline(failures));
    }

    /// <summary>
    /// Everything rule 1 establishes: the interval the request describes, the
    /// local date it starts on, and the day window the open-hours rule needs.
    /// Shared so placement and rule-checking cannot disagree about what a
    /// request even means.
    /// </summary>
    private sealed record PlacementWindow(
        TimeZoneInfo Zone,
        BookingInterval Interval,
        DateOnly LocalStartDate,
        DateOnly WindowFrom,
        DateOnly WindowTo,
        DateTimeOffset NowUtc);

    private DomainResult<PlacementWindow> ResolveWindow(
        TimeZoneInfo zone, DateTimeOffset start, TimeSpan duration)
    {
        // Rule 1: interval-invalid — including an interval that cannot be
        // represented at all. The addition is guarded rather than attempted:
        // unguarded it throws before `BookingInterval.Create` gets the chance to
        // reject it, which is an unhandled exception out of an anonymous
        // endpoint rather than a structured failure (out-of-range-dates D2).
        if (!CalendarBounds.TryAdd(start, duration, out var end))
        {
            return DomainResult<PlacementWindow>.Failure(
                FailureCodes.IntervalInvalid,
                "The requested start and duration do not describe a representable interval.");
        }

        var intervalResult = BookingInterval.Create(start, end, settings.TimeZoneId);
        if (!intervalResult.Succeeded)
        {
            return DomainResult<PlacementWindow>.Failure(intervalResult.Failures);
        }

        var interval = intervalResult.Value;
        var localStartDate = WallClockMapper.ToLocalDate(interval.StartUtc, zone);

        // Still rule 1: the open-hours rule inspects the day either side of the
        // start, and the walk steps once past the later of them. At the edges of
        // the calendar that window cannot be formed, so the request cannot be
        // evaluated — reported as an unrepresentable interval rather than thrown
        // (design D3). Checked here, ahead of the accumulating rules, because
        // `interval-invalid` is first in the documented pipeline order.
        if (!CalendarBounds.TryWindowAround(localStartDate, out var windowFrom, out var windowTo))
        {
            return DomainResult<PlacementWindow>.Failure(
                FailureCodes.IntervalInvalid,
                "The requested start is too close to the limits of the calendar to be evaluated.");
        }

        return DomainResult<PlacementWindow>.Success(new PlacementWindow(
            zone, interval, localStartDate, windowFrom, windowTo, timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Rules 2–7 for one resource: duration, lead time, horizon, open hours and
    /// the window-relative grid. The single home of every rule that is a
    /// property of a resource rather than of the calendar as a whole.
    /// <para>
    /// Rules 5 and 6 read <paramref name="terms"/> rather than the resource: they are
    /// policy about who is placing, and <see cref="PlacementTerms"/> says why. Every other
    /// rule here reads the resource, as it always has.
    /// </para>
    /// </summary>
    private static List<DomainFailure> ValidateAgainst(
        Resource resource, PlacementWindow window, TimeSpan duration, PlacementTerms terms)
    {
        var constraints = resource.Availability.Constraints;
        var interval = window.Interval;
        var failures = new List<DomainFailure>();

        // Rules 2–4: granularity (duration part), duration bounds
        failures.AddRange(AvailabilityService.ValidateDuration(duration, constraints));

        // Rule 5: lead-time. Under operator terms the lead is zero, which makes this rule
        // exactly "the start has not passed" — the one guard that survives for everybody.
        if (interval.StartUtc < window.NowUtc + terms.LeadTime)
        {
            failures.Add(new DomainFailure(
                FailureCodes.LeadTime,
                terms.LeadTime > TimeSpan.Zero
                    ? $"Bookings require at least {terms.LeadTime.TotalMinutes:0} minutes notice."
                    : "The start time has already passed."));
        }

        // Rule 6: horizon. Saturating, because a horizon reaching past the end of
        // the calendar means "no effective limit" rather than an error — and
        // `HorizonDays` is only validated as positive, so a large one would
        // otherwise throw for every request against that resource. Absent under
        // operator terms: there is nothing to saturate and nothing to refuse.
        if (terms.HorizonDays is { } horizonDays)
        {
            var lastLocalDate = CalendarBounds.AddDaysSaturating(
                WallClockMapper.ToLocalDate(window.NowUtc, window.Zone), horizonDays);
            if (window.LocalStartDate > lastLocalDate)
            {
                failures.Add(new DomainFailure(
                    FailureCodes.Horizon,
                    $"Bookings may be placed at most {horizonDays} days ahead."));
            }
        }

        // Rule 7: outside-open-hours — the interval must fit inside one open
        // window; granularity of the start is relative to its window's start,
        // which keeps placement consistent with slot projection.
        var open = FreeTimeCalculator.OpenIntervals(
            resource.Availability, window.Zone, window.WindowFrom, window.WindowTo);
        var openWindow = open.FirstOrDefault(w => w.StartUtc <= interval.StartUtc && interval.EndUtc <= w.EndUtc);

        if (openWindow == default)
        {
            failures.Add(new DomainFailure(
                FailureCodes.OutsideOpenHours, "The requested interval is outside the resource's open hours."));
        }
        else if ((interval.StartUtc - openWindow.StartUtc).Ticks % constraints.Granularity.Ticks != 0)
        {
            failures.Add(new DomainFailure(
                FailureCodes.Granularity,
                $"The start time must align to {constraints.Granularity.TotalMinutes:0}-minute steps from the window's start."));
        }

        return failures;
    }

    private static readonly string[] PipelineOrder =
    [
        FailureCodes.IntervalInvalid,
        FailureCodes.Granularity,
        FailureCodes.DurationTooShort,
        FailureCodes.DurationTooLong,
        FailureCodes.LeadTime,
        FailureCodes.Horizon,
        FailureCodes.OutsideOpenHours,
        FailureCodes.Conflict,
    ];

    private static List<DomainFailure> OrderByPipeline(List<DomainFailure> failures)
        => [.. failures.OrderBy(f => Array.IndexOf(PipelineOrder, f.Code))];
}
