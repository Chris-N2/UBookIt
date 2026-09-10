using UBookIt.Core;

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

    /// <summary>
    /// The dates in the listed window that can be booked at the chosen length, ascending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Filtered to the chosen length, deliberately.</b> A date offering only half-hour gaps is
    /// not offered to somebody who asked for two hours: the whole point is that selecting a
    /// listed date leads to times, and a list that ignored the length would move the blind pick
    /// one step later rather than removing it.
    /// </para>
    /// <para>
    /// Empty means the window holds nothing at this length — which is a statement to render, not
    /// a list to render emptily. See <see cref="LongestAvailableInWindowMinutes"/>.
    /// </para>
    /// </remarks>
    IReadOnlyList<AvailableDate> AvailableDates { get; }

    /// <summary>Whether the selected date is one of <see cref="AvailableDates"/>.</summary>
    /// <remarks>
    /// False when a visitor has jumped past the window, or picked a date the window lists as
    /// unavailable. Either way the step must say which date it is showing, or it presents a list
    /// with nothing selected beside times for a date the list does not contain — a page
    /// disagreeing with itself.
    /// </remarks>
    bool SelectedDateIsListed { get; }

    /// <summary>How many days the listed window spans, for wording that names it.</summary>
    int WindowDays { get; }

    /// <summary>
    /// The longest length, in whole minutes, bookable anywhere in the window — or null when the
    /// window holds no availability at all.
    /// </summary>
    /// <remarks>
    /// What turns "nothing is available" into a next move. A window empty at two hours may be
    /// full of half-hour gaps, and the difference between telling somebody that and telling them
    /// only that there is nothing is the difference between a choice and a dead end. The same
    /// idea as <see cref="LongestAvailableMinutes"/>, at window scale rather than for one day.
    /// </remarks>
    int? LongestAvailableInWindowMinutes { get; }

    /// <summary>The error message associated with a field id, if any (for aria wiring).</summary>
    string? ErrorFor(string fieldId);

    /// <summary>
    /// What the form tells a person about the personal data it is asking for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It passes this interface's own test, though not in the obvious way.</b> The rule above
    /// is that a member must be answerable from <em>what is being booked</em> rather than from
    /// <em>which flow is asking</em>, and the notice is answerable from neither — it is a
    /// property of the site. What the rule is protecting against is a per-flow flag turning a
    /// shared partial into a lowest-common-denominator component by accretion, and a value that
    /// is <em>identical in both flows</em> cannot do that. It belongs here because
    /// <c>_YourDetails</c> renders it and both flows render <c>_YourDetails</c>.
    /// </para>
    /// <para>
    /// <b>Values, never a sentence.</b> A theme receives this model, so a pre-composed English
    /// string would let a theme print our wording or discard it wholesale and nothing else.
    /// Structured values let it state the same facts in its own markup, its own words and its
    /// own language — which is the front-end contract applied to prose: the package publishes
    /// data, and turning data into sentences belongs to the view.
    /// </para>
    /// </remarks>
    PrivacyNoticeView PrivacyNotice { get; }
}

/// <summary>
/// The facts the booking form states about the personal data it collects.
/// </summary>
/// <param name="RetentionDays">
/// How many days after a booking's end its personal data is erased automatically, or
/// <c>null</c> when the site has configured no retention period.
/// <para>
/// <b>Read from the same settings value the retention sweep acts on.</b> There is deliberately
/// no second copy and nothing authored: a notice written independently of the code could claim a
/// period the code does not keep, which is the failure this whole feature exists to prevent and
/// the reason it was built after retention rather than before it.
/// </para>
/// <para>
/// <b>Null is a state to render, not a state to skip.</b> Retention is off by default, so a view
/// that omitted the sentence when this is null would be omitting it on most installs — publishing
/// a notice quietly missing one of the four things it exists to say, in the ordinary case.
/// </para>
/// </param>
/// <param name="PolicyUrl">
/// A link to the site's own privacy policy, or <c>null</c> when none is configured or the
/// configured value could not be used as a link. Never a placeholder: a link that goes nowhere is
/// worse than no link here, because it looks like the policy exists.
/// </param>
/// <param name="SendsBookerEmail">
/// Whether a message will actually be sent to the person filling in this form.
/// <para>
/// <b>The whole condition, not the setting alone.</b> A site that has asked for booker messages on
/// a host that cannot send mail sends nothing, and a notice promising a confirmation there would
/// assert processing the package does not perform — which is the one thing this capability's own
/// requirement forbids. So this is <see cref="BookingNotificationSettings.WillEmailBooker"/>, the
/// same expression the sending path is gated on, rather than a restatement of it.
/// </para>
/// <para>
/// <b>Internal recipients do not affect it.</b> The notice speaks to the person filling in the
/// form; a site telling its own staff that a booking happened is not a message to the booker, and
/// saying so here would describe processing that person will never see.
/// </para>
/// </param>
public sealed record PrivacyNoticeView(int? RetentionDays, string? PolicyUrl, bool SendsBookerEmail)
{
    /// <summary>Whether the site has configured an automatic removal period.</summary>
    /// <remarks>
    /// Named rather than left as a null check at each call site, because the two branches say
    /// materially different things to a visitor and a view that reads
    /// <c>@if (Model.PrivacyNotice.HasRetentionPeriod)</c> states which case it is rendering.
    /// </remarks>
    public bool HasRetentionPeriod => RetentionDays is not null;

    /// <summary>Whether the site has configured a usable link to its own privacy policy.</summary>
    public bool HasPolicyUrl => !string.IsNullOrWhiteSpace(PolicyUrl);

    /// <summary>
    /// The notice for a site, from its settings.
    /// </summary>
    /// <remarks>
    /// <b>The only place this type is constructed from settings, deliberately.</b> Both flows go
    /// through here, so neither can acquire its own reading of the retention period — and the
    /// guarantee that the notice and the retention sweep cannot disagree is a property of there
    /// being one source, not of two call sites happening to agree today.
    /// </remarks>
    /// <param name="settings">The site's settings.</param>
    /// <param name="hostCanSendMail">
    /// Whether the host reports it can send mail. Passed in rather than read here because this
    /// type is a view model and asking is a collaborator's job — and because the caller is the one
    /// that can decide what to do when the host refuses to answer.
    /// </param>
    public static PrivacyNoticeView From(SiteBookingSettings settings, bool hostCanSendMail)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new PrivacyNoticeView(
            settings.RetentionDays,
            settings.PrivacyPolicyUrl,
            settings.Notifications.WillEmailBooker(hostCanSendMail));
    }
}


/// <summary>One date the first step offers.</summary>
/// <param name="Date">The date, in the site's zone.</param>
/// <param name="IsSelected">Whether it is the date the step is currently showing times for.</param>
/// <remarks>
/// A value rather than pre-rendered markup, on the same terms as everything else a theme
/// receives: what the package publishes is data, and turning a date into a label belongs to the
/// view that renders it — including a view that wants to write it in another language.
/// </remarks>
public readonly record struct AvailableDate(DateOnly Date, bool IsSelected);
