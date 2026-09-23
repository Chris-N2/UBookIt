using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>What an operator may do with one holiday a source returned.</summary>
public enum HolidayRowState
{
    /// <summary>No closure exists for the date. Offered, and selected by default.</summary>
    New,

    /// <summary>
    /// A closure already exists for the date — whoever made it, and whenever. Not offered.
    /// </summary>
    /// <remarks>
    /// At most one closure may exist per date, and the one already there is not this import's to
    /// replace: it may carry a label an operator chose, and overwriting it would rewrite what an
    /// editor typed.
    /// </remarks>
    AlreadyClosed,

    /// <summary>
    /// The holiday would be refused by the domain — a blank or over-long name. Not offered, and
    /// shown with its reason rather than dropped.
    /// </summary>
    CannotImport,
}

/// <summary>One holiday from a source, classified against the closures a site already has.</summary>
/// <param name="Date">The holiday's date.</param>
/// <param name="Name">The name the source gave it, trimmed.</param>
/// <param name="State">What the operator may do with it.</param>
/// <param name="Reason">
/// Why it cannot be imported, for <see cref="HolidayRowState.CannotImport"/>; null otherwise. A
/// stable failure code, not a sentence, so a client renders its own words.
/// </param>
/// <param name="CollapsedDuplicate">
/// True when the source returned more than one holiday for this date and the others were dropped.
/// <b>Reported rather than silent</b>: an operator who expected a name they cannot see deserves to
/// know it was collapsed rather than lost.
/// </param>
public sealed record HolidayRow(
    DateOnly Date,
    string Name,
    HolidayRowState State,
    string? Reason = null,
    bool CollapsedDuplicate = false);

/// <summary>
/// Decides what an operator is offered when they preview an import.
/// </summary>
/// <remarks>
/// <b>A pure function over two inputs</b> — what the source returned, and which dates already
/// carry closures — so every interesting case is a unit test over data: a duplicate, an over-long
/// name, a date outside the window, a date already closed. No database, no network, no HTTP.
/// <para>
/// <b>It lives in the domain rather than in the store.</b> The store's business is rows; deciding
/// what an operator should be offered is a rule, and a rule with a second home in SQL is a rule
/// free to disagree with itself — the same reasoning that keeps closure precedence out of the
/// query layer.
/// </para>
/// </remarks>
public static class HolidayImport
{
    /// <summary>
    /// Classifies what a source returned for a window against the dates already closed.
    /// </summary>
    /// <param name="holidays">What the source returned, in its own order.</param>
    /// <param name="closedDates">Every date the site already has a closure for.</param>
    /// <param name="from">Inclusive start of the window the operator asked for.</param>
    /// <param name="to">Inclusive end of the window the operator asked for.</param>
    /// <returns>One row per distinct in-window date, ordered by date.</returns>
    public static IReadOnlyList<HolidayRow> Classify(
        IEnumerable<PublicHoliday> holidays,
        IEnumerable<DateOnly> closedDates,
        DateOnly from,
        DateOnly to)
    {
        var closed = closedDates.ToHashSet();
        var rows = new Dictionary<DateOnly, HolidayRow>();

        foreach (var holiday in holidays)
        {
            // Outside the window the operator asked to see. Ignored rather than reported: the
            // window is the question, and a source answering more broadly is not at fault.
            if (holiday.Date < from || holiday.Date > to)
            {
                continue;
            }

            if (rows.TryGetValue(holiday.Date, out var existing))
            {
                // A source merging regions can legitimately return two names for one date, and
                // only one closure may exist per date. FIRST WINS — joining the names would
                // invent a label neither row carried, and that label is what an operator reads
                // against an opt-out. The collapse is reported on the row that survived.
                rows[holiday.Date] = existing with { CollapsedDuplicate = true };
                continue;
            }

            rows[holiday.Date] = Row(holiday, closed);
        }

        return rows.Values.OrderBy(row => row.Date).ToList();
    }

    private static HolidayRow Row(PublicHoliday holiday, HashSet<DateOnly> closed)
    {
        // Asked of the domain rather than re-implemented here: whether a name can be a label is
        // SiteClosure's rule, and a second copy of it would be free to drift from the one the
        // import itself must satisfy a moment later.
        var candidate = SiteClosure.Create(holiday.Date, holiday.Name);

        if (!candidate.Succeeded)
        {
            return new HolidayRow(
                holiday.Date,
                holiday.Name ?? string.Empty,
                HolidayRowState.CannotImport,
                FailureCodes.HolidayNotImportable);
        }

        // Already closed wins over anything else the row might have been: there is nothing to
        // offer, whatever the name looks like.
        var state = closed.Contains(holiday.Date)
            ? HolidayRowState.AlreadyClosed
            : HolidayRowState.New;

        return new HolidayRow(holiday.Date, candidate.Value.Label, state);
    }
}
