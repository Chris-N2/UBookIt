using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A run of dates — one or many — on which the organisation itself is closed, inherited
/// by every resource unless that resource opts out of this particular closure.
/// </summary>
/// <remarks>
/// <b>A closure is not one of a resource's exceptions, and is never stored as one.</b>
/// It is a separate layer consulted ahead of them by
/// <see cref="AvailabilityConfiguration.EffectiveWindows"/>, which is what keeps a
/// resource's own exceptions exactly what its editor typed: the collection the
/// resource write path reads is not the collection closures live in, so no save can
/// convert an inherited date into one the resource owns.
/// <para>
/// <b>Full-day.</b> Closing part of a day is what a resource's own override exception
/// already expresses, and a site-wide half day is expressible as one.
/// </para>
/// </remarks>
public sealed class SiteClosure
{
    /// <summary>
    /// The longest permitted label, matching the package's other name columns.
    /// </summary>
    /// <remarks>
    /// Held here rather than in the EF configuration so that the column and the
    /// validation cannot drift apart — the same arrangement
    /// <c>BookingReference.Length</c> already has with the reference column.
    /// </remarks>
    public const int MaxLabelLength = 512;

    private SiteClosure(Guid id, DateOnly date, DateOnly endDate, string label)
    {
        Id = id;
        Date = date;
        EndDate = endDate;
        Label = label;
    }

    /// <summary>
    /// Stable across edits to either the date or the label. A resource's opt-out
    /// names this, never the date: editing a closure's date then carries its
    /// opt-outs with it, and a date deleted and later recreated is a new closure
    /// that nobody has yet been exempted from.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The first date the closure covers. Named <c>Date</c> because a closure was a single
    /// date before it was a range, and every existing reader of it still means "where it
    /// begins".
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// The last date the closure covers, inclusive. Equal to <see cref="Date"/> for a closure
    /// of one day — which is not a separate kind of closure, only a short range.
    /// </summary>
    public DateOnly EndDate { get; }

    /// <summary>Whether the closure covers <paramref name="date"/>.</summary>
    public bool Covers(DateOnly date) => date >= Date && date <= EndDate;

    /// <summary>Whether this closure and <paramref name="other"/> share any date.</summary>
    public bool Overlaps(SiteClosure other) => Date <= other.EndDate && other.Date <= EndDate;

    /// <summary>
    /// The site's own name for the date. Required, because this is what a resource
    /// editor shows beside an opt-out control.
    /// </summary>
    public string Label { get; }

    /// <summary>A closure of the one day <paramref name="date"/>.</summary>
    /// <remarks>
    /// The signature published before closures were ranges, kept exactly — an assembly compiled
    /// against it binds to this method by its full signature, and widening it in place would
    /// break that assembly at run time even though its source still compiles. New behaviour is
    /// a new member: <see cref="CreateRange"/>.
    /// </remarks>
    public static DomainResult<SiteClosure> Create(DateOnly date, string? label, Guid? id = null)
        => CreateRange(date, date, label, id);

    /// <summary>A closure covering every date from <paramref name="date"/> to <paramref name="endDate"/>, both inclusive.</summary>
    /// <param name="date">The first date the closure covers.</param>
    /// <param name="endDate">The last date covered; the same date for a closure of one day.</param>
    /// <param name="label">The site's own name for it; required.</param>
    /// <param name="id">An existing closure's id, when rebuilding or editing one.</param>
    public static DomainResult<SiteClosure> CreateRange(
        DateOnly date, DateOnly endDate, string? label, Guid? id = null)
    {
        var end = endDate;

        if (end < date)
        {
            return DomainResult<SiteClosure>.Failure(
                FailureCodes.ClosureRangeInvalid,
                $"A closure cannot end ({end:yyyy-MM-dd}) before it starts ({date:yyyy-MM-dd}).",
                nameof(EndDate));
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return DomainResult<SiteClosure>.Failure(
                FailureCodes.ClosureLabelInvalid,
                "A closure label is required.",
                nameof(Label));
        }

        var trimmed = label.Trim();

        if (trimmed.Length > MaxLabelLength)
        {
            // Validated here rather than left to the column, so an over-long label
            // is the stable `closure-label-invalid` code and not a 500 at INSERT.
            return DomainResult<SiteClosure>.Failure(
                FailureCodes.ClosureLabelInvalid,
                $"A closure label may be at most {MaxLabelLength} characters.",
                nameof(Label));
        }

        return DomainResult<SiteClosure>.Success(new SiteClosure(id ?? Guid.NewGuid(), date, end, trimmed));
    }
}
