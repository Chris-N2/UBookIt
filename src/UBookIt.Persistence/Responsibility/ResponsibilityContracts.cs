using UBookIt.Core.Bookings;
using UBookIt.Core.Common;

namespace UBookIt.Persistence.Responsibility;

/// <summary>What a responsibility assignment is on: a resource or a service.</summary>
public enum ResponsibilitySubject
{
    Resource,

    Service,
}

/// <summary>What a responsibility assignment points at: an Umbraco user or user group.</summary>
public enum ResponsibilityPartyKind
{
    User,

    Group,
}

/// <summary>
/// One responsible party assigned to a resource or service: a kind and an Umbraco key,
/// and deliberately nothing else. No name and no email address is ever stored beside the
/// key — those are Umbraco's facts, resolved from Umbraco at the moment they are needed,
/// so the reference is the only thing here that can go stale.
/// </summary>
public sealed record ResponsibilityAssignment(ResponsibilityPartyKind Kind, Guid Key);

/// <summary>
/// A stored assignment annotated with what its party currently resolves to, for the
/// editing surface: a stale or disabled party must be shown with its condition marked,
/// never silently dropped from the display.
/// </summary>
/// <param name="Assignment">The assignment as stored.</param>
/// <param name="Exists">Whether the referenced user or group still exists.</param>
/// <param name="DisplayName">The party's current display name, where it exists.</param>
/// <param name="UserState">
/// For a user party that exists, the name of its Umbraco state (<c>Active</c>,
/// <c>Inactive</c>, <c>LockedOut</c>, <c>Disabled</c>, <c>Invited</c>), so the editor can
/// mark states that sending skips. Null for groups and for parties that no longer exist.
/// </param>
public sealed record ResponsibilityPartyStatus(
    ResponsibilityAssignment Assignment,
    bool Exists,
    string? DisplayName,
    string? UserState);

/// <summary>
/// Storage for responsibility assignments. The management API's only route to them, on the
/// same terms as the other management ports: typed operations, never the context.
/// </summary>
public interface IResponsibilityStore
{
    /// <summary>Reads a subject's assignments. Empty for a subject with none — including one that does not exist.</summary>
    Task<IReadOnlyList<ResponsibilityAssignment>> GetAsync(
        ResponsibilitySubject subject, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a subject's assignments wholesale — what is saved is what was seen, never a
    /// merge. Fails if the subject does not exist; does NOT fail for a party key that no
    /// longer resolves, because a save must not fail for racing the deletion of a user.
    /// Duplicate assignments in the set are stored once.
    /// </summary>
    Task<DomainResult> ReplaceAsync(
        ResponsibilitySubject subject,
        Guid subjectId,
        IReadOnlyCollection<ResponsibilityAssignment> assignments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any assignment exists for the booking's subjects — its service attribution
    /// and every resource it claims. The cheap existence question the sending gate asks
    /// before anything is resolved.
    /// </summary>
    Task<bool> HasAnyForBookingAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// The assignments on the booking's subjects — its service attribution and every
    /// resource it claims — as one flat, distinct set. Which parties those assignments
    /// still resolve to is the resolver's question, not storage's.
    /// </summary>
    Task<IReadOnlyList<ResponsibilityAssignment>> GetForBookingAsync(
        Booking booking, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves responsibility assignments to the people behind them: the email addresses a
/// booking's internal message is due, and the current condition of a subject's assigned
/// parties for the editing surface.
/// </summary>
public interface IResponsibleRecipientResolver
{
    /// <summary>
    /// Whether any assignment exists for the booking's subjects at all — the cheap
    /// existence question the sending gate asks before the host is consulted and before
    /// anything is resolved. True for an assignment that would resolve to nobody: this
    /// answers "has the site asked", not "is there someone to tell".
    /// </summary>
    Task<bool> HasAssignmentsAsync(Booking booking, CancellationToken cancellationToken = default);

    /// <summary>
    /// The email addresses of the booking's responsible parties: directly assigned users
    /// and the current members of assigned groups, deduplicated case-insensitively.
    /// Skips, silently: users whose state is Disabled or Invited, users with no usable
    /// address, and assignments whose user or group no longer exists — staleness is an
    /// editing concern, not a sending concern.
    /// </summary>
    Task<IReadOnlyList<string>> ResolveAddressesAsync(
        Booking booking, CancellationToken cancellationToken = default);

    /// <summary>Annotates assignments with what their parties currently resolve to.</summary>
    Task<IReadOnlyList<ResponsibilityPartyStatus>> DescribeAsync(
        IReadOnlyCollection<ResponsibilityAssignment> assignments,
        CancellationToken cancellationToken = default);
}
