using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
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
            // Offered for direct booking. The domain default is the opposite;
            // these fixtures stand for ordinary bookable resources, and the
            // permission itself is exercised explicitly in DirectBookingTests.
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: Mins(granularity),
                    minDuration: Mins(min),
                    maxDuration: Mins(max)).Value),
            id: Id(id)).Value;

    /// <summary>
    /// A two-role service — a `room` requiring `projector` and a `therapist`
    /// requiring nothing — with one eligible resource of each type.
    /// </summary>
    /// <summary>A service whose single role requires two distinct resources.</summary>
    private static Harness WireCounted()
        => WireService(Service.Create("Workshop", null, [new ServiceRole(ResourceTypes.Room, 2)]).Value);

    /// <summary>
    /// Two roles of one resource type, told apart only by their required
    /// capabilities — the composition that makes the published order matter.
    /// </summary>
    private static Harness WireSameType()
        => WireService(Service.Create(
            "Joint session",
            null,
            [
                new ServiceRole("therapist", 1)
                {
                    RequiredCapabilities = CapabilitySet.Create(["cert-x"]).Value,
                },
                new ServiceRole("therapist", 1),
            ]).Value);

    private static Harness WireMultiRole()
    {
        var service = Service.Create(
            "Massage",
            null,
            [
                new ServiceRole(ResourceTypes.Room, 1)
                {
                    RequiredCapabilities = CapabilitySet.Create(["projector"]).Value,
                },
                new ServiceRole("therapist", 1),
            ]).Value;

        // The therapist deliberately holds the LOWER id, so resource-id order
        // and role order disagree. With them agreeing, a response sorted by id
        // and a response in role order are indistinguishable, and any assertion
        // about which is which passes either way.
        var room = Resource.Create(
            ResourceTypes.Room,
            "Red Room",
            directlyBookable: true,
            capabilities: ["projector"],
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(2)).Value;

        var therapist = Resource.Create(
            "therapist",
            "Mary",
            directlyBookable: true,
            availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
            id: Id(1)).Value;

        return WireService(service, room, therapist);
    }

    private static Harness Wire(ServiceDuration? duration = null, params Resource[] resources)
        => WireService(
            Service.Create("Consultation", duration, [new ServiceRole(ResourceTypes.Room, 1)]).Value,
            resources.Length > 0 ? resources : [Room(1)]);

    private static Harness WireService(Service service, params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();

        foreach (var resource in resources)
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
        string start = "09:00", int? durationMinutes = 60, Guid? pinned = null)
        => new()
        {
            Start = TestData.Utc(Date, start),
            DurationMinutes = durationMinutes,
            PinnedResourceId = pinned,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        };

    [Fact]
    public async Task Spec_scenario_service_placement_on_a_withholding_resource_succeeds()
    {
        // The other half of the direct-booking permission, at the HTTP boundary
        // rather than only in Core: a resource nobody may book on its own is
        // booked through a service that resolves to it, and the request succeeds.
        //
        // Built explicitly rather than through the shared Room helper, because the
        // helper grants the permission and this test is about a resource that does
        // not — with the helper it would prove nothing.
        var withholding = Resource.Create(
            ResourceTypes.Room,
            "Withholding Room",
            directlyBookable: false,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(480)).Value),
            id: new Guid("00000000-0000-0000-0000-0000000000aa")).Value;

        var h = WireService(
            Service.Create("Consultation", null, [new ServiceRole(ResourceTypes.Room, 1)]).Value,
            withholding);

        var placed = Ok<ServicePlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        Assert.Equal([withholding.Id], placed.Resources.Select(r => r.ResourceId));
    }

    [Fact]
    public async Task Spec_scenario_a_pin_that_cannot_be_honoured_is_reported_not_substituted()
    {
        // At the HTTP boundary: the pinned room is busy, the other is free, so the
        // old behaviour would have returned 200 with a resource the caller never
        // asked for.
        var h = Wire(null, Room(1), Room(2));

        Assert.True((await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = new Guid("00000000-0000-0000-0000-000000000002"),
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var (status, errors) = Problem(await h.Controller.PlaceServiceBooking(
            h.Service.Id,
            Placement(pinned: new Guid("00000000-0000-0000-0000-000000000002"))));

        // 400 through the existing catch-all, asserted rather than assumed — that
        // mapping requirement is not modified, so this is the only thing holding it.
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(FailureCodes.PinnedResourceUnavailable, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_a_pin_failure_is_distinguishable_from_a_conflict()
    {
        // The reason the code exists: a front end must be able to tell "the person
        // you chose is not free" from "nothing could be booked", because only the
        // first has a useful next step.
        var pinnedBusy = Wire(null, Room(1), Room(2));
        Assert.True((await pinnedBusy.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = new Guid("00000000-0000-0000-0000-000000000002"),
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var (_, pinFailure) = Problem(await pinnedBusy.Controller.PlaceServiceBooking(
            pinnedBusy.Service.Id,
            Placement(pinned: new Guid("00000000-0000-0000-0000-000000000002"))));

        // The same service with its ONLY room busy: nothing could be booked.
        var allBusy = Wire(null, Room(1));
        Assert.True((await allBusy.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = new Guid("00000000-0000-0000-0000-000000000001"),
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var (_, poolFailure) = Problem(await allBusy.Controller.PlaceServiceBooking(
            allBusy.Service.Id, Placement()));

        Assert.NotEqual(Assert.Single(poolFailure).Code, Assert.Single(pinFailure).Code);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(poolFailure).Code);
    }

    [Fact]
    public async Task Spec_scenario_nothing_bookable_at_all_is_not_reported_as_the_pins_failure()
    {
        // Design D4a at the boundary. The pinned room is busy AND it is the only
        // room, so no assignment exists with or without the pin. Blaming the pin
        // would be true and misleading: it invites the front end to offer the
        // resources that were free, and there are none.
        var h = Wire(null, Room(1));

        Assert.True((await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = new Guid("00000000-0000-0000-0000-000000000001"),
            Start = TestData.Utc(Date, "09:00"),
            Duration = TimeSpan.FromMinutes(60),
            Booker = TestData.Booker(),
        })).Succeeded);

        var (status, errors) = Problem(await h.Controller.PlaceServiceBooking(
            h.Service.Id,
            Placement(pinned: new Guid("00000000-0000-0000-0000-000000000001"))));

        Assert.Equal(StatusCodes.Status409Conflict, status);
        Assert.Equal(FailureCodes.Conflict, Assert.Single(errors).Code);
    }

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
        Assert.Equal(ResourceTypes.Room, Assert.Single(model.Roles).ResourceType);
    }

    [Fact]
    public async Task Spec_scenario_a_single_role_service_still_publishes_a_collection()
    {
        // A consumer written against this contract must not need changing when a
        // service gains a role, so the single-role case is a collection of one
        // rather than an inline role.
        var h = Wire();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.Single(model.Roles);
        Assert.DoesNotContain(
            typeof(ServiceReadModel).GetProperties(),
            p => p.Name is "ResourceType" or "RequiredCapabilities");
    }

    [Fact]
    public async Task Spec_scenario_a_multi_role_service_publishes_every_role()
    {
        var h = WireMultiRole();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.Equal([ResourceTypes.Room, "therapist"], model.Roles.Select(r => r.ResourceType));
        Assert.Equal(["projector"], model.Roles[0].RequiredCapabilities);

        // A role requiring none carries an empty collection, never a null or an
        // absent member.
        Assert.NotNull(model.Roles[1].RequiredCapabilities);
        Assert.Empty(model.Roles[1].RequiredCapabilities);
    }

    [Fact]
    public async Task Spec_scenario_a_roles_count_is_published()
    {
        var h = WireCounted();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.Equal(2, Assert.Single(model.Roles).Count);
    }

    [Fact]
    public async Task Spec_scenario_a_count_of_one_is_stated_rather_than_omitted()
    {
        // A consumer must never have to read an absent count as a default. The
        // wire value is asserted, not the property's initializer — a mapper that
        // forgot to set it would still read 1 from the model's own default, so the
        // assertion is paired with the count-2 case above, which that mapper fails.
        var h = WireMultiRole();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.All(model.Roles, role => Assert.Equal(1, role.Count));
    }

    [Fact]
    public async Task Spec_scenario_two_roles_of_one_type_are_published_separately_and_stably()
    {
        // Type alone no longer distinguishes two roles, so the deterministic order
        // the contract promises has to survive them sharing one. Read twice,
        // because a single read cannot tell a stable order from a lucky one.
        var h = WireSameType();

        var first = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));
        var second = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.Equal(["therapist", "therapist"], first.Roles.Select(r => r.ResourceType));

        // Distinguishable only by what they require — which is the point.
        Assert.Equal([[], ["cert-x"]], first.Roles.Select(r => r.RequiredCapabilities));
        Assert.Equal(
            first.Roles.Select(r => r.RequiredCapabilities),
            second.Roles.Select(r => r.RequiredCapabilities));
    }

    [Fact]
    public async Task Spec_scenario_a_consumer_can_compute_the_candidate_pools()
    {
        // Every role's required capabilities are published alongside each
        // resource's own, so the pools are computable without a further request —
        // which is what keeps `resource-not-eligible` free of disclosure.
        var h = WireMultiRole();

        var service = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        Assert.All(service.Roles, role => Assert.NotNull(role.RequiredCapabilities));
        Assert.Equal(2, service.Roles.Count);
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

        var model = Ok<ServicePlacementResponseModel>(
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

        var model = Ok<ServicePlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        Assert.Equal(Id(2), Assert.Single(model.Resources).ResourceId);
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
    public async Task Spec_scenario_the_pin_is_optional()
    {
        var h = Wire(null, Room(1), Room(2));

        var model = Ok<ServicePlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(pinned: null)));

        Assert.Equal(Id(1), Assert.Single(model.Resources).ResourceId);
    }

    [Fact]
    public async Task Spec_scenario_the_resolved_resources_are_reported()
    {
        var h = WireMultiRole();

        var model = Ok<ServicePlacementResponseModel>(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement()));

        // Both resolved resources, not one of them and not none: a booker is told
        // everything they got — and in role order, which the fixture's inverted
        // ids distinguish from id order.
        Assert.Equal([Id(2), Id(1)], model.Resources.Select(r => r.ResourceId));
        Assert.Equal(
            [ResourceTypes.Room, "therapist"],
            h.Service.Roles.Select(r => r.ResourceType));
        Assert.Equal("Confirmed", model.Status);
    }

    [Fact]
    public void Spec_scenario_direct_placement_is_unchanged()
    {
        // The direct endpoint's response model still carries a single resource id
        // and no collection. Two roles' worth of contract change must not leak
        // into ⑤'s path.
        var properties = typeof(PlacementResponseModel).GetProperties().Select(p => p.Name).ToArray();

        // `Reference` joined this list deliberately: a consumer builds its own confirmation
        // screen and a person reads it, so returning only the opaque id would leave them
        // showing a Guid — the very defect this pin's neighbours were written about.
        //
        // What the pin guards is unchanged and still holds: ONE `ResourceId`, singular, and no
        // resource collection. That is ⑤'s contract staying out of the direct endpoint.
        Assert.Equal(
            ["BookingId", "Reference", "Status", "ResourceId", "Interval", "Booker"],
            properties);

        Assert.Equal(
            typeof(Guid),
            typeof(PlacementResponseModel).GetProperty("ResourceId")!.PropertyType);
    }

    [Fact]
    public async Task Spec_scenario_ineligible_pinned_resource_is_rejected()
    {
        var h = Wire(null, Room(1), Room(9, type: "therapist"));

        var (status, errors) = Problem(
            await h.Controller.PlaceServiceBooking(h.Service.Id, Placement(pinned: Id(9))));

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

    // ---------------------------------------------------------------------
    // 6 — visitor-selectability and the pinned availability query.
    // ---------------------------------------------------------------------

    /// <summary>
    /// A massage whose therapist role a visitor may choose from, with two
    /// therapists and one room.
    /// </summary>
    private static Harness WireChoosable()
        => WireService(
            Service.Create(
                "Massage",
                null,
                [
                    new ServiceRole(ResourceTypes.Room, 1),
                    new ServiceRole("therapist", 1) { VisitorSelectable = true },
                ]).Value,
            Room(1),
            Room(2, type: "therapist"),
            Room(3, type: "therapist"));

    [Fact]
    public async Task A_visitor_selectable_role_is_identifiable()
    {
        var h = WireChoosable();

        var model = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));

        var therapist = Assert.Single(model.Roles, r => r.ResourceType == "therapist");
        var room = Assert.Single(model.Roles, r => r.ResourceType == ResourceTypes.Room);

        Assert.True(therapist.VisitorSelectable);
        Assert.False(room.VisitorSelectable);
    }

    [Fact]
    public async Task Visitor_selectability_is_stated_rather_than_omitted()
    {
        // A service offering no choice at all still publishes the member on every
        // role, carrying false — so a consumer never has to treat its absence as a
        // default. Asserted through both endpoints, since a mapper can be fixed in
        // one and forgotten in the other.
        var h = WireMultiRole();

        var byId = Ok<ServiceReadModel>(await h.Controller.GetService(h.Service.Id));
        var listed = Ok<PagedServicesModel>(await h.Controller.ListServices()).Items.Single();

        Assert.All(byId.Roles, role => Assert.False(role.VisitorSelectable));
        Assert.All(listed.Roles, role => Assert.False(role.VisitorSelectable));

        // Present rather than nullable: the property exists on the contract and is
        // not an optional extra a serializer could drop.
        Assert.Equal(
            typeof(bool),
            typeof(ServiceRoleReadModel).GetProperty(nameof(ServiceRoleReadModel.VisitorSelectable))!.PropertyType);
    }

    private static async Task<ServiceBookableStartsResponseModel> StartsAsync(
        Harness h, Guid? pinned = null, DateOnly? from = null, DateOnly? to = null)
        => Ok<ServiceBookableStartsResponseModel>(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, from ?? Date, to ?? Date, pinned));

    [Fact]
    public async Task A_pinned_query_narrows_the_answer()
    {
        var h = WireChoosable();

        Assert.True((await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(180),
            Booker = TestData.Booker(),
        })).Succeeded);

        var pinned = await StartsAsync(h, Id(2));

        Assert.NotEmpty(pinned.Starts);
        Assert.All(pinned.Starts, start => Assert.True(start.StartUtc >= TestData.Utc(Date, "12:00")));

        // And the service itself is bookable through the morning, on the other
        // therapist — which is the promise a dropped pin would have made.
        Assert.Contains(
            (await StartsAsync(h)).Starts,
            start => start.StartUtc == TestData.Utc(Date, "09:00"));
    }

    [Fact]
    public async Task An_omitted_pin_leaves_the_response_unchanged()
    {
        var h = WireChoosable();

        var omitted = Ok<ServiceBookableStartsResponseModel>(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date));

        var explicitNull = await StartsAsync(h, pinned: null);

        Assert.Equal(
            omitted.Starts.Select(s => s.StartUtc),
            explicitNull.Starts.Select(s => s.StartUtc));

        Assert.Equal(h.Service.Id, omitted.ServiceId);
        Assert.Equal(TestData.Settings.TimeZoneId, omitted.ZoneId);
        Assert.Null(omitted.Reason);
        Assert.NotEmpty(omitted.Starts);

        // Non-vacuity: over this very service a pin CAN change the answer, so
        // "unchanged" is a property of omitting one rather than of the fixture.
        Assert.True((await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(2),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(480),
            Booker = TestData.Booker(),
        })).Succeeded);

        Assert.Empty((await StartsAsync(h, Id(2))).Starts);
        Assert.NotEmpty((await StartsAsync(h)).Starts);
    }

    [Fact]
    public async Task A_pin_outside_every_pool_is_rejected()
    {
        var h = WireChoosable();

        // The room is a perfectly real resource of this service — just not one of
        // the therapist role's — so this also proves the rejection is about the
        // pools rather than about the id being unknown.
        var (status, errors) = Problem(
            await h.Controller.GetServiceBookableStarts(h.Service.Id, Date, Date, Id(404)));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(FailureCodes.ResourceNotEligible, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task A_pinned_response_names_no_resource_either()
    {
        // Design D3: the pin is in the REQUEST. A pinned answer is conditional on a
        // resource the caller already named, so the payload still carries no
        // resource id — and an unpinned one still makes no promise about which
        // candidate a booker will get.
        var h = WireChoosable();

        foreach (var pin in new Guid?[] { null, Id(2) })
        {
            var response = await StartsAsync(h, pin);

            Assert.NotEmpty(response.Starts);

            var serialized = System.Text.Json.JsonSerializer.Serialize(response);

            foreach (var resource in new[] { Id(1), Id(2), Id(3) })
            {
                Assert.DoesNotContain(resource.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void The_per_resource_bookable_starts_endpoint_is_untouched()
    {
        // Route, shape and semantics, asserted structurally: the resource query
        // takes no pin and its response carries no reason code, so nothing here
        // leaked into a contract this change does not modify.
        var method = typeof(AvailabilityController)
            .GetMethods()
            .Single(m => m.Name == nameof(AvailabilityController.GetBookableStarts));

        Assert.DoesNotContain(method.GetParameters(), p => p.Name!.Contains("pin", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(BookableStartsResponseModel).GetProperties(),
            p => p.Name is "Reason" or "PinnedResourceId");
    }

    // ---------------------------------------------------------------------
    // 7 — the structural reason code on an empty answer (⑨-1a's deferral).
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_structurally_unfulfillable_service_says_so()
    {
        // Two roles that can never be filled by distinct resources: one therapist
        // satisfies both, forever. Indistinguishable from a fully booked week
        // without the code, and a consumer cannot tell whether to offer another
        // date or to stop asking.
        var h = WireService(
            Service.Create(
                "Joint session",
                null,
                [
                    new ServiceRole("therapist", 1)
                    {
                        RequiredCapabilities = CapabilitySet.Create(["cert-x"]).Value,
                    },
                    new ServiceRole("therapist", 1),
                ]).Value,
            Resource.Create(
                "therapist",
                "Jane",
                directlyBookable: true,
                capabilities: ["cert-x"],
                availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
                id: Id(2)).Value);

        var response = await StartsAsync(h);

        Assert.Empty(response.Starts);
        Assert.Equal(ServiceFulfillability.NotFulfillableCode, response.Reason);
    }

    [Fact]
    public async Task A_busy_week_says_nothing_permanent()
    {
        // The pair that makes the assertion above mean something: a correctly
        // configured service that is simply full carries NO code, because coming
        // back next week can help and the structural questions consult no calendar.
        var h = Wire();

        Assert.True((await h.Bookings.PlaceAsync(new BookingRequest
        {
            ResourceId = Id(1),
            Start = TestData.Utc(Date, "09:00"),
            Duration = Mins(480),
            Booker = TestData.Booker(),
        })).Succeeded);

        var response = await StartsAsync(h);

        Assert.Empty(response.Starts);
        Assert.Null(response.Reason);
    }

    [Fact]
    public async Task Grid_misalignment_and_no_common_length_are_reported_as_permanent()
    {
        // The two structural faults that leave every pool healthy — and the two an
        // implementation asking only "is some pool short" would report as ordinary
        // unavailability forever.
        var misaligned = WireService(
            Service.Create("Massage", null,
                [new ServiceRole(ResourceTypes.Room, 1), new ServiceRole("therapist", 1)]).Value,
            Room(1, granularity: 30, min: 30, max: 120),
            Resource.Create(
                "therapist",
                "Jane",
                directlyBookable: true,
                availability: TestData.Config(
                    TestData.Weekly("09:15", "17:00", Date.DayOfWeek),
                    constraints: BookingConstraints.Create(
                        granularity: Mins(20), minDuration: Mins(20), maxDuration: Mins(120)).Value),
                id: Id(2)).Value);

        var noCommonLength = WireService(
            Service.Create("Massage", null,
                [new ServiceRole(ResourceTypes.Room, 1), new ServiceRole("therapist", 1)]).Value,
            Room(1, granularity: 30, min: 30, max: 30),
            Room(2, granularity: 20, min: 20, max: 20, type: "therapist"));

        foreach (var h in new[] { misaligned, noCommonLength })
        {
            var response = await StartsAsync(h);

            Assert.Empty(response.Starts);
            Assert.Equal(ServiceFulfillability.NotFulfillableCode, response.Reason);
        }
    }

    [Fact]
    public async Task Starts_and_the_reason_code_are_exclusive()
    {
        // The two answers are mutually exclusive: a response carrying starts cannot
        // be structurally impossible, and the code never accompanies starts.
        // Asserted over every service this file wires, rather than over one.
        foreach (var h in new[] { Wire(), WireMultiRole(), WireSameType(), WireCounted(), WireChoosable() })
        {
            var response = await StartsAsync(h);

            if (response.Starts.Count > 0)
            {
                Assert.Null(response.Reason);
            }
            else
            {
                // Not asserted to be non-null: an empty answer may simply be a busy
                // one. What is asserted is that a code, where present, is the only
                // code there is.
                Assert.True(response.Reason is null or ServiceFulfillability.NotFulfillableCode);
            }
        }
    }

    [Fact]
    public async Task The_reason_code_discloses_no_configuration()
    {
        // The response is anonymous. The backoffice diagnostics that name the role,
        // the type, the capability, the count and the pool size are for the person
        // who can fix them.
        var h = WireService(
            Service.Create("Couples massage", null,
                [
                    new ServiceRole("therapist", 2)
                    {
                        RequiredCapabilities = CapabilitySet.Create(["cert-x"]).Value,
                    },
                ]).Value,
            Resource.Create(
                "therapist",
                "Jane",
                directlyBookable: true,
                capabilities: ["cert-x"],
                availability: TestData.Config(TestData.Weekly("09:00", "17:00", Date.DayOfWeek)),
                id: Id(2)).Value);

        var response = await StartsAsync(h);

        Assert.Equal(ServiceFulfillability.NotFulfillableCode, response.Reason);

        var serialized = System.Text.Json.JsonSerializer.Serialize(response);

        foreach (var disclosure in new[] { "therapist", "cert-x", "Jane", Id(2).ToString() })
        {
            Assert.DoesNotContain(disclosure, serialized, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_public_read_and_the_in_process_flow_derive_from_one_function()
    {
        // The whole point of lifting the triad into Core (design D6): the same
        // permanently unfulfillable service is reported by both surfaces, and
        // neither evaluates the rules itself.
        //
        // Asserted as agreement over a service each surface answers separately,
        // rather than by inspecting which function is called: two implementations
        // would be free to agree on an easy case and diverge on a hard one, so the
        // hard case — a healthy pool with no common length — is the one used.
        var service = Service.Create("Massage", null,
            [new ServiceRole(ResourceTypes.Room, 1), new ServiceRole("therapist", 1)]).Value;

        var resources = new InMemoryResourceStore()
            .Add(Room(1, granularity: 30, min: 30, max: 30))
            .Add(Room(2, granularity: 20, min: 20, max: 20, type: "therapist"));

        var services = new InMemoryServiceStore().Add(service);
        var h = WireService(service, Room(1, granularity: 30, min: 30, max: 30),
            Room(2, granularity: 20, min: 20, max: 20, type: "therapist"));

        // The public read's answer.
        var response = await StartsAsync(h);

        Assert.Empty(response.Starts);
        Assert.Equal(ServiceFulfillability.NotFulfillableCode, response.Reason);

        // The in-process flow's, over the same configuration — the very function
        // `ServiceUnavailableModel.IsUnavailable` now asks.
        var resolved = await TestData.ServiceBookingWith(services, resources).Services
            .ResolveCandidatesAsync(service.Id);

        Assert.True(resolved.Succeeded);
        Assert.True(ServiceFulfillability.IsPermanentlyUnfulfillable(resolved.Value));
    }

    [Fact]
    public void Spec_scenario_service_placement_has_its_own_model()
    {
        // Not optional service fields bolted onto the direct placement model:
        // that would admit combinations with no meaning.
        Assert.DoesNotContain(
            typeof(PlacementRequestModel).GetProperties(),
            p => p.Name.Contains("Service", StringComparison.OrdinalIgnoreCase)
                || p.Name.Contains("Pinned", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(ServicePlacementRequestModel).GetProperties(),
            p => p.Name == nameof(PlacementRequestModel.ResourceId));
    }
}
