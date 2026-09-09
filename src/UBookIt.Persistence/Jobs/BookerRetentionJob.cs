using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using Umbraco.Cms.Infrastructure.BackgroundJobs;

namespace UBookIt.Persistence.Jobs;

/// <summary>
/// Erases the booker of every booking whose interval ended longer ago than the site's configured
/// retention period. Does nothing at all unless a period is configured.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="IDistributedBackgroundJob"/>, not <c>IRecurringBackgroundJob</c>.</b> Umbraco 17
/// ships both. The recurring one runs a hosted service per job on every server, gated on server
/// role and main-dom; the distributed one takes a database-backed lease so that exactly one server
/// runs the job, and recovers it if that server stops mid-run. Every cleanup job Umbraco itself
/// ships — content versions, the log scrubber, temporary files — uses the distributed one, and
/// retention is that shape exactly: periodic, idempotent, and needing to run <i>somewhere</i>
/// rather than on a particular machine. It also means the package needs no lock of its own.
/// </para>
/// <para>
/// <b>This job adds no erasure mechanics.</b> It is a clock, a way to find what the clock has
/// caught, and a loop. Erasure itself belongs to <see cref="IBookingService.EraseBookerAsync"/>
/// and stays there, so that anything erasure grows later — an audit record, a notification —
/// reaches retention without this file being touched. A set-based bulk erase would be faster and
/// would be a second place for erasure to happen; the next change to erasure would update one of
/// them, and nothing would look broken because both would still erase.
/// </para>
/// </remarks>
internal sealed class BookerRetentionJob(
    IServiceScopeFactory scopeFactory,
    SiteBookingSettings settings,
    TimeProvider timeProvider,
    ILogger<BookerRetentionJob> logger) : IDistributedBackgroundJob
{
    /// <summary>
    /// How many bookings are selected and erased per unit of work.
    /// </summary>
    /// <remarks>
    /// Not configurable. A knob whose right value nobody can reason about is surface rather than
    /// flexibility, and this one trades only the size of a transaction against the number of round
    /// trips — neither of which a site operator is in a position to tune.
    /// </remarks>
    internal const int BatchSize = 100;

    /// <summary>
    /// The lease key, stored durably in Umbraco's own scheduling table.
    /// </summary>
    /// <remarks>
    /// <b>A fixed constant, never derived.</b> Umbraco records the job against this name; deriving
    /// it from a type name, an assembly name or a package version would silently orphan the
    /// existing row and restart the schedule the first time any of those changed.
    /// </remarks>
    public string Name => "UBookItBookerRetention";

    /// <summary>
    /// How often the sweep runs. Hourly, matching Umbraco's own cleanup jobs.
    /// </summary>
    /// <remarks>
    /// The retention <i>policy</i> is expressed in days, so the period is an implementation detail
    /// rather than something a site should tune: running hourly costs one indexed query against an
    /// empty result set, and it means a site that has just switched retention on does not wait a
    /// day to see it take effect.
    /// </remarks>
    public TimeSpan Period => TimeSpan.FromHours(1);

    /// <inheritdoc />
    public Task ExecuteAsync() => ExecuteAsync(CancellationToken.None);

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (settings.RetentionDays is not int retentionDays)
        {
            // Off, which is the default. No query, no scope, no log line — a feature nobody has
            // turned on should cost nothing observable, or its absence becomes noise every hour
            // on every site that will never use it.
            return;
        }

        // From the injected clock, never DateTimeOffset.UtcNow. The whole feature is one
        // comparison against this instant, and a test that cannot move the clock cannot test it.
        var cutoffUtc = timeProvider.GetUtcNow().AddDays(-retentionDays);

        var erasedTotal = 0;
        var failedTotal = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // A scope per batch. This job is a SINGLETON resolved from the root container, while
            // the store and the booking service are scoped over a DbContext — capturing either
            // would hold one DbContext for the lifetime of the application.
            using var scope = scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IBookingStore>();
            var bookings = scope.ServiceProvider.GetRequiredService<IBookingService>();

            // ALWAYS THE HEAD OF THE SET, NEVER AN OFFSET.
            //
            // The due set is defined by "not yet erased", so erasing a booking removes it from
            // this result. Paging with skip = 0, 100, 200 … would therefore step over exactly as
            // many un-erased bookings as the previous batch erased: after the first hundred are
            // erased the set shifts down by a hundred, and skip = 100 lands past a hundred
            // bookings nothing ever touched. The sweep would report success having erased a
            // fraction of the data, and every test whose fixtures fit in one batch would pass.
            var due = await store
                .GetBookingIdsDueForErasureAsync(cutoffUtc, BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (due.Count == 0)
            {
                break;
            }

            var erasedInBatch = 0;

            foreach (var bookingId in due)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Between units of work, not inside one. Erasure is absorbing, so a sweep
                    // stopped here leaves every booking it reached erased and every booking it did
                    // not reach still due — which is what makes the next run complete the work
                    // with no double erasure and no gap.
                    //
                    // Guarded by A_sweep_cancelled_PART_WAY_stops_and_leaves_the_rest_due, which
                    // cancels from INSIDE the loop. QA found this check unfalsifiable when the
                    // only interruption test cancelled before the run: the while condition is then
                    // false on entry and this line is never reached.
                    break;
                }

                try
                {
                    var result = await bookings
                        .EraseBookerAsync(bookingId, cancellationToken)
                        .ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        erasedInBatch++;
                    }
                    else
                    {
                        failedTotal++;

                        // The failure CODE, not its message. Nothing in this package's failure
                        // messages carries a booker's details today, and this is the one code path
                        // with no user accountable for what it reads — so it takes the value whose
                        // contents are a fixed vocabulary rather than the one composed at the
                        // throw site.
                        logger.LogWarning(
                            "uBookIt retention could not erase booking {BookingId}: {FailureCode}.",
                            bookingId,
                            result.Failures.Count > 0 ? result.Failures[0].Code : "unknown");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One booking must not stop the others. EraseBookerAsync throws if a row is
                    // erased and then cannot be read back; letting that escape would abort the run
                    // and let a single permanently bad row block retention for every booking
                    // behind it, on every run, for ever.
                    failedTotal++;
                    logger.LogError(ex, "uBookIt retention failed to erase booking {BookingId}.", bookingId);
                }
            }

            erasedTotal += erasedInBatch;

            if (erasedInBatch == 0 && !cancellationToken.IsCancellationRequested)
            {
                // ANTI-SPIN. A booking that fails to erase still matches the due predicate, so it
                // is selected again next time round — for ever, holding a connection, in a loop
                // that looks from the outside like a busy site. A batch that achieved nothing is
                // the signal that no further batch will achieve anything either.
                logger.LogError(
                    "uBookIt retention stopped after a batch of {BatchCount} booking(s) in which nothing could be erased. "
                    + "{ErasedTotal} booking(s) were erased before this point and {FailedTotal} failed.",
                    due.Count,
                    erasedTotal,
                    failedTotal);
                break;
            }
        }

        if (erasedTotal > 0 || failedTotal > 0)
        {
            // Counts and identifiers only, never a person. The booker-erasure capability requires
            // that contact details have exactly one durable home, and names retention reporting as
            // precisely the feature that would casually create a second one.
            logger.LogInformation(
                "uBookIt retention erased {ErasedTotal} booking(s) that ended before {CutoffUtc:o}, with {FailedTotal} failure(s).",
                erasedTotal,
                cutoffUtc,
                failedTotal);
        }
    }
}
