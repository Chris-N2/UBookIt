namespace UBookIt.Core.Availability;

/// <summary>
/// One public holiday as a site's own source reports it: the date, and what that date is called.
/// </summary>
/// <param name="Date">The calendar date the holiday falls on.</param>
/// <param name="Name">
/// What the source calls it — "Christmas Day", "Boxing Day (substitute day)". Becomes the
/// closure's label if an operator chooses to import it.
/// </param>
/// <remarks>
/// <b>A name, not just a date, and the port requires it.</b> A closure's label is required, so a
/// date-only source would oblige the package to invent one — and every imported closure would
/// then read identically in the opt-out list a resource's editor shows, which is the bare-date
/// problem the label exists to prevent. Sources that have dates generally have names: the UK
/// feed supplies a title, and a source that genuinely has none can supply a constant.
/// </remarks>
public sealed record PublicHoliday(DateOnly Date, string Name);

/// <summary>
/// Where a site's public holidays come from. <b>Implemented by the site, never by the package.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>uBookIt ships no holiday data, for any country, and will not.</b> Shipping dates means
/// maintaining them — every jurisdiction, every substitution rule, forever — and being silently
/// wrong the year a government moves one. What the package can do is take what a site's own code
/// supplies and turn it into closures an operator controls.
/// </para>
/// <para>
/// <b>An implementation may read from anywhere.</b> An HTTP API, a file, a database table, a
/// hard-coded list. The package makes no assumption beyond this signature.
/// </para>
/// <para>
/// <b>No region, country or locale parameter, deliberately.</b> Which jurisdiction's holidays a
/// site wants is a property of the implementation it registered, not something the package could
/// validate, default, or meaningfully pass through. A site needing England and Scotland merges
/// them in its own source; the import collapses any same-date duplicates that produces and says
/// so.
/// </para>
/// <para>
/// <b>Optional.</b> Registering one is how a site turns the feature on, and registering none is
/// how it stays off — there is no setting. Resolved as a nullable dependency, on the same terms
/// as <c>IBookingObserver</c>: a host that supplies one gets the feature, a host that does not
/// gets no trace of it.
/// </para>
/// <para>
/// <b>Called only when an operator asks.</b> Nothing in the package schedules this, runs it at
/// startup, or calls it as a side effect of anything else — see the `public-holidays` spec for
/// why an automatic import would make a deleted closure impossible to delete.
/// </para>
/// </remarks>
public interface IPublicHolidaySource
{
    /// <summary>
    /// The holidays this source knows about within the inclusive window
    /// <paramref name="from"/>–<paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// <b>Returning fewer than the window asked for is not an error</b>, including returning none:
    /// a source that publishes three years ahead legitimately answers a five-year window with
    /// three. Dates outside the window are ignored by the caller rather than treated as a fault.
    /// <para>
    /// <b>Respect the cancellation token.</b> This runs inside an operator's request, and a source
    /// that ignores cancellation holds their screen. The package sets no timeout of its own: the
    /// token is the contract.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<PublicHoliday>> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
