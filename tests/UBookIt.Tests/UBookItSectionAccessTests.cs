using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Validation.AspNetCore;
using UBookIt.Backoffice;
using UBookIt.Backoffice.Composers;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Security;
using UBookIt.Core.Common;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Tests;

/// <summary>
/// Who may call a uBookIt management endpoint.
///
/// <para>
/// <b>These assert the guarantee, not the attribute.</b> A test that checked for the
/// presence of some <c>[Authorize]</c> attribute would have passed on the configuration
/// this change exists to correct — the base controller carried one the whole time, naming
/// Umbraco's <b>Content</b> section while the package ships its own. What matters is which
/// users get in, so that is what is exercised.
/// </para>
/// <para>
/// Built on Umbraco's real <c>User</c> and <c>ReadOnlyUserGroup</c> rather than a fake
/// <c>IUser</c>: the interface is large, and a stub would be a second opinion about what
/// <c>AllowedSections</c> means.
/// </para>
/// </summary>
public class UBookItSectionAccessTests
{
    private static IUser UserWithSections(params string[] sections)
    {
        var user = new User(new GlobalSettings());

        user.AddGroup(new ReadOnlyUserGroup(
            id: 1,
            key: Guid.NewGuid(),
            name: "Test group",
            description: null,
            icon: null,
            startContentId: null,
            startMediaId: null,
            alias: "testGroup",
            allowedLanguages: [],
            allowedSections: sections,
            permissions: new HashSet<string>(),
            granularPermissions: new HashSet<IGranularPermission>(),
            hasAccessToAllLanguages: true));

        return user;
    }

    private static async Task<bool> IsAuthorizedAsync(IUser? user)
    {
        var requirement = new UBookItSectionRequirement();
        var context = new AuthorizationHandlerContext(
            [requirement], new ClaimsPrincipal(new ClaimsIdentity()), resource: null);

        await new UBookItSectionHandler(new StubAuthorizationHelper(user))
            .HandleAsync(context);

        return context.HasSucceeded;
    }

    [Fact]
    public async Task A_user_holding_the_packages_section_is_authorized()
    {
        Assert.True(await IsAuthorizedAsync(UserWithSections(Constants.SectionAlias)));
    }

    [Fact]
    public async Task A_user_without_the_packages_section_is_refused_whatever_else_they_hold()
    {
        // Including Content, which is what the endpoints previously authorized on. This is
        // the half of the fix that closes access rather than opening it: a Content user
        // could call every uBookIt endpoint for a section they cannot see.
        Assert.False(await IsAuthorizedAsync(
            UserWithSections("content", "media", "settings", "users", "packages")));
    }

    [Fact]
    public async Task The_packages_section_alone_is_sufficient()
    {
        // The other half: a user granted uBookIt and nothing else was previously refused
        // an API for the only section they had.
        Assert.True(await IsAuthorizedAsync(UserWithSections(Constants.SectionAlias)));
        Assert.False(await IsAuthorizedAsync(UserWithSections()));
    }

    [Fact]
    public async Task A_request_with_no_backoffice_user_is_refused()
    {
        // The guarantee the requirement already carried and must not lose: anonymous
        // requests do not get in. The handler fails explicitly rather than staying
        // silent, so another handler cannot satisfy the requirement on its behalf.
        Assert.False(await IsAuthorizedAsync(user: null));
    }

    [Fact]
    public async Task The_alias_comparison_ignores_case()
    {
        // Stored values come back from the database, whose collation is not the package's
        // to assume. Casing is not what should decide whether an operator can work.
        Assert.True(await IsAuthorizedAsync(UserWithSections("ubookit.section")));
    }

    [Fact]
    public void The_section_alias_matches_the_backoffice_manifest()
    {
        // The constant is only correct if it equals the alias the client manifest
        // declares — that is the value a user group stores when the section is granted,
        // measured against a running site's umbracoUserGroup2App. Tying the two together
        // means renaming the section in the manifest fails here rather than silently
        // locking every operator out of the API.
        var manifest = Support.RepoFiles.Read(
            "src/UBookIt.Backoffice/Client/src/section/manifest.ts");

        Assert.Contains(
            $"alias: \"{Constants.SectionAlias}\"",
            manifest,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Every_management_controller_inherits_the_shared_authorized_base()
    {
        // Authorization is applied in one place. A controller that derived from
        // ControllerBase directly would be anonymous, and nothing else in the suite would
        // notice.
        var controllers = typeof(UBookItBackofficeApiControllerBase).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true }
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(controllers);
        Assert.All(
            controllers,
            controller => Assert.True(
                typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(controller),
                $"{controller.Name} does not derive from the authorized base controller."));
    }

    [Fact]
    public async Task The_policy_the_endpoints_name_is_the_one_the_handler_answers()
    {
        // The chain this asserts — base controller's policy NAME -> the composer's
        // registration under that name -> the requirement -> the handler -> who gets in —
        // had no guard at all, and every link in it was silently breakable. Restoring the
        // original defect (`SectionAccessContent` on the base) passed the entire suite,
        // which is precisely the failure the class comment above warns about: the earlier
        // tests assert the handler's answer, and nothing connected the handler to the
        // attribute the endpoints actually carry.
        //
        // So this goes through Umbraco's nothing and ASP.NET's everything: the real
        // AuthorizationService resolving the real registered policy by the name read off
        // the real attribute. A wrong name does not fail an assertion — the policy does not
        // exist, and the framework throws.
        Assert.True(await IsAuthorizedByTheRegisteredPolicyAsync(
            UserWithSections(Constants.SectionAlias)));

        Assert.False(await IsAuthorizedByTheRegisteredPolicyAsync(
            UserWithSections("content", "media", "settings")));
    }

    [Fact]
    public async Task The_registered_policy_carries_the_backoffice_authentication_scheme()
    {
        // Omitting it rejects an authenticated user with nothing useful to say about why —
        // the request never resolves to a backoffice principal, so the handler sees no user
        // and refuses. It is invisible to the test above, which hands the policy a principal
        // directly rather than authenticating one, and it was invisible to the whole suite:
        // deleting the line passed 860/860. The live check in task 4.6 caught it once; a
        // one-off confirmation is not a regression guard.
        var provider = await PolicyProvider().GetPolicyAsync(PolicyOnTheSharedBase);

        Assert.NotNull(provider);
        Assert.Contains(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
            provider.AuthenticationSchemes);
    }

    /// <summary>
    /// The policy name the endpoints actually carry, read from the attribute rather than
    /// from the constant — so naming the wrong policy is what fails, not a mismatch between
    /// two things a single edit changes together.
    /// </summary>
    private static string PolicyOnTheSharedBase
        => typeof(UBookItBackofficeApiControllerBase)
               .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
               .Cast<AuthorizeAttribute>()
               .Select(attribute => attribute.Policy)
               .Single(policy => !string.IsNullOrWhiteSpace(policy))
           ?? throw new InvalidOperationException(
               "The shared base controller names no authorization policy.");

    private static IAuthorizationPolicyProvider PolicyProvider(IUser? user = null)
        => Composed(user).GetRequiredService<IAuthorizationPolicyProvider>();

    private static async Task<bool> IsAuthorizedByTheRegisteredPolicyAsync(IUser user)
    {
        var services = Composed(user);

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
            resource: null,
            PolicyOnTheSharedBase);

        return result.Succeeded;
    }

    /// <summary>Everything the composer registers, and the one thing the handler reads.</summary>
    private static ServiceProvider Composed(IUser? user)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IAuthorizationHelper>(new StubAuthorizationHelper(user));

        new UBookItAuthorizationComposer().Compose(new ServicesOnlyUmbracoBuilder(services));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// An <see cref="IUmbracoBuilder"/> that offers a service collection and nothing else.
    /// A composer that started reading configuration or the type loader would fail loudly
    /// here rather than being handed an invented answer.
    /// </summary>
    private sealed class ServicesOnlyUmbracoBuilder(IServiceCollection services) : IUmbracoBuilder
    {
        public IServiceCollection Services { get; } = services;

        public IConfiguration Config => throw new NotSupportedException(Explanation);

        public TypeLoader TypeLoader => throw new NotSupportedException(Explanation);

        public ILoggerFactory BuilderLoggerFactory => throw new NotSupportedException(Explanation);

        public IProfiler Profiler => throw new NotSupportedException(Explanation);

        public AppCaches AppCaches => throw new NotSupportedException(Explanation);

        public TBuilder WithCollectionBuilder<TBuilder>() where TBuilder : ICollectionBuilder
            => throw new NotSupportedException(Explanation);

        public void Build() => throw new NotSupportedException(Explanation);

        private const string Explanation =
            "The authorization composer registers services and reads nothing else. If that "
            + "changed, give this stub a real answer rather than an invented one.";
    }

    /// <summary>
    /// Answers only the question the handler asks. Everything else throws, so a handler
    /// that started reading more would say so here rather than passing on an invented
    /// answer — the same discipline the rendering suite's Umbraco context uses.
    /// </summary>
    private sealed class StubAuthorizationHelper(IUser? user) : IAuthorizationHelper
    {
        public IUser GetUmbracoUser(IPrincipal currentUser)
            => throw new NotSupportedException(
                "The section handler resolves the user through TryGetUmbracoUser. If that "
                + "changed, decide what the honest answer is rather than returning one.");

        // [NotNullWhen(true)] to match the interface: the contract is that a `true` return
        // guarantees a non-null user, and dropping the attribute makes every caller's
        // null-state analysis quietly weaker than the real helper's.
        public bool TryGetUmbracoUser(
            IPrincipal currentUser,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IUser? resolved)
        {
            resolved = user;
            return user is not null;
        }
    }
}
