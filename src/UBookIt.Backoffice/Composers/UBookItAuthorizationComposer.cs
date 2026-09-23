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
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItSensitiveDataHandler>();
        builder.Services.AddSingleton<IAuthorizationHandler, UBookItVerbHandler>();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(Constants.SectionAccessPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.Requirements.Add(new UBookItSectionRequirement());
            });

            // The second gate, for endpoints that act on a booker's personal data. It carries
            // the section requirement as well as the sensitive-data one, so that applying it
            // to an action can only ever ADD a condition — an endpoint that named this policy
            // alone would otherwise be reachable by somebody without the section at all,
            // which is a widening dressed as a tightening.
            options.AddPolicy(Constants.SensitiveDataAccessPolicy, policy =>
            {
                policy.AuthenticationSchemes.Add(
                    OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
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
            policy.AuthenticationSchemes.Add(
                OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.Requirements.Add(new UBookItSectionRequirement());
            policy.Requirements.Add(new UBookItVerbRequirement(anyOfVerbs));
        });
}
