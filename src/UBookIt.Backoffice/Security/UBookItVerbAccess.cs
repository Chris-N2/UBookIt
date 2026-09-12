using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Backoffice.Security;

/// <summary>
/// Requires that the backoffice user's groups hold at least one of the named permission
/// verbs. Always applied alongside <see cref="UBookItSectionRequirement"/> — the section
/// is the outer gate; verbs refine within it and never substitute for it.
/// </summary>
/// <param name="anyOf">
/// The verbs that satisfy this requirement. More than one expresses an implication —
/// the read requirement is satisfied by <see cref="Constants.Verbs.BookingsRead"/> OR
/// <see cref="Constants.Verbs.BookingsManage"/>, which is where "Manage implies Read"
/// lives: one rule, never a copy of verbs onto groups.
/// </param>
public sealed class UBookItVerbRequirement(params string[] anyOf) : IAuthorizationRequirement
{
    public IReadOnlyList<string> AnyOf { get; } = anyOf;
}

/// <summary>
/// Grants a verb requirement when the union of the user's groups' permissions contains
/// any of its verbs.
/// </summary>
/// <remarks>
/// <para>
/// The verbs live on <c>IReadOnlyUserGroup.Permissions</c> — Umbraco's own group
/// permission storage, written by the group editor's default-permissions toggles that the
/// package's <c>entityUserPermission</c> manifest declares. The package stores nothing of
/// its own; the union across groups is the user's capability set, matching how Umbraco
/// itself reads fallback permissions.
/// </para>
/// <para>
/// Ordinal comparison, deliberately: the verbs are this package's own constants, written
/// by this package's own manifest, so a casing mismatch is a bug to surface rather than
/// tolerate — the one-vocabulary guard keeps the two sides identical.
/// </para>
/// <para>
/// Fails explicitly rather than merely declining to succeed, for the same reason the
/// section handler records: a silent handler leaves the requirement satisfiable by
/// somebody else's.
/// </para>
/// </remarks>
public sealed class UBookItVerbHandler(IAuthorizationHelper authorizationHelper)
    : AuthorizationHandler<UBookItVerbRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UBookItVerbRequirement requirement)
    {
        if (authorizationHelper.TryGetUmbracoUser(context.User, out IUser? user)
            && user.Groups.SelectMany(group => group.Permissions ?? Enumerable.Empty<string>())
                .Intersect(requirement.AnyOf, StringComparer.Ordinal)
                .Any())
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
