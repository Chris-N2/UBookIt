using Microsoft.AspNetCore.Authorization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Backoffice.Security;

/// <summary>
/// Requires that the backoffice user may see sensitive data, per Umbraco's built-in
/// <b>Sensitive data</b> user group.
/// </summary>
public sealed class UBookItSensitiveDataRequirement : IAuthorizationRequirement;

/// <summary>
/// Grants access to a backoffice user Umbraco permits to see sensitive data, and refuses
/// everyone else.
/// </summary>
/// <remarks>
/// <para>
/// <b>A policy rather than a check inside a handler, deliberately.</b> The alternative — an
/// endpoint that runs and then decides — makes the gate a line of code somebody must
/// remember to write, and leaves a route that reaches the operation's body without having
/// established anything. As a policy, the requirement is a property of the endpoint: a
/// caller who does not satisfy it never arrives.
/// </para>
/// <para>
/// <b>It composes with the section policy, it does not replace it.</b> Both apply to an
/// endpoint carrying this attribute, because they answer different questions: the section
/// decides whether a user may reach uBookIt at all, and this decides whether they may act on
/// the people inside it. A user with neither reaches nothing; a user with the section alone
/// reaches the bookings list without contact details, and cannot erase them.
/// </para>
/// <para>
/// <b>It reuses Umbraco's own membership test</b> rather than inventing a group, flag or
/// setting of its own — the same mechanism that decides whether contact details appear in a
/// response, so that one idea governs reading them and removing them. Two mechanisms would
/// be free to disagree, and the disagreement worth worrying about is the one where somebody
/// who may not read a name may still destroy it.
/// </para>
/// <para>
/// It fails explicitly rather than merely declining to succeed, matching
/// <see cref="UBookItSectionHandler"/>: a handler that stays silent leaves the requirement
/// unmet but lets another handler satisfy it, which is not the same guarantee.
/// </para>
/// </remarks>
public sealed class UBookItSensitiveDataHandler(IAuthorizationHelper authorizationHelper)
    : AuthorizationHandler<UBookItSensitiveDataRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UBookItSensitiveDataRequirement requirement)
    {
        if (authorizationHelper.TryGetUmbracoUser(context.User, out IUser? user)
            && user.HasAccessToSensitiveData())
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
