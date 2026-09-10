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
    /// <remarks>
    /// Submitted by the list of available dates. <b>Not shared with
    /// <see cref="OtherDateQuery"/></b>, and the separation is not tidiness: a form containing
    /// two controls with one name submits BOTH values, and which one binds is an accident of
    /// model binding rather than a decision anybody made.
    /// </remarks>
    public const string DateQuery = "ubDate";

    /// <summary>
    /// Query parameter carrying a date typed into the "another date" field, for dates beyond the
    /// listed window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It wins over <see cref="DateQuery"/> when present and parseable.</b> Typing a date is
    /// the more deliberate act: a visitor who selects a date from the list and then types one is
    /// correcting themselves, and the opposite precedence would silently discard what they typed
    /// in favour of what they had already moved on from.
    /// </para>
    /// <para>
    /// A separate parameter exists because the window is smaller than what a site offers — a
    /// horizon is commonly months and a window is at most weeks — so the list alone would put
    /// most of a site's own availability out of reach.
    /// </para>
    /// </remarks>
    public const string OtherDateQuery = "ubDateOther";

    /// <summary>Query parameter carrying the chosen booking length in whole minutes.</summary>
    public const string DurationQuery = "ubMins";

    /// <summary>
    /// Query parameter carrying the resource a visitor chose to fulfil a
    /// service's visitor-selectable role, where one was offered.
    /// <para>
    /// Its own parameter beside the date and the length, and for the same reason:
    /// every step of the flow stays linkable, bookmarkable and reachable by the
    /// back button. A resource id in the URL discloses nothing a public read does
    /// not already carry, and a link naming a resource that no longer fulfils the
    /// service is handled as a stale choice rather than as an error (design D11).
    /// </para>
    /// </summary>
    public const string ResourceQuery = "ubWho";

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
