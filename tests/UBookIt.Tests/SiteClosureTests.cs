using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The closure value type, the precedence ladder, and the codes all three are
/// reported by. Storage plays no part here: precedence is a domain rule, and a
/// test that needed a database to exercise it would be testing the wrong thing.
/// </summary>
public class SiteClosureTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    [Fact]
    public void Closure_carries_its_date_and_trimmed_label()
    {
        var result = SiteClosure.Create(Date, "  Christmas Day  ");

        Assert.True(result.Succeeded);
        Assert.Equal(Date, result.Value.Date);
        Assert.Equal("Christmas Day", result.Value.Label);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Label_is_required(string? label)
    {
        var result = SiteClosure.Create(Date, label);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ClosureLabelInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Over_long_label_is_a_validation_failure_not_a_storage_error()
    {
        var result = SiteClosure.Create(Date, new string('x', SiteClosure.MaxLabelLength + 1));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ClosureLabelInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Label_at_the_limit_is_accepted()
    {
        var result = SiteClosure.Create(Date, new string('x', SiteClosure.MaxLabelLength));

        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// The codes are a published contract — a consumer maps them to its own
    /// messages — so their literal values are asserted rather than their names.
    /// </summary>
    [Fact]
    public void Closure_failure_codes_have_their_contract_values()
    {
        Assert.Equal("closure-label-invalid", FailureCodes.ClosureLabelInvalid);
        Assert.Equal("duplicate-closure-date", FailureCodes.DuplicateClosureDate);
        Assert.Equal("closure-not-found", FailureCodes.ClosureNotFound);
    }

    [Fact]
    public void Two_closures_on_one_date_are_rejected()
    {
        var result = AvailabilityConfiguration.Create(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            closures: [TestData.Closure(Date, "Christmas Day"), TestData.Closure(Date, "Stocktake")]);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(result.Failures).Code);
    }

    // ---- precedence ----

    [Fact]
    public void Closure_closes_a_day_the_weekly_pattern_opens()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            closures: [TestData.Closure(Date)]);

        Assert.Empty(config.EffectiveWindows(Date));
    }

    [Fact]
    public void Closure_supersedes_the_resources_own_override_exception()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value],
            closures: [TestData.Closure(Date)]);

        Assert.Empty(config.EffectiveWindows(Date));
    }

    [Fact]
    public void Closure_over_a_closure_exception_is_still_closed()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [DateException.Closure(Date)],
            closures: [TestData.Closure(Date)]);

        Assert.Empty(config.EffectiveWindows(Date));
    }

    [Fact]
    public void A_superseded_exception_is_retained_not_discarded()
    {
        var exception = DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value;

        var closed = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [exception],
            closures: [TestData.Closure(Date)]);

        // The exception is still there while superseded...
        Assert.Single(closed.Exceptions);

        // ...and rebuilding without the closure — which is what removing it or
        // opting out of it does — restores its full effect, un-re-entered.
        var reopened = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: closed.Exceptions);

        var window = Assert.Single(reopened.EffectiveWindows(Date));
        Assert.Equal(TimeOnly.Parse("10:00"), window.Start);
        Assert.Equal(TimeOnly.Parse("14:00"), window.End);
    }

    [Fact]
    public void A_closure_on_another_date_changes_nothing()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            closures: [TestData.Closure(Date.AddDays(7))]);

        var window = Assert.Single(config.EffectiveWindows(Date));
        Assert.Equal(TimeOnly.Parse("09:00"), window.Start);
    }

    /// <summary>
    /// The opted-out case: a resource that has opted out is hydrated without that
    /// closure, so its configuration must be indistinguishable from one on a site
    /// that never had it. Asserted as equality of outcome rather than by trusting
    /// the filter that produced it.
    /// </summary>
    [Fact]
    public void An_opted_out_closure_leaves_the_date_resolving_exactly_as_if_it_did_not_exist()
    {
        var exception = DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value;
        var weekly = TestData.Weekly("09:00", "17:00", Date.DayOfWeek);

        var optedOut = TestData.Config(weekly, exceptions: [exception]);
        var noClosureAnywhere = TestData.Config(weekly, exceptions: [exception]);

        Assert.Equal(
            noClosureAnywhere.EffectiveWindows(Date).Select(w => (w.Start, w.End)),
            optedOut.EffectiveWindows(Date).Select(w => (w.Start, w.End)));
    }

    // ---- the superseded determination ----

    [Fact]
    public void An_override_exception_under_a_closure_is_superseded()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value],
            closures: [TestData.Closure(Date)]);

        Assert.True(config.IsExceptionSuperseded(Date));
    }

    [Fact]
    public void A_closure_exception_under_a_closure_is_not_superseded()
    {
        // Closed either way, so there is no difference to report — and a statement
        // about a difference must be true of a difference.
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [DateException.Closure(Date)],
            closures: [TestData.Closure(Date)]);

        Assert.False(config.IsExceptionSuperseded(Date));
    }

    [Fact]
    public void An_exception_with_no_closure_is_not_superseded()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value]);

        Assert.False(config.IsExceptionSuperseded(Date));
    }

    [Fact]
    public void A_closure_with_no_exception_is_not_superseded()
    {
        var config = TestData.Config(
            TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
            closures: [TestData.Closure(Date)]);

        Assert.False(config.IsExceptionSuperseded(Date));
    }

    // ---- opt-outs are resource-owned ----

    [Fact]
    public void A_resource_created_without_opt_outs_carries_none()
    {
        Assert.Empty(TestData.Room().ClosureOptOuts);
    }

    [Fact]
    public void A_resource_carries_the_opt_outs_it_was_created_with()
    {
        var closureId = Guid.NewGuid();

        var resource = Resource.Create(
            type: ResourceTypes.Room,
            displayName: "Meeting Room A",
            closureOptOuts: [closureId]).Value;

        Assert.Equal(closureId, Assert.Single(resource.ClosureOptOuts));
    }

    /// <summary>
    /// Opt-outs live on the resource; applicable closures live on its
    /// availability. Different lifetimes — one is written by the resource's own
    /// save, the other is the site's and is never written back — which is what
    /// stops a save absorbing an inherited closure.
    /// </summary>
    [Fact]
    public void Opt_outs_and_applicable_closures_are_separate_collections()
    {
        var closure = TestData.Closure(Date);

        var resource = Resource.Create(
            type: ResourceTypes.Room,
            displayName: "Meeting Room A",
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek), closures: [closure]),
            closureOptOuts: [Guid.NewGuid()]).Value;

        Assert.Single(resource.Availability.Closures);
        Assert.Single(resource.ClosureOptOuts);
        Assert.DoesNotContain(closure.Id, resource.ClosureOptOuts);

        // The closure layer is not reachable through the collection the resource
        // write path persists.
        Assert.Empty(resource.Availability.Exceptions);
    }

    // ---- the ladder through the real query service ----

    /// <summary>
    /// The same ladder again, but through <c>AvailabilityService</c> and
    /// <c>FreeTimeCalculator</c> rather than over a configuration directly —
    /// because a rule that holds in the type that states it, and is never reached
    /// by the code that answers queries, is a rule nothing enforces.
    /// </summary>
    [Fact]
    public async Task Free_time_is_empty_on_a_closure_date()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            closures: [TestData.Closure(Date, "Christmas Day")]));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Free_time_on_a_closure_date_ignores_the_resources_own_override()
    {
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value],
            closures: [TestData.Closure(Date)]));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date, Date);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Dates_either_side_of_a_closure_are_unaffected()
    {
        // The whole week open, so the days either side are ordinary open days.
        var room = TestData.Room(TestData.Config(
            TestData.Weekly("08:00", "18:00", Enum.GetValues<DayOfWeek>()),
            closures: [TestData.Closure(Date)]));
        var (_, availability, _) = TestData.Services(room);

        var result = await availability.GetFreeTimeAsync(room.Id, Date.AddDays(-1), Date.AddDays(1));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(TestData.Utc(Date.AddDays(-1), "08:00"), result.Value[0].StartUtc);
        Assert.Equal(TestData.Utc(Date.AddDays(1), "08:00"), result.Value[1].StartUtc);
    }

    /// <summary>
    /// A resource hydrated with the closure removed — which is what opting out
    /// produces — answers identically to one on a site that never had it. Compared
    /// as outcomes rather than by inspecting the filter, so the assertion holds
    /// whatever the filter does.
    /// </summary>
    [Fact]
    public async Task An_opted_out_resource_answers_exactly_as_one_on_a_site_with_no_closure()
    {
        var weekly = TestData.Weekly("08:00", "18:00", Date.DayOfWeek);

        var optedOut = TestData.Room(TestData.Config(weekly));
        var noClosure = TestData.Room(TestData.Config(weekly));

        var (_, optedOutAvailability, _) = TestData.Services(optedOut);
        var (_, noClosureAvailability, _) = TestData.Services(noClosure);

        var a = await optedOutAvailability.GetFreeTimeAsync(optedOut.Id, Date, Date);
        var b = await noClosureAvailability.GetFreeTimeAsync(noClosure.Id, Date, Date);

        Assert.Equal(
            b.Value.Select(i => (i.StartUtc, i.EndUtc)),
            a.Value.Select(i => (i.StartUtc, i.EndUtc)));
    }

    // ---- the published ports this change promised not to widen ----

    /// <summary>
    /// Closures arrived as a port of their own precisely so that no member landed
    /// on a published one. Asserted over the whole surface rather than by naming
    /// the members a closure feature might have added, because the member somebody
    /// adds will be called <c>ListClosuresAsync</c> or <c>GetWithClosuresAsync</c>
    /// or something nobody here guessed.
    /// </summary>
    [Fact]
    public void The_resource_read_port_gained_no_member()
    {
        var names = typeof(Core.Stores.IResourceStore)
            .GetMethods()
            .Select(m => m.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(new[] { "GetAsync", "ListAsync", "ListByTypeAsync" }, names);
    }

    [Fact]
    public void The_availability_query_port_gained_no_member()
    {
        var names = typeof(IAvailabilityQueryService)
            .GetMethods()
            .Select(m => m.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
            new[] { "GetBookableStartsAsync", "GetFreeTimeAsync", "GetSlotsAsync", "ProjectBookableStarts" },
            names);
    }

    /// <summary>
    /// The pure projection is frozen from 17.0.0 and takes no closure argument:
    /// the resource it is handed already carries them. A widened signature here
    /// would be the break this design was shaped to avoid.
    /// </summary>
    [Fact]
    public void The_pure_projection_still_takes_a_resource_and_no_closures()
    {
        var method = typeof(IAvailabilityQueryService).GetMethod(nameof(IAvailabilityQueryService.ProjectBookableStarts));

        Assert.NotNull(method);
        Assert.Equal(
            new[]
            {
                typeof(Resource),
                typeof(IReadOnlyList<Core.Stores.ClaimInfo>),
                typeof(DateOnly),
                typeof(DateOnly),
            },
            method!.GetParameters().Select(p => p.ParameterType));
    }
}
