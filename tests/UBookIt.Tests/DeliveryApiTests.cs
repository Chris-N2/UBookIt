using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;
using UBookIt.Web.Controllers;
using UBookIt.Web.Mapping;
using UBookIt.Web.Models;

namespace UBookIt.Tests;

/// <summary>
/// Delivery API behaviour at the controller boundary: the real controllers,
/// mapper, and problem-details projection over the real Core services (backed
/// by in-memory stores). HTTP-pipeline concerns that need a host — anonymous
/// access actually flowing through middleware and the separate OpenAPI document
/// — are verified live against the running site (tasks 7.2/7.4); the auth stance
/// is pinned structurally here.
/// </summary>
public class DeliveryApiTests
{
    private static readonly DateOnly BaseDate = TestData.BaseDate;

    private sealed class Harness
    {
        public Resource Room { get; }
        public ResourcesController Resources { get; }
        public AvailabilityController Availability { get; }
        public BookingsController Bookings { get; }
        public InMemoryBookingStore BookingStore { get; }

        public Harness()
        {
            Room = TestData.Room();
            var resources = new InMemoryResourceStore().Add(Room);
            BookingStore = new InMemoryBookingStore();
            var time = new FixedTimeProvider(TestData.Now);
            var settings = TestData.Settings;

            Resources = new ResourcesController(resources, settings);
            Availability = new AvailabilityController(
                new AvailabilityService(resources, BookingStore, time, settings), settings);
            Bookings = new BookingsController(new BookingService(resources, BookingStore, time, settings));
        }

        public PlacementRequestModel ValidPlacement(int durationMinutes = 60, string? email = "test@example.com")
            => new()
            {
                ResourceId = Room.Id,
                Start = TestData.Utc(BaseDate, "09:00"),
                DurationMinutes = durationMinutes,
                Booker = new BookerModel { Name = "Test Person", Email = email },
            };
    }

    private static T Ok<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value);
    }

    private static (int Status, ApiErrorModel[] Errors) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
        return (obj.StatusCode!.Value, errors);
    }

    // --- Resource read (6.3) ---

    [Fact]
    public async Task List_returns_page_and_total()
    {
        var h = new Harness();

        var model = Ok<PagedResourcesModel>(await h.Resources.ListResources());

        Assert.Equal(1, model.Total);
        Assert.Equal(h.Room.Id, Assert.Single(model.Items).Id);
    }

    [Fact]
    public async Task Get_by_id_returns_read_model_with_zone_and_constraints()
    {
        var h = new Harness();

        var model = Ok<ResourceReadModel>(await h.Resources.GetResource(h.Room.Id));

        Assert.Equal(h.Room.Id, model.Id);
        Assert.Equal("Europe/London", model.ZoneId);
        Assert.Equal(15, model.Constraints.GranularityMinutes);
    }

    [Fact]
    public async Task Unknown_resource_is_404()
    {
        var h = new Harness();

        var (status, errors) = Problem(await h.Resources.GetResource(Guid.NewGuid()));

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(errors).Code);
    }

    // --- Availability read (6.3) ---

    [Fact]
    public async Task Free_time_carries_utc_intervals_and_zone()
    {
        var h = new Harness();

        var model = Ok<FreeTimeResponseModel>(await h.Availability.GetFreeTime(h.Room.Id, BaseDate, BaseDate));

        Assert.Equal("Europe/London", model.ZoneId);
        var interval = Assert.Single(model.Intervals);
        Assert.Equal(TestData.Utc(BaseDate, "08:00"), interval.StartUtc);
        Assert.Equal(TestData.Utc(BaseDate, "18:00"), interval.EndUtc);
    }

    [Fact]
    public async Task Slots_carry_duration_in_minutes()
    {
        var h = new Harness();

        var model = Ok<SlotsResponseModel>(await h.Availability.GetSlots(h.Room.Id, BaseDate, BaseDate, 60));

        Assert.Equal(60, model.DurationMinutes);
        Assert.NotEmpty(model.Slots);
        Assert.All(model.Slots, s => Assert.Equal(60, s.DurationMinutes));
    }

    [Fact]
    public async Task Over_wide_range_is_400_date_range_too_large()
    {
        var h = new Harness();

        var (status, errors) = Problem(await h.Availability.GetFreeTime(h.Room.Id, BaseDate, BaseDate.AddDays(31)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(errors).Code);
    }

    // --- Placement (6.3) ---

    [Fact]
    public async Task Valid_placement_returns_booking_id_and_confirmed()
    {
        var h = new Harness();

        var model = Ok<PlacementResponseModel>(await h.Bookings.PlaceBooking(h.ValidPlacement()));

        Assert.NotEqual(Guid.Empty, model.BookingId);
        Assert.Equal(nameof(BookingStatus.Confirmed), model.Status);
        Assert.Equal(h.Room.Id, model.ResourceId);
        Assert.Equal(TestData.Utc(BaseDate, "09:00"), model.Interval.StartUtc);
    }

    [Fact]
    public async Task Conflicting_placement_is_409()
    {
        var h = new Harness();
        await h.Bookings.PlaceBooking(h.ValidPlacement());

        var (status, errors) = Problem(await h.Bookings.PlaceBooking(h.ValidPlacement()));

        Assert.Equal(409, status);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Multi_rule_failure_echoes_every_code()
    {
        var h = new Harness();

        // Duration 10 is both unaligned (not a multiple of 15) and too short (< 30).
        var (status, errors) = Problem(await h.Bookings.PlaceBooking(h.ValidPlacement(durationMinutes: 10)));

        Assert.Equal(400, status);
        Assert.Contains(errors, e => e.Code == FailureCodes.Granularity);
        Assert.Contains(errors, e => e.Code == FailureCodes.DurationTooShort);
    }

    [Fact]
    public async Task Missing_email_is_400_identifying_the_field()
    {
        var h = new Harness();

        var (status, errors) = Problem(await h.Bookings.PlaceBooking(h.ValidPlacement(email: null)));

        Assert.Equal(400, status);
        var error = Assert.Single(errors);
        Assert.Equal(FailureCodes.EmailInvalid, error.Code);
        Assert.Equal("Email", error.Field);
    }

    // --- Auth stance & structural contract (6.4, 6.5) ---

    [Fact]
    public void Base_controller_is_anonymous_with_no_authorization()
    {
        var type = typeof(UBookItDeliveryApiControllerBase);

        Assert.NotNull(type.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(type.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void Route_is_the_public_versioned_path()
    {
        var route = typeof(UBookItDeliveryApiControllerBase).GetCustomAttribute<RouteAttribute>();

        Assert.NotNull(route);
        Assert.Contains("umbraco/ubookit/api", route!.Template);
        Assert.DoesNotContain("management", route.Template); // not the backoffice route
    }

    [Fact]
    public void Placement_request_exposes_no_member_key()
    {
        var names = typeof(PlacementRequestModel).GetProperties()
            .Concat(typeof(BookerModel).GetProperties())
            .Select(p => p.Name);

        Assert.DoesNotContain(names, n => n.Contains("member", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Placed_booking_has_no_member_key_inferred()
    {
        var h = new Harness();

        var model = Ok<PlacementResponseModel>(await h.Bookings.PlaceBooking(h.ValidPlacement()));
        var stored = await h.BookingStore.GetBookingAsync(model.BookingId);

        Assert.NotNull(stored);
        Assert.Null(stored!.Booker.MemberKey);
    }

    [Fact]
    public void Placement_response_is_a_view_model_not_the_aggregate()
    {
        var names = typeof(PlacementResponseModel).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(names, n => n.Equals("Claims", StringComparison.OrdinalIgnoreCase));
        Assert.False(typeof(Booking).IsAssignableFrom(typeof(PlacementResponseModel)));
    }

    [Fact]
    public void Resource_read_model_omits_management_configuration()
    {
        var names = typeof(ResourceReadModel).GetProperties()
            .Concat(typeof(ConstraintsModel).GetProperties())
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain(names, n =>
            n.Contains("open", StringComparison.OrdinalIgnoreCase)
            || n.Contains("hour", StringComparison.OrdinalIgnoreCase)
            || n.Contains("exception", StringComparison.OrdinalIgnoreCase)
            || n.Contains("window", StringComparison.OrdinalIgnoreCase));
    }

    // Transport/model-binding failures share the domain envelope (design D7).
    // The factory itself only fires in the real MVC pipeline (verified live);
    // its projection is unit-tested here.
    [Fact]
    public void Model_binding_failures_project_to_the_uniform_envelope()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("durationMinutes", "The value 'x' is not valid.");

        var (status, errors) = Problem(ApiResults.ToValidationProblemResult(modelState));

        Assert.Equal(400, status);
        var error = Assert.Single(errors);
        Assert.Equal(UBookIt.Web.Constants.InvalidRequestCode, error.Code);
        Assert.Equal("durationMinutes", error.Field);
    }
}
