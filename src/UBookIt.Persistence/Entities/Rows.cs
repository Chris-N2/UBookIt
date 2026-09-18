using UBookIt.Core.Services;

namespace UBookIt.Persistence.Entities;

/// <summary>Table: uBookItResource. Constraint values stored as integer minutes/days.</summary>
internal sealed class ResourceRow
{
    public Guid Id { get; set; }

    public required string Type { get; set; }

    public required string DisplayName { get; set; }

    public string? Description { get; set; }

    public int GranularityMinutes { get; set; }

    public int MinDurationMinutes { get; set; }

    public int MaxDurationMinutes { get; set; }

    public int LeadTimeMinutes { get; set; }

    public int HorizonDays { get; set; }

    /// <summary>
    /// Whether the resource may be booked on its own. Non-nullable with a default
    /// of false, so every existing row takes the withheld answer — which is the
    /// behaviour change this change exists to make, applied by the migration
    /// rather than by a backfill.
    /// </summary>
    public bool DirectlyBookable { get; set; }

    public List<OpenHoursRow> OpenHours { get; set; } = [];

    public List<ExceptionRow> Exceptions { get; set; } = [];

    public List<ResourceCapabilityRow> Capabilities { get; set; } = [];
}

/// <summary>
/// Table: uBookItResourceCapability. One row per capability a resource carries.
/// Keyed by (resource, key) so a duplicate is impossible in storage and not only
/// in <c>CapabilitySet</c>.
/// </summary>
internal sealed class ResourceCapabilityRow
{
    public Guid ResourceId { get; set; }

    public required string Key { get; set; }
}

/// <summary>Table: uBookItResourceOpenHours. One row per weekly window.</summary>
internal sealed class OpenHoursRow
{
    public long Id { get; set; }

    public Guid ResourceId { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }
}

/// <summary>
/// Table: uBookItResourceException. One row per exception window; a closure is
/// a single row for the date with NULL start and end times.
/// </summary>
internal sealed class ExceptionRow
{
    public long Id { get; set; }

    public Guid ResourceId { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }
}

/// <summary>Table: uBookItBooking.</summary>
internal sealed class BookingRow
{
    public Guid Id { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public required string TimeZoneId { get; set; }

    public int Status { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// The booking's quotable reference, in canonical form — upper case, no separator.
    /// </summary>
    /// <remarks>
    /// <b>Stored canonical, and uniquely indexed.</b> Uniqueness lives here rather than in a
    /// check before writing, because a check followed by a write is a race and the database is
    /// the only thing that can actually enforce it. Canonical form is what makes the index
    /// meaningful: a reference stored as typed rather than as normalised would let two rows
    /// differ only by case and be, to every person who reads them, the same reference.
    /// </remarks>
    public required string Reference { get; set; }

    /// <summary>
    /// The booker's Umbraco member key, where they had one. NULL once erased, on the same
    /// terms as the contact columns — a member key identifies a person as reliably as an
    /// email address, so an erasure that left it behind would not be one.
    /// </summary>
    public Guid? MemberKey { get; set; }

    /// <summary>
    /// The booker's name, or NULL once erased.
    /// </summary>
    /// <remarks>
    /// <b>Nullable means erased — it never means "a placement that omitted it".</b> The
    /// domain requires a non-empty name and a well-formed email of every booker it places,
    /// so no row is ever written with these NULL at placement. They become NULL only when
    /// somebody erases the booking's personal data, and <see cref="BookerErasedUtc"/> then
    /// records when, so that the state is stored as a fact rather than inferred from
    /// missing values.
    /// </remarks>
    public string? BookerName { get; set; }

    /// <inheritdoc cref="BookerName"/>
    public string? BookerEmail { get; set; }

    /// <inheritdoc cref="BookerName"/>
    public string? BookerPhone { get; set; }

    /// <summary>
    /// When this booking's booker contact details were erased, or NULL if they never were.
    /// </summary>
    /// <remarks>
    /// Non-NULL exactly when <see cref="BookerName"/> and <see cref="BookerEmail"/> are
    /// NULL. Stored rather than derived from their absence so that "erased" is something the
    /// row states, and so that a reader is never asked to interpret a missing value.
    /// </remarks>
    public DateTimeOffset? BookerErasedUtc { get; set; }

    /// <summary>
    /// The service this booking was placed for, or NULL for one placed directly.
    /// </summary>
    /// <remarks>
    /// <b>No foreign key, deliberately.</b> A booking is a historical fact and must survive
    /// its service being renamed, retired or deleted. A nullable FK with
    /// <c>ON DELETE SET NULL</c> would convert "placed for a service that no longer exists"
    /// into "placed directly", and that is the one meaning NULL has to keep.
    /// </remarks>
    public Guid? ServiceId { get; set; }

    /// <summary>
    /// The service's name as it stood at placement — a snapshot, not a reference.
    /// </summary>
    /// <remarks>
    /// Stored rather than joined so a row stays answerable after the service is renamed or
    /// deleted. A join would report the name the service has now, or none at all.
    /// NULL exactly when <see cref="ServiceId"/> is NULL.
    /// </remarks>
    public string? ServiceName { get; set; }

    public List<ClaimRow> Claims { get; set; } = [];
}

/// <summary>Table: uBookItResourceClaim. Unique per (BookingId, ResourceId).</summary>
internal sealed class ClaimRow
{
    public long Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid ResourceId { get; set; }
}

/// <summary>
/// Table: uBookItService. The duration kind is stored as its name; bounds are
/// integer minutes. A fixed duration stores the same value in both bounds; a
/// variable one may leave either NULL, meaning the resource's own bound applies.
/// </summary>
internal sealed class ServiceRow
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ServiceDurationKind DurationKind { get; set; }

    public int? MinDurationMinutes { get; set; }

    public int? MaxDurationMinutes { get; set; }

    public List<ServiceRoleRow> Roles { get; set; } = [];
}

/// <summary>Table: uBookItServiceRole. One row per required role (v1: exactly one per service).</summary>
internal sealed class ServiceRoleRow
{
    public long Id { get; set; }

    public Guid ServiceId { get; set; }

    public required string ResourceType { get; set; }

    public int Count { get; set; }

    /// <summary>
    /// Whether a visitor may choose which resource fills this role. A
    /// non-nullable <c>bit</c> defaulting to <c>false</c>, so every role stored
    /// before the column existed loads as not selectable and no service changes
    /// behaviour — the shape <c>AddDirectBookability</c> established.
    /// </summary>
    public bool VisitorSelectable { get; set; }

    public List<ServiceRoleCapabilityRow> Capabilities { get; set; } = [];
}

/// <summary>
/// Table: uBookItServiceRoleCapability. One row per capability a service role
/// requires. Keyed by (role, key), as for resource capabilities.
/// </summary>
internal sealed class ServiceRoleCapabilityRow
{
    public long ServiceRoleId { get; set; }

    public required string Key { get; set; }
}

/// <summary>
/// Table: uBookItResponsibility. One row per responsible party assigned to a
/// resource or service. All four columns are the key: an assignment either
/// exists or does not, carries no payload, and writing it twice is
/// indistinguishable from writing it once.
/// <para>
/// Keys only, on purpose. The party columns reference an Umbraco user or user
/// group by its key; no name and above all no email address is copied here, so
/// nothing in this table can go stale except the reference itself — which
/// resolution skips silently and the editors surface visibly.
/// </para>
/// </summary>
internal sealed class ResponsibilityRow
{
    /// <summary><see cref="ResponsibilitySubjectTypes.Resource"/> or <see cref="ResponsibilitySubjectTypes.Service"/>.</summary>
    public required string SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary><see cref="ResponsibilityPartyTypes.User"/> or <see cref="ResponsibilityPartyTypes.Group"/>.</summary>
    public required string PartyType { get; set; }

    public Guid PartyKey { get; set; }
}

/// <summary>The stored discriminator values for what a responsibility assignment is on.</summary>
internal static class ResponsibilitySubjectTypes
{
    public const string Resource = "resource";

    public const string Service = "service";
}

/// <summary>The stored discriminator values for what a responsibility assignment points at.</summary>
internal static class ResponsibilityPartyTypes
{
    public const string User = "user";

    public const string Group = "group";
}

/// <summary>
/// Table: uBookItFlag. One row per one-shot operation the package has completed —
/// the permissions seed is the first. The key is the primary key: a flag exists or
/// it does not, and carries nothing but when it was applied. No personal data, ever.
/// </summary>
internal sealed class FlagRow
{
    public required string Key { get; set; }

    public DateTimeOffset AppliedUtc { get; set; }
}

/// <summary>
/// Table: uBookItSetting. One row per setting a site has overridden through the backoffice,
/// keyed by the configuration key it overrides. A key with no row is not overridden — there is
/// no sentinel, because several settings treat "no value" as meaningful. The value is held as
/// text, exactly as a configuration source would supply it. No personal data, ever.
/// </summary>
internal sealed class SettingRow
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// One outstanding cancellation secret, held as a hash.
/// </summary>
/// <remarks>
/// <b>Nothing on this row is a credential.</b> The secret itself is never written here — only
/// <see cref="Hash"/>, which a redemption is looked up by — so a database copy, a backup, or a
/// person with read access holds nothing that can cancel a booking. That is the same reasoning
/// that keeps booker contact details out of messages to a site's own recipients: a control built
/// deliberately must not be reachable by a route around it.
/// <para>
/// <b>No personal data, ever.</b> A booking's identifier, a hash, two instants and a flag name
/// nobody, which is why erasure does not reach this table and why <c>booker-erasure</c> records
/// that consequence where an operator will meet it.
/// </para>
/// </remarks>
internal sealed class CancellationSecretRow
{
    /// <summary>The hash IS the primary key: at most one row per secret, enforced by the schema.</summary>
    public required string Hash { get; set; }

    public Guid BookingId { get; set; }

    /// <summary>
    /// When the secret stops being usable — the booking's start, computed at issue and stored.
    /// </summary>
    /// <remarks>
    /// Stored rather than derived at redemption so that moving the booking, or changing a site's
    /// configuration, cannot silently extend or revoke a link already sitting in somebody's inbox.
    /// </remarks>
    public DateTimeOffset ExpiresUtc { get; set; }

    public DateTimeOffset IssuedUtc { get; set; }

    public DateTimeOffset? RedeemedUtc { get; set; }
}
