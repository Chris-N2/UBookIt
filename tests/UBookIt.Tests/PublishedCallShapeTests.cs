using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// Calls a host could have written against 17.2.x, which must still COMPILE and bind to the
/// published members.
/// </summary>
/// <remarks>
/// <para>
/// <b>This guard is the compiler.</b> <see cref="PublishedSignatureTests"/> proves the old
/// signatures still exist; it cannot prove callers still bind to them. closure-date-ranges proved
/// the difference: its first fix added a <c>Create</c> overload whose second parameter was an
/// unrelated collection type, and <c>Create(hours, null)</c> and <c>Create(hours, [])</c> — both
/// valid against 17.2.x — became ambiguous (CS0121) while every test stayed green (QA round 2).
/// </para>
/// <para>
/// So each shape below is written out exactly as a host would write it. An overload that makes any
/// of them ambiguous stops this project compiling, which is the earliest and loudest place to learn
/// it. The tests also run the calls, so they assert the binding produced the published behaviour.
/// </para>
/// </remarks>
public class PublishedCallShapeTests
{
    private static readonly WeeklyOpenHours Hours = WeeklyOpenHours.Empty;

    [Fact]
    public void AvailabilityConfiguration_Create_accepts_every_published_call_shape()
    {
        var shapes = new[]
        {
            AvailabilityConfiguration.Create(Hours),
            AvailabilityConfiguration.Create(Hours, null),
            AvailabilityConfiguration.Create(Hours, []),
            AvailabilityConfiguration.Create(Hours, null, null),
            AvailabilityConfiguration.Create(Hours, null, null, null),
            AvailabilityConfiguration.Create(Hours, [], BookingConstraints.Default),
            AvailabilityConfiguration.Create(Hours, null, null, []),
        };

        Assert.All(shapes, result =>
        {
            Assert.True(result.Succeeded);
            Assert.Empty(result.Value.ClosedPeriods);
        });
    }

    [Fact]
    public void SiteClosure_Create_accepts_every_published_call_shape()
    {
        var date = new DateOnly(2026, 12, 25);

        var shapes = new[]
        {
            SiteClosure.Create(date, "Christmas Day"),
            SiteClosure.Create(date, "Christmas Day", null),
            SiteClosure.Create(date, "Christmas Day", Guid.NewGuid()),
        };

        // Bound to the published member: a single day, as it always was.
        Assert.All(shapes, result => Assert.Equal(date, result.Value.EndDate));
    }

    [Fact]
    public void AvailableDateWindow_Compute_accepts_its_published_call_shape()
    {
        var today = new DateOnly(2026, 10, 11);

        Assert.Equal(today, AvailableDateWindow.Compute(today, 90, 31).From);
    }

    [Fact]
    public void BookingFormBuilder_Build_accepts_default_for_its_tenth_argument()
    {
        // `default` as the failed submission: ambiguous if an overload's tenth parameter were a
        // value type. Run, too, to show it binds to the published, unshifted behaviour.
        var today = new DateOnly(2026, 10, 11);
        var room = TestData.Room();

        var form = BookingFormBuilder.Build(
            room, today, today, [], [], TimeSpan.FromHours(1), TestData.London,
            new PrivacyNoticeView(null, null, false), 30, default);

        Assert.Null(form.ShiftedWindowFrom);
    }

    [Fact]
    public void Service_Create_accepts_every_published_call_shape()
    {
        // service-booker-instructions gave the service a new member without touching this factory
        // (design 1). Every shape a host could write against 17.4.x still binds, and still yields a
        // service with no instructions.
        ServiceRole[] roles = [new("room", 1)];

        var shapes = new[]
        {
            Service.Create("Massage", null, roles),
            Service.Create("Massage", null, roles, null),
            Service.Create("Massage", null, roles, Guid.NewGuid()),
            Service.Create("Massage", ServiceDuration.Unbounded, [new ServiceRole("room", 1)]),
            Service.Create(name: "Massage", duration: null, roles: roles, id: null),
        };

        Assert.All(shapes, result =>
        {
            Assert.True(result.Succeeded);
            Assert.Null(result.Value.BookerInstructions);
        });
    }

    [Fact]
    public void BookingMessageComposer_accepts_every_published_constructor_shape()
    {
        // service-booker-instructions added a four-parameter constructor taking a service store.
        // Every shape written against the published one must still bind to it — `(r, null, null)`
        // above all, which a constructor taking the store SECOND would have made ambiguous.
        var resources = new InMemoryResourceStore();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<
            UBookIt.Persistence.Notifications.BookingMessageComposer>.Instance;

        var shapes = new[]
        {
            new UBookIt.Persistence.Notifications.BookingMessageComposer(resources, logger),
            new UBookIt.Persistence.Notifications.BookingMessageComposer(resources, logger, null),
            new UBookIt.Persistence.Notifications.BookingMessageComposer(resources, null!, null),
        };

        Assert.All(shapes, Assert.NotNull);
    }

    [Fact]
    public void BookingFlowViewComponent_keeps_its_published_four_argument_constructor()
    {
        // single-choice-shortcut added a five-argument constructor for the site settings. The
        // published four-argument shape must still compile and bind; the flows are never invoked
        // here, so nulls stand in for them.
        var component = new BookingFlowViewComponent(
            null!, null!, null!, new UBookIt.Web.FrontendSettings());

        Assert.NotNull(component);
    }

    // ── service-email-templates: the new members, as a host would call them ─────────────────

    [Fact]
    public void Service_template_set_members_bind_as_a_host_would_write_them()
    {
        // `null` literals and bare strings, the arguments an added overload would make ambiguous.
        var service = Service.Create("Massage", null, [new ServiceRole("room", 1)]).Value;

        Assert.Null(service.TemplateSet);
        Assert.Equal("remote", service.WithTemplateSet("Remote").Value.TemplateSet);
        Assert.Null(service.WithTemplateSet(null).Value.TemplateSet);
        Assert.Null(Service.NormalizeTemplateSet(null).Value);
        Assert.Equal("remote", Service.NormalizeTemplateSet(" remote ").Value);
        Assert.False(Service.IsValidTemplateSetName(null));
        Assert.True(Service.IsValidTemplateSetName("remote"));

        const int max = Service.TemplateSetMaxLength;
        const string field = Service.TemplateSetField;
        Assert.Equal(64, max);
        Assert.Equal("templateSet", field);
        Assert.Equal("service-template-set-invalid", UBookIt.Core.Common.FailureCodes.ServiceTemplateSetInvalid);

        // The instructions member chained either side of the set, both orders.
        Assert.Equal("remote", service.WithBookerInstructions(null).Value.WithTemplateSet("remote").Value.TemplateSet);
    }

    [Fact]
    public void The_message_models_take_the_service_id_and_set_in_an_initializer_or_not_at_all()
    {
        // A view model built by a host for its own preview or test, before and after the members
        // existed: both compile, and leaving them out means "absent".
        var before = new UBookIt.Core.Notifications.InternalMessageModel
        {
            Kind = UBookIt.Core.Notifications.BookingMessageKind.InternalPlaced,
            Status = BookingStatus.Confirmed,
            Reference = "7QX4-M2NP",
            ResourceNames = [],
            LocalStart = DateTimeOffset.UnixEpoch,
            LocalEnd = DateTimeOffset.UnixEpoch,
            TimeZoneId = "Europe/London",
            AwaitsApproval = false,
        };
        var after = before with { ServiceId = null, TemplateSet = null };
        var withBoth = before with { ServiceId = Guid.Empty, TemplateSet = "remote" };

        Assert.Null(before.ServiceId);
        Assert.Null(before.TemplateSet);
        Assert.Equal(before, after);
        Assert.Equal("remote", withBoth.TemplateSet);
    }

    [Fact]
    public void The_renderers_set_members_bind_as_a_host_would_write_them()
    {
        // Method groups, the shape an overload would make ambiguous for a delegate conversion,
        // and the published members beside them, unchanged.
        Func<string, UBookIt.Core.Notifications.BookingMessageKind, string> setPath =
            UBookIt.Web.Emails.RazorBookingTemplateRenderer.SetPathFor;
        Func<UBookIt.Core.Notifications.BookingMessageKind, string> path =
            UBookIt.Web.Emails.RazorBookingTemplateRenderer.PathFor;

        Assert.Equal(
            "~/Views/Partials/UBookIt/Emails/remote/BookerPlaced.cshtml",
            setPath("remote", UBookIt.Core.Notifications.BookingMessageKind.BookerPlaced));
        Assert.Equal(
            "~/Views/Partials/UBookIt/Emails/BookerPlaced.cshtml",
            path(UBookIt.Core.Notifications.BookingMessageKind.BookerPlaced));
    }

    /// <summary>
    /// <b>Exactly one member of each published name.</b> service-email-templates gave the renderer
    /// <c>SetPathFor</c> and <c>IsSuppliedInSet</c> as NEW NAMES rather than overloads of
    /// <c>PathFor</c> and <c>IsSupplied</c> (design 4), because an overload has broken source
    /// compatibility here once already (2026-10-11). An overload that happens not to be ambiguous
    /// for the shapes above is still a second meaning for one name; this turns it red at the moment
    /// it is added, rather than at the first ambiguous call a host writes.
    /// </summary>
    [Theory]
    [InlineData(typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer), "PathFor")]
    [InlineData(typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer), "SetPathFor")]
    [InlineData(typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer), "IsSupplied")]
    [InlineData(typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer), "IsSuppliedInSet")]
    [InlineData(typeof(UBookIt.Web.Emails.RazorBookingTemplateRenderer), "RenderAsync")]
    [InlineData(typeof(UBookIt.Core.Notifications.IBookingTemplateRenderer), "RenderAsync")]
    [InlineData(typeof(UBookIt.Web.Emails.UBookItEmailTemplateBootCheck), "Run")]
    [InlineData(typeof(UBookIt.Web.Emails.UBookItEmailTemplateSetBootCheck), "RunAsync")]
    [InlineData(typeof(Service), "Create")]
    [InlineData(typeof(Service), "WithTemplateSet")]
    [InlineData(typeof(Service), "NormalizeTemplateSet")]
    [InlineData(typeof(Service), "IsValidTemplateSetName")]
    [InlineData(typeof(Service), "WithBookerInstructions")]
    public void Each_published_name_has_exactly_one_overload(Type type, string name)
        => Assert.Single(
            type.GetMethods(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.DeclaredOnly),
            m => m.Name == name);
}
