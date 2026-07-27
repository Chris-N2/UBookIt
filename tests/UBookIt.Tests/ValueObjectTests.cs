using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class BookingIntervalTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void End_before_start_is_interval_invalid()
    {
        var result = BookingInterval.Create(T0, T0.AddHours(-1), TestData.LondonZoneId);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Zero_length_is_interval_invalid()
    {
        var result = BookingInterval.Create(T0, T0, TestData.LondonZoneId);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.IntervalInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Missing_zone_id_is_rejected()
    {
        var result = BookingInterval.Create(T0, T0.AddHours(1), "  ");

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.TimeZoneInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Instants_are_normalized_to_utc()
    {
        var offsetStart = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(1));

        var interval = BookingInterval.Create(offsetStart, offsetStart.AddHours(1), TestData.LondonZoneId).Value;

        Assert.Equal(TimeSpan.Zero, interval.StartUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero), interval.StartUtc);
    }

    [Fact]
    public void Overlap_is_half_open()
    {
        var a = BookingInterval.Create(T0, T0.AddHours(1), TestData.LondonZoneId).Value;
        var touching = BookingInterval.Create(T0.AddHours(1), T0.AddHours(2), TestData.LondonZoneId).Value;
        var overlapping = BookingInterval.Create(T0.AddMinutes(59), T0.AddHours(2), TestData.LondonZoneId).Value;

        Assert.False(a.Overlaps(touching));
        Assert.True(a.Overlaps(overlapping));
    }
}

public class OpenHoursTests
{
    [Fact]
    public void Window_with_start_not_before_end_is_rejected()
    {
        var result = DayWindow.Create(new TimeOnly(12, 0), new TimeOnly(12, 0));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.WindowInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Overlapping_windows_on_a_day_are_rejected()
    {
        var result = WeeklyOpenHours.Create(
        [
            (DayOfWeek.Monday, TestData.Win("08:00", "12:00")),
            (DayOfWeek.Monday, TestData.Win("11:00", "14:00")),
        ]);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.WindowsOverlap, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Touching_windows_are_allowed()
    {
        var result = WeeklyOpenHours.Create(
        [
            (DayOfWeek.Monday, TestData.Win("08:00", "12:00")),
            (DayOfWeek.Monday, TestData.Win("12:00", "14:00")),
        ]);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Day_without_windows_is_closed()
    {
        var hours = TestData.Weekly("08:00", "18:00", DayOfWeek.Monday);

        Assert.Empty(hours.WindowsFor(DayOfWeek.Sunday));
    }

    [Fact]
    public void Override_exception_with_overlapping_windows_is_rejected()
    {
        var result = DateException.Override(
            TestData.BaseDate, [TestData.Win("08:00", "12:00"), TestData.Win("11:00", "13:00")]);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.WindowsOverlap, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Closure_exception_has_no_windows()
    {
        var closure = DateException.Closure(TestData.BaseDate);

        Assert.True(closure.IsClosure);
        Assert.Empty(closure.Windows);
    }

    [Fact]
    public void Duplicate_exception_dates_are_rejected()
    {
        var result = AvailabilityConfiguration.Create(
            WeeklyOpenHours.Empty,
            [DateException.Closure(TestData.BaseDate), DateException.Closure(TestData.BaseDate)]);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DuplicateExceptionDate, Assert.Single(result.Failures).Code);
    }
}

public class BookingConstraintsTests
{
    [Fact]
    public void Defaults_match_the_spec()
    {
        var defaults = BookingConstraints.Default;

        Assert.Equal(TimeSpan.FromMinutes(15), defaults.Granularity);
        Assert.Equal(TimeSpan.FromMinutes(30), defaults.MinDuration);
        Assert.Equal(TimeSpan.FromHours(8), defaults.MaxDuration);
        Assert.Equal(TimeSpan.Zero, defaults.LeadTime);
        Assert.Equal(90, defaults.HorizonDays);
    }

    [Fact]
    public void Resource_without_explicit_constraints_gets_defaults()
    {
        var resource = TestData.Room();

        Assert.Equal(BookingConstraints.Default, resource.Availability.Constraints);
    }

    [Fact]
    public void Min_duration_exceeding_max_is_incoherent()
    {
        var result = BookingConstraints.Create(
            minDuration: TimeSpan.FromMinutes(60), maxDuration: TimeSpan.FromMinutes(30));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ConstraintsIncoherent, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Durations_not_multiples_of_granularity_are_incoherent()
    {
        var result = BookingConstraints.Create(minDuration: TimeSpan.FromMinutes(20));

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.ConstraintsIncoherent, Assert.Single(result.Failures).Code);
    }
}

public class BookerTests
{
    [Fact]
    public void External_booker_without_member_key_is_valid()
    {
        var result = Booker.Create(null, "Jo Visitor", "jo@example.org");

        Assert.True(result.Succeeded);
        Assert.Null(result.Value.MemberKey);
    }

    [Fact]
    public void Missing_email_is_rejected_identifying_the_field()
    {
        var result = Booker.Create(null, "Jo Visitor", null);

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.EmailInvalid, failure.Code);
        Assert.Equal("Email", failure.Field);
    }

    [Fact]
    public void Malformed_email_is_rejected()
    {
        var result = Booker.Create(null, "Jo Visitor", "not-an-email");

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.EmailInvalid, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Missing_name_is_rejected()
    {
        var result = Booker.Create(null, "  ", "jo@example.org");

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.NameRequired, Assert.Single(result.Failures).Code);
    }
}

public class ResourceTests
{
    [Fact]
    public void Valid_room_resource_is_created()
    {
        var result = Resource.Create(ResourceTypes.Room, "Meeting Room A");

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("room", result.Value.Type);
    }

    [Fact]
    public void Empty_display_name_is_rejected()
    {
        var result = Resource.Create(ResourceTypes.Room, "   ");

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.DisplayNameRequired, Assert.Single(result.Failures).Code);
    }

    [Fact]
    public void Non_normalized_type_key_is_rejected()
    {
        var result = Resource.Create("Meeting Room", "Meeting Room A");

        Assert.False(result.Succeeded);
        var failure = Assert.Single(result.Failures);
        Assert.Equal(FailureCodes.TypeKeyInvalid, failure.Code);
    }

    [Fact]
    public void Unknown_but_well_formed_type_key_is_accepted()
    {
        var result = Resource.Create("person", "Dr Sam Smith");

        Assert.True(result.Succeeded);
        Assert.Equal("person", result.Value.Type);
    }
}
