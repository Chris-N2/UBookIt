using UBookIt.Core.Bookings;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Persistence.Responsibility;

/// <summary>
/// Resolves responsibility assignments against Umbraco's user store, at the moment the
/// answer is needed — a group's members are whoever is in the group when the message is
/// sent, so membership changes take effect with no uBookIt action.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not <c>IUserService.FilterAsync</c>.</b> That API requires a
/// requesting user (there is none in a background send), applies that user's visibility
/// rules to the answer, and fails the whole call when any group key does not resolve —
/// each of which is wrong here. A dangling group must resolve to nothing, silently,
/// while every other assignment still resolves; so groups are walked one by one via
/// <see cref="IUserGroupService.GetAsync(Guid)"/> and
/// <see cref="IUserService.GetAllInGroup(int?)"/>.
/// </para>
/// <para>
/// <b>The state rule</b> (spec: "Resolution skips who cannot or will not act, silently"):
/// Active, Inactive and LockedOut receive — an account never yet logged into belongs to a
/// real colleague, and a lockout is transient and must not cost the site a notification.
/// Disabled and Invited do not — one had its access ended on purpose, the other has no
/// accepted account and possibly no verified address.
/// </para>
/// </remarks>
internal sealed class ResponsibleRecipientResolver(
    IResponsibilityStore store,
    IUserService userService,
    IUserGroupService userGroupService)
    : IResponsibleRecipientResolver
{
    public Task<bool> HasAssignmentsAsync(Booking booking, CancellationToken cancellationToken = default)
        => store.HasAnyForBookingAsync(booking, cancellationToken);

    public async Task<IReadOnlyList<string>> ResolveAddressesAsync(
        Booking booking, CancellationToken cancellationToken = default)
    {
        var assignments = await store.GetForBookingAsync(booking, cancellationToken).ConfigureAwait(false);

        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in assignments)
        {
            switch (assignment.Kind)
            {
                case ResponsibilityPartyKind.User:
                    // A miss is a dangling assignment: expected, silent, and nobody
                    // else's message is affected.
                    var user = await userService.GetAsync(assignment.Key).ConfigureAwait(false);
                    AddIfReceives(addresses, user);
                    break;

                case ResponsibilityPartyKind.Group:
                    foreach (var member in await MembersAsync(assignment.Key).ConfigureAwait(false))
                    {
                        AddIfReceives(addresses, member);
                    }

                    break;
            }
        }

        return [.. addresses];
    }

    public async Task<IReadOnlyList<ResponsibilityPartyStatus>> DescribeAsync(
        IReadOnlyCollection<ResponsibilityAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        var statuses = new List<ResponsibilityPartyStatus>(assignments.Count);

        foreach (var assignment in assignments)
        {
            statuses.Add(assignment.Kind switch
            {
                ResponsibilityPartyKind.User =>
                    await userService.GetAsync(assignment.Key).ConfigureAwait(false) is { } user
                        ? new ResponsibilityPartyStatus(
                            assignment, Exists: true, user.Name, user.UserState.ToString())
                        : Missing(assignment),
                _ =>
                    await userGroupService.GetAsync(assignment.Key).ConfigureAwait(false) is { } group
                        ? new ResponsibilityPartyStatus(
                            assignment, Exists: true, group.Name, UserState: null)
                        : Missing(assignment),
            });
        }

        return statuses;

        static ResponsibilityPartyStatus Missing(ResponsibilityAssignment assignment)
            => new(assignment, Exists: false, DisplayName: null, UserState: null);
    }

    private async Task<IEnumerable<IUser>> MembersAsync(Guid groupKey)
    {
        // Key to int id first: the group-membership read is only published against the
        // integer id. A missing group resolves to no members, on the same silent terms
        // as a missing user.
        var group = await userGroupService.GetAsync(groupKey).ConfigureAwait(false);

        return group is null ? [] : userService.GetAllInGroup(group.Id);
    }

    private static void AddIfReceives(HashSet<string> addresses, IUser? user)
    {
        if (user is null || !Receives(user.UserState))
        {
            return;
        }

        // An address is usable when it is non-blank; nothing more is judged here. A
        // malformed address fails at the mail server, whose rejection the send path
        // already treats as a loggable fault rather than a booking problem.
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            addresses.Add(user.Email);
        }
    }

    /// <summary>The state rule, total over <see cref="UserState"/> — see the class remarks.</summary>
    private static bool Receives(UserState state) => state switch
    {
        UserState.Active => true,
        UserState.Inactive => true,
        UserState.LockedOut => true,
        UserState.Disabled => false,
        UserState.Invited => false,

        // UserState.All is a filtering sentinel no stored user reports; anything else is
        // a state Umbraco added after this was written. Not receiving is the safe wrong
        // answer: a missed internal message is recoverable from the bookings screen, an
        // unwanted one sent to an unknown state is not.
        _ => false,
    };
}
