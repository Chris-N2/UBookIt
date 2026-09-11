using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Persistence.Responsibility;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;

namespace UBookIt.Tests;

/// <summary>
/// The resolver's decisions: the state rule, deduplication, and dangling parties. The
/// spec requirement is "Resolution skips who cannot or will not act, silently".
/// </summary>
/// <remarks>
/// <b>One test per user state, deliberately.</b> The state rule is a total switch with
/// five named arms; a single parameterised test whose expectations were listed in one
/// place could be satisfied by a rule that hard-codes the majority answer (the
/// email-templates round-5 lesson: a parameterised dimension whose expectations are
/// identical across its values is not self-falsifying). Here every arm has its own test
/// with its own expected direction, so flipping any single arm fails exactly one named
/// test.
/// </remarks>
public class ResponsibleRecipientResolverTests
{
    // ---- the state rule, arm by arm ----

    [Fact]
    public async Task An_active_user_receives()
        => Assert.Equal(["ada@example.com"], await ResolveOneUserAsync(ActiveUser("ada@example.com")));

    [Fact]
    public async Task An_inactive_user_receives()
    {
        // Created, approved, never yet logged in: a real colleague with a real inbox.
        var user = ActiveUser("grace@example.com");
        user.LastLoginDate = null;

        Assert.Equal(UserState.Inactive, user.UserState);
        Assert.Equal(["grace@example.com"], await ResolveOneUserAsync(user));
    }

    [Fact]
    public async Task A_locked_out_user_receives()
    {
        // Transient, expires, and must not cost the site a booking notification.
        var user = ActiveUser("alan@example.com");
        user.IsLockedOut = true;

        Assert.Equal(UserState.LockedOut, user.UserState);
        Assert.Equal(["alan@example.com"], await ResolveOneUserAsync(user));
    }

    [Fact]
    public async Task A_disabled_user_is_skipped()
    {
        var user = ActiveUser("edsger@example.com");
        user.IsApproved = false;

        Assert.Equal(UserState.Disabled, user.UserState);
        Assert.Empty(await ResolveOneUserAsync(user));
    }

    [Fact]
    public async Task An_invited_user_is_skipped()
    {
        var user = ActiveUser("barbara@example.com");
        user.IsApproved = false;
        user.LastLoginDate = null;
        user.InvitedDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(UserState.Invited, user.UserState);
        Assert.Empty(await ResolveOneUserAsync(user));
    }

    /// <summary>
    /// The skip is PER PARTY, never per booking: the spec scenario says "that user
    /// receives nothing and every other recipient is unaffected", and a rule that
    /// short-circuited the whole resolution on the first skip would pass every
    /// single-user test above.
    /// </summary>
    [Fact]
    public async Task A_skipped_user_does_not_affect_the_others()
    {
        var disabled = ActiveUser("edsger@example.com");
        disabled.IsApproved = false;

        var resolver = Resolver(
            [User(out var disabledKey), User(out var activeKey)],
            directory: new FakeDirectory().WithUser(disabledKey, disabled).WithUser(activeKey, ActiveUser("ada@example.com")));

        Assert.Equal(["ada@example.com"], await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    // ---- dangling parties ----

    [Fact]
    public async Task A_deleted_user_is_skipped_without_a_fault()
    {
        var resolver = Resolver(
            [User(out _), User(out var activeKey)],
            directory: new FakeDirectory().WithUser(activeKey, ActiveUser("ada@example.com")));

        // The deleted user's key is simply absent from the directory. No throw, and
        // everyone else still resolves.
        Assert.Equal(["ada@example.com"], await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    [Fact]
    public async Task A_deleted_group_is_skipped_without_a_fault()
    {
        var resolver = Resolver(
            [Group(out _), User(out var activeKey)],
            directory: new FakeDirectory().WithUser(activeKey, ActiveUser("ada@example.com")));

        Assert.Equal(["ada@example.com"], await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    // ---- groups and deduplication ----

    [Fact]
    public async Task Group_members_are_resolved_with_the_same_state_rule()
    {
        var disabled = ActiveUser("edsger@example.com");
        disabled.IsApproved = false;

        var resolver = Resolver(
            [Group(out var groupKey)],
            directory: new FakeDirectory().WithGroup(groupKey, ActiveUser("ada@example.com"), disabled));

        Assert.Equal(["ada@example.com"], await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    [Fact]
    public async Task One_person_many_routes_is_one_address()
    {
        // Directly assigned AND a member of an assigned group, with the case differing
        // between the two records — still one recipient, or one person is written to
        // twice about the same booking.
        var resolver = Resolver(
            [User(out var userKey), Group(out var groupKey)],
            directory: new FakeDirectory()
                .WithUser(userKey, ActiveUser("Ada@Example.com"))
                .WithGroup(groupKey, ActiveUser("ada@example.com")));

        Assert.Single(await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    [Fact]
    public async Task A_user_with_no_usable_address_is_skipped()
    {
        // Constructed valid, then blanked: the constructor validates, the setter does not,
        // and a stored user whose address was later emptied is the state being tested.
        var blank = ActiveUser("blank@example.com");
        blank.Email = " ";

        var resolver = Resolver(
            [User(out var key)],
            directory: new FakeDirectory().WithUser(key, blank));

        Assert.Empty(await resolver.ResolveAddressesAsync(AnyBooking()));
    }

    // ---- DescribeAsync: what the editing surface is shown ----

    [Fact]
    public async Task Describe_reports_an_existing_user_with_name_and_state()
    {
        var user = ActiveUser("ada@example.com");
        user.IsApproved = false;

        var assignment = new ResponsibilityAssignment(ResponsibilityPartyKind.User, Guid.NewGuid());
        var resolver = Resolver([], directory: new FakeDirectory().WithUser(assignment.Key, user));

        var status = Assert.Single(await resolver.DescribeAsync([assignment]));

        Assert.True(status.Exists);
        Assert.Equal("Test User", status.DisplayName);
        Assert.Equal("Disabled", status.UserState);
    }

    [Fact]
    public async Task Describe_reports_an_existing_group_with_name_and_no_state()
    {
        var assignment = new ResponsibilityAssignment(ResponsibilityPartyKind.Group, Guid.NewGuid());
        var resolver = Resolver(
            [], directory: new FakeDirectory().WithGroup(assignment.Key, "Studio Team"));

        var status = Assert.Single(await resolver.DescribeAsync([assignment]));

        Assert.True(status.Exists);
        Assert.Equal("Studio Team", status.DisplayName);
        Assert.Null(status.UserState);
    }

    [Fact]
    public async Task Describe_marks_a_dangling_party_rather_than_hiding_it()
    {
        var user = new ResponsibilityAssignment(ResponsibilityPartyKind.User, Guid.NewGuid());
        var group = new ResponsibilityAssignment(ResponsibilityPartyKind.Group, Guid.NewGuid());
        var resolver = Resolver([], directory: new FakeDirectory());

        var statuses = await resolver.DescribeAsync([user, group]);

        // Both come BACK — a dangling assignment is reported, not dropped — marked as
        // no longer resolving.
        Assert.Equal(2, statuses.Count);
        Assert.All(statuses, s => Assert.False(s.Exists));
        Assert.All(statuses, s => Assert.Null(s.DisplayName));
    }

    // ---- fixtures ----

    private static async Task<IReadOnlyList<string>> ResolveOneUserAsync(User user)
    {
        var resolver = Resolver(
            [User(out var key)],
            directory: new FakeDirectory().WithUser(key, user));

        return await resolver.ResolveAddressesAsync(AnyBooking());
    }

    private static ResponsibleRecipientResolver Resolver(
        IReadOnlyList<ResponsibilityAssignment> assignments, FakeDirectory directory)
        => new(new FakeResponsibilityStore(assignments), directory);

    private static ResponsibilityAssignment User(out Guid key)
        => new(ResponsibilityPartyKind.User, key = Guid.NewGuid());

    private static ResponsibilityAssignment Group(out Guid key)
        => new(ResponsibilityPartyKind.Group, key = Guid.NewGuid());

    /// <summary>Approved, has logged in: <see cref="UserState.Active"/>.</summary>
    private static User ActiveUser(string email)
        => new(new GlobalSettings(), "Test User", email, email, "irrelevant-password")
        {
            LastLoginDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };

    /// <summary>
    /// Any booking at all: the store is faked per test, so the resolver's answer cannot
    /// depend on the booking's content here — which subjects map to which assignments is
    /// the SQL store's job and is integration-tested against the real database.
    /// </summary>
    private static Booking AnyBooking()
        => Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                TestData.Utc(TestData.BaseDate, "09:00"),
                TestData.Utc(TestData.BaseDate, "10:00"),
                TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada.visitor@example.com", null).Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            TestData.Now).Value;

    private sealed class FakeResponsibilityStore(IReadOnlyList<ResponsibilityAssignment> assignments)
        : IResponsibilityStore
    {
        public Task<IReadOnlyList<ResponsibilityAssignment>> GetAsync(
            ResponsibilitySubject subject, Guid subjectId, CancellationToken cancellationToken = default)
            => Task.FromResult(assignments);

        public Task<DomainResult> ReplaceAsync(
            ResponsibilitySubject subject,
            Guid subjectId,
            IReadOnlyCollection<ResponsibilityAssignment> newAssignments,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The resolver never writes.");

        public Task<bool> HasAnyForBookingAsync(Booking booking, CancellationToken cancellationToken = default)
            => Task.FromResult(assignments.Count > 0);

        public Task<IReadOnlyList<ResponsibilityAssignment>> GetForBookingAsync(
            Booking booking, CancellationToken cancellationToken = default)
            => Task.FromResult(assignments);
    }

    private sealed class FakeDirectory : IUmbracoUserDirectory
    {
        private readonly Dictionary<Guid, IUser> _users = [];
        private readonly Dictionary<Guid, DirectoryGroup> _groups = [];
        private readonly Dictionary<int, IUser[]> _members = [];
        private int _nextGroupId = 1;

        public FakeDirectory WithUser(Guid key, IUser user)
        {
            _users[key] = user;
            return this;
        }

        public FakeDirectory WithGroup(Guid key, params IUser[] members)
            => WithGroup(key, "Group " + key.ToString("N")[..8], members);

        public FakeDirectory WithGroup(Guid key, string name, params IUser[] members)
        {
            var group = new DirectoryGroup(_nextGroupId++, name);
            _groups[key] = group;
            _members[group.Id] = members;
            return this;
        }

        public Task<IUser?> GetUserAsync(Guid key)
            => Task.FromResult(_users.TryGetValue(key, out var user) ? user : null);

        public Task<DirectoryGroup?> GetGroupAsync(Guid key)
            => Task.FromResult(_groups.TryGetValue(key, out var group) ? group : null);

        public IEnumerable<IUser> GetMembers(DirectoryGroup group)
            => _members.TryGetValue(group.Id, out var members) ? members : [];
    }
}
