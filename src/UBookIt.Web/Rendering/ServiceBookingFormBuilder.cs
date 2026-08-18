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
    public static IReadOnlyList<int> DurationOptions(IReadOnlyList<RoleCandidates> pools)
    {
        if (pools.Count == 0)
        {
            return [];
        }

        HashSet<int>? shared = null;

        foreach (var pool in pools)
        {
            var offered = pool.Candidates
                .SelectMany(candidate => BookingForm.LengthGrid(
                    (int)candidate.Range.Min.TotalMinutes,
                    (int)candidate.Range.Max.TotalMinutes,
                    (int)candidate.Granularity.TotalMinutes))
                .ToHashSet();

            if (shared is null)
            {
                shared = offered;
            }
            else
            {
                shared.IntersectWith(offered);
            }
        }

        return [.. shared!.Order()];
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
        string? flowToken = null)
    {
        var duration = TimeSpan.FromMinutes(durationMinutes);

        return new ServiceFormModel
        {
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
