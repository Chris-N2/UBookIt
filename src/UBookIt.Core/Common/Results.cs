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

    /// <summary>
    /// A booking cannot be cancelled by the person who made it because it has already begun.
    /// </summary>
    /// <remarks>
    /// <b>Names the fact, not a policy.</b> "Already started" is a property of the booking and the
    /// clock; "too late to cancel" would be a statement about a site's rules, and the package has
    /// none — a cancellation window is a separate feature with its own vocabulary. Choosing the
    /// factual name means that feature can arrive without this code becoming a lie.
    /// <para>
    /// Produced only by the visitor's entry point. An operator may cancel a booking that has
    /// started, which is ordinary no-show tidying.
    /// </para>
    /// </remarks>
    public const string BookingAlreadyStarted = "booking-already-started";

    /// <summary>
    /// A move was asked for to the interval the booking already holds. Refused rather than
    /// reported as success, on the same grounds as cancelling twice: a caller told "moved"
    /// when nothing changed cannot tell a completed action from a rejected one.
    /// </summary>
    public const string IntervalUnchanged = "interval-unchanged";

    /// <summary>
    /// A caller named a booking status that does not exist. Distinct from
    /// <see cref="InvalidStatusTransition"/>, which is about a status that exists being
    /// unreachable from the current one.
    /// </summary>
    public const string BookingStatusInvalid = "booking-status-invalid";

    // Lookups and queries
    public const string ResourceNotFound = "resource-not-found";
    public const string ResourceInUse = "resource-in-use";
    public const string BookingNotFound = "booking-not-found";

    /// <summary>
    /// The value offered as a booking reference is not one: wrong length, or a character outside
    /// the reference alphabet once separators and case are discarded.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="BookingNotFound"/> because the two call for different
    /// corrections — "you mistyped it" against "no booking has that reference" — and an
    /// operator on the telephone needs to know which. Collapsing them into not-found would
    /// send somebody to re-check a date when the fault was a transcribed letter.
    /// </remarks>
    public const string ReferenceInvalid = "reference-invalid";
    public const string DateRangeInvalid = "date-range-invalid";
    public const string DateRangeTooLarge = "date-range-too-large";
    public const string TimeZoneInvalid = "time-zone-invalid";

    // Definitional validation
    public const string ClaimsInvalid = "claims-invalid";

    /// <summary>
    /// The reference offered for a new booking is already in use.
    /// <para>
    /// <b>Internal to placement — this never reaches a consumer.</b>
    /// <c>BookingService</c> answers it by generating another reference and trying again, so
    /// it is a message between the service and its store rather than a report to a caller.
    /// It is deliberately absent from the delivery API's failure mapping: a caller can do
    /// nothing with it, and nothing they did caused it.
    /// </para>
    /// </summary>
    public const string ReferenceTaken = "reference-taken";
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

    /// <summary>
    /// A service names more than one visitor-selectable role. Its own code,
    /// distinct from every other role failure, because the fault is a property of
    /// the <em>service</em> rather than of any one role: either role would be
    /// legal alone (visitor-selectable-role design D1).
    /// <para>
    /// A pinned resource names the booking rather than a role, so a booking
    /// request carries at most one — two selectable roles could not both be
    /// honoured by it. The failure is reported against every role in conflict, so
    /// a consumer can mark all the rows involved rather than one chosen
    /// arbitrarily, and it is never resolved by clearing the flag on all but one:
    /// which one the editor meant is not derivable.
    /// </para>
    /// </summary>
    public const string ServiceRoleMultipleSelectable = "service-role-multiple-selectable";
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
    /// A placement on a booker's behalf named both a service and a resource, or neither.
    /// </summary>
    /// <remarks>
    /// <b>Its own code rather than <see cref="IntervalInvalid"/>, which it briefly shared.</b>
    /// A caller branching on a stable code could not tell "you named two things to book" from
    /// "your date and time were malformed" — two mistakes with nothing in common and different
    /// corrections. The endpoint is required to distinguish causes an operator can act on, and a
    /// code that describes an interval cannot describe this one.
    /// </remarks>
    public const string BookingSubjectInvalid = "booking-subject-invalid";

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

    // Site closures

    /// <summary>
    /// A site closure's label is absent, blank once trimmed, or longer than the
    /// package's name-column length.
    /// <para>
    /// <b>One code for shape and length</b>, on the same grounds as
    /// <see cref="TypeKeyInvalid"/>: the length is validated in the domain rather
    /// than left to the column, so that an over-long label is a stable validation
    /// failure instead of a 500 at INSERT.
    /// </para>
    /// <para>
    /// The label is required rather than optional because an inherited closure is
    /// offered for opt-out where a resource is edited, and a bare date asks an
    /// operator to exempt something they cannot identify.
    /// </para>
    /// </summary>
    public const string ClosureLabelInvalid = "closure-label-invalid";

    /// <summary>
    /// A second site closure was offered for a date that already carries one.
    /// Distinct from <see cref="DuplicateExceptionDate"/>, which is about a single
    /// resource's own exceptions: a date may legitimately carry both a closure and
    /// a resource's exception, and only the closure layer is site-wide.
    /// </summary>
    public const string DuplicateClosureDate = "duplicate-closure-date";

    /// <summary>
    /// A resource's closure opt-out named an id no closure carries. Refused rather
    /// than ignored: an opt-out silently dropped would leave a resource closed on a
    /// date its editor believed they had exempted it from.
    /// </summary>
    public const string ClosureNotFound = "closure-not-found";
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
