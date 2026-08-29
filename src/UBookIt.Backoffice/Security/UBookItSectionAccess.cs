using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Backoffice.Security;

/// <summary>
/// Requires that the backoffice user has been granted uBookIt's own section.
/// </summary>
public sealed class UBookItSectionRequirement : IAuthorizationRequirement;

/// <summary>
/// Grants access to a backoffice user holding uBookIt's section, and refuses everyone else.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the package writes this rather than using Umbraco's.</b> Umbraco has exactly this
/// handler — <c>AllowedApplicationHandler</c>, backing every <c>SectionAccess*</c> policy —
/// but it is <c>internal</c> to <c>Umbraco.Cms.Api.Management</c> and its requirement takes
/// the built-in application constants. Everything it depends on is public, so the check is
/// reproduced here against the package's own section rather than approximated with a
/// different rule.
/// </para>
/// <para>
/// <b>What replaced, and why it mattered.</b> The shared controller base previously
/// authorized on <c>SectionAccessContent</c> — Umbraco's Content section — while this
/// package ships its own. That was wrong in both directions: a user granted uBookIt but not
/// Content was refused an API for a section they could see, and a user granted Content but
/// not uBookIt could call every uBookIt endpoint for a section they could not. Tolerable
/// while the payload was resource configuration; not once it is booker names and email
/// addresses.
/// </para>
/// <para>
/// <b>The alias is measured, not assumed.</b> Read from a running site's
/// <c>umbracoUserGroup2App</c>: a custom section stores its <i>manifest alias</i> verbatim
/// (<c>UBookIt.Section</c>), where the built-ins store short lowercase names
/// (<c>content</c>, <c>media</c>). The two are not the same shape, which is exactly why
/// this was checked rather than inferred.
/// </para>
/// <para>
/// It fails explicitly rather than merely declining to succeed. A handler that stays silent
/// leaves the requirement unmet but lets another handler satisfy it, which is not the same
/// guarantee.
/// </para>
/// </remarks>
public sealed class UBookItSectionHandler(IAuthorizationHelper authorizationHelper)
    : AuthorizationHandler<UBookItSectionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UBookItSectionRequirement requirement)
    {
        if (authorizationHelper.TryGetUmbracoUser(context.User, out IUser? user)
            && user.AllowedSections.Contains(Constants.SectionAlias, StringComparer.OrdinalIgnoreCase))
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
