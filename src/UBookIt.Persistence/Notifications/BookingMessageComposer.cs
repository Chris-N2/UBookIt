using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using UBookIt.Core.Resources;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Notifications;

/// <summary>What happened to a booking, as far as a message is concerned.</summary>
public enum BookingEvent
{
    /// <summary>The booking has just been placed and stored.</summary>
    Placed,

    /// <summary>The booking has just been confirmed by an operator.</summary>
    /// <remarks>
    /// Only an operator's confirmation of a <c>Requested</c> booking. A booking placed as
    /// confirmed under auto-confirm is a <see cref="Placed"/> event whose booking happens to
    /// be confirmed — auto-confirmation is not an event, it is what placement produced.
    /// </remarks>
    Confirmed,

    /// <summary>The booking has just been declined by an operator.</summary>
    Declined,

    /// <summary>The booking has just been cancelled.</summary>
    Cancelled,
}

/// <summary>A composed message: a subject and a plain-text body.</summary>
public sealed record BookingMessage(string Subject, string Body);

/// <summary>
/// Turns a booking into the messages the package sends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Plain text, deliberately.</b> Email HTML is not web HTML — inline styles, tables, no
/// stylesheet — so it is a separate rendering path whichever way this goes, and building a
/// throwaway one now buys nothing that a later templating feature would keep.
/// </para>
/// <para>
/// <b>Almost everything comes from the booking itself.</b> The one exception is the name of a
/// directly-booked resource, because a claim carries an id and nothing else. That read reports the
/// resource's name as it stands NOW, which is knowingly weaker than the snapshot a service booking
/// carries — acceptable in a message sent within seconds of the event, and the alternative is a
/// domain and schema change.
/// </para>
/// <para>
/// <b>A message is never withheld because an enrichment could not be resolved.</b> The reference
/// and the time are the parts a person cannot reconstruct for themselves; a name that could not be
/// read is a line that is not there.
/// </para>
/// </remarks>
public sealed class BookingMessageComposer(
    IResourceStore resources,
    ILogger<BookingMessageComposer> logger)
{
    /// <summary>
    /// The formats are invariant and explicit rather than culture-driven. The package ships one
    /// culture today, so a culture-sensitive format would vary with whatever thread the
    /// notification happened to run on — which is not localisation, it is nondeterminism that
    /// looks like localisation. Real localisation is its own piece of work.
    /// </summary>
    private const string DateFormat = "dddd d MMMM yyyy";

    private const string TimeFormat = "h:mm tt";

    /// <summary>
    /// The message sent to the person who booked. Never called for an erased booker — reaching a
    /// booker's address requires establishing that the details are present, which the caller has
    /// necessarily already done.
    /// </summary>
    public async Task<BookingMessage> ForBookerAsync(
        Booking booking, BookingEvent bookingEvent, CancellationToken cancellationToken = default)
    {
        var what = await DescribeAsync(booking, cancellationToken).ConfigureAwait(false);
        var subject = SubjectFor(booking, bookingEvent);

        var body = new StringBuilder()
            .AppendLine(subject)
            .AppendLine()
            .Append(Details(booking, what))
            .AppendLine()
            .AppendLine(ClosingLineFor(booking, bookingEvent))
            .ToString();

        return new BookingMessage(subject, body);
    }

    /// <summary>
    /// The message sent to the site's own recipients.
    /// </summary>
    /// <remarks>
    /// <b>This carries no booker contact details, and that is a guarantee rather than an
    /// omission.</b> Who may see a booker's name, address and telephone number is decided by the
    /// backoffice group the sensitive-data handling tests; a list of addresses in configuration is
    /// a different population, gated by who can edit configuration. So the message identifies the
    /// booking and links to where it can be seen under that control, rather than carrying the
    /// details past it.
    /// </remarks>
    public async Task<BookingMessage> ForSiteAsync(
        Booking booking,
        BookingEvent bookingEvent,
        Uri? backofficeUrl,
        CancellationToken cancellationToken = default)
    {
        var what = await DescribeAsync(booking, cancellationToken).ConfigureAwait(false);

        // DERIVED FROM THE BOOKING'S STATUS, not from the AutoConfirm setting. The message
        // describes the booking it announces; reading the setting instead would let the two
        // drift the day anything else decides a placement's status. "Awaits approval" is a
        // fact about the booking, not about the person — no contact detail rides with it.
        var awaitsApproval = bookingEvent == BookingEvent.Placed
            && booking.Status == BookingStatus.Requested;

        var subject = bookingEvent == BookingEvent.Cancelled
            ? $"Booking cancelled — {booking.Reference.Display}"
            : awaitsApproval
                ? $"New booking awaiting approval — {booking.Reference.Display}"
                : $"New booking — {booking.Reference.Display}";

        var body = new StringBuilder()
            .AppendLine(subject)
            .AppendLine()
            .Append(Details(booking, what));

        if (awaitsApproval)
        {
            body.AppendLine()
                .AppendLine(
                    "This booking awaits approval. It holds its time until somebody confirms "
                    + "or declines it in the backoffice.");
        }

        if (backofficeUrl is not null)
        {
            body.AppendLine()
                .AppendLine("View this booking in the backoffice:")
                .AppendLine(backofficeUrl.ToString());
        }

        return new BookingMessage(subject, body.ToString());
    }

    /// <summary>
    /// What the message says about the booking's state, derived from that state.
    /// </summary>
    /// <remarks>
    /// <b>Not written on the assumption that a placed booking is confirmed.</b> This switch was
    /// written when placement could only produce a confirmed booking, precisely so that the day
    /// approval shipped could not silently turn "your booking is confirmed" into a lie in a
    /// message already in a customer's inbox. That day was the <c>approval-decline</c> change:
    /// every arm is now reachable, and nothing here had to move.
    /// </remarks>
    private static string SubjectFor(Booking booking, BookingEvent bookingEvent)
        => bookingEvent == BookingEvent.Cancelled
            ? "Your booking has been cancelled"
            : booking.Status switch
            {
                BookingStatus.Confirmed => "Your booking is confirmed",
                BookingStatus.Requested => "We have received your booking",
                BookingStatus.Declined => "Your booking could not be accepted",
                BookingStatus.Cancelled => "Your booking has been cancelled",
                _ => "Your booking",
            };

    /// <summary>
    /// The sentence after the details, derived from the booking's state on the same terms as
    /// <see cref="SubjectFor"/>.
    /// </summary>
    /// <remarks>
    /// The <c>Requested</c> arm promises another message, and that promise is kept by the same
    /// configuration that sent this one: this line is only ever composed for a booker email, so
    /// booker emails are on, and the confirm and decline events send through the identical
    /// gate. It says "confirmed or declined" rather than guessing which, because that is the
    /// one thing this message cannot know.
    /// </remarks>
    private static string ClosingLineFor(Booking booking, BookingEvent bookingEvent)
        => bookingEvent == BookingEvent.Cancelled
            ? "This booking has been cancelled. If that is unexpected, quote the reference above."
            : booking.Status switch
            {
                BookingStatus.Requested =>
                    "This booking is not confirmed yet. You will receive another message when it "
                    + "is confirmed or declined. Please quote the reference above if you need to "
                    + "get in touch about this booking.",
                BookingStatus.Declined =>
                    "The time has not been reserved. If that is unexpected, quote the reference "
                    + "above when you get in touch.",
                _ => "Please quote the reference above if you need to get in touch about this booking.",
            };

    private static string Details(Booking booking, string? what)
    {
        var (date, time, zone) = LocalParts(booking.Interval);
        var details = new StringBuilder()
            .AppendLine($"Reference: {booking.Reference.Display}");

        // Omitted rather than rendered empty. A "What:" line with nothing after it reads as a
        // fault in the message; its absence reads as a message about a booking, which is what it is.
        if (what is not null)
        {
            details.AppendLine($"What:      {what}");
        }

        return details
            .AppendLine($"When:      {date}")
            .AppendLine($"           at {time} ({zone})")
            .ToString();
    }

    /// <summary>
    /// The booking's start in <b>the zone it was placed against</b>, which the interval carries —
    /// not the site's current setting. A site that changes its time zone does not thereby
    /// retrospectively restate when its existing bookings are.
    /// </summary>
    /// <remarks>
    /// A zone id that can no longer be resolved falls back to UTC and says UTC, rather than
    /// throwing. The id was valid when the booking was placed, so this means the host's zone
    /// database has changed underneath it — vanishingly rare, and not a reason to withhold a
    /// message whose reference and date are still perfectly good.
    /// </remarks>
    private static (string Date, string Time, string Zone) LocalParts(BookingInterval interval)
    {
        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(interval.TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            var utc = interval.StartUtc.UtcDateTime;
            return (
                utc.ToString(DateFormat, CultureInfo.InvariantCulture),
                utc.ToString(TimeFormat, CultureInfo.InvariantCulture),
                "UTC");
        }

        var local = TimeZoneInfo.ConvertTime(interval.StartUtc, zone);

        return (
            local.ToString(DateFormat, CultureInfo.InvariantCulture),
            local.ToString(TimeFormat, CultureInfo.InvariantCulture),
            interval.TimeZoneId);
    }

    /// <summary>
    /// What was booked: the service's snapshot name where there is one, otherwise the names of the
    /// resources claimed. <c>null</c> where nothing could be established.
    /// </summary>
    private async Task<string?> DescribeAsync(Booking booking, CancellationToken cancellationToken)
    {
        // The snapshot the booking already carries, which says what was SOLD rather than what the
        // service happens to be called now. No read, and nothing to fail.
        if (booking.Service is { } service)
        {
            return service.DisplayName;
        }

        var names = new List<string>();

        foreach (var claim in booking.Claims)
        {
            Resource? resource;

            try
            {
                resource = await resources.GetAsync(claim.ResourceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // CANCELLATION IS EXCLUDED, matching the filtered catch two methods below.
                // A real token reaches this call, so on shutdown an unfiltered catch would log
                // a warning per claim and then carry on composing and sending a message nobody
                // asked for any more — turning "stop" into "do it anyway, noisily".
                //
                // CAUGHT SO THE MESSAGE STILL GOES. "What was booked cannot be established" is a
                // requirement with a stated answer — send the reference and the time without the
                // name — and a store that THREW has established it just as surely as one that
                // returned nothing. Before this, a transient database fault cost both messages
                // rather than one line, which is the opposite of the trade the requirement makes.
                //
                // Narrow on purpose: this wraps one enrichment read, and every other failure on
                // this path still escapes to the observer. The booking id only, never the booker.
                logger.LogWarning(
                    exception,
                    "uBookIt could not read a resource while composing a message for booking "
                    + "{BookingId}. The message is sent without naming what was booked.",
                    booking.Id);

                continue;
            }

            if (resource is not null)
            {
                names.Add(resource.DisplayName);
            }
        }

        return names.Count > 0 ? string.Join(", ", names) : null;
    }
}
