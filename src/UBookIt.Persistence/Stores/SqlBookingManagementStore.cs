using Microsoft.EntityFrameworkCore;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;

namespace UBookIt.Persistence.Stores;

/// <summary>
/// Lists bookings for the backoffice: the window, the filters, the page, and the
/// resource names a list row needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>It does not validate, and has nothing to validate.</b> A <see cref="BookingQuery"/>
/// cannot be constructed with an unusable window, so there is no invalid state to defend
/// against here. See <see cref="IBookingManagementStore"/>.
/// </para>
/// <para>
/// <b>The query is rooted on bookings, not on claims, and that is load-bearing.</b> A
/// booking claims 1..N resources. Rooting on the claim join — the obvious way to filter
/// by resource — returns one row per claim, so a booking claiming two resources appears
/// twice, pages wrongly and double-counts in the total. Both the filter and the names are
/// therefore expressed as subqueries over the claims of a booking that has already been
/// selected exactly once.
/// </para>
/// </remarks>
internal sealed class SqlBookingManagementStore(UBookItDbContext db) : IBookingManagementStore
{
    public async Task<BookingPage> ListAsync(
        BookingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // No defaulting, no clamping and no validating here: a BookingQuery cannot be
        // constructed in a state that needs any of them. That is the point of the type.
        var matching = Matching(query);

        // Over the window and filters, not over the page: a pager showing the page size
        // as the total is a pager that never offers a second page.
        var total = await matching.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await OrderedPage(matching, query)
            .Select(booking => new
            {
                booking.Id,
                booking.Reference,
                booking.StartUtc,
                booking.EndUtc,
                booking.TimeZoneId,
                booking.Status,
                booking.CreatedUtc,
                booking.BookerName,
                booking.BookerEmail,
                booking.BookerErasedUtc,

                // Read from the booking row, NOT joined to the service table. The stored
                // name is the snapshot taken at placement; a join would answer with the
                // name the service has now, and would answer with nothing once the service
                // is deleted — losing an attribution the booking definitely had.
                booking.ServiceId,
                booking.ServiceName,

                // Correlated, so it cannot multiply the booking row above. Joined to
                // resources here because a claim carries only an id, and resolving names
                // at the call site is one read per claim per row — the cost this port
                // exists to remove.
                Resources = (from claim in db.Claims
                             join resource in db.Resources on claim.ResourceId equals resource.Id
                             where claim.BookingId == booking.Id
                             orderby resource.DisplayName, resource.Id
                             select new { resource.Id, resource.DisplayName })
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(row => new BookingSummary(
                row.Id,
                BookingReference.FromCanonical(row.Reference),
                BookingInterval.Create(row.StartUtc, row.EndUtc, row.TimeZoneId).Value,
                (BookingStatus)row.Status,
                row.CreatedUtc,
                ToSummaryBooker(row.BookerName, row.BookerEmail, row.BookerErasedUtc),
                [.. row.Resources.Select(r => new BookedResource(r.Id, r.DisplayName))],
                BookingAttributionMapper.ToAttribution(row.ServiceId, row.ServiceName)))
            .ToList();

        return new BookingPage(items, total);
    }

    /// <summary>
    /// The matching bookings, ordered and paged.
    /// <para>
    /// Ordered by start, then id. <b>The id tiebreak is required rather than tidy:</b>
    /// two bookings routinely share a start time, and <c>Skip</c>/<c>Take</c> over an
    /// order that is not total silently repeats or drops rows between pages.
    /// </para>
    /// <para>
    /// Separated from <see cref="ListAsync"/> so a test can read the SQL this produces.
    /// The tiebreak has no observable effect on results today — <c>Id</c> is the
    /// clustered key, so SQL Server's incidental order already matches it — which means
    /// the only way to catch its removal is to look at the generated <c>ORDER BY</c>.
    /// Without this seam the guard would have been a comment.
    /// </para>
    /// </summary>
    internal IQueryable<Entities.BookingRow> OrderedPage(
        IQueryable<Entities.BookingRow> matching, BookingQuery query)
        => matching
            .OrderBy(booking => booking.StartUtc)
            .ThenBy(booking => booking.Id)
            .Skip(query.Skip)
            .Take(query.Take);

    /// <summary>Convenience for tests: the ordered, paged query for a whole request.</summary>
    internal IQueryable<Entities.BookingRow> OrderedPage(BookingQuery query)
        => OrderedPage(Matching(query), query);

    /// <summary>
    /// The bookings the query selects, before ordering and paging — shared by the count
    /// and the page so the two can never disagree about what "matching" means.
    /// </summary>
    private IQueryable<Entities.BookingRow> Matching(BookingQuery query)
    {
        var statuses = query.Statuses.Select(status => (int)status).ToList();

        var matching = db.Bookings
            .AsNoTracking()

            // Half-open overlap, exactly as SqlBookingStore matches claims: a booking
            // that starts before the window and runs into it is within it. The naive
            // `StartUtc >= from` reading silently drops precisely those.
            .Where(booking => booking.StartUtc < query.ToUtc && query.FromUtc < booking.EndUtc)
            .Where(booking => statuses.Contains(booking.Status));

        if (query.ResourceIds.Count > 0)
        {
            // `Any`, not a join: an empty set means no filter rather than "match
            // nothing", and a booking claiming several of the named resources is still
            // one booking.
            var ids = query.ResourceIds.ToList();

            matching = matching.Where(
                booking => db.Claims.Any(
                    claim => claim.BookingId == booking.Id && ids.Contains(claim.ResourceId)));
        }

        return matching;
    }

    /// <summary>
    /// The stored booker columns as the read port's two-state booker.
    /// </summary>
    /// <remarks>
    /// <b>The erasure column decides, not the absence of a name.</b> Inferring "erased" from
    /// a NULL name would read the state off missing data, which is what storing the instant
    /// exists to avoid — and it would report a row damaged by any other means as a lawful
    /// erasure. Mirrors <c>SqlBookingStore.ToBooker</c> deliberately: two projections of the
    /// same columns that disagreed about what they mean would be worse than either.
    /// </remarks>
    private static SummaryBooker ToSummaryBooker(string? name, string? email, DateTimeOffset? erasedUtc)
    {
        if (erasedUtc is { } erased)
        {
            return SummaryBooker.Erased(erased);
        }

        // Not suppressed with `!`. A row that is neither erased nor carrying details is a
        // state the schema and the domain both forbid, so meeting one means something has
        // written the table by another route — and quietly presenting it as an empty name
        // on an operator's screen is the worst of the available answers. Failing here says
        // which row and what is wrong with it.
        if (name is null || email is null)
        {
            throw new InvalidOperationException(
                "A booking row carries no erasure instant and no booker contact details. "
                + "The booker columns are NULL only for an erased booking, so this row was "
                + "not written by uBookIt.");
        }

        return SummaryBooker.Of(new SummaryContact(name, email));
    }
}
