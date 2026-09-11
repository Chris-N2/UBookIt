using System.Diagnostics.CodeAnalysis;
using UBookIt.Core.Services;

namespace UBookIt.Web.Rendering;

/// <summary>
/// View model for the service booking form. Purpose-built for rendering — no
/// domain aggregate on the surface — and satisfying the shared
/// <see cref="IBookingFormView"/> contract so the accessibility-critical markup
/// is the resource flow's, not a copy of it.
/// </summary>
public sealed class ServiceFormModel : IBookingFormView
{
    /// <inheritdoc />
    public required PrivacyNoticeView PrivacyNotice { get; init; }
    /// <inheritdoc />
    public IReadOnlyList<AvailableDate> AvailableDates { get; init; } = [];

    /// <inheritdoc />
    public bool SelectedDateIsListed { get; init; }

    /// <inheritdoc />
    public int WindowDays { get; init; }

    /// <inheritdoc />
    public int? LongestAvailableInWindowMinutes { get; init; }

    public required Guid ServiceId { get; init; }

    public required string ServiceName { get; init; }

    /// <inheritdoc />
    public string? FlowToken { get; init; }

    public required DateOnly SelectedDate { get; init; }

    /// <summary>Earliest selectable date (today in the site zone).</summary>
    public required DateOnly MinDate { get; init; }

    /// <summary>Latest selectable date (today + the horizon every role can reach).</summary>
    public required DateOnly MaxDate { get; init; }

    /// <summary>The chosen booking length, in whole minutes.</summary>
    public required int DurationMinutes { get; init; }

    /// <summary>Every length the service's roles can all provide, ascending.</summary>
    public IReadOnlyList<int> DurationOptions { get; init; } = [];

    /// <inheritdoc />
    public bool LengthIsFixed { get; init; }

    /// <inheritdoc />
    public int? LongestAvailableMinutes { get; init; }

    /// <summary>
    /// The resources a visitor may choose between for this service's
    /// visitor-selectable role, by display name — empty when no role is
    /// selectable, in which case no control is rendered and the flow is exactly as
    /// it was.
    /// <para>
    /// The role's resolved candidate pool, <b>not</b> filtered by the chosen date
    /// (design D10). Filtering the people by date would make the control's
    /// contents change under the visitor as they change the date, so a name could
    /// vanish from under the cursor — and it would answer with the control a
    /// question the start list already answers directly and better.
    /// </para>
    /// </summary>
    public IReadOnlyList<BookingResourceChoice> ResourceChoices { get; init; } = [];

    /// <inheritdoc />
    public Guid? ChosenResourceId { get; init; }

    /// <inheritdoc />
    public int ResourceChoiceCount { get; init; }

    /// <inheritdoc />
    public bool ResourceChoiceWasReset { get; init; }

    /// <inheritdoc />
    public bool OffersResourceChoice => ResourceChoices.Count > 0;

    public IReadOnlyList<BookingTimeOption> Times { get; init; } = [];

    // Repopulation after a failed submission.
    public string? SelectedTimeIso { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public IReadOnlyList<BookingError> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;

    public bool HasTimes => Times.Count > 0;

    /// <inheritdoc />
    public bool LengthIsTheProblem => !HasTimes && LongestAvailableMinutes is not null;

    /// <inheritdoc />
    public string? ErrorFor(string fieldId) => Errors.FirstOrDefault(e => e.FieldId == fieldId)?.Message;
}

/// <summary>
/// Why the service flow has nothing to offer. The two reasons must not be
/// conflated: one is a fault and one is an answer, and they send a visitor to do
/// entirely different things.
/// </summary>
public enum ServiceUnavailableReason
{
    /// <summary>
    /// The service could not be read, or the site's time zone is unusable. A
    /// fault rather than an answer, so "try again later" is honest.
    /// </summary>
    Unknown,

    /// <summary>
    /// The service, as configured, can never be fulfilled: some role has no
    /// eligible resource, the roles cannot be filled at once by distinct
    /// resources, their start grids can never coincide, or no length exists that
    /// every role can provide.
    /// <para>
    /// Deterministic. Coming back tomorrow cannot change it, so the page must not
    /// invite a retry — and must not say "no times available", which sends a
    /// visitor back tomorrow and tells the site owner their opening hours are
    /// wrong when they are not.
    /// </para>
    /// </summary>
    NotFulfillable,
}

/// <summary>
/// View model for the service flow's unavailable state. Carries the reason and
/// <b>nothing about the configuration</b> — not the role, the resource type, the
/// capability or the count. The pool-sufficiency diagnostic that names those was
/// built for the person who can fix them; a visitor cannot, is not owed the
/// site's staffing, and would be told by any such message how many therapists the
/// business employs.
/// </summary>
public sealed class ServiceUnavailableModel
{
    public required ServiceUnavailableReason Reason { get; init; }

    /// <summary>
    /// The service's name, when it could be read. Naming what was asked for is
    /// not a configuration detail — a visitor arrived by choosing it.
    /// </summary>
    public string? ServiceName { get; init; }

    public static ServiceUnavailableModel Unknown { get; } =
        new() { Reason = ServiceUnavailableReason.Unknown };

    public static ServiceUnavailableModel NotFulfillable(string serviceName)
        => new() { Reason = ServiceUnavailableReason.NotFulfillable, ServiceName = serviceName };

    /// <summary>
    /// Whether the flow has anything to offer for this service, and if not, why.
    /// False means carry on and render the form.
    /// <para>
    /// A function rather than a branch inside the ViewComponent, for the reason
    /// the resource flow's twin records: a ViewComponent needs a host to
    /// exercise, and QA proved that such a branch can be reverted to the generic
    /// answer with a whole suite green. This is the decision that keeps "busy
    /// now" and "never bookable as configured" apart, so it is the one most worth
    /// being able to attack directly.
    /// </para>
    /// <para>
    /// The order is load-bearing. A service that could not be read, or a site zone
    /// that will not resolve, is a fault and honestly gets "try again"; only a
    /// service that exists and cannot be fulfilled gets the permanent answer.
    /// </para>
    /// <para>
    /// The deterministic test is asked of Core, over the resolved pools the
    /// booking path itself acts on — never of a second implementation of the rule.
    /// All three of its questions live in
    /// <see cref="ServiceFulfillability.IsPermanentlyUnfulfillable"/>, which the
    /// public availability read asks too, so the two surfaces cannot disagree. An
    /// empty pool is not tested separately: a slot with no candidates has no
    /// saturating assignment, so the shortfall finding already reports it, and
    /// asking twice would be two answers to one question.
    /// </para>
    /// <para>
    /// What it must never do is <em>say</em> any of this. The shortfall, the
    /// misaligned pair and the empty length set all name roles, resources and
    /// capabilities; none of that reaches the model, which carries the reason and
    /// the service's name and nothing else.
    /// </para>
    /// </summary>
    public static bool IsUnavailable(
        [NotNullWhen(false)] Service? service,
        [NotNullWhen(false)] IReadOnlyList<RoleCandidates>? pools,
        bool zoneResolved,
        [NotNullWhen(true)] out ServiceUnavailableModel? model)
    {
        if (service is null || pools is null || !zoneResolved)
        {
            model = Unknown;
            return true;
        }

        // All three structural questions, asked of the one Core function that
        // answers them (design D6). It used to ask them here, over a length
        // intersection that lived in this project — which is why the delivery API
        // could not reach it, and why publishing the same answer would have meant
        // a second implementation of the rule free to disagree with this one.
        if (ServiceFulfillability.IsPermanentlyUnfulfillable(pools))
        {
            model = NotFulfillable(service.Name);
            return true;
        }

        model = null;
        return false;
    }
}

/// <summary>
/// View model for the confirmation shown after a placed service booking.
/// <para>
/// <see cref="ResourceNames"/> is a collection because a service booking claims
/// <b>several</b> resources and a confirmation naming one of them describes a
/// different booking from the one that exists. A single-role service resolves to
/// a collection of one and is not special-cased — the special case is exactly the
/// shape that passes every single-role test and fails the multi-role case this
/// flow exists to serve.
/// </para>
/// </summary>
public sealed class ServiceConfirmationModel
{
    public required Guid BookingId { get; init; }

    /// <summary>
    /// The quotable reference, grouped for display — see <c>BookingConfirmationModel</c>. A
    /// service booking needs it for exactly the same reason a direct one does: somebody is
    /// going to have to talk about this booking later.
    /// </summary>
    public required string Reference { get; init; }

    /// <summary>
    /// Whether the placed booking awaits the site's confirmation — see
    /// <c>BookingConfirmationModel.IsPending</c>; a service booking needs it for exactly the
    /// same reason a direct one does.
    /// </summary>
    public bool IsPending { get; init; }

    public required string ServiceName { get; init; }

    /// <summary>Every resource the service resolved to, in the order the booking claims them.</summary>
    public required IReadOnlyList<string> ResourceNames { get; init; }

    public required string LocalStart { get; init; }

    public required string LocalEnd { get; init; }

    public required string BookerName { get; init; }

    public required string BookerEmail { get; init; }

    public string? BookerPhone { get; init; }
}
