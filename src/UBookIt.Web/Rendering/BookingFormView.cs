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

    /// <summary>
    /// The resources a visitor may choose between to fulfil what they are
    /// booking, or empty when no choice is offered.
    /// <para>
    /// Answerable from <em>what is being booked</em>, which is what earns it a
    /// place here: a service with a visitor-selectable role offers its pool, a
    /// service without one offers nothing, and a directly booked resource offers
    /// nothing because it is already the resource the visitor chose. No partial
    /// asks which flow is rendering it.
    /// </para>
    /// <para>
    /// The choice sits in the same GET step as the date and the length precisely
    /// so it needs no page of its own: a control that had to follow the start
    /// would need a third page in a no-JS flow.
    /// </para>
    /// </summary>
    IReadOnlyList<BookingResourceChoice> ResourceChoices { get; }

    /// <summary>The chosen resource, or null for "any".</summary>
    Guid? ChosenResourceId { get; }

    /// <summary>
    /// How many resources the choice is one <em>of</em>: the selectable role's
    /// count. Greater than 1 means the visitor chooses one and the remainder are
    /// assigned, which the control must say — a picker that implies a choice and
    /// then books resources the visitor never chose is a substitution, however
    /// friendly its wording (design D5).
    /// </summary>
    int ResourceChoiceCount { get; }

    /// <summary>
    /// True when the request named a resource that no longer fulfils this
    /// service — deleted, no longer eligible, or its role no longer selectable —
    /// and the choice was reset to "any".
    /// <para>
    /// Rendered as a message rather than absorbed. A bookmarked link can carry a
    /// stale choice; falling back silently would quietly make it a different
    /// booking, and refusing to render the form at all would punish a visitor for
    /// a change they had no part in.
    /// </para>
    /// </summary>
    bool ResourceChoiceWasReset { get; }

    /// <summary>
    /// Whether a choice of resource is offered at all — the guard the who control
    /// is rendered under.
    /// <para>
    /// Declared rather than defaulted, so it is answerable on the concrete models
    /// too. A default interface member is invisible to a caller holding the model
    /// itself, and a test asserting what a page offers holds the model.
    /// </para>
    /// </summary>
    bool OffersResourceChoice { get; }

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
