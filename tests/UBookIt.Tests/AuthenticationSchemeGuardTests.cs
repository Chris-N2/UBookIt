using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using UBookIt.Backoffice;
using UBookIt.Backoffice.Composers;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every uBookIt management policy names BOTH authentication schemes: the backoffice cookie,
/// which is how an editor arrives from Umbraco 19, and OpenIddict validation, which is how an
/// API user arrives.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> On Umbraco 19 the backoffice sends the cookie and no bearer token. A
/// policy without the cookie scheme answers every editor with a 401, which the backoffice reads
/// as an expired session and answers with its sign-in prompt. That is the whole reason the
/// <c>run-on-umbraco-19</c> port was needed. QA round 1 removed the cookie scheme from every
/// policy, and the unit suite still passed 2520/2520: the older guards assert only the OpenIddict
/// scheme, and only the live check could see it. A one-off confirmation is not a regression
/// guard. This file is the regression guard.
/// </para>
/// <para>
/// <b>The policies are enumerated, not listed.</b> The set is every policy constant in
/// <see cref="Constants"/> and every policy named by an <see cref="AuthorizeAttribute"/> in the
/// backoffice assembly, so a new policy, or an endpoint naming one, cannot escape the guard. The
/// scheme set is asserted EXACTLY. Dropping either scheme fails, and so does adding a third,
/// which would widen who reaches these endpoints.
/// </para>
/// </remarks>
public class AuthenticationSchemeGuardTests
{
    private static readonly string[] BothSchemes =
    [
        OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
        Umbraco.Cms.Core.Constants.Security.BackOfficeAuthenticationType,
    ];

    public static TheoryData<string> EveryPolicy()
    {
        var data = new TheoryData<string>();
        foreach (var name in PolicyConstants().Union(PoliciesNamedByEndpoints()).Order(StringComparer.Ordinal))
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPolicy))]
    public async Task Each_policy_names_exactly_the_cookie_and_OpenIddict_schemes(string policyName)
    {
        var policy = await ComposedPolicyAsync(policyName);

        Assert.NotNull(policy);
        Assert.Equal(
            BothSchemes.Order(StringComparer.Ordinal),
            policy.AuthenticationSchemes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_enumeration_finds_every_policy_the_section_has()
    {
        // The theory above is only as wide as its data. If reflection found nothing (a renamed
        // nested class, or a controller assembly not loaded), the theory would have nothing to
        // run, and would pass. So pin the floor: today there are seven policies (section,
        // sensitive data, and five verbs), and every one is also named by an endpoint.
        var constants = PolicyConstants().ToHashSet(StringComparer.Ordinal);
        var named = PoliciesNamedByEndpoints().ToHashSet(StringComparer.Ordinal);

        Assert.Equal(7, constants.Count);
        Assert.Subset(constants, named);
        Assert.Subset(named, constants);
    }

    private static async Task<AuthorizationPolicy?> ComposedPolicyAsync(string policyName)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        new UBookItAuthorizationComposer().Compose(new ServicesOnlyUmbracoBuilder(services));

        await using var provider = services.BuildServiceProvider();
        return await provider
            .GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(policyName);
    }

    /// <summary>The top-level policy constants, plus every constant in <c>Constants.VerbPolicies</c>.</summary>
    private static IEnumerable<string> PolicyConstants()
    {
        yield return Constants.SectionAccessPolicy;
        yield return Constants.SensitiveDataAccessPolicy;

        foreach (var field in typeof(Constants.VerbPolicies)
                     .GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f is { IsLiteral: true } && f.FieldType == typeof(string)))
        {
            yield return (string)field.GetRawConstantValue()!;
        }
    }

    /// <summary>Every policy named by <c>[Authorize]</c> on a backoffice controller or its actions.</summary>
    private static IEnumerable<string> PoliciesNamedByEndpoints()
    {
        var controllers = typeof(UBookItAuthorizationComposer).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t));

        foreach (var controller in controllers)
        {
            var members = new MemberInfo[] { controller }
                .Concat(controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

            foreach (var member in members)
            {
                foreach (var attribute in member.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                {
                    if (attribute.Policy is { Length: > 0 } policy)
                    {
                        yield return policy;
                    }
                }
            }
        }
    }
}
