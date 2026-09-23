using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    [Fact]
    public void The_privacy_read_publishes_a_configured_retention_period()
    {
        var settings = TestData.Settings with { RetentionDays = 90 };

        var result = Assert.IsType<OkObjectResult>(new PrivacyController(settings).GetPrivacy());
        var model = Assert.IsType<PrivacyModel>(result.Value);

        Assert.Equal(90, model.RetentionDays);
    }

    [Fact]
    public void The_privacy_read_reports_no_period_as_null_and_never_as_a_number()
    {
        // A consumer must be able to tell "erased after N days" from "no automatic removal is
        // set", because only one of those is a promise it may publish on its own page. Reporting
        // the second as 0 would let it tell visitors their data goes immediately.
        var settings = TestData.Settings with { RetentionDays = null };

        var result = Assert.IsType<OkObjectResult>(new PrivacyController(settings).GetPrivacy());
        var model = Assert.IsType<PrivacyModel>(result.Value);

        Assert.Null(model.RetentionDays);
        Assert.NotEqual(0, model.RetentionDays ?? -1);
    }

    [Fact]
    public void The_privacy_read_publishes_no_prose()
    {
        // The number, and nothing else. Publishing the package's English sentences would make
        // untranslatable prose part of a versioned contract — and a headless consumer writes its
        // own page in its own language anyway.
        var members = typeof(PrivacyModel)
            .GetProperties()
            .Select(p => (p.Name, p.PropertyType))
            .ToList();

        var member = Assert.Single(members);
        Assert.Equal(nameof(PrivacyModel.RetentionDays), member.Name);
        Assert.Equal(typeof(int?), member.PropertyType);
    }

    [Fact]
    public void Retention_does_not_ride_on_the_resource_or_service_reads()
    {
        // It is a site-wide fact. Repeating it on every row of a paged read would duplicate one
        // value and invite a consumer to believe it varies by resource.
        foreach (var type in new[] { typeof(ResourceReadModel), typeof(ServiceReadModel) })
        {
            Assert.DoesNotContain(
                type.GetProperties(),
                p => p.Name.Contains("Retention", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void The_privacy_read_carries_no_authorization_of_its_own()
    {
        // Anonymous on the same terms as every other delivery endpoint, inherited from the base
        // controller rather than restated — and asserted so that adding a policy here, which
        // would break every headless consumer, cannot pass unnoticed.
        var attributes = typeof(PrivacyController)
            .GetCustomAttributes(inherit: false)
            .Select(a => a.GetType().Name)
            .ToList();

        Assert.DoesNotContain("AuthorizeAttribute", attributes);
        Assert.Contains("ApiVersionAttribute", attributes);
    }

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

        // The delivery envelope is kept in step with the backoffice one, where
        // a missing `type` causes the client to discard the whole payload.
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));

        return (obj.StatusCode!.Value, errors);
    }

    /// <summary>
    /// A harness whose resource withholds direct booking, otherwise identical.
    /// Built here rather than by flipping the shared helper, because the point of
    /// every test below is which answer the resource gave.
    /// </summary>
    private sealed class WithholdingHarness
    {
        public Resource Room { get; }
        public ResourcesController Resources { get; }
        public AvailabilityController Availability { get; }
        public BookingsController Bookings { get; }

        public WithholdingHarness()
        {
            Room = Resource.Create(
                ResourceTypes.Room,
                "Meeting Room A",
                directlyBookable: false,
                availability: TestData.Config(
                    TestData.Weekly("08:00", "18:00", BaseDate.DayOfWeek))).Value;

            var resources = new InMemoryResourceStore().Add(Room);
            var store = new InMemoryBookingStore();
            var time = new FixedTimeProvider(TestData.Now);
            var settings = TestData.Settings;

            Resources = new ResourcesController(resources, settings);
            Availability = new AvailabilityController(
                new AvailabilityService(resources, store, time, settings), settings);
            Bookings = new BookingsController(new BookingService(resources, store, time, settings));
        }
    }

    // --- Direct bookability on the read model and at placement ---

    [Fact]
    public async Task Spec_scenario_direct_bookability_is_readable_before_it_is_needed()
    {
        var h = new WithholdingHarness();

        var model = Ok<ResourceReadModel>(await h.Resources.GetResource(h.Room.Id));

        Assert.False(model.DirectlyBookable);

        // And present on the list too, so a consumer filtering a direct-booking UI
        // never has to fetch each resource to find out.
        var page = Ok<PagedResourcesModel>(await h.Resources.ListResources());
        Assert.False(Assert.Single(page.Items).DirectlyBookable);
    }

    [Fact]
    public async Task A_permitting_resource_reads_as_permitting()
    {
        // The pair that makes the assertion above non-vacuous: a member that were
        // always false would pass it.
        var h = new Harness();

        Assert.True(Ok<ResourceReadModel>(await h.Resources.GetResource(h.Room.Id)).DirectlyBookable);
    }

    [Fact]
    public async Task Spec_scenario_direct_placement_on_a_withholding_resource_is_refused()
    {
        var h = new WithholdingHarness();

        var (status, errors) = Problem(await h.Bookings.PlaceBooking(new PlacementRequestModel
        {
            ResourceId = h.Room.Id,
            Start = TestData.Utc(BaseDate, "09:00"),
            DurationMinutes = 60,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        }));

        // 400 through the existing catch-all, asserted rather than assumed: the
        // mapping requirement is not being modified, so this is the only thing
        // holding the status.
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(FailureCodes.ResourceNotDirectlyBookable, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Spec_scenario_the_refusal_is_distinguishable_from_unavailability()
    {
        var withholding = new WithholdingHarness();
        var permitting = new Harness();

        var (_, refused) = Problem(await withholding.Bookings.PlaceBooking(new PlacementRequestModel
        {
            ResourceId = withholding.Room.Id,
            Start = TestData.Utc(BaseDate, "09:00"),
            DurationMinutes = 60,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        }));

        // The same shaped request against a permitting resource, at an hour it is
        // closed, so the two failures differ only in their cause.
        var (_, closed) = Problem(await permitting.Bookings.PlaceBooking(new PlacementRequestModel
        {
            ResourceId = permitting.Room.Id,
            Start = TestData.Utc(BaseDate, "05:00"),
            DurationMinutes = 60,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        }));

        Assert.NotEqual(closed.Single().Code, refused.Single().Code);
        Assert.Equal(FailureCodes.OutsideOpenHours, closed.Single().Code);
    }

    [Fact]
    public async Task Spec_scenario_availability_reads_answer_for_a_withholding_resource()
    {
        // The counter-intuitive half, asserted directly because it is the part most
        // likely to be "tidied away" later: a resource nobody can book directly
        // still publishes when it is free, because a composite booking needs that.
        var withholding = new WithholdingHarness();
        var permitting = new Harness();

        var from = BaseDate;
        var to = BaseDate;

        var withheldStarts = Ok<BookableStartsResponseModel>(
            await withholding.Availability.GetBookableStarts(withholding.Room.Id, from, to));
        var permittedStarts = Ok<BookableStartsResponseModel>(
            await permitting.Availability.GetBookableStarts(permitting.Room.Id, from, to));

        Assert.NotEmpty(withheldStarts.Starts);

        // Identically, not merely non-empty: the permission changes nothing here.
        Assert.Equal(
            permittedStarts.Starts.Select(s => s.StartUtc),
            withheldStarts.Starts.Select(s => s.StartUtc));
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

    // --- Bookable starts (delivery-api spec, "Bookable-start read") ---

    [Fact]
    public async Task Bookable_starts_carry_the_range_of_lengths_per_start()
    {
        var h = new Harness();

        var model = Ok<BookableStartsResponseModel>(
            await h.Availability.GetBookableStarts(h.Room.Id, BaseDate, BaseDate));

        Assert.Equal(h.Room.Id, model.ResourceId);
        Assert.Equal("Europe/London", model.ZoneId);
        Assert.NotEmpty(model.Starts);

        var constraints = h.Room.Availability.Constraints;
        Assert.All(model.Starts, s =>
        {
            Assert.Equal((int)constraints.MinDuration.TotalMinutes, s.MinDurationMinutes);
            Assert.InRange(s.MaxDurationMinutes, s.MinDurationMinutes, (int)constraints.MaxDuration.TotalMinutes);
        });

        // Ordered, and the maximum shortens towards the end of the free window.
        Assert.Equal(model.Starts.OrderBy(s => s.StartUtc).Select(s => s.StartUtc), model.Starts.Select(s => s.StartUtc));
        Assert.True(model.Starts[^1].MaxDurationMinutes < model.Starts[0].MaxDurationMinutes);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(120)]
    public async Task Any_length_is_answerable_from_one_bookable_starts_response(int minutes)
    {
        // The spec's headline claim for this endpoint: a client filters the
        // response instead of issuing one request per length.
        var h = new Harness();

        var starts = Ok<BookableStartsResponseModel>(
            await h.Availability.GetBookableStarts(h.Room.Id, BaseDate, BaseDate));
        var slots = Ok<SlotsResponseModel>(
            await h.Availability.GetSlots(h.Room.Id, BaseDate, BaseDate, minutes));

        Assert.Equal(
            slots.Slots.Select(s => s.StartUtc).ToArray(),
            starts.Starts
                .Where(s => s.MinDurationMinutes <= minutes && s.MaxDurationMinutes >= minutes)
                .Select(s => s.StartUtc)
                .ToArray());
    }

    [Fact]
    public async Task Bookable_starts_over_wide_range_is_400_date_range_too_large()
    {
        var h = new Harness();

        var (status, errors) = Problem(
            await h.Availability.GetBookableStarts(h.Room.Id, BaseDate, BaseDate.AddDays(31)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DateRangeTooLarge, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Bookable_starts_inverted_range_is_400_date_range_invalid()
    {
        var h = new Harness();

        var (status, errors) = Problem(
            await h.Availability.GetBookableStarts(h.Room.Id, BaseDate, BaseDate.AddDays(-1)));

        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.DateRangeInvalid, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Bookable_starts_unknown_resource_is_404_resource_not_found()
    {
        var h = new Harness();

        var (status, errors) = Problem(
            await h.Availability.GetBookableStarts(Guid.NewGuid(), BaseDate, BaseDate));

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task Bookable_starts_reflect_an_existing_booking()
    {
        var h = new Harness();

        Ok<PlacementResponseModel>(await h.Bookings.PlaceBooking(new PlacementRequestModel
        {
            ResourceId = h.Room.Id,
            Start = TestData.Utc(BaseDate, "10:00"),
            DurationMinutes = 60,
            Booker = new BookerModel { Name = "Test Person", Email = "test@example.com" },
        }));

        var model = Ok<BookableStartsResponseModel>(
            await h.Availability.GetBookableStarts(h.Room.Id, BaseDate, BaseDate));

        Assert.DoesNotContain(model.Starts, s => s.StartUtc == TestData.Utc(BaseDate, "10:00"));
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
    public async Task Placement_returns_the_bookings_own_reference()
    {
        // Spec: "A direct placement returns something quotable". Compared against the
        // reference the STORE holds, not against a shape or a non-empty check — a response
        // hard-wired to a constant satisfies "there is a reference here" and is useless to the
        // person reading it, and that mutation passed the entire suite before this test.
        var h = new Harness();

        var model = Ok<PlacementResponseModel>(await h.Bookings.PlaceBooking(h.ValidPlacement()));

        var placed = await h.BookingStore.GetBookingAsync(model.BookingId);

        Assert.NotNull(placed);
        Assert.Equal(placed.Reference.Value, model.Reference);

        // Canonical, not the grouped display form: this is a data contract and presentation
        // is the consumer's decision.
        Assert.DoesNotContain('-', model.Reference);
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

        // `closure` and `optout` joined this list when site closures arrived. The forbidden
        // set is the POPULATION of internal availability configuration, not the sample that
        // existed when the guard was written — a new kind of configuration added to the read
        // model would otherwise pass a guard whose whole purpose is to refuse it.
        Assert.DoesNotContain(names, n =>
            n.Contains("open", StringComparison.OrdinalIgnoreCase)
            || n.Contains("hour", StringComparison.OrdinalIgnoreCase)
            || n.Contains("exception", StringComparison.OrdinalIgnoreCase)
            || n.Contains("window", StringComparison.OrdinalIgnoreCase)
            || n.Contains("closure", StringComparison.OrdinalIgnoreCase)
            || n.Contains("closed", StringComparison.OrdinalIgnoreCase)
            || n.Contains("optout", StringComparison.OrdinalIgnoreCase)
            || n.Contains("opt_out", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The same rule over the SERIALIZED body of a REAL response, because a label can reach a
    /// caller through a member whose name says nothing — an "unavailableReason", a note, a
    /// description carried through from configuration.
    /// </summary>
    /// <remarks>
    /// <b>It must go through the controller.</b> The first version of this built a
    /// <c>ResourceReadModel</c> by hand and serialized that, so it inspected the type's defaults
    /// rather than the endpoint's output — QA proved it vacuous by adding an
    /// <c>UnavailableReason</c> member, having the mapper set it to "Site closure: Christmas
    /// Day", and watching all 38 delivery tests stay green while the public API handed anonymous
    /// callers the closure label. A guard over a hand-built instance can only ever see the
    /// members the test itself remembered to set.
    /// </remarks>
    [Fact]
    public async Task A_real_resource_response_carries_no_trace_of_the_closure_it_is_subject_to()
    {
        // A resource genuinely subject to a closure with a distinctive label: if any part of the
        // read path carries it outward, the label is in the body.
        var closure = TestData.Closure(BaseDate, "Zzyzx Stocktake Day");
        var room = Resource.Create(
            ResourceTypes.Room,
            "Consulting Room",
            directlyBookable: true,
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", BaseDate.DayOfWeek), closures: [closure])).Value;

        var resources = new InMemoryResourceStore().Add(room);
        var controller = new ResourcesController(resources, TestData.Settings);

        // THE GUARD'S OWN PRECONDITION, and its absence is what QA proved twice over: a
        // `DoesNotContain` over a subject that never carried the label is the string-matching
        // twin of an `Assert.All` over an empty sequence. Dropping the `closures:` argument in a
        // refactor would retire this guard silently.
        Assert.Single(room.Availability.Closures);

        // And the closure is IN FORCE on the very read path this guard inspects, not merely
        // attached to the fixture: the date it covers offers nothing.
        var availability = new AvailabilityController(
            new AvailabilityService(
                resources, new InMemoryBookingStore(), new FixedTimeProvider(TestData.Now), TestData.Settings),
            TestData.Settings);

        var slots = Ok<SlotsResponseModel>(await availability.GetSlots(room.Id, BaseDate, BaseDate, 60));
        Assert.Empty(slots.Slots);

        var single = Ok<ResourceReadModel>(await controller.GetResource(room.Id));
        var page = Ok<PagedResourcesModel>(await controller.ListResources());

        foreach (var body in new[]
        {
            System.Text.Json.JsonSerializer.Serialize(single),
            System.Text.Json.JsonSerializer.Serialize(page),
        })
        {
            Assert.DoesNotContain("Zzyzx", body, StringComparison.OrdinalIgnoreCase);

            foreach (var forbidden in new[] { "closure", "closed", "stocktake", "optOut", "opt_out" })
            {
                Assert.DoesNotContain(forbidden, body, StringComparison.OrdinalIgnoreCase);
            }
        }
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
