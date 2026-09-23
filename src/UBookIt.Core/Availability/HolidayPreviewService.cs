using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Core.Availability;

/// <summary>
/// Produces the preview an operator selects from: what a site's source returned for a window,
/// classified against the closures the site already has.
/// </summary>
public interface IHolidayPreviewService
{
    /// <summary>
    /// Whether this site has a holiday source at all.
    /// </summary>
    /// <remarks>
    /// The feature is <b>absent</b> without one rather than disabled: the client renders no import
    /// control, and the endpoints refuse, so the absence is not something only a client observes.
    /// </remarks>
    bool SourceRegistered { get; }

    /// <summary>
    /// The rows an operator may choose from for the inclusive window
    /// <paramref name="from"/>–<paramref name="to"/>.
    /// </summary>
    /// <remarks>
    /// <b>Creates nothing, and is safe to repeat.</b>
    /// <para>
    /// Fails with <see cref="FailureCodes.HolidaySourceAbsent"/> when no source is registered, and
    /// with <see cref="FailureCodes.HolidaySourceFailed"/> when the site's own source throws —
    /// <b>never</b> by reporting a broken source as an empty list, because a window containing no
    /// holidays is a real answer and a broken source is not an answer at all.
    /// </para>
    /// </remarks>
    Task<DomainResult<IReadOnlyList<HolidayRow>>> PreviewAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IHolidayPreviewService"/>
/// <remarks>
/// <b>The source is optional, on the same terms as <c>IBookingObserver</c>.</b> A site that
/// registers one gets the feature; a site that does not gets no trace of it, with no setting to
/// configure and nothing logged.
/// </remarks>
public sealed class HolidayPreviewService(
    ISiteClosureStore closures,
    IPublicHolidaySource? source = null) : IHolidayPreviewService
{
    public bool SourceRegistered => source is not null;

    public async Task<DomainResult<IReadOnlyList<HolidayRow>>> PreviewAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (source is null)
        {
            return DomainResult<IReadOnlyList<HolidayRow>>.Failure(
                FailureCodes.HolidaySourceAbsent, "This site has no public holiday source.");
        }

        if (to < from)
        {
            // An inverted window is a malformed request, not a window with nothing in it. Left
            // unchecked it reads back as "the source returned no holidays for those dates",
            // which is the same conflation of "no answer" with "an empty answer" that this
            // service exists to prevent one layer up. Reported with the code the availability
            // surface already uses for the same mistake, rather than a new one.
            return DomainResult<IReadOnlyList<HolidayRow>>.Failure(
                FailureCodes.DateRangeInvalid, "The from date must not be after the to date.");
        }

        IReadOnlyList<PublicHoliday> holidays;

        try
        {
            // The cancellation token goes STRAIGHT THROUGH to the site's own code. This runs
            // inside an operator's request, frequently over a network somebody else owns, and a
            // source that cannot be abandoned holds the screen. The package sets no timeout of
            // its own: a timeout is a policy needing a setting and a default, where the token is
            // the standard contract.
            holidays = await source.GetAsync(from, to, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The operator's own doing, not a fault to report as one.
            throw;
        }
        catch (Exception exception)
        {
            // EVERYTHING else a host's code can throw. Deliberately broad: this is arbitrary
            // site code reaching an arbitrary place, and the failure modes are not ours to
            // enumerate. What matters is that it is reported AS a failure rather than as an
            // empty holiday list.
            //
            // **The exception's own message is deliberately NOT carried into the result.** It is
            // arbitrary text from arbitrary site code reaching an arbitrary place, and the things
            // that end up in such a message — a URI with its query string, a connection string, a
            // token in a header dump — would then travel into an HTTP response body. The
            // operator's client renders a fixed sentence from this code and never shows the
            // detail, so carrying it would expose it to nobody's benefit. The type name is kept
            // because it names the shape of the failure without quoting anything the host
            // composed; whoever registered the source has their own logs for the rest.
            return DomainResult<IReadOnlyList<HolidayRow>>.Failure(
                FailureCodes.HolidaySourceFailed,
                $"The site's public holiday source failed ({exception.GetType().Name}).");
        }

        var closedDates = (await closures.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(closure => closure.Date);

        return DomainResult<IReadOnlyList<HolidayRow>>.Success(
            HolidayImport.Classify(holidays ?? [], closedDates, from, to));
    }
}
