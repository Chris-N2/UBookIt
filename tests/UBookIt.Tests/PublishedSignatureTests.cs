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
    public void BookingFlowViewComponent_keeps_its_published_constructor()
    {
        // single-choice-shortcut added a constructor taking the site settings; the published one
        // stays, by exact parameter types, so an assembly built against it still binds.
        var constructor = typeof(BookingFlowViewComponent).GetConstructor(
            [typeof(ResourceBookingFlow), typeof(ServiceBookingFlow), typeof(BookingCatalogue), typeof(UBookIt.Web.FrontendSettings)]);

        Assert.True(constructor is { IsPublic: true }, "BookingFlowViewComponent's four-argument constructor is published and must stay.");
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

    [Fact]
    public void BookingMessageComposer_keeps_its_published_constructor()
    {
        // service-booker-instructions gave the composer a service store through a NEW constructor;
        // the published one stays, by exact parameter types.
        var constructor = typeof(UBookIt.Persistence.Notifications.BookingMessageComposer).GetConstructor(
        [
            typeof(UBookIt.Core.Stores.IResourceStore),
            typeof(Microsoft.Extensions.Logging.ILogger<UBookIt.Persistence.Notifications.BookingMessageComposer>),
            typeof(UBookIt.Core.Notifications.IBookingTemplateRenderer),
        ]);

        Assert.True(constructor is { IsPublic: true }, "BookingMessageComposer's three-parameter constructor is published and must stay.");
    }

    [Fact]
    public void The_renderer_seam_keeps_its_published_signatures()
    {
        // service-email-templates carried the set on the MODEL, so none of these widened (design 1).
        var kind = typeof(UBookIt.Core.Notifications.BookingMessageKind);
        var model = typeof(UBookIt.Core.Notifications.BookingMessageModel);
        var renderer = typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer);
        var instance = BindingFlags.Public | BindingFlags.Instance;

        var port = typeof(UBookIt.Core.Notifications.IBookingTemplateRenderer)
            .GetMethod("RenderAsync", instance, [kind, model, typeof(CancellationToken)]);
        Assert.Equal(typeof(Task<UBookIt.Core.Notifications.BookingTemplateResult>), port?.ReturnType);

        AssertHas(renderer, "PathFor", kind);
        Assert.Equal(typeof(string), renderer.GetMethod("PathFor", [kind])!.ReturnType);
        Assert.Equal(typeof(bool), renderer.GetMethod("IsSupplied", instance, [kind])?.ReturnType);

        var run = typeof(UBookIt.Web.Emails.UBookItEmailTemplateBootCheck).GetMethod("Run", instance, Type.EmptyTypes);
        Assert.Equal(typeof(IReadOnlyList<(UBookIt.Core.Notifications.BookingMessageKind, bool)>), run?.ReturnType);
    }

    [Fact]
    public void Service_Create_keeps_its_published_signature()
        // service-booker-instructions added the instructions through `WithBookerInstructions`
        // precisely so that this signature did not widen (design 1).
        => AssertHas(
            typeof(Service),
            nameof(Service.Create),
            typeof(string),
            typeof(ServiceDuration),
            typeof(IEnumerable<ServiceRole>),
            typeof(Guid?));
}
