using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using UBookIt.Backoffice;
using UBookIt.Backoffice.Composers;
using UBookIt.Persistence.Stores;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Exceptions;
using Constants = UBookIt.Backoffice.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;

namespace UBookIt.Tests;

/// <summary>
/// The seed's FLOW — QA round 1's first MAJOR: the selection rule was tested arm by arm
/// and the store against real SQL, but nothing executed <c>HandleAsync</c>, so the
/// flag-exists skip, the exception tolerance and — above all — "the flag is written only
/// on full success" were inspection-only. The failure arm is the one the live check
/// could not produce, and "simple by inspection" is where this project's defects live.
/// </summary>
public class PermissionSeedFlowTests
{
    [Fact]
    public async Task A_present_flag_skips_everything()
    {
        var groups = new StubUserGroupService(SectionGroupWithoutVerbs());
        var flags = new StubFlagStore { Present = true };

        await RunAsync(flags, groups);

        Assert.False(groups.WasListed);
        Assert.Empty(groups.Updated);
        Assert.False(flags.SetCalled);
    }

    [Fact]
    public async Task Full_success_grants_and_writes_the_flag()
    {
        var seedable = SectionGroupWithoutVerbs();
        var untouchable = SectionGroupWithoutVerbs();
        untouchable.Permissions.Add(Constants.Verbs.Configure);
        var groups = new StubUserGroupService(seedable, untouchable);
        var flags = new StubFlagStore();

        await RunAsync(flags, groups);

        var updated = Assert.Single(groups.Updated);
        Assert.Same(seedable, updated);
        Assert.Contains(Constants.Verbs.BookingsRead, seedable.Permissions);
        Assert.Contains(Constants.Verbs.BookingsManage, seedable.Permissions);
        Assert.Contains(Constants.Verbs.Configure, seedable.Permissions);
        Assert.True(flags.SetCalled);

        // THE SETTINGS VERB IS NEVER SEEDED, on a fresh seed or any other.
        //
        // Seeding it would widen privilege into exactly what the verb exists to separate: every
        // group holding Configure because somebody ticked "may add a meeting room" would gain the
        // site's retention posture, its anonymous exposure and where bookers' details are emailed.
        // It would also make a freshly installed site differ from an upgraded one, since the flag
        // is already recorded wherever this seed has run.
        Assert.DoesNotContain(Constants.Verbs.Settings, seedable.Permissions);
        Assert.DoesNotContain(Constants.Verbs.Settings, untouchable.Permissions);
    }

    [Fact]
    public async Task An_upgrade_grants_the_settings_verb_to_nobody()
    {
        // The upgrade case, which is the one that actually happens: groups already hold uBookIt
        // verbs from a previous version, so the selection rule skips them AND the flag is already
        // recorded. Nobody gains the settings verb, and nobody loses anything either.
        var alreadySeeded = SectionGroupWithoutVerbs();
        alreadySeeded.Permissions.Add(Constants.Verbs.BookingsRead);
        alreadySeeded.Permissions.Add(Constants.Verbs.BookingsManage);
        alreadySeeded.Permissions.Add(Constants.Verbs.Configure);

        var groups = new StubUserGroupService(alreadySeeded);
        var flags = new StubFlagStore();

        await RunAsync(flags, groups);

        Assert.DoesNotContain(Constants.Verbs.Settings, alreadySeeded.Permissions);
        Assert.Contains(Constants.Verbs.BookingsRead, alreadySeeded.Permissions);
        Assert.Contains(Constants.Verbs.BookingsManage, alreadySeeded.Permissions);
        Assert.Contains(Constants.Verbs.Configure, alreadySeeded.Permissions);
        Assert.Empty(groups.Updated);
    }

    [Fact]
    public void The_seeded_set_is_every_verb_except_settings()
    {
        // Structural, so that adding a FIFTH verb has to make a decision here rather than being
        // seeded by default. Derived from the server's constants for the same reason the
        // vocabulary guard is: a hand-written expectation cannot see a verb nobody added to it.
        var allVerbs = typeof(Constants.Verbs)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var seeded = (string[])typeof(UBookItPermissionSeed)
            .GetField("AllVerbs", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.NotEmpty(allVerbs);
        Assert.Equal(
            allVerbs.Except([Constants.Verbs.Settings], StringComparer.Ordinal).OrderBy(v => v, StringComparer.Ordinal),
            seeded.OrderBy(v => v, StringComparer.Ordinal));
    }

    /// <summary>
    /// The failure arm the live check could not produce: one group's update fails, the
    /// flag stays unwritten (so the next boot retries), and nothing throws out of the
    /// handler — a permissions seed is not worth a site's boot.
    /// </summary>
    [Fact]
    public async Task A_failed_update_leaves_the_flag_unwritten_and_does_not_throw()
    {
        // The FIRST group fails (QA round 2's nit): this pins that the loop continues
        // past a failure and still grants the later group — abort-on-first-failure
        // would leave `second` ungranted and this test red.
        var first = SectionGroupWithoutVerbs();
        var second = SectionGroupWithoutVerbs();
        var groups = new StubUserGroupService(first, second) { FailUpdateFor = first };
        var flags = new StubFlagStore();

        await RunAsync(flags, groups);

        Assert.False(flags.SetCalled);
        Assert.Same(second, Assert.Single(groups.Updated));

        // The retry, as the next boot would see it: the granted group's PERSISTED state
        // holds the verbs (it was updated), so the selection rule skips it; the failed
        // group's persisted state holds none — the in-memory mutation the seed made
        // before the failed update is discarded with the boot — so it is retried. The
        // fresh instance below is that re-read, made explicit rather than assumed.
        Assert.Contains(Constants.Verbs.Configure, second.Permissions);
        Assert.False(UBookItPermissionSeed.ShouldSeed(second.AllowedSections, second.Permissions));

        var firstAsPersisted = SectionGroupWithoutVerbs();
        Assert.True(UBookItPermissionSeed.ShouldSeed(
            firstAsPersisted.AllowedSections, firstAsPersisted.Permissions));
    }

    [Fact]
    public async Task A_throwing_service_is_logged_not_rethrown()
    {
        var groups = new StubUserGroupService(SectionGroupWithoutVerbs()) { ThrowOnList = true };
        var flags = new StubFlagStore();

        // Must not throw: the observer contract — the seed is not worth a boot.
        await RunAsync(flags, groups);

        Assert.False(flags.SetCalled);
    }

    [Fact]
    public async Task An_unbooted_runtime_does_nothing()
    {
        var groups = new StubUserGroupService(SectionGroupWithoutVerbs());
        var flags = new StubFlagStore();

        await RunAsync(flags, groups, RuntimeLevel.Install);

        Assert.False(groups.WasListed);
        Assert.False(flags.SetCalled);
    }

    // ---- harness ----

    private static async Task RunAsync(
        StubFlagStore flags, StubUserGroupService groups, RuntimeLevel level = RuntimeLevel.Run)
    {
        var seed = new UBookItPermissionSeed(
            flags, groups, new StubRuntimeState(level), NullLogger<UBookItPermissionSeed>.Instance);

        await seed.HandleAsync(new UmbracoApplicationStartedNotification(false), CancellationToken.None);
    }

    private static UserGroup SectionGroupWithoutVerbs()
    {
        var group = new UserGroup(new DefaultShortStringHelper(new DefaultShortStringHelperConfig()))
        {
            Alias = $"group-{Guid.NewGuid():N}",
            Name = "Seed Test Group",
        };
        group.AddAllowedSection(Constants.SectionAlias);
        return group;
    }

    private sealed class StubFlagStore : IFlagStore
    {
        public bool Present { get; set; }

        public bool SetCalled { get; private set; }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Present);

        public Task SetAsync(string key, CancellationToken cancellationToken = default)
        {
            SetCalled = true;
            Present = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubRuntimeState(RuntimeLevel level) : IRuntimeState
    {
        public RuntimeLevel Level => level;

        public string? CurrentMigrationState => null;

        public string? FinalMigrationState => null;

        public RuntimeLevelReason Reason => RuntimeLevelReason.Run;

        public Umbraco.Cms.Core.Semver.SemVersion SemanticVersion => null!;

        public Version Version => new(17, 0, 0);

        public string VersionComment => string.Empty;

        public BootFailedException? BootFailedException => null;

        public IReadOnlyDictionary<string, object> StartupState => new Dictionary<string, object>();

        public void Configure(RuntimeLevel level, RuntimeLevelReason reason, Exception? bootFailedException = null)
            => throw new NotSupportedException("The seed only reads the level.");

        public void DetermineRuntimeLevel()
            => throw new NotSupportedException("The seed only reads the level.");
    }

    /// <summary>
    /// Answers only what the seed asks — list and update. Everything else throws, so a
    /// seed that started doing more would say so here rather than be handed an invented
    /// answer.
    /// </summary>
    private sealed class StubUserGroupService(params IUserGroup[] groups) : IUserGroupService
    {
        public bool WasListed { get; private set; }

        public bool ThrowOnList { get; init; }

        public IUserGroup? FailUpdateFor { get; init; }

        public List<IUserGroup> Updated { get; } = [];

        public Task<PagedModel<IUserGroup>> GetAllAsync(int skip, int take)
        {
            WasListed = true;

            return ThrowOnList
                ? throw new InvalidOperationException("The group store is unreachable.")
                : Task.FromResult(new PagedModel<IUserGroup> { Items = groups, Total = groups.Length });
        }

        public Task<Attempt<IUserGroup, UserGroupOperationStatus>> UpdateAsync(IUserGroup userGroup, Guid userKey)
        {
            if (ReferenceEquals(userGroup, FailUpdateFor))
            {
                return Task.FromResult(
                    Attempt.FailWithStatus(UserGroupOperationStatus.CancelledByNotification, userGroup));
            }

            Updated.Add(userGroup);
            return Task.FromResult(Attempt.SucceedWithStatus(UserGroupOperationStatus.Success, userGroup));
        }

        private const string Explanation = "The seed only lists groups and updates them.";

        public Task<IEnumerable<IUserGroup>> GetAsync(params int[] ids) => throw new NotSupportedException(Explanation);

        public Task<IEnumerable<IUserGroup>> GetAsync(params string[] aliases) => throw new NotSupportedException(Explanation);

        public Task<IUserGroup?> GetAsync(string alias) => throw new NotSupportedException(Explanation);

        public Task<IUserGroup?> GetAsync(int id) => throw new NotSupportedException(Explanation);

        public Task<IUserGroup?> GetAsync(Guid key) => throw new NotSupportedException(Explanation);

        public Task<IEnumerable<IUserGroup>> GetAsync(IEnumerable<Guid> keys) => throw new NotSupportedException(Explanation);

        public Task<Attempt<PagedModel<IUserGroup>, UserGroupOperationStatus>> FilterAsync(
            Guid userKey, string? filter, int skip, int take) => throw new NotSupportedException(Explanation);

        public Task<Attempt<IUserGroup, UserGroupOperationStatus>> CreateAsync(
            IUserGroup userGroup, Guid userKey, Guid[]? groupMembersKeys = null) => throw new NotSupportedException(Explanation);

        public Task<Attempt<UserGroupOperationStatus>> DeleteAsync(ISet<Guid> userGroupKeys) => throw new NotSupportedException(Explanation);

        public Task<Attempt<UserGroupOperationStatus>> UpdateUserGroupsOnUsersAsync(
            ISet<Guid> userGroupKeys, ISet<Guid> userKeys) => throw new NotSupportedException(Explanation);

        public Task<Attempt<UserGroupOperationStatus>> AddUsersToUserGroupAsync(
            UsersToUserGroupManipulationModel addUsersModel, Guid performingUserKey)
            => throw new NotSupportedException(Explanation);

        public Task<Attempt<UserGroupOperationStatus>> RemoveUsersFromUserGroupAsync(
            UsersToUserGroupManipulationModel removeUsersModel, Guid performingUserKey)
            => throw new NotSupportedException(Explanation);
    }
}
