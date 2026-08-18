namespace UBookIt.Web.Rendering;

/// <summary>
/// What the shared booking partials render. Both flows' form models satisfy it,
/// so the markup most easily got subtly wrong — the error summary's links to the
/// offending fields, <c>aria-describedby</c> wiring, <c>aria-invalid</c>, the
/// fieldset/legend pairing — is written once and satisfied once (design D1).
/// <para>
/// The seam is defined by <b>what must not drift</b>, not by what happens to look
/// similar. What a flow is booking, its headings, its hidden ids, its outcome
/// wording and its confirmation are deliberately absent: those differ between the
/// flows and belong to each of them.
/// </para>
/// <para>
/// A member here must be answerable from <em>what is being booked</em>, never
/// from <em>which flow is asking</em>. A partial that took a per-flow flag would
/// be a lowest-common-denominator component by accretion, and the signal to split
/// it back — see <see cref="LengthIsFixed"/>, which is a property of the thing
/// being booked and passes that test.
/// </para>
/// </summary>
public interface IBookingFormView
{
    /// <summary>
    /// The opaque token identifying what is being booked, carried through GET
    /// reloads and the Post-Redirect-Get round trip so a step stays linkable.
    /// Null when a site author named the subject on the component itself, in
    /// which case there is no flow state in the URL to preserve.
    /// </summary>
    string? FlowToken { get; }

    DateOnly SelectedDate { get; }

    /// <summary>Earliest selectable date (today in the site zone).</summary>
    DateOnly MinDate { get; }

    /// <summary>Latest selectable date (today + booking horizon).</summary>
    DateOnly MaxDate { get; }

    /// <summary>The chosen booking length, in whole minutes.</summary>
    int DurationMinutes { get; }

    /// <summary>Every length that may be chosen, in whole minutes, ascending.</summary>
    IReadOnlyList<int> DurationOptions { get; }

    /// <summary>
    /// Whether the length is settled rather than chosen, in which case it is
    /// stated in text and submitted as a hidden value instead of being offered
    /// as a control (design D5).
    /// <para>
    /// A property of what is being booked — a fixed-duration service has exactly
    /// one length — and not of the flow rendering it. A labelled control offering
    /// a choice of one is noise and a WCAG-adjacent nuisance; the field is still
    /// sent and still validated server-side either way, because removing the
    /// control does not remove the field.
    /// </para>
    /// </summary>
    bool LengthIsFixed { get; }

    /// <summary>
    /// The longest length bookable anywhere on the selected date, or null when
    /// the date has no availability at all. Drives the explanatory empty state.
    /// </summary>
    int? LongestAvailableMinutes { get; }

    IReadOnlyList<BookingTimeOption> Times { get; }

    /// <summary>The previously selected instant, repopulated after a failed submission.</summary>
    string? SelectedTimeIso { get; }

    string? Name { get; }

    string? Email { get; }

    string? Phone { get; }

    IReadOnlyList<BookingError> Errors { get; }

    bool HasErrors { get; }

    bool HasTimes { get; }

    /// <summary>
    /// True when the date has availability but none of it fits the chosen
    /// length — the case that earns an explanation rather than a bare "no times".
    /// </summary>
    bool LengthIsTheProblem { get; }

    /// <summary>The error message associated with a field id, if any (for aria wiring).</summary>
    string? ErrorFor(string fieldId);
}
