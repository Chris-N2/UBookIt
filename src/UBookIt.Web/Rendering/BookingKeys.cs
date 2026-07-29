namespace UBookIt.Web.Rendering;

/// <summary>Shared keys for the booking flow (query param + TempData handoffs).</summary>
internal static class BookingKeys
{
    /// <summary>Query parameter carrying the chosen date (yyyy-MM-dd).</summary>
    public const string DateQuery = "ubDate";

    /// <summary>TempData key for a failed submission redrawn on the form (design D3).</summary>
    public const string FailedSubmission = "UBookIt.Booking.Failed";

    /// <summary>TempData key for a successful placement shown on the confirmation.</summary>
    public const string Confirmation = "UBookIt.Booking.Confirmation";
}
