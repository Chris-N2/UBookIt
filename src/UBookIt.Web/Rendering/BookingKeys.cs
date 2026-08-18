namespace UBookIt.Web.Rendering;

/// <summary>Shared keys for the booking flows (query params + TempData handoffs).</summary>
public static class BookingKeys
{
    /// <summary>
    /// Query parameter carrying what is being booked, as a
    /// <see cref="BookingSubject"/> token. Read by the dispatcher and carried
    /// through every step so each is linkable.
    /// </summary>
    public const string SubjectQuery = "ubBook";

    /// <summary>Query parameter carrying the chosen date (yyyy-MM-dd).</summary>
    public const string DateQuery = "ubDate";

    /// <summary>Query parameter carrying the chosen booking length in whole minutes.</summary>
    public const string DurationQuery = "ubMins";

    /// <summary>TempData key for a failed submission redrawn on the form (design D3).</summary>
    public const string FailedSubmission = "UBookIt.Booking.Failed";

    /// <summary>TempData key for a successful placement shown on the confirmation.</summary>
    public const string Confirmation = "UBookIt.Booking.Confirmation";

    /// <summary>
    /// TempData key for a failed <em>service</em> submission. Distinct from the
    /// resource flow's so a page hosting the dispatcher cannot redraw one flow's
    /// input on the other's form.
    /// </summary>
    public const string ServiceFailedSubmission = "UBookIt.Service.Failed";

    /// <summary>TempData key for a successful service placement.</summary>
    public const string ServiceConfirmation = "UBookIt.Service.Confirmation";
}
