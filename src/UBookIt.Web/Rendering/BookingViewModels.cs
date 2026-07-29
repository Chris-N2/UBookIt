namespace UBookIt.Web.Rendering;

/// <summary>
/// View model for the booking form (default front-end). Purpose-built for
/// rendering — no domain aggregate on the surface. Times carry the exact UTC
/// instant as their value and a site-zone wall-clock label for display.
/// </summary>
public sealed class BookingFormModel
{
    public required Guid ResourceId { get; init; }

    public required string ResourceName { get; init; }

    public required DateOnly SelectedDate { get; init; }

    /// <summary>Earliest selectable date (today in the site zone).</summary>
    public required DateOnly MinDate { get; init; }

    /// <summary>Latest selectable date (today + booking horizon).</summary>
    public required DateOnly MaxDate { get; init; }

    public required int DurationMinutes { get; init; }

    public IReadOnlyList<BookingTimeOption> Times { get; init; } = [];

    // Repopulation after a failed submission.
    public string? SelectedTimeIso { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public IReadOnlyList<BookingError> Errors { get; init; } = [];

    public bool HasErrors => Errors.Count > 0;

    public bool HasTimes => Times.Count > 0;

    /// <summary>The error message associated with a field id, if any (for aria wiring).</summary>
    public string? ErrorFor(string fieldId) => Errors.FirstOrDefault(e => e.FieldId == fieldId)?.Message;
}

/// <summary>One selectable start time: the exact UTC instant plus its site-zone label.</summary>
public sealed record BookingTimeOption(string InstantIso, string Label);

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
}

/// <summary>
/// A failed submission carried back to the re-rendered form via TempData
/// (first-pass mechanism; see design D3). Serialized as JSON.
/// </summary>
public sealed class FailedSubmission
{
    public DateOnly Date { get; init; }

    public string? SelectedTimeIso { get; init; }

    public string? Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public List<BookingError> Errors { get; init; } = [];
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
