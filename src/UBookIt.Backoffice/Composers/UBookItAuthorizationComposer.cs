using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using UBookIt.Backoffice.Security;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UBookIt.Backoffice.Composers;

/// <summary>
/// Registers the authorization policy every uBookIt management endpoint requires.
/// </summary>
/// <remarks>
/// <para>
/// <b>The OpenIddict validation scheme is not optional.</b> A backoffice API policy that
/// omits it rejects an authenticated user with nothing useful to say about why — the
/// request never resolves to a backoffice principal in the first place, so the handler sees
/// no user and refuses. This is the shape Umbraco's own documentation gives for a custom
/// backoffice policy, and the reason it is worth stating is that leaving it out fails in a
/// way that looks like the handler being wrong.
/// </para>
/// </remarks>
public sealed class UBookItAuthorizationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItSectionHandler>();

        builder.Services.AddAuthorization(options =>
            options.AddPolicy(Constants.SectionAccessPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.Requirements.Add(new UBookItSectionRequirement());
            }));
    }
}
