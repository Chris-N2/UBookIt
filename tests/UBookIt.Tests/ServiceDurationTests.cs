using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Services;

namespace UBookIt.Tests;

/// <summary>
/// The `ServiceDuration` value object: construction and validation of both
/// kinds, and narrow-only resolution against a resource's constraints
/// (services spec, "Service duration semantics").
/// </summary>
public class ServiceDurationTests
{
    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static BookingConstraints Constraints(int granularity, int min, int max)
        => BookingConstraints.Create(
            granularity: Mins(granularity), minDuration: Mins(min), maxDuration: Mins(max)).Value;

    // ---- Construction and validation (task 1.5) ----

    [Fact]
    public void Fixed_duration_is_a_degenerate_range()
    {
        var result = ServiceDuration.Fixed(Mins(60));

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceDurationKind.Fixed, result.Value.Kind);
        Assert.Equal(Mins(60), result.Value.FixedLength);
        Assert.Equal(Mins(60), result.Value.Min);
        Assert.Equal(Mins(60), result.Value.Max);
    }

    [Fact]
    public void Variable_duration_carries_its_bounds()
    {
        var result = ServiceDuration.Variable(Mins(45), Mins(120));

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceDurationKind.Variable, result.Value.Kind);
        Assert.Equal(Mins(45), result.Value.Min);
        Assert.Equal(Mins(120), result.Value.Max);
        Assert.Null(result.Value.FixedLength);
    }

    [Fact]
    public void Unbounded_variable_has_no_bounds()
    {
        var result = ServiceDuration.Variable(null, null);

        Assert.True(result.Succeeded);
        Assert.Equal(ServiceDurationKind.Variable, result.Value.Kind);
        Assert.Null(result.Value.Min);
        Assert.Null(result.Value.Max);
        Assert.Equal(ServiceDuration.Unbounded, result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Non_positive_fixed_length_is_rejected(int minutes)
    {
        var result = ServiceDuration.Fixed(Mins(minutes));

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.ServiceDurationInvalid, failure.Code);
        Assert.Equal(ServiceDuration.LengthField, failure.Field);
    }

    [Fact]
    public void Sub_minute_fixed_length_is_rejected()
    {
        // Durations persist and round-trip as whole minutes; 90 seconds must
        // not be silently truncated to 1 minute.
        var result = ServiceDuration.Fixed(TimeSpan.FromSeconds(90));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceDurationInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Non_positive_variable_bounds_are_rejected(int minutes)
    {
        var min = ServiceDuration.Variable(Mins(minutes), null);
        var max = ServiceDuration.Variable(null, Mins(minutes));

        Assert.False(min.Succeeded);
        Assert.Equal(ServiceDuration.MinField, Assert.Single(min.Failures).Field);
        Assert.False(max.Succeeded);
        Assert.Equal(ServiceDuration.MaxField, Assert.Single(max.Failures).Field);
    }

    [Fact]
    public void Sub_minute_variable_bound_is_rejected()
    {
        var result = ServiceDuration.Variable(TimeSpan.FromSeconds(90), null);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.ServiceDurationInvalid);
    }

    [Fact]
    public void Inverted_bounds_are_rejected_against_the_offending_input()
    {
        var result = ServiceDuration.Variable(Mins(120), Mins(45));

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.ServiceDurationInvalid, failure.Code);
        Assert.Equal(ServiceDuration.MinField, failure.Field);
    }

    [Fact]
    public void Equal_bounds_are_permitted()
    {
        // A variable duration pinned to one length is legitimate — it is not
        // the same object as a fixed duration, but it is not incoherent.
        var result = ServiceDuration.Variable(Mins(60), Mins(60));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void An_unsound_bound_does_not_also_report_inversion()
    {
        // Reporting "minimum exceeds maximum" for a negative bound would be
        // noise; the caller should see one actionable message per input.
        var result = ServiceDuration.Variable(Mins(-30), Mins(45));

        Assert.False(result.Succeeded);
        Assert.Single(result.Failures);
    }

    // ---- Narrow-only resolution (task 1.6) ----

    [Fact]
    public void Spec_scenario_service_bounds_narrow_the_resource_range()
    {
        var duration = ServiceDuration.Variable(Mins(45), Mins(180)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out var range);

        Assert.True(resolved);
        Assert.Equal(Mins(45), range.Min);
        Assert.Equal(Mins(120), range.Max);
    }

    [Fact]
    public void Spec_scenario_a_resource_maximum_cannot_be_exceeded()
    {
        var duration = ServiceDuration.Fixed(Mins(90)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 60), out _);

        Assert.False(resolved);
    }

    [Fact]
    public void Spec_scenario_unbounded_variable_defers_entirely_to_the_resource()
    {
        var resolved = ServiceDuration.Unbounded.TryResolveAgainst(Constraints(15, 30, 480), out var range);

        Assert.True(resolved);
        Assert.Equal(Mins(30), range.Min);
        Assert.Equal(Mins(480), range.Max);
    }

    [Fact]
    public void Spec_scenario_bounds_need_not_align_to_granularity()
    {
        // A 40-minute minimum on a 15-minute grid means the shortest bookable
        // length is 45 — the bound moves inward, never outward.
        var duration = ServiceDuration.Variable(Mins(40), null).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out var range);

        Assert.True(resolved);
        Assert.Equal(Mins(45), range.Min);
        Assert.Equal(Mins(120), range.Max);
    }

    [Fact]
    public void A_maximum_bound_is_floored_to_the_grid()
    {
        var duration = ServiceDuration.Variable(null, Mins(100)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(30, 30, 480), out var range);

        Assert.True(resolved);
        Assert.Equal(Mins(90), range.Max);
    }

    [Fact]
    public void A_fixed_length_off_the_grid_cannot_be_fulfilled()
    {
        // 40 minutes is unbookable on a 15-minute grid: rounding the single
        // length inward from both sides leaves an empty range.
        var duration = ServiceDuration.Fixed(Mins(40)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_service_minimum_above_the_resource_maximum_cannot_be_fulfilled()
    {
        var duration = ServiceDuration.Variable(Mins(180), null).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_service_maximum_below_the_resource_minimum_cannot_be_fulfilled()
    {
        var duration = ServiceDuration.Variable(null, Mins(20)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out _);

        Assert.False(resolved);
    }

    [Fact]
    public void A_fixed_length_matching_the_resource_exactly_resolves_to_one_length()
    {
        var duration = ServiceDuration.Fixed(Mins(60)).Value;

        var resolved = duration.TryResolveAgainst(Constraints(15, 30, 120), out var range);

        Assert.True(resolved);
        Assert.True(range.IsSingleLength);
        Assert.Equal(Mins(60), range.Min);
    }
}
