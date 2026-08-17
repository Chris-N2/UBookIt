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
    /// Two of a service's roles name the same resource type <em>and</em> require
    /// the same capabilities. Distinct from <see cref="TypeKeyInvalid"/> because
    /// the two faults are corrected differently: that one is a typo in a key, this
    /// one a requirement stated twice where a count was meant.
    /// <para>
    /// Narrowed from "same type" by multi-role-matching design D5, once real
    /// assignment made overlapping pools resolvable. Two roles of one type
    /// requiring <em>different</em> capabilities are now valid and are the case
    /// the assignment exists for — "a senior therapist and any therapist". What
    /// remains rejected is two roles identical in both, which are two spellings of
    /// one requirement: allowing both would leave two representations that compare
    /// unequal and publish differently while meaning the same thing, and silently
    /// merging them would rewrite what the editor entered. The message therefore
    /// names the count as the correction.
    /// </para>
    /// </summary>
    public const string ServiceRoleDuplicateType = "service-role-duplicate-type";

    /// <summary>
    /// A service role's count is below 1 or above the permitted maximum. Its own
    /// code rather than the general <see cref="ServiceRoleInvalid"/> so a consumer
    /// can land the message on the count control of the offending row.
    /// <para>
    /// The upper bound is a sanity limit on work rather than a domain claim: each
    /// unit is a slot the assignment must fill, so an unbounded count is an
    /// unbounded amount of work for a configuration that cannot succeed — the
    /// reasoning that made <c>NormalizedKey.MaxLength</c> turn an over-long key
    /// into a validation failure rather than a 500 at INSERT (design D8).
    /// </para>
    /// <para>
    /// A count exceeding the number of <em>eligible</em> resources is deliberately
    /// NOT this failure, or any failure: that is a property of the pool rather than
    /// of the service, and resources may be added later.
    /// </para>
    /// </summary>
    public const string ServiceRoleCountInvalid = "service-role-count-invalid";
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
    /// A service placement named a pinned resource that is not in the
    /// service's candidate pool. Rejected rather than ignored: a caller who
    /// names a resource has stated an expectation, and silently booking a
    /// different one discards it invisibly.
    /// </summary>
    public const string ResourceNotEligible = "resource-not-eligible";

    /// <summary>
    /// A booking named a single resource that is not offered on its own. A
    /// property of what the business offers, not of the calendar — so it is
    /// deliberately distinct from every unavailability code: retrying at another
    /// time cannot help, and the resource's opening hours are not wrong.
    /// <para>
    /// It says nothing about the resource being bookable <em>at all</em>: the same
    /// resource is claimed by a service booking without ever reaching this rule.
    /// </para>
    /// </summary>
    public const string ResourceNotDirectlyBookable = "resource-not-directly-bookable";

    /// <summary>
    /// A service placement named a resource that is eligible, but no saturating
    /// assignment at that instant could include it. The caller chose that
    /// resource; confirming a booking on a different one would answer a question
    /// they did not ask.
    /// <para>
    /// <b>Transient.</b> The pinned resource may be free at another time or may
    /// free up, so a retry can succeed — unlike
    /// <see cref="ServiceUnavailable"/>, which exists to say it cannot.
    /// </para>
    /// <para>
    /// Distinct from <see cref="Conflict"/>, which says nothing could be booked
    /// and leaves a caller unable to tell that other resources were free; and from
    /// <see cref="ResourceNotEligible"/>, which says the named resource could never
    /// fulfil this service at all.
    /// </para>
    /// </summary>
    public const string PinnedResourceUnavailable = "pinned-resource-unavailable";
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
