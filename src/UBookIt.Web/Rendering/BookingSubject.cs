using System.Globalization;
using Microsoft.AspNetCore.Http;

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

    /// <summary>
    /// The submitted subject, but only when it agrees with what was actually
    /// booked; null otherwise.
    /// <para>
    /// The subject travels as a POST field, so a hand-made submission can name a
    /// service while booking a resource. Redirecting on the submitted value alone
    /// would place the booking and then land the visitor in the <em>other</em>
    /// flow, which reads a different TempData key — so the confirmation for a real
    /// booking is dropped. A disagreement is treated as no subject at all rather
    /// than as an error: the booking succeeded, and the plain redirect still shows
    /// it.
    /// </para>
    /// </summary>
    public static BookingSubject? Agreeing(string? token, BookingSubject actual)
        => TryParse(token, out var parsed) && parsed == actual ? parsed : null;
}

/// <summary>
/// The query string a step is reached at: what is being booked, the chosen date
/// and the chosen length — plus, since the preserved-query requirement, the
/// host page's site-listed parameters, and nothing else, ever.
/// <para>
/// Built here rather than at each redirect so both flows produce one vocabulary,
/// and so the rule that contact details never travel in a URL is a property of a
/// function rather than of two call sites agreeing. Every flow value is
/// re-serialised from parsed input; the preserved tail is the one deliberate
/// exception — visitor-controlled values, bounded by the site's configured
/// allow-list upstream and percent-encoded here, never raw.
/// </para>
/// <para>
/// The date and length are carried because a step has to be <b>linkable</b>: a
/// redirect that dropped them would land the visitor on a URL disagreeing with
/// the page it drew, which is the defect a server-side wizard state produces and
/// the reason the choices live in the URL at all.
/// </para>
/// </summary>
public static class BookingFlowLink
{
    public static QueryString For(
        BookingSubject subject,
        DateOnly date,
        int durationMinutes,
        Guid? chosenResourceId = null,
        IReadOnlyList<PreservedQueryPair>? preserved = null)
    {
        var query = QueryString.Create(BookingKeys.SubjectQuery, subject.Token)
            .Add(BookingKeys.DateQuery, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // A length of zero is what an omitted form field binds to. Carrying it
        // would put a value in the URL that no control could have produced.
        if (durationMinutes > 0)
        {
            query = query.Add(
                BookingKeys.DurationQuery,
                durationMinutes.ToString(CultureInfo.InvariantCulture));
        }

        // The chosen resource travels for the same reason the date and the length
        // do: the redraw after a failed submission must land on a URL that agrees
        // with the page it draws, and "who" is now one of the step's choices.
        // Absent when the visitor chose nobody, so a flow offering no choice
        // produces exactly the query string it produced before.
        if (chosenResourceId is { } chosen)
        {
            query = query.Add(BookingKeys.ResourceQuery, chosen.ToString("D", CultureInfo.InvariantCulture));
        }

        return AppendPreserved(query, preserved);
    }

    /// <summary>
    /// A redirect query that is ONLY the preserved host-page parameters — the
    /// component-named flow's case, where there is no flow state to carry but the
    /// page's own parameters must still survive the submission. Empty pairs produce
    /// an empty query, so that flow's redirect stays byte-for-byte what it was on
    /// every site that has configured nothing.
    /// </summary>
    public static QueryString Carrying(IReadOnlyList<PreservedQueryPair> preserved)
        => AppendPreserved(QueryString.Empty, preserved);

    /// <summary>
    /// The whole post-submission redirect decision, as one pure function: with a
    /// subject, the flow's own state plus the preserved tail; without one, the
    /// preserved tail alone; with neither, <c>null</c> — meaning "redirect with no
    /// query at all", which keeps the component-named flow's redirect byte-for-byte
    /// what it always was on an unconfigured site.
    /// </summary>
    /// <remarks>
    /// Extracted from the controllers at QA round 1's insistence, and the reason is
    /// worth keeping: the branch selection and the preserved tail lived in a private
    /// controller method a test could only see as source text, and QA proved the
    /// suite green with the tail dropped from the branch every normal submission
    /// takes. As a value-returning function the decision is exercised directly, and
    /// what remains in each controller is a single expression.
    /// </remarks>
    public static QueryString? AfterSubmission(
        BookingSubject? subject,
        DateOnly date,
        int durationMinutes,
        Guid? chosenResourceId,
        IReadOnlyList<PreservedQueryPair> preserved)
    {
        if (subject is { } chosen)
        {
            return For(chosen, date, durationMinutes, chosenResourceId, preserved);
        }

        return preserved.Count > 0 ? Carrying(preserved) : null;
    }

    // The preserved tail rides here, not at the call sites, so "every query string
    // the controllers produce is built by one vocabulary" stays a property of this
    // class — and so the Location-header rule below can be stated once: preserved
    // values are visitor-controlled, BOUNDED by the site's allow-list (that filter is
    // PreservedQuery.Compute's, upstream), and re-serialised through QueryString.Add,
    // which percent-encodes them. Nothing reaches the header as raw text.
    private static QueryString AppendPreserved(
        QueryString query, IReadOnlyList<PreservedQueryPair>? preserved)
    {
        foreach (var pair in preserved ?? [])
        {
            query = query.Add(pair.Name, pair.Value);
        }

        return query;
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
