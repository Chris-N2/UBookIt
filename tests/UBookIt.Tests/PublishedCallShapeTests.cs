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
}
