using System.Globalization;

namespace UBookIt.Web.Rendering;

/// <summary>Which kind of bookable thing a flow is about.</summary>
public enum BookableKind
{
    /// <summary>A service, fulfilled by one resource per role.</summary>
    Service,

    /// <summary>A single resource, booked on its own.</summary>
    Resource,
}

/// <summary>
/// What a flow is booking, as it travels in the URL: a kind and an id.
/// <para>
/// <b>One query parameter for both kinds</b>, rather than a <c>serviceId</c>
/// beside a <c>resourceId</c>. The catalogue offers services and resources as one
/// set of choices — a visitor books "a massage" or "meeting room A" and is not
/// helped by being told which is which — so a single radio group has to name
/// them, and a radio group has one name. Two parameters would also admit a URL
/// that says both, which nothing could answer sensibly.
/// </para>
/// <para>
/// A site author never writes this: they name <c>serviceId</c> or
/// <c>resourceId</c> on the component, which is typed and reads as what it is.
/// The token is the flow's own state, and it is parsed strictly rather than
/// echoed — a malformed one is no subject at all, not a subject with a strange
/// name.
/// </para>
/// </summary>
public readonly record struct BookingSubject(BookableKind Kind, Guid Id)
{
    private const char ServicePrefix = 's';
    private const char ResourcePrefix = 'r';

    public static BookingSubject Service(Guid id) => new(BookableKind.Service, id);

    public static BookingSubject Resource(Guid id) => new(BookableKind.Resource, id);

    /// <summary>The URL form: a kind prefix, a colon, and the id.</summary>
    public string Token
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{(Kind == BookableKind.Service ? ServicePrefix : ResourcePrefix)}:{Id:D}");

    public static bool TryParse(string? token, out BookingSubject subject)
    {
        subject = default;

        if (token is null || token.Length < 2 || token[1] != ':')
        {
            return false;
        }

        var kind = token[0] switch
        {
            ServicePrefix => BookableKind.Service,
            ResourcePrefix => BookableKind.Resource,
            _ => (BookableKind?)null,
        };

        if (kind is not { } resolved || !Guid.TryParseExact(token[2..], "D", out var id))
        {
            return false;
        }

        subject = new BookingSubject(resolved, id);
        return true;
    }
}

/// <summary>
/// Where a visitor entered the flow: what they are booking, and whether the site
/// author named it or the URL did.
/// <para>
/// A value rather than a branch inside the dispatcher, for the reason the two
/// unavailable decisions are: a ViewComponent needs a host to exercise, and this
/// rule decides both which flow runs and whether flow state goes in the URL.
/// </para>
/// </summary>
/// <param name="Subject">What is being booked, or null for the catalogue.</param>
/// <param name="NamedByAuthor">
/// True when a site author supplied the id on the component. The author's id
/// wins over the query — that is what makes "a site with one service" work, since
/// no catalogue then exists to be traversed.
/// </param>
public readonly record struct FlowEntry(BookingSubject? Subject, bool NamedByAuthor)
{
    /// <summary>
    /// The token to carry through the flow's steps, or null when there is none.
    /// <para>
    /// Null when the author named the subject: there is no flow state in the URL
    /// to preserve, and emitting a token would change the resource flow's
    /// Post-Redirect-Get target for every site already using the <c>Booking</c>
    /// component.
    /// </para>
    /// </summary>
    public string? Token => NamedByAuthor ? null : Subject?.Token;

    public static FlowEntry Resolve(Guid? serviceId, Guid? resourceId, BookingSubject? fromQuery)
    {
        if (serviceId is { } service)
        {
            return new FlowEntry(BookingSubject.Service(service), NamedByAuthor: true);
        }

        return resourceId is { } resource
            ? new FlowEntry(BookingSubject.Resource(resource), NamedByAuthor: true)
            : new FlowEntry(fromQuery, NamedByAuthor: false);
    }
}
