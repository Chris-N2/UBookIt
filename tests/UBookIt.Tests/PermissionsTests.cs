using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Backoffice;
using UBookIt.Backoffice.Composers;
using UBookIt.Backoffice.Controllers;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Tests;

/// <summary>
/// The permissions capability: three verbs deciding access within the section, the
/// section as the unremovable outer gate, and the seed's selection rule.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorization is exercised through the REAL pipeline</b> — the real composer
/// composed (`ServicesOnlyUmbracoBuilder`, so the registration production relies on is
/// what runs; the delivery-exposure lesson), the real policies resolved from
/// <see cref="IAuthorizationService"/>, the real handlers evaluating a real
/// <see cref="User"/> whose groups carry the verbs under test. Only the principal→user
/// resolution is stubbed, on the section tests' established terms.
/// </para>
/// <para>
/// <b>The endpooint classification is a hard-coded snapshot</b>, for the reason the
/// delivery-exposure tests record: deriving the expectation from the attributes the
/// enforcement reads would be circular, so the map below is the decision and a
/// reclassified action fails here by name.
/// </para>
/// </remarks>
public class PermissionsTests
{
    // ---- the classification snapshot (design D2) ----

    private static readonly Dictionary<string, string> RecordedClassification = new()
    {
        ["BookingsController.ListBookings"] = Constants.VerbPolicies.BookingsRead,
        ["BookingsController.FindBookingsByBooker"] = Constants.VerbPolicies.BookingsRead,
        ["BookingsController.EraseBooker"] = Constants.VerbPolicies.BookingsRead,
        ["BookingsController.CancelBooking"] = Constants.VerbPolicies.BookingsManage,
        ["BookingsController.ConfirmBooking"] = Constants.VerbPolicies.BookingsManage,
        ["BookingsController.DeclineBooking"] = Constants.VerbPolicies.BookingsManage,
        ["ResourcesController.ListResources"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.GetResource"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.CreateResource"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.UpdateResource"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.DeleteResource"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.ListResourceTypes"] = Constants.VerbPolicies.Configure,
        ["ResourcesController.ListCapabilities"] = Constants.VerbPolicies.Configure,
        ["ServicesController.ListServices"] = Constants.VerbPolicies.Configure,
        ["ServicesController.GetService"] = Constants.VerbPolicies.Configure,
        ["ServicesController.CreateService"] = Constants.VerbPolicies.Configure,
        ["ServicesController.UpdateService"] = Constants.VerbPolicies.Configure,
        ["ServicesController.DeleteService"] = Constants.VerbPolicies.Configure,
        ["ServicesController.PreviewServiceConfiguration"] = Constants.VerbPolicies.Configure,
        ["ResponsibilityController.GetResourceResponsibility"] = Constants.VerbPolicies.Configure,
        ["ResponsibilityController.GetServiceResponsibility"] = Constants.VerbPolicies.Configure,
        ["ResponsibilityController.PutResourceResponsibility"] = Constants.VerbPolicies.Configure,
        ["ResponsibilityController.PutServiceResponsibility"] = Constants.VerbPolicies.Configure,
    };

    private static readonly string[] VerbPolicyNames =
    [
        Constants.VerbPolicies.BookingsRead,
        Constants.VerbPolicies.BookingsManage,
        Constants.VerbPolicies.Configure,
    ];

    /// <summary>
    /// The totality guard: every management action carries exactly one verb policy, and
    /// the classification matches the recorded snapshot — an action nobody classified is
    /// a named failure here, never a silently looser or tighter gate.
    /// </summary>
    [Fact]
    public void Every_management_action_carries_exactly_one_verb_policy()
    {
        var actual = new Dictionary<string, string>();
        var offenders = new List<string>();

        foreach (var action in ManagementActions())
        {
            var name = $"{action.DeclaringType!.Name}.{action.Name}";
            var verbPolicies = action.GetCustomAttributes<AuthorizeAttribute>()
                .Select(a => a.Policy)
                .Where(p => p is not null && VerbPolicyNames.Contains(p))
                .ToList();

            if (verbPolicies.Count != 1)
            {
                offenders.Add($"{name} carries {verbPolicies.Count} verb policies");
            }
            else
            {
                actual[name] = verbPolicies[0]!;
            }
        }

        Assert.True(offenders.Count == 0,
            "Every management action must carry exactly one verb policy:\n  " + string.Join("\n  ", offenders));
        Assert.Equal(
            RecordedClassification.OrderBy(p => p.Key, StringComparer.Ordinal),
            actual.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    /// <summary>The endpoints acting on personal data keep their sensitive-data policy beside the verb.</summary>
    [Fact]
    public void Sensitive_data_endpoints_keep_their_gate()
    {
        foreach (var name in new[] { "FindBookingsByBooker", "EraseBooker" })
        {
            var action = ManagementActions().Single(m => m.Name == name);

            Assert.Contains(
                action.GetCustomAttributes<AuthorizeAttribute>(),
                a => a.Policy == Constants.SensitiveDataAccessPolicy);
        }
    }

    /// <summary>
    /// One vocabulary: the client's verb module and manifest use the server's strings
    /// verbatim. Read from the source files raw — a typo would otherwise split the
    /// system into two verb sets with every suite green.
    /// </summary>
    [Fact]
    public void The_client_and_server_share_one_verb_vocabulary()
    {
        var module = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/section/permission-verbs.ts");
        var manifest = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/section/manifest.ts");

        Assert.Contains($"\"{Constants.Verbs.BookingsRead}\"", module, StringComparison.Ordinal);
        Assert.Contains($"\"{Constants.Verbs.BookingsManage}\"", module, StringComparison.Ordinal);
        Assert.Contains($"\"{Constants.Verbs.Configure}\"", module, StringComparison.Ordinal);

        // The manifest declares exactly three permission entries, one per verb, via the
        // module's constants (so the manifest cannot drift from the module, and the
        // module cannot drift from the server without the assertions above failing).
        Assert.Equal(3, CountOf(manifest, "type: \"entityUserPermission\""));
        Assert.Contains("verbs: [BOOKINGS_READ_VERB]", manifest, StringComparison.Ordinal);
        Assert.Contains("verbs: [BOOKINGS_MANAGE_VERB]", manifest, StringComparison.Ordinal);
        Assert.Contains("verbs: [CONFIGURE_VERB]", manifest, StringComparison.Ordinal);
    }

    // ---- the pipeline, composed for real ----

    [Fact]
    public async Task Read_without_manage_sees_but_cannot_act()
    {
        var user = UserWith(section: true, Constants.Verbs.BookingsRead);

        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsRead));
        Assert.False(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsManage));
    }

    [Fact]
    public async Task Manage_implies_read()
    {
        var user = UserWith(section: true, Constants.Verbs.BookingsManage);

        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsRead));
        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsManage));
    }

    [Fact]
    public async Task Configure_does_not_reach_bookings()
    {
        var user = UserWith(section: true, Constants.Verbs.Configure);

        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.Configure));
        Assert.False(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsRead));
        Assert.False(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsManage));
    }

    [Fact]
    public async Task Verbs_union_across_groups()
    {
        var user = UserWith(section: true);
        user.AddGroup(Group(1, sections: [], verbs: [Constants.Verbs.BookingsRead]));
        user.AddGroup(Group(2, sections: [], verbs: [Constants.Verbs.Configure]));

        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsRead));
        Assert.True(await AuthorizeAsync(user, Constants.VerbPolicies.Configure));
        Assert.False(await AuthorizeAsync(user, Constants.VerbPolicies.BookingsManage));
    }

    /// <summary>The section is the unremovable outer gate: all verbs, no section, nothing.</summary>
    [Fact]
    public async Task Verbs_without_the_section_grant_nothing()
    {
        var user = UserWith(section: false,
            Constants.Verbs.BookingsRead, Constants.Verbs.BookingsManage, Constants.Verbs.Configure);

        foreach (var policy in VerbPolicyNames)
        {
            Assert.False(await AuthorizeAsync(user, policy));
        }
    }

    /// <summary>The section alone (post-seed) is the shell: visible, and every verb policy refuses.</summary>
    [Fact]
    public async Task The_section_alone_reaches_no_verb_policy()
    {
        var user = UserWith(section: true);

        foreach (var policy in VerbPolicyNames)
        {
            Assert.False(await AuthorizeAsync(user, policy));
        }
    }

    // ---- the seed's selection rule, arm by arm ----

    [Fact]
    public void A_section_group_with_no_verbs_is_seeded()
        => Assert.True(UBookItPermissionSeed.ShouldSeed([Constants.SectionAlias], []));

    [Fact]
    public void A_group_without_the_section_is_never_touched()
        => Assert.False(UBookItPermissionSeed.ShouldSeed(["content"], []));

    [Fact]
    public void A_group_holding_any_ubookit_verb_is_never_touched()
    {
        // ANY uBookIt verb, not only the three current ones: this is what makes a retry
        // after partial failure idempotent, and what keeps the seed's hands off a group
        // an administrator edited — including one holding only a FUTURE verb.
        Assert.False(UBookItPermissionSeed.ShouldSeed(
            [Constants.SectionAlias], [Constants.Verbs.Configure]));
        Assert.False(UBookItPermissionSeed.ShouldSeed(
            [Constants.SectionAlias], ["UBookIt.Settings"]));
    }

    [Fact]
    public void Foreign_permissions_do_not_stop_the_seed()
        => Assert.True(UBookItPermissionSeed.ShouldSeed(
            [Constants.SectionAlias], ["Umb.Document.Read"]));

    // ---- harness ----

    private static IEnumerable<MethodInfo> ManagementActions()
        => typeof(UBookItBackofficeApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName);

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static User UserWith(bool section, params string[] verbs)
    {
        var user = new User(new GlobalSettings(), "Permission Tester", "perms@example.com", "perms@example.com", "irrelevant");
        user.AddGroup(Group(10, sections: section ? [Constants.SectionAlias] : [], verbs: verbs));
        return user;
    }

    private static ReadOnlyUserGroup Group(int id, string[] sections, string[] verbs)
        => new(
            id,
            Guid.NewGuid(),
            $"Group {id}",
            description: null,
            "icon-users",
            startContentId: null,
            startMediaId: null,
            alias: $"group{id}",
            allowedLanguages: [],
            allowedSections: sections,
            permissions: new HashSet<string>(verbs),
            granularPermissions: new HashSet<Umbraco.Cms.Core.Models.Membership.Permissions.IGranularPermission>(),
            hasAccessToAllLanguages: true);

    /// <summary>
    /// The whole pipeline: the REAL composer composed, the REAL policies and handlers,
    /// a real user — only the principal→user resolution stubbed (the section tests'
    /// established terms).
    /// </summary>
    private static async Task<bool> AuthorizeAsync(IUser user, string policy)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthorizationHelper>(new StubAuthorizationHelper(user));

        new UBookItAuthorizationComposer().Compose(new ServicesOnlyUmbracoBuilder(services));

        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(
            new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test")),
            resource: null,
            policy);

        return result.Succeeded;
    }

    private sealed class StubAuthorizationHelper(IUser? user) : IAuthorizationHelper
    {
        public IUser GetUmbracoUser(IPrincipal currentUser)
            => throw new NotSupportedException("The handlers resolve the user through TryGetUmbracoUser.");

        public bool TryGetUmbracoUser(
            IPrincipal currentUser,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IUser? resolved)
        {
            resolved = user;
            return user is not null;
        }
    }
}
