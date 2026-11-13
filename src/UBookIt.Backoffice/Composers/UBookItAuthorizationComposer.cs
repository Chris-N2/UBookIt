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
/// <b>Every policy names BOTH authentication schemes, and neither is optional.</b> A policy
/// authenticates a request only through the schemes it names. A request that matches none of
/// them never resolves to a backoffice principal, so the handlers see no user and refuse, in a
/// way that looks like the handler being wrong.
/// </para>
/// <list type="bullet">
/// <item><b>The backoffice cookie</b> (<c>Constants.Security.BackOfficeAuthenticationType</c>)
/// is how an editor arrives. From Umbraco 19 the backoffice sends the cookie and no bearer
/// token: the bridge that turned the cookie into a token Umbraco 17/18 validated through
/// OpenIddict is gone. A policy without this scheme answers every editor with a 401, which the
/// backoffice reads as an expired session and answers with its sign-in prompt.</item>
/// <item><b>OpenIddict validation</b> is how an API user arrives, with a client-credentials
/// bearer token.</item>
/// </list>
/// <para>
/// The pair is exactly what Umbraco 19's own backoffice policies name
/// (<c>BackOfficeAuthPolicyBuilderExtensions.AddAuthenticationSchemes</c>). Accepting the cookie
/// takes Umbraco's stance on cross-site request forgery, no weaker and no stronger: the
/// Management API has no anti-forgery tokens, and the cookie's <c>SameSite</c> setting
/// (<c>Strict</c> by default, path <c>/</c>) is what keeps a cross-site request from carrying it,
/// for these routes as for Umbraco's.
/// </para>
/// </remarks>
public sealed class UBookItAuthorizationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItSectionHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItSensitiveDataHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItVerbHandler>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Constants.SectionAccessPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                    Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType);
                policy.Requirements.Add(new UBookItSectionRequirement());
            });

            // The second gate, for endpoints that act on a booker's personal data. It carries
            // the section requirement as well as the sensitive-data one, so that applying it
            // to an action can only ever ADD a condition — an endpoint that named this policy
            // alone would otherwise be reachable by somebody without the section at all,
            // which is a widening dressed as a tightening.
            options.AddPolicy(Constants.SensitiveDataAccessPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                    Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType);
                policy.Requirements.Add(new UBookItSectionRequirement());
                policy.Requirements.Add(new UBookItSensitiveDataRequirement());
            });

            // The three verb policies (permissions capability). Each carries the section
            // requirement as well as its verb, for the reason the sensitive-data policy
            // records: naming one on an action can only ever ADD a condition. "Manage
            // implies Read" lives HERE and nowhere else — the read policy is satisfied by
            // either verb, so no group's stored verbs ever restate the implication.
            AddVerbPolicy(options, Constants.VerbPolicies.BookingsRead,
                Constants.Verbs.BookingsRead, Constants.Verbs.BookingsManage);
            AddVerbPolicy(options, Constants.VerbPolicies.BookingsManage,
                Constants.Verbs.BookingsManage);
            AddVerbPolicy(options, Constants.VerbPolicies.Configure,
                Constants.Verbs.Configure);

            // ONE verb, no implication. Unlike the bookings pair, nothing else satisfies this:
            // Configure does not reach the settings and Settings does not reach resources,
            // services or bookings. See Constants.Verbs.Settings for why.
            AddVerbPolicy(options, Constants.VerbPolicies.Settings,
                Constants.Verbs.Settings);

            // Reading the closure list, satisfied by EITHER verb — and, like the bookings
            // pair, that any-of lives here rather than as verbs copied onto groups. It is
            // not an implication: neither verb acquires anything else the other holds, and
            // WRITING closures names the settings policy above, not this one.
            AddVerbPolicy(options, Constants.VerbPolicies.ClosuresRead,
                Constants.Verbs.Configure, Constants.Verbs.Settings);
        });
    }

    private static void AddVerbPolicy(
        AuthorizationOptions options, string policyName, params string[] anyOfVerbs)
        => options.AddPolicy(policyName, policy =>
        {
            policy.AddAuthenticationSchemes(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
                Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType);
            policy.Requirements.Add(new UBookItSectionRequirement());
            policy.Requirements.Add(new UBookItVerbRequirement(anyOfVerbs));
        });
}
