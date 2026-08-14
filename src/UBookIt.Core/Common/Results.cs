namespace UBookIt.Core.Common;

/// <summary>
/// A single validation or operation failure with a stable machine-readable code.
/// Codes are part of the public contract; consumers (delivery API, front-ends)
/// map them to user-facing messages.
/// </summary>
public sealed record DomainFailure(string Code, string Message, string? Field = null);

/// <summary>Stable failure codes. Values are contract; never change an existing value.</summary>
public static class FailureCodes
{
    // Placement pipeline (order matters; see bookings spec)
    public const string IntervalInvalid = "interval-invalid";
    public const string Granularity = "granularity";
    public const string DurationTooShort = "duration-too-short";
    public const string DurationTooLong = "duration-too-long";
    public const string LeadTime = "lead-time";
    public const string Horizon = "horizon";
    public const string OutsideOpenHours = "outside-open-hours";
    public const string Conflict = "conflict";

    // Status machine
    public const string InvalidStatusTransition = "invalid-status-transition";

    // Lookups and queries
    public const string ResourceNotFound = "resource-not-found";
    public const string ResourceInUse = "resource-in-use";
    public const string BookingNotFound = "booking-not-found";
    public const string DateRangeInvalid = "date-range-invalid";
    public const string DateRangeTooLarge = "date-range-too-large";
    public const string TimeZoneInvalid = "time-zone-invalid";

    // Definitional validation
    public const string ClaimsInvalid = "claims-invalid";
    public const string DisplayNameRequired = "display-name-required";
    public const string TypeKeyInvalid = "type-key-invalid";

    /// <summary>
    /// A capability key is not a normalized key. Distinct from
    /// <see cref="TypeKeyInvalid"/> so that a service role carrying both a
    /// malformed resource type and a malformed capability reports two failures a
    /// consumer can associate with two different controls.
    /// </summary>
    public const string CapabilityKeyInvalid = "capability-key-invalid";
    public const string WindowInvalid = "window-invalid";
    public const string WindowsOverlap = "windows-overlap";
    public const string DuplicateExceptionDate = "duplicate-exception-date";
    public const string ConstraintsIncoherent = "constraints-incoherent";
    public const string NameRequired = "name-required";
    public const string EmailInvalid = "email-invalid";

    // Services
    public const string ServiceNameRequired = "service-name-required";
    public const string ServiceRoleInvalid = "service-role-invalid";

    /// <summary>
    /// Two of a service's roles name the same resource type. Distinct from
    /// <see cref="TypeKeyInvalid"/> because the two faults are corrected
    /// differently: that one is a typo in a key, this one a composition this
    /// version does not support.
    /// <para>
    /// A deliberate restriction of this version rather than a property of
    /// services (multi-role-composition design D1): roles of the same type draw
    /// from overlapping eligibility pools, where assigning each role its first
    /// available candidate can report a service unavailable when it was
    /// bookable. The change that implements real assignment lifts it.
    /// </para>
    /// </summary>
    public const string ServiceRoleDuplicateType = "service-role-duplicate-type";
    public const string ServiceDurationInvalid = "service-duration-invalid";
    public const string ServiceNotFound = "service-not-found";

    /// <summary>
    /// Every candidate resource rejected a service placement deterministically —
    /// the start is off their grids, outside their open hours, inside their lead
    /// times, or beyond their horizons. Distinct from <see cref="Conflict"/>:
    /// retrying cannot succeed, because nothing was taken.
    /// <para>
    /// Doubles as a drift signal (service-booking spec). A client placing only
    /// starts and lengths taken from the service availability query cannot
    /// legitimately provoke this, so its arrival from a conforming client means
    /// availability and placement disagree about the same rules.
    /// </para>
    /// </summary>
    public const string ServiceUnavailable = "service-unavailable";

    /// <summary>
    /// A service placement named a preferred resource that is not in the
    /// service's candidate pool. Rejected rather than ignored: a caller who
    /// names a resource has stated an expectation, and silently booking a
    /// different one discards it invisibly.
    /// </summary>
    public const string ResourceNotEligible = "resource-not-eligible";
}

/// <summary>
/// Outcome of a domain operation. Expected rejections are results, not exceptions.
/// </summary>
public class DomainResult
{
    private static readonly IReadOnlyList<DomainFailure> NoFailures = [];

    private protected DomainResult(bool succeeded, IReadOnlyList<DomainFailure> failures)
    {
        Succeeded = succeeded;
        Failures = failures;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<DomainFailure> Failures { get; }

    public static DomainResult Success() => new(true, NoFailures);

    public static DomainResult Failure(params IReadOnlyList<DomainFailure> failures)
    {
        if (failures.Count == 0)
        {
            throw new ArgumentException("At least one failure is required.", nameof(failures));
        }

        return new DomainResult(false, [.. failures]);
    }

    public static DomainResult Failure(string code, string message, string? field = null)
        => Failure(new DomainFailure(code, message, field));
}

/// <summary>A <see cref="DomainResult"/> carrying a value on success.</summary>
public sealed class DomainResult<T> : DomainResult
{
    private readonly T? _value;

    private DomainResult(bool succeeded, T? value, IReadOnlyList<DomainFailure> failures)
        : base(succeeded, failures)
    {
        _value = value;
    }

    /// <summary>The successful value. Throws if the result is a failure.</summary>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read {nameof(Value)} of a failed result ({string.Join(", ", Failures.Select(f => f.Code))}).");

    public static DomainResult<T> Success(T value) => new(true, value, []);

    public static new DomainResult<T> Failure(params IReadOnlyList<DomainFailure> failures)
    {
        if (failures.Count == 0)
        {
            throw new ArgumentException("At least one failure is required.", nameof(failures));
        }

        return new DomainResult<T>(false, default, [.. failures]);
    }

    public static new DomainResult<T> Failure(string code, string message, string? field = null)
        => Failure(new DomainFailure(code, message, field));
}
