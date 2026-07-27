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
    public const string BookingNotFound = "booking-not-found";
    public const string DateRangeInvalid = "date-range-invalid";
    public const string TimeZoneInvalid = "time-zone-invalid";

    // Definitional validation
    public const string DisplayNameRequired = "display-name-required";
    public const string TypeKeyInvalid = "type-key-invalid";
    public const string WindowInvalid = "window-invalid";
    public const string WindowsOverlap = "windows-overlap";
    public const string DuplicateExceptionDate = "duplicate-exception-date";
    public const string ConstraintsIncoherent = "constraints-incoherent";
    public const string NameRequired = "name-required";
    public const string EmailInvalid = "email-invalid";
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
