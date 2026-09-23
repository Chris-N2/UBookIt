using UBookIt.Core.Common;

namespace UBookIt.Core.Availability;

/// <summary>
/// A date on which the organisation itself is closed, inherited by every resource
/// unless that resource opts out of this particular closure.
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

    private SiteClosure(Guid id, DateOnly date, string label)
    {
        Id = id;
        Date = date;
        Label = label;
    }

    /// <summary>
    /// Stable across edits to either the date or the label. A resource's opt-out
    /// names this, never the date: editing a closure's date then carries its
    /// opt-outs with it, and a date deleted and later recreated is a new closure
    /// that nobody has yet been exempted from.
    /// </summary>
    public Guid Id { get; }

    public DateOnly Date { get; }

    /// <summary>
    /// The site's own name for the date. Required, because this is what a resource
    /// editor shows beside an opt-out control.
    /// </summary>
    public string Label { get; }

    public static DomainResult<SiteClosure> Create(DateOnly date, string? label, Guid? id = null)
    {
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

        return DomainResult<SiteClosure>.Success(new SiteClosure(id ?? Guid.NewGuid(), date, trimmed));
    }
}
