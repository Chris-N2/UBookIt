using System.Globalization;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Services;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Assembles the service booking form view model from Core data
/// (host-independent, so it is unit-testable without an Umbraco host).
/// <para>
/// Everything here is computed over the resolved <see cref="RoleCandidates"/> —
/// the very pools the booking path acts on — rather than over a second
/// eligibility filter of its own. A form that disagreed with placement about
/// which lengths a service offers would be a second implementation of the rule,
/// free to diverge.
/// </para>
/// </summary>
public static class ServiceBookingFormBuilder
{
    /// <summary>
    /// Every whole-minute length that <em>some</em> candidate of <em>every</em>
    /// role can provide, ascending.
    /// <para>
    /// An <b>affordance, not a trust boundary</b>. A length surviving this
    /// intersection is one each role could provide separately; whether distinct
    /// resources can provide it simultaneously is an assignment question, asked
    /// per start by Core's availability query and again by placement. So the
    /// control can offer a length that no start admits — the times list is then
    /// empty and says so — but it can never offer one the service could not
    /// possibly book, which is what a control is for.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Delegates to Core. The computation lifted when the
    /// structural-unfulfillability triad did (design D6): an empty result here is
    /// the third of the three questions "can this service ever be fulfilled" asks,
    /// and the delivery API has to be able to publish the same answer. Keeping a
    /// second implementation beside it is the fault ⑧a design D1 exists to
    /// prevent.
    /// </remarks>
    public static IReadOnlyList<int> DurationOptions(IReadOnlyList<RoleCandidates> pools)
        => ServiceFulfillability.CommonLengthMinutes(pools);

    /// <summary>
    /// The visitor's choice of resource for this service: what may be chosen,
    /// what was chosen, how many the choice is one of, and whether a stale
    /// request had its choice reset.
    /// <para>
    /// A value resolved by a pure function rather than a branch inside the flow,
    /// for the reason the two unavailable decisions are: it decides what a visitor
    /// is offered and what reaches placement as the pin, and it must be
    /// attackable without a host.
    /// </para>
    /// </summary>
    /// <param name="Choices">
    /// The selectable role's resolved candidate pool, by display name. Empty when
    /// the service has no visitor-selectable role, which is the whole of "render
    /// no control".
    /// </param>
    /// <param name="Chosen">The chosen resource, or null for "any".</param>
    /// <param name="Count">
    /// The selectable role's count. Greater than 1 means the visitor chooses one
    /// and the rest are assigned, which the control has to say (design D5).
    /// </param>
    /// <param name="WasReset">
    /// Whether a resource was requested that this service can no longer be
    /// fulfilled by, and the choice fell back to "any" (design D11).
    /// </param>
    public readonly record struct ResourceChoiceState(
        IReadOnlyList<BookingResourceChoice> Choices, Guid? Chosen, int Count, bool WasReset)
    {
        // There is deliberately no `None` singleton. There was one, and the
        // round-1 defect was a branch returning it: "no selectable role" is NOT
        // the same state as "no choice was made", because a request that named a
        // resource still has to report that its choice was dropped. Leaving the
        // value here would invite someone to reach for it and reintroduce exactly
        // that. `default` covers the genuinely empty case, which is a flow that
        // was never asked about a resource at all.

        /// <summary>
        /// What the flow offers and honours, from the resolved pools and whatever
        /// the request asked for.
        /// <para>
        /// The list is the selectable role's pool <b>unfiltered by date</b>
        /// (design D10): a resource with no free time on the chosen date is still
        /// offered, and the start list then reports that there are no times.
        /// Filtering the people by date would make the control's contents change
        /// under the visitor as they change the date.
        /// </para>
        /// <para>
        /// Ordered by display name with the resource id as the tiebreak, so the
        /// order is total and stable. <b>Presentation only</b> — pool order stays
        /// ascending by resource id, which `resource-pin`, ⑦-2 and ⑨-2 all draw
        /// determinism guarantees from, and which decides ⑨-1a's misalignment
        /// witness.
        /// </para>
        /// <para>
        /// A requested resource outside the offered list is a <em>stale</em>
        /// choice: reset to "any" and reported as reset, never silently honoured
        /// and never silently dropped. Nothing has been committed at this point,
        /// so saying "the person you chose is no longer offered — choose again" is
        /// both honest and unblocking.
        /// </para>
        /// </summary>
        public static ResourceChoiceState Resolve(
            IReadOnlyList<RoleCandidates> pools, Guid? requestedResourceId)
        {
            // At most one role of a service may be visitor-selectable, so the
            // first is the only one. `Service.Create` rejects a second, and this
            // takes no view of its own on which one an editor meant.
            var selectable = pools.FirstOrDefault(pool => pool.Role.VisitorSelectable);

            if (selectable is null)
            {
                // No control, and therefore no choice to honour — including for a
                // request that named one. A pin offered by no control is not this
                // flow's to act on; placement still accepts one from a caller that
                // has its own reasons (design D8), and this flow is not one.
                //
                // But a request that DID name one is still a stale choice, and the
                // third of D11's three causes: the resource may be perfectly
                // eligible and the editor has simply turned the picker off. It
                // resets like the other two, and it SAYS SO like the other two —
                // returning `None` here made the notice structurally unreachable
                // for this cause, so a bookmarked "book with Jane" link quietly
                // became a booking for anyone. QA found it.
                return new ResourceChoiceState(
                    [], null, 0, WasReset: requestedResourceId is not null);
            }

            var choices = selectable.Candidates
                .Select(candidate => new BookingResourceChoice(
                    candidate.ResourceId, candidate.Resource.DisplayName))
                .OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(choice => choice.Id)
                .ToList();

            var honoured = requestedResourceId is { } requested
                && choices.Any(choice => choice.Id == requested)
                    ? requestedResourceId
                    : null;

            return new ResourceChoiceState(
                choices,
                honoured,
                selectable.Role.Count,
                WasReset: requestedResourceId is not null && honoured is null);
        }
    }

    /// <summary>
    /// The default booking length: the shortest every role can provide. For a
    /// fixed-duration service the intersection holds exactly that one length, so
    /// this returns it without a special case.
    /// </summary>
    public static int? DefaultDurationMinutes(IReadOnlyList<int> options)
        => options.Count == 0 ? null : options[0];

    /// <summary>
    /// The length to <em>render</em>: the requested one when the service offers
    /// it, otherwise the default. Defaulting is correct here — an absent or
    /// hand-edited query parameter should draw a usable form rather than an error
    /// page.
    /// <para>
    /// Deliberately NOT used when placing. Substituting a length on the write path
    /// would confirm a booking the visitor never chose; submissions pass their
    /// length to Core unchanged, which is required of a fixed-duration service
    /// precisely so a permitted length is never silently substituted.
    /// </para>
    /// </summary>
    public static int? ResolveDisplayDurationMinutes(IReadOnlyList<int> options, int? requestedMinutes)
        => requestedMinutes is { } minutes && options.Contains(minutes)
            ? minutes
            : DefaultDurationMinutes(options);

    /// <summary>
    /// The longest length bookable anywhere among these starts, or null when
    /// there are none. Drives the explanatory empty state, exactly as the
    /// resource flow's does.
    /// </summary>
    public static int? LongestAvailableMinutes(IReadOnlyList<ServiceBookableStart> starts)
    {
        var longest = starts
            .SelectMany(start => start.Runs)
            .Select(run => run.Max)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();

        return longest == TimeSpan.Zero ? null : (int)longest.TotalMinutes;
    }

    /// <summary>
    /// The furthest date any assignment could reach: within each role, the most
    /// generous candidate's horizon; across roles, the least of those.
    /// <para>
    /// A date beyond some role's every candidate cannot be filled by that role,
    /// whatever the other roles permit — the same reasoning the pool-wide length
    /// bounds use.
    /// </para>
    /// </summary>
    public static int HorizonDays(IReadOnlyList<RoleCandidates> pools)
        => pools.Min(pool => pool.Candidates.Max(c => c.Resource.Availability.Constraints.HorizonDays));

    /// <summary>
    /// The confirmation for a placed service booking, naming <b>every</b> resource
    /// it claims, in the order the booking claims them.
    /// <para>
    /// A visitor who booked a room and a therapist was given both, and a
    /// confirmation naming one of them describes a different booking from the one
    /// that exists. A single-role service produces a list of one and takes the
    /// same path — the special case is exactly the shape that passes every
    /// single-role test and fails the multi-role case this flow exists to serve.
    /// </para>
    /// <para>
    /// A claim whose resource name could not be read falls back to its id rather
    /// than being dropped: the count of things booked stays honest, which is the
    /// whole point of reporting all of them.
    /// </para>
    /// <para>
    /// Host-free, and separated from the controller for that reason: this is the
    /// claim the flow makes about what a visitor was given, and it must be
    /// attackable without an Umbraco host.
    /// </para>
    /// </summary>
    public static ServiceConfirmationModel BuildConfirmation(
        Booking booking,
        string serviceName,
        IReadOnlyDictionary<Guid, string> resourceNames,
        TimeZoneInfo zone)
    {
        var start = TimeZoneInfo.ConvertTime(booking.Interval.StartUtc, zone);
        var end = TimeZoneInfo.ConvertTime(booking.Interval.EndUtc, zone);

        return new ServiceConfirmationModel
        {
            BookingId = booking.Id,
            Reference = booking.Reference.Display,
            ServiceName = serviceName,
            ResourceNames = [.. booking.Claims.Select(claim =>
                resourceNames.TryGetValue(claim.ResourceId, out var name)
                    ? name
                    : claim.ResourceId.ToString())],
            LocalStart = start.ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture),
            LocalEnd = end.ToString("HH:mm", CultureInfo.InvariantCulture),
            BookerName = booking.Booker.Name,
            BookerEmail = booking.Booker.Email,
            BookerPhone = booking.Booker.Phone,
        };
    }

    public static ServiceFormModel Build(
        Service service,
        IReadOnlyList<RoleCandidates> pools,
        DateOnly selectedDate,
        DateOnly today,
        IReadOnlyList<ServiceBookableStart> starts,
        int durationMinutes,
        TimeZoneInfo zone,
        FailedSubmission? failed = null,
        string? flowToken = null,
        ResourceChoiceState choice = default)
    {
        var duration = TimeSpan.FromMinutes(durationMinutes);

        return new ServiceFormModel
        {
            ResourceChoices = choice.Choices ?? [],
            ChosenResourceId = choice.Chosen,
            ResourceChoiceCount = choice.Count,
            ResourceChoiceWasReset = choice.WasReset,
            FlowToken = flowToken,
            ServiceId = service.Id,
            ServiceName = service.Name,
            SelectedDate = selectedDate,
            MinDate = today,

            // Saturating: HorizonDays is only validated as positive, so a large
            // one would otherwise throw while rendering the form.
            MaxDate = CalendarBounds.AddDaysSaturating(today, HorizonDays(pools)),
            DurationMinutes = durationMinutes,
            DurationOptions = DurationOptions(pools),
            LengthIsFixed = service.Duration.Kind == ServiceDurationKind.Fixed,
            LongestAvailableMinutes = LongestAvailableMinutes(starts),
            Times = BookingForm.ToOptions(
                starts.Where(start => start.Admits(duration)).Select(start => start.StartUtc), zone),
            SelectedTimeIso = failed?.SelectedTimeIso,
            Name = failed?.Name,
            Email = failed?.Email,
            Phone = failed?.Phone,
            Errors = failed?.Errors ?? [],
        };
    }
}
