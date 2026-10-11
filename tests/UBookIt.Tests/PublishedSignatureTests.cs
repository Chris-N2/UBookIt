using System.Reflection;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// Signatures published before closure-date-ranges are still present, exactly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Adding an optional parameter is a binary break.</b> The source of a caller still compiles,
/// but an assembly built against the old signature binds to it by its full parameter list and
/// fails with <see cref="MissingMethodException"/> at run time. closure-date-ranges first widened
/// all five of these in place, and QA caught it: the project had already recorded the rule —
/// "new members, not widened ones" (booking-on-behalf, design) — and broken it anyway.
/// </para>
/// <para>
/// So each is asserted by its exact parameter types. A future change that widens one turns this
/// red, which is the point: it must add an overload instead, or declare the break.
/// </para>
/// </remarks>
public class PublishedSignatureTests
{
    private static void AssertHas(Type type, string name, params Type[] parameters)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters);

        Assert.True(
            method is not null,
            $"{type.Name}.{name}({string.Join(", ", parameters.Select(p => p.Name))}) is published and must stay.");
    }

    [Fact]
    public void SiteClosure_Create_keeps_its_published_signature()
        => AssertHas(typeof(SiteClosure), nameof(SiteClosure.Create), typeof(DateOnly), typeof(string), typeof(Guid?));

    [Fact]
    public void AvailabilityConfiguration_Create_keeps_its_published_signature()
        => AssertHas(
            typeof(AvailabilityConfiguration),
            nameof(AvailabilityConfiguration.Create),
            typeof(WeeklyOpenHours),
            typeof(IEnumerable<DateException>),
            typeof(BookingConstraints),
            typeof(IEnumerable<SiteClosure>));

    [Fact]
    public void AvailableDateWindow_Compute_keeps_its_published_signature()
        => AssertHas(typeof(AvailableDateWindow), nameof(AvailableDateWindow.Compute), typeof(DateOnly), typeof(int), typeof(int));

    [Fact]
    public void BookingFormBuilder_Build_keeps_its_published_signature()
        => AssertHas(
            typeof(BookingFormBuilder),
            nameof(BookingFormBuilder.Build),
            typeof(Resource),
            typeof(DateOnly),
            typeof(DateOnly),
            typeof(IReadOnlyList<BookableStart>),
            typeof(IReadOnlyList<BookableStart>),
            typeof(TimeSpan),
            typeof(TimeZoneInfo),
            typeof(PrivacyNoticeView),
            typeof(int),
            typeof(FailedSubmission),
            typeof(string),
            typeof(IReadOnlyList<PreservedQueryPair>));

    [Fact]
    public void ServiceBookingFormBuilder_Build_keeps_its_published_signature()
        => AssertHas(
            typeof(ServiceBookingFormBuilder),
            nameof(ServiceBookingFormBuilder.Build),
            typeof(Service),
            typeof(IReadOnlyList<RoleCandidates>),
            typeof(DateOnly),
            typeof(DateOnly),
            typeof(IReadOnlyList<ServiceBookableStart>),
            typeof(IReadOnlyList<ServiceBookableStart>),
            typeof(int),
            typeof(TimeZoneInfo),
            typeof(PrivacyNoticeView),
            typeof(int),
            typeof(FailedSubmission),
            typeof(string),
            typeof(ServiceBookingFormBuilder.ResourceChoiceState),
            typeof(IReadOnlyList<PreservedQueryPair>));
}
