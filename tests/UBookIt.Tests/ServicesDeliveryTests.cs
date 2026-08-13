using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Controllers;
using UBookIt.Web.Models;

namespace UBookIt.Tests;

/// <summary>
/// The service delivery endpoints at the controller boundary: the real
/// controller, mapper, and problem-details projection over real Core services
/// backed by in-memory stores (delivery-api spec, service requirements).
/// </summary>
public class ServicesDeliveryTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static TimeSpan Mins(int minutes) => TimeSpan.FromMinutes(minutes);

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private sealed class Harness
    {
        public required Service Service { get; init; }

        public required ServicesController Controller { get; init; }

        public required InMemoryBookingStore Store { get; init; }

        public required IBookingService Bookings { get; init; }
    }

    private static Resource Room(int id, int granularity = 30, int min = 30, int max = 480, string type = ResourceTypes.Room)
        => Resource.Create(
            type,
            $"Resource {id}",
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    private static Harness Wire(ServiceDuration? duration = null, params Resource[] resources)
    {
        var service = Service.Create("Consultation", duration, [new ServiceRole(ResourceTypes.Room, 1)]).Value;

        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources.Length > 0 ? resources : [Room(1)])
        {
            resourceStore.Add(resource);
        }

        var serviceStore = new InMemoryServiceStore().Add(service);
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = TestData.Settings;

        var availability = new AvailabilityService(resourceStore, bookingStore, time, settings);
        var bookings = new BookingService(resourceStore, bookingStore, time, settings);
        var serviceBooking = new ServiceBookingService(
            serviceStore, resourceStore, bookingStore, availability, bookings, settings);

        return new Harness
        {
            Service = service,
            Controller = new ServicesController(serviceStore, serviceBooking, settings),
            Store = bookingStore,
            Bookings = bookings,
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

        Assert.False(string.IsNullOrWhiteSpace(problem.Type));

        return (obj.StatusCode!.Value, errors);
    }

    private static ServicePlacementRequestModel Placement(
        string start = "09:00", int? durationMinutes = 60, Guid? preferred = null)
        => new()
        {
            Start = TestData.Utc(Date, start),
            DurationMinutes = durationMinutes,
            PreferredResourceId = preferred,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        };

    // --- service read ---

    [Fact]
    public async Task List_returns_page_and_total()
    {
        var h = Wire();

        var model = Ok<PagedServicesModel>(await h.Controller.ListServices());

        Assert.Equal(1, model.Total);
        Assert.Equal(h.Service.Id, Assert.Single(model.Items).Id);
    }

    [Fact]
    public async Task Get_by_id_returns_the_public_model()
    {
        var h = Wire();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.Equal(h.Service.Id, model.Id);
        Assert.Equal("Consultation", model.Name);
        Assert.Equal(ResourceTypes.Room, model.ResourceType);
    }

    [Fact]
    public async Task Spec_scenario_fixed_and_variable_durations_are_distinguishable()
    {
        var fixedHarness = Wire(ServiceDuration.Fixed(Mins(60)).Value);
        var fixedDuration = Ok<ServiceReadModel>(
            await fixedHarness.Controller.GetService(fixedHarness.Service.Id));

        Assert.Equal("fixed", fixedDuration.Duration.Kind);
        Assert.Equal(60, fixedDuration.Duration.DurationMinutes);
        Assert.Null(fixedDuration.Duration.MinDurationMinutes);

        var unbounded = Wire();
        var variable = Ok<ServiceReadModel>(await unbounded.Controller.GetService(unbounded.Service.Id));

        Assert.Equal("variable", variable.Duration.Kind);
        Assert.Null(variable.Duration.DurationMinutes);
        Assert.Null(variable.Duration.MinDurationMinutes);
        Assert.Null(variable.Duration.MaxDurationMinutes);
    }

    [Fact]
    public async Task Spec_scenario_unknown_service_id_on_read_is_404()
    {
        var h = Wire();

        var (status, errors) = Problem(await h.Controller.GetService(Guid.NewGuid()));

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ServiceNotFound, Assert.Single(errors).Code);
    }

    // --- bookable starts ---

    [Fact]
    public async Task Bookable_starts_carry_runs_and_the_zone()
    {
        var h = Wire();

        var model = Ok<ServiceBookableStartsResponseModel>(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date));

        Assert.Equal("Europe/London", model.ZoneId);
        Assert.Equal(h.Service.Id, model.ServiceId);
        Assert.NotEmpty(model.Starts);
        Assert.All(model.Starts, s => Assert.NotEmpty(s.Runs));
    }

    [Fact]
    public async Task Spec_scenario_heterogeneous_pool_yields_multiple_runs()
    {
        var h = Wire(null, Room(1, granularity: 30, min: 30, max: 90), Room(2, granularity: 20, min: 20, max: 120));

        var model = Ok<ServiceBookableStartsResponseModel>(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date));

        var nine = Assert.Single(model.Starts, s => s.StartUtc == TestData.Utc(Date, "09:00"));

        Assert.Equal(2, nine.Runs.Count);
        Assert.Contains(nine.Runs, r => r.StepMinutes == 30);
        Assert.Contains(nine.Runs, r => r.StepMinutes == 20);
    }

    [Fact]
    public void Spec_scenario_response_names_no_resource()
    {
        var carriers = new[] { typeof(ServiceBookableStartsResponseModel), typeof(ServiceBookableStartModel), typeof(LengthRunModel) };

        Assert.All(carriers, type => Assert.DoesNotContain(
            type.GetProperties(),
            p => p.Name.Contains("Resource", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Spec_scenario_over_wide_range_is_rejected()
    {
        var h = Wire();

        var (status, errors) = Problem(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date.AddYears(2)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_inverted_range_is_rejected()
    {
        var h = Wire();

        var (status, errors) = Problem(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date.AddDays(-1)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_unknown_service_on_availability_is_404()
    {
        var h = Wire();

        var (status, errors) = Problem(
            await h.Controller.GetServiceBookableStarts(Guid.NewGuid(), Date, Date));

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ServiceNotFound, Assert.Single(errors).Code);
    }

    // --- placement ---

    [Fact]
    public async Task Spec_scenario_valid_service_placement_succeeds()
    {
        var h = Wire();

        var model = Ok<PlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        Assert.Equal("Confirmed", model.Status);
        Assert.NotEqual(Guid.Empty, model.BookingId);
        Assert.Equal(TestData.Utc(Date, "09:00"), model.Interval.StartUtc);
    }

    [Fact]
    public async Task Spec_scenario_the_resolved_resource_is_reported()
    {
        var h = Wire(null, Room(1), Room(2));

        await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var model = Ok<PlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        Assert.Equal(Id(2), model.ResourceId);
    }

    [Fact]
    public void Spec_scenario_requested_length_is_required()
    {
        // The model, not the controller, is what rejects an omitted length: it is
        // nullable-and-[Required] so binding fails with the field named, rather
        // than defaulting to zero and surfacing as some unrelated duration error.
        var model = Placement(durationMinutes: null);
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ServicePlacementRequestModel.DurationMinutes)));
    }

    [Fact]
    public async Task Spec_scenario_an_unpermitted_length_is_rejected_not_substituted()
    {
        var h = Wire(ServiceDuration.Fixed(Mins(60)).Value);

        var (status, errors) = Problem(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(durationMinutes: 90)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DurationTooLong, Assert.Single(errors).Code);

        Assert.Empty(await h.Store.GetClaimsAsync(
            [Id(1)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_preferred_resource_is_optional()
    {
        var h = Wire(null, Room(1), Room(2));

        var model = Ok<PlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(preferred: null)));

        Assert.Equal(Id(1), model.ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_ineligible_preferred_resource_is_rejected()
    {
        var h = Wire(null, Room(1), Room(9, type: "therapist"));

        var (status, errors) = Problem(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(preferred: Id(9))));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.ResourceNotEligible, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_unknown_service_on_placement_is_404()
    {
        var h = Wire();

        var (status, errors) = Problem(await h.Controller.PlaceServiceBooking(Guid.NewGuid(), Placement()));

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ServiceNotFound, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_service_unavailable_maps_to_400()
    {
        var h = Wire();

        // 20:00 is outside the only candidate's open hours: deterministic.
        var (status, errors) = Problem(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(start: "20:00")));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.ServiceUnavailable, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Conflict_maps_to_409()
    {
        var h = Wire();

        await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(60),
            Booker = TestData.Booker(),
        });

        var (status, errors) = Problem(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        Assert.Equal(409, status);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(errors).Code);
    }

    [Fact]
    public void Spec_scenario_request_model_carries_no_member_key()
    {
        Assert.DoesNotContain(
            typeof(ServicePlacementRequestModel).GetProperties().Concat(typeof(BookerModel).GetProperties()),
            p => p.Name.Contains("Member", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Spec_scenario_service_placement_has_its_own_model()
    {
        // Not optional service fields bolted onto the direct placement model:
        // that would admit combinations with no meaning.
        Assert.DoesNotContain(
            typeof(PlacementRequestModel).GetProperties(),
            p => p.Name.Contains("Service", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Preferred", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(ServicePlacementRequestModel).GetProperties(),
            p => p.Name == nameof(PlacementRequestModel.ResourceId));
    }
}
