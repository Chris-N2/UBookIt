using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using UBookIt.Backoffice.Security;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Security.Authorization;

namespace UBookIt.Tests;

/// <summary>
/// The handler that decides whether a caller may erase a booker's details.
/// </summary>
/// <remarks>
/// <para>
/// <b>This was the one part of the erase gate nothing exercised.</b> The endpoint tests
/// establish that the action carries the policy, that the policy composes the section and
/// sensitive-data requirements, and that it carries the backoffice authentication scheme — all
/// true, all necessary, and none of it touches the handler that answers the question. Deleting
/// <c>&amp;&amp; user.HasAccessToSensitiveData()</c> from the condition left every one of those
/// tests green, and the whole suite with them: irreversible destruction of a booker's personal
/// data would have been available to any user with mere section access.
/// </para>
/// <para>
/// The existing identifier-set guard could not see it either, and the reason is this project's
/// recurring lesson. It asserts <c>HasAccessToSensitiveData</c> appears in <c>src</c>; the READ
/// path names it too, so the string survives the deletion. The guard observes that the
/// mechanism exists somewhere, not that this path consults it.
/// </para>
/// <para>
/// Built on Umbraco's real <c>User</c>, <c>ReadOnlyUserGroup</c> and real group key rather than
/// a stubbed <c>IUser</c>, on the same reasoning as the section-access tests: a stub would be a
/// second opinion about what membership means, and membership is precisely what is under test.
/// </para>
/// </remarks>
public class UBookItSensitiveDataAccessTests
{
    private static IUser UserInGroup(Guid groupKey)
    {
        var user = new User(new GlobalSettings());

        user.AddGroup(new ReadOnlyUserGroup(
            id: 1,
            key: groupKey,
            name: "Test group",
            description: null,
            icon: null,
            startContentId: null,
            startMediaId: null,
            startElementId: null,
            alias: "testGroup",
            allowedLanguages: [],
            allowedSections: [UBookIt.Backoffice.Constants.SectionAlias],
            permissions: new HashSet<string>(),
            granularPermissions: new HashSet<IGranularPermission>(),
            hasAccessToAllLanguages: true));

        return user;
    }

    /// <summary>
    /// Runs the handler and returns the whole context — <b>not just whether it succeeded.</b>
    /// </summary>
    /// <remarks>
    /// <c>HasSucceeded</c> is <c>false</c> both when the handler calls <c>Fail()</c> and when it
    /// returns having done nothing, so a helper returning only that cannot observe the
    /// difference. The handler's own documentation claims it "fails explicitly rather than
    /// merely declining to succeed", and replacing <c>context.Fail()</c> with an empty branch
    /// left all four of these tests — and the whole suite — green. Behaviourally identical
    /// today, because nothing else can satisfy this requirement; a real hole the moment a
    /// second handler for it exists, since silence lets that one grant what this one refused.
    /// </remarks>
    private static async Task<AuthorizationHandlerContext> EvaluateAsync(IUser? user)
    {
        var requirement = new UBookItSensitiveDataRequirement();
        var context = new AuthorizationHandlerContext(
            [requirement], new ClaimsPrincipal(new ClaimsIdentity()), resource: null);

        await new UBookItSensitiveDataHandler(new StubAuthorizationHelper(user))
            .HandleAsync(context);

        return context;
    }

    private static async Task<bool> IsAuthorizedAsync(IUser? user)
        => (await EvaluateAsync(user)).HasSucceeded;

    private static async Task AssertRefusedExplicitlyAsync(IUser? user)
    {
        var context = await EvaluateAsync(user);

        Assert.False(context.HasSucceeded);
        Assert.True(
            context.HasFailed,
            "The handler declined to succeed but did not FAIL. Another handler for this "
            + "requirement could then satisfy it, granting erasure to a caller this one "
            + "refused — which is not the same guarantee.");
    }

    [Fact]
    public async Task A_user_in_Umbracos_sensitive_data_group_may_erase()
    {
        Assert.True(await IsAuthorizedAsync(UserInGroup(Constants.Security.SensitiveDataGroupKey)));
    }

    [Fact]
    public async Task A_user_in_some_other_group_may_not()
    {
        // A group that is emphatically NOT the sensitive-data one, rather than no group at
        // all: "an ordinary editor" is a user who belongs to something, and a handler that
        // only refused the group-less would let every one of them through.
        await AssertRefusedExplicitlyAsync(UserInGroup(Guid.NewGuid()));
    }

    [Fact]
    public async Task Section_access_alone_does_not_grant_it()
    {
        // The fixture above gives every user the package's section, so this asserts the
        // sensitive-data gate is independent of it: holding the section is not holding this.
        // (Since the verbs, the section alone no longer reaches the list either — but that is
        // the verb policies' refusal, tested in PermissionsTests; THIS gate must refuse on its
        // own grounds, not lean on theirs.) A caller the site permits to see bookings must not
        // thereby be permitted to destroy the people in them.
        var sectionOnly = UserInGroup(Guid.NewGuid());

        Assert.Contains(
            UBookIt.Backoffice.Constants.SectionAlias,
            sectionOnly.AllowedSections);

        await AssertRefusedExplicitlyAsync(sectionOnly);
    }

    [Fact]
    public async Task An_unresolvable_user_is_refused()
    {
        // Fails rather than declining silently. A handler that merely stayed quiet would leave
        // the requirement unmet but allow another handler to satisfy it, which is not the same
        // guarantee — and the failure mode of guessing here is destroying somebody's data on
        // behalf of a caller nobody could identify.
        await AssertRefusedExplicitlyAsync(null);
    }

    /// <summary>
    /// Answers only the question the handler asks; everything else throws, so a handler that
    /// started reading more would say so here rather than being handed an invented answer.
    /// </summary>
    private sealed class StubAuthorizationHelper(IUser? user) : IAuthorizationHelper
    {
        public IUser GetUmbracoUser(IPrincipal currentUser)
            => throw new NotSupportedException(
                "The sensitive-data handler resolves the user through TryGetUmbracoUser. If "
                + "that changed, decide what the honest answer is rather than returning one.");

        public bool TryGetUmbracoUser(
            IPrincipal currentUser,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IUser? resolved)
        {
            resolved = user;
            return user is not null;
        }
    }
}
