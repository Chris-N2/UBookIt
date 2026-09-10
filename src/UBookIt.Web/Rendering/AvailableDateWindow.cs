using UBookIt.Core.Availability;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Which span of dates the first step reads availability for, and offers as a list.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived from the site's own bounds, never a fixed number of days.</b> The preferred span is
/// a preference and nothing more: it is clamped by the subject's horizon and — the one that
/// matters — by <c>SiteBookingSettings.MaxQueryRangeDays</c>.
/// </para>
/// <para>
/// <b>That clamp is not a nicety, it is the difference between a shorter list and no flow at
/// all.</b> A read wider than the guardrail is <i>refused</i> with
/// <see cref="UBookIt.Core.Common.FailureCodes.DateRangeTooLarge"/>. A site that sets the
/// guardrail to 7 would therefore fail the availability read on <b>every</b> first-step render,
/// not merely list too much — and because every bound is generous by default, an implementation
/// that ignored the guardrail would look perfectly correct until somebody tightened one.
/// </para>
/// <para>
/// <b>Lead time needs no term here, and that is worth stating so nobody adds one.</b> The
/// requirement said for a while that the window was derived from it; that was wrong, and it
/// now says what is actually guaranteed — that a date the lead time leaves nothing bookable
/// on is not LISTED, which is a property of the list rather than of the window's bounds.
/// Lead time is
/// a <c>TimeSpan</c>, not a number of days: a two-hour lead time does not make today unbookable,
/// it makes this morning unbookable. The projection already drops starts inside it, so a date with
/// nothing left simply has no admitting start and is not listed. Shifting the window's start by a
/// lead time would skip whole days the site is still willing to sell.
/// </para>
/// </remarks>
public static class AvailableDateWindow
{
    /// <summary>
    /// How many days the step would like to offer, before the site's bounds are applied.
    /// </summary>
    /// <remarks>
    /// Not configurable. A site owner cannot reason about the right value better than this can —
    /// it is bounded below by being useful and above by what one page can sensibly show, and the
    /// bounds that genuinely vary per site are already read from that site's configuration.
    /// </remarks>
    public const int PreferredDays = 30;

    /// <summary>
    /// The inclusive span the first step reads and lists, in the site's own zone.
    /// </summary>
    /// <param name="today">Today in the site zone.</param>
    /// <param name="horizonDays">How many days ahead the subject may be booked.</param>
    /// <param name="maxQueryRangeDays">The site's guardrail on how wide one availability read may be.</param>
    public static (DateOnly From, DateOnly To) Compute(
        DateOnly today, int horizonDays, int maxQueryRangeDays)
    {
        // Every term is an INCLUSIVE day count, because that is what the guardrail counts:
        // the availability service computes `to - from + 1` and refuses a span above the
        // maximum. Mixing an inclusive count with an exclusive one here is how a window ends
        // up exactly one day too wide and fails only for sites at the boundary.
        //
        // The horizon is a count of days AHEAD, so the span it permits is one greater.
        var span = Math.Min(
            PreferredDays,
            Math.Min(
                horizonDays >= 0 ? horizonDays + 1 : 1,
                Math.Max(maxQueryRangeDays, 1)));

        // Saturating, like the date field's own upper bound: HorizonDays is validated as
        // positive but not as small, so a large one must not throw while rendering a form.
        return (today, CalendarBounds.AddDaysSaturating(today, span - 1));
    }
}
