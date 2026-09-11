using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Persistence.Responsibility;

/// <summary>
/// The resolver's view of Umbraco's user store: three lookups and nothing else. Exists so
/// the resolver's actual decisions — the state rule, deduplication, dangling parties —
/// are testable against real <see cref="IUser"/> values without faking the whole of
/// <see cref="IUserService"/>, which has dozens of members and no test double in this
/// codebase.
/// </summary>
/// <remarks>
/// The implementation below must stay logic-free — pure delegation — because a seam
/// between two tested halves is tested by neither (the available-dates lesson): the only
/// code here that no unit test sees is code that does nothing but forward.
/// </remarks>
internal interface IUmbracoUserDirectory
{
    /// <summary>The user with this key, or null — a dangling assignment, not an error.</summary>
    Task<IUser?> GetUserAsync(Guid key);

    /// <summary>The group with this key, or null — a dangling assignment, not an error.</summary>
    Task<DirectoryGroup?> GetGroupAsync(Guid key);

    /// <summary>The group's current members, at the moment of asking.</summary>
    IEnumerable<IUser> GetMembers(DirectoryGroup group);
}

/// <summary>
/// The two facts about a group the package uses: the integer id the membership read is
/// published against, and the display name the editors show. Not <see cref="IUserGroup"/>,
/// which would make every test fake a two-dozen-member interface to state a name.
/// </summary>
internal sealed record DirectoryGroup(int Id, string? Name);

/// <inheritdoc />
internal sealed class UmbracoUserDirectory(
    IUserService userService,
    IUserGroupService userGroupService) : IUmbracoUserDirectory
{
    public Task<IUser?> GetUserAsync(Guid key) => userService.GetAsync(key);

    public async Task<DirectoryGroup?> GetGroupAsync(Guid key)
        => await userGroupService.GetAsync(key).ConfigureAwait(false) is { } group
            ? new DirectoryGroup(group.Id, group.Name)
            : null;

    // The membership read is only published against the integer id, hence the group
    // object rather than its key: the caller has already resolved it, and this cannot be
    // called with a group that does not exist.
    public IEnumerable<IUser> GetMembers(DirectoryGroup group) => userService.GetAllInGroup(group.Id);
}
