using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A run of dates on which one resource is closed by its own rule — "being painted",
/// "on leave" — from <see cref="StartDate"/> to <see cref="EndDate"/>, both inclusive.
/// </summary>
/// <remarks>
/// <para>
/// <b>Closed only.</b> Different hours on a date stay the single-day exception's job; a
/// period of altered hours would need a weekly pattern of its own, which is not what an
/// operator closing a room for a fortnight is describing.
/// </para>
/// <para>
/// <b>The resource's own, at the same rung as its exceptions.</b> A site closure still
/// comes first; a resource that opts out of one falls through to its closed period. The
/// rules that keep a period from contradicting the resource's other rules — no overlap with
/// another period, no date shared with an exception — are judged where the complete set is
/// known, in <see cref="AvailabilityConfiguration.Create"/>.
/// </para>
/// <para>
/// <b>The note is the site's own and is never disclosed publicly</b>, for the reason a
/// closure's label is not: "broken leg" is about a person, and the booking flow needs only
/// to know the date is unavailable.
/// </para>
/// </remarks>
public sealed class ClosedPeriod
{
    /// <summary>The longest permitted note, matching the package's other name columns.</summary>
    public const int MaxNoteLength = 512;

    private ClosedPeriod(DateOnly startDate, DateOnly endDate, string? note)
    {
        StartDate = startDate;
        EndDate = endDate;
        Note = note;
    }

    public DateOnly StartDate { get; }

    public DateOnly EndDate { get; }

    /// <summary>The site's own note, if any; trimmed, and null rather than blank.</summary>
    public string? Note { get; }

    public bool Covers(DateOnly date) => date >= StartDate && date <= EndDate;

    public bool Overlaps(ClosedPeriod other) => StartDate <= other.EndDate && other.StartDate <= EndDate;

    public static DomainResult<ClosedPeriod> Create(DateOnly startDate, DateOnly endDate, string? note = null)
    {
        if (endDate < startDate)
        {
            return DomainResult<ClosedPeriod>.Failure(
                FailureCodes.ClosedPeriodInvalid,
                $"A closed period cannot end ({endDate:yyyy-MM-dd}) before it starts ({startDate:yyyy-MM-dd}).",
                nameof(EndDate));
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > MaxNoteLength })
        {
            // Validated here rather than left to the column, so an over-long note is the
            // stable code and not a 500 at INSERT.
            return DomainResult<ClosedPeriod>.Failure(
                FailureCodes.ClosedPeriodInvalid,
                $"A closed period's note may be at most {MaxNoteLength} characters.",
                nameof(Note));
        }

        return DomainResult<ClosedPeriod>.Success(new ClosedPeriod(startDate, endDate, trimmed));
    }
}
