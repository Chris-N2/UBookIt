using System.Diagnostics.CodeAnalysis;
using UBookIt.Core.Resources;

namespace UBookIt.Web.Rendering;

/// <summary>
/// View model for the booking form (default front-end). Purpose-built for
/// rendering — no domain aggregate on the surface. Times carry the exact UTC
/// instant as their value and a site-zone wall-clock label for display.
/// </summary>
public sealed class BookingFormModel : IBookingFormView
{
    public required Guid ResourceId { get; init; }

    public required string ResourceName { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Null unless the flow was entered through the dispatcher's query string.
    /// A site author who invokes the <c>Booking</c> component with a resource id
    /// has no flow state in the URL, and the rendered markup is then byte-for-byte
    /// what it was before the service flow existed.
    /// </remarks>
    public string? FlowToken { get; init; }

    /// <summary>
    /// Always false: a resource's length is the visitor's choice from the grid
    /// its constraints permit, even when that grid holds a single value. Stated
    /// rather than inherited, because rendering a one-option grid as settled text
    /// would change this flow's behaviour.
    /// </summary>
    public bool LengthIsFixed => false;

    public required DateOnly SelectedDate { get; init; }

    /// <summary>Earliest selectable date (today in the site zone).</summary>
    public required DateOnly MinDate { get; init; }

    /// <summary>Latest selectable date (today + booking horizon).</summary>
    public required DateOnly MaxDate { get; init; }

    /// <summary>The chosen booking length, in whole minutes.</summary>
    public required int DurationMinutes { get; init; }

    /// <summary>Every length this resource permits, in whole minutes, ascending.</summary>
    public IReadOnlyList<int> DurationOptions { get; init; } = [];

    /// <summary>
    /// The longest length bookable anywhere on the selected date, or null when
    /// the date has no availability at all. Drives the explanatory empty state:
    /// "no 3-hour times, the longest available is 90 minutes".
    /// </summary>
    public int? LongestAvailableMinutes { get; init; }

    /// <summary>
    /// Always empty, and structurally so: a directly booked resource <b>is</b> the
    /// resource the visitor chose, so there is nothing left to pick between. This
    /// flow renders no such control and has no field that could carry one.
    /// </summary>
    public IReadOnlyList<BookingResourceChoice> ResourceChoices => [];

    /// <inheritdoc />
    public Guid? ChosenResourceId => null;

    /// <inheritdoc />
    public int ResourceChoiceCount => 0;

    /// <inheritdoc />
    public bool ResourceChoiceWasReset => false;

    /// <inheritdoc />
    public bool OffersResourceChoice => false;

    public IReadOnlyList<BookingTimeOption> Times { get; init; } = [];

    // Repopulation after a failed submission.
    public string? SelectedTimeIso { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public IReadOnlyList<BookingError> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;

    public bool HasTimes => Times.Count > 0;

    /// <summary>
    /// True when the date has availability but none of it fits the chosen
    /// length — the case that earns an explanation rather than a bare "no times".
    /// </summary>
    public bool LengthIsTheProblem => !HasTimes && LongestAvailableMinutes is not null;

    /// <summary>The error message associated with a field id, if any (for aria wiring).</summary>
    public string? ErrorFor(string fieldId) => Errors.FirstOrDefault(e => e.FieldId == fieldId)?.Message;
}

/// <summary>One selectable start time: the exact UTC instant plus its site-zone label.</summary>
public sealed record BookingTimeOption(string InstantIso, string Label);

/// <summary>
/// One resource a visitor may choose to fulfil a service's selectable role: its
/// id, and the name it is offered under.
/// <para>
/// The name is the resource's display name — the visitor is choosing a person or
/// a room, not a key — and the id is what travels in the URL and reaches
/// placement as the pin.
/// </para>
/// </summary>
public sealed record BookingResourceChoice(Guid Id, string Name);

/// <summary>
/// A user-facing error message plus the id of the control it belongs to (null
/// for a general error). Drives the error-summary links and per-field aria.
/// </summary>
public sealed record BookingError(string Message, string? FieldId);

/// <summary>Form-control ids, shared by the view and the failure→field mapping so they stay in sync.</summary>
public static class BookingFieldIds
{
    public const string Name = "ubookit-name";
    public const string Email = "ubookit-email";
    public const string Times = "ubookit-times";
    public const string Duration = "ubookit-duration";

    /// <summary>
    /// The control choosing who fulfils a service. Rendered only where a service
    /// offers that choice — a refusal naming it can only arise from a submission
    /// that carried one.
    /// </summary>
    public const string Resource = "ubookit-who";
}

/// <summary>
/// A failed submission carried back to the re-rendered form via TempData
/// (first-pass mechanism; see design D3). Serialized as JSON.
/// </summary>
public sealed class FailedSubmission
{
    public DateOnly Date { get; init; }

    public int DurationMinutes { get; init; }

    public string? SelectedTimeIso { get; init; }

    /// <summary>
    /// The resource the submission chose, where the service offered a choice, so
    /// a redraw shows the visitor's own choice rather than dropping it back to
    /// "any" — which would be a quiet substitution on the page they are about to
    /// resubmit.
    /// </summary>
    public Guid? ChosenResourceId { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public List<BookingError> Errors { get; init; } = [];
}

/// <summary>
/// Why the booking flow has nothing to offer. The two reasons must not be
/// conflated: one is temporary and one is permanent, and they send a visitor to
/// do entirely different things.
/// </summary>
public enum BookingUnavailableReason
{
    /// <summary>
    /// The resource could not be read, or the site's time zone is unusable. A
    /// fault rather than an answer, so "try again later" is honest.
    /// </summary>
    Unknown,

    /// <summary>
    /// The resource exists and is simply not offered for booking on its own.
    /// Coming back tomorrow will not change it, and the site owner's opening
    /// hours are not the problem — so this must never be phrased as "no times
    /// available".
    /// </summary>
    NotOfferedIndividually,
}

/// <summary>
/// View model for the unavailable state, carrying which of the two reasons
/// applies. Rendered by one view rather than two, because the page's shape is
/// identical and only the sentence differs.
/// </summary>
public sealed class BookingUnavailableModel
{
    public required BookingUnavailableReason Reason { get; init; }

    public static BookingUnavailableModel Unknown { get; } =
        new() { Reason = BookingUnavailableReason.Unknown };

    public static BookingUnavailableModel NotOfferedIndividually { get; } =
        new() { Reason = BookingUnavailableReason.NotOfferedIndividually };

    /// <summary>
    /// Whether the flow has anything to offer for this resource, and if not, why.
    /// False means carry on and render the form.
    /// <para>
    /// A function rather than a branch inside the ViewComponent, because this is
    /// where the distinction actually lives and a ViewComponent needs a host to
    /// exercise. QA proved the branch could be reverted to <see cref="Unknown"/>
    /// — restoring the "not available at the moment, please try again later"
    /// conflation this change exists to remove — with the whole suite green.
    /// </para>
    /// <para>
    /// The order is load-bearing. A resource that could not be read, or a site
    /// zone that will not resolve, is a fault and honestly gets "try again"; only
    /// a resource that exists and withholds gets the permanent answer.
    /// </para>
    /// <para>
    /// Shaped as a Try-method rather than a nullable return so the <b>caller</b>
    /// keeps its null-flow guarantee. An earlier version returned
    /// <c>BookingUnavailableModel?</c>, and the compiler could not infer that a
    /// null answer implied a non-null resource — so the resource stayed nullable
    /// for the rest of the component and the build warned (CS8604), which is an
    /// error in CI. <see cref="NotNullWhenAttribute"/> states the thing that was
    /// always true and was merely no longer visible.
    /// </para>
    /// </summary>
    public static bool IsUnavailable(
        [NotNullWhen(false)] Resource? resource,
        bool zoneResolved,
        [NotNullWhen(true)] out BookingUnavailableModel? model)
    {
        if (resource is null || !zoneResolved)
        {
            model = Unknown;
            return true;
        }

        if (!resource.DirectlyBookable)
        {
            model = NotOfferedIndividually;
            return true;
        }

        model = null;
        return false;
    }
}

/// <summary>View model for the confirmation page shown after a successful placement.</summary>
public sealed class BookingConfirmationModel
{
    public required Guid BookingId { get; init; }

    public required string ResourceName { get; init; }

    public required string LocalStart { get; init; }

    public required string LocalEnd { get; init; }

    public required string BookerName { get; init; }

    public required string BookerEmail { get; init; }

    public string? BookerPhone { get; init; }
}
