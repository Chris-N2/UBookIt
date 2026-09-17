using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;
using Umbraco.Cms.Core.Security;

namespace UBookIt.Tests;

/// <summary>
/// The endpoint that records a booking on a booker's behalf (booking-management spec, "An
/// operator can place a booking on a booker's behalf").
/// </summary>
/// <remarks>
/// <b>Driven through the controller with a real domain behind it</b>, not with a double
/// standing in for placement: the endpoint's job is to translate a request into the domain's
/// operator placement without adding or relaxing a rule, and a double would let this file agree
/// with itself about which rules ran.
/// </remarks>
public class PlaceOnBehalfEndpointTests
{
    private const string Therapist = "therapist";

    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Room() => Resource.Create(
        ResourceTypes.Room,
        "Meeting Room A",
        directlyBookable: false,
        availability: TestData.Config(
            TestData.Weekly("08:00", "18:00", Date.DayOfWeek),
            constraints: BookingConstraints.Create(
                granularity: TimeSpan.FromMinutes(30),
                minDuration: TimeSpan.FromMinutes(30),
                maxDuration: TimeSpan.FromMinutes(240),
                leadTime: TimeSpan.FromHours(24),
                horizonDays: 90).Value),
        id: Id(1)).Value;

    private sealed class Harness
    {
        public required BookingsController Controller { get; init; }

        public required InMemoryBookingStore Bookings { get; init; }
    }

    private static Harness Wire(string zone = TestData.LondonZoneId, bool autoConfirm = true)
    {
        var resourceStore = new InMemoryResourceStore().Add(Room());
        var serviceStore = new InMemoryServiceStore();
        var bookingStore = new InMemoryBookingStore();
        var time = new FixedTimeProvider(TestData.Now);
        var settings = new SiteBookingSettings
        {
            TimeZoneId = zone,
            MaxQueryRangeDays = 31,
            AutoConfirm = autoConfirm,
        };

        var bookings = new BookingService(resourceStore, bookingStore, time, settings);
        var services = new ServiceBookingService(
            serviceStore,
            resourceStore,
            bookingStore,
            new AvailabilityService(resourceStore, bookingStore, time, settings),
            bookings,
            settings);

        return new Harness
        {
            Controller = new BookingsController(
                new UnusedManagementStore(),
                bookings,
                services,
                resourceStore,
                serviceStore,
                settings,
                NoSecurity.Instance),
            Bookings = bookingStore,
        };
    }

    private static PlaceBookingOnBehalfRequestModel Request(
        Guid? resourceId = null,
        Guid? serviceId = null,
        string start = "10:00",
        int minutes = 60,
        string name = "Ada Lovelace",
        string email = "ada@example.com",
        string? phone = null)
        => new()
        {
            ResourceId = resourceId ?? (serviceId is null ? Id(1) : null),
            ServiceId = serviceId,
            Start = DateTime.Parse($"{Date:yyyy-MM-dd}T{start}:00"),
            LengthMinutes = minutes,
            BookerName = name,
            BookerEmail = email,
            BookerPhone = phone,
        };

    private static PlacedBookingModel Placed(IActionResult result)
        => Assert.IsType<PlacedBookingModel>(Assert.IsType<OkObjectResult>(result).Value);

    /// <summary>
    /// The errors a refusal carries, read the way the rest of the endpoint suite reads them.
    /// </summary>
    /// <remarks>
    /// Asserts the payload's SHAPE on the way past — the extension key, and that it is a
    /// non-empty array of the error model. A helper that silently returned an empty sequence
    /// when it could not find them would make every "is refused" assertion below vacuous, which
    /// is exactly what the first version of it did.
    /// </remarks>
    private static IReadOnlyList<ApiErrorModel> Failures(IActionResult result)
    {
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);

        Assert.NotEmpty(errors);

        return errors;
    }

    // ---- what it places -----------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_an_operator_places_a_booking_for_a_resource()
    {
        var harness = Wire();

        var result = await harness.Controller.PlaceBookingOnBehalf(Request());

        var placed = Placed(result);
        Assert.NotEqual(Guid.Empty, placed.BookingId);
        Assert.False(string.IsNullOrWhiteSpace(placed.Reference));
        Assert.Equal(nameof(BookingStatus.Confirmed), placed.Status);
    }

    [Fact]
    public async Task Spec_scenario_the_start_is_read_in_the_sites_zone()
    {
        // 10:00 London on a BST date is 09:00Z. Asserted as a differential rather than against a
        // literal, so a fixture change cannot quietly make it vacuous.
        var harness = Wire();

        var placed = Placed(await harness.Controller.PlaceBookingOnBehalf(Request(start: "10:00")));

        Assert.Equal(TestData.Utc(Date, "10:00"), placed.StartUtc);
        Assert.Equal(TestData.LondonZoneId, placed.TimeZoneId);
    }

    [Fact]
    public async Task Spec_scenario_a_start_with_an_offset_is_refused()
    {
        var harness = Wire();
        var model = Request();
        model.Start = DateTime.SpecifyKind(model.Start, DateTimeKind.Utc);

        var result = await harness.Controller.PlaceBookingOnBehalf(model);

        Assert.Contains(
            Failures(result),
            f => f.Code == FailureCodes.IntervalInvalid && f.Field == nameof(model.Start));
        Assert.Empty(await harness.Bookings.GetClaimsAsync(
            [Id(1)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_naming_both_a_service_and_a_resource_is_refused()
    {
        var harness = Wire();

        var both = await harness.Controller.PlaceBookingOnBehalf(
            Request(resourceId: Id(1), serviceId: Guid.NewGuid()));

        var neither = await harness.Controller.PlaceBookingOnBehalf(
            new PlaceBookingOnBehalfRequestModel
            {
                Start = DateTime.Parse($"{Date:yyyy-MM-dd}T10:00:00"),
                LengthMinutes = 60,
                BookerName = "Ada Lovelace",
                BookerEmail = "ada@example.com",
            });

        Assert.NotEmpty(Failures(both));
        Assert.NotEmpty(Failures(neither));
        Assert.Empty(await harness.Bookings.GetClaimsAsync(
            [Id(1)], TestData.Utc(Date, "00:00"), TestData.Utc(Date.AddDays(1), "00:00")));
    }

    [Fact]
    public async Task Spec_scenario_a_malformed_booker_address_is_distinguishable_from_a_refused_time()
    {
        var harness = Wire();

        var badAddress = await harness.Controller.PlaceBookingOnBehalf(
            Request(email: "not-an-address"));
        var badTime = await harness.Controller.PlaceBookingOnBehalf(Request(start: "07:00"));

        // The address failure names the field the operator must correct; the time failure names
        // the rule. An operator on the telephone needs to know which of the two to fix.
        Assert.Contains(
            Failures(badAddress),
            f => f.Field == nameof(PlaceBookingOnBehalfRequestModel.BookerEmail));
        Assert.Contains(Failures(badTime), f => f.Code == FailureCodes.OutsideOpenHours);
    }

    [Fact]
    public async Task The_endpoint_waives_direct_bookability_as_the_domain_does()
    {
        // The fixture's resource withholds direct booking, so this succeeding IS the waiver
        // reaching the endpoint. It would fail if the endpoint reached the visitor's placement.
        var harness = Wire();

        var result = await harness.Controller.PlaceBookingOnBehalf(Request());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task An_operators_booking_is_confirmed_on_an_approval_site()
    {
        var harness = Wire(autoConfirm: false);

        var placed = Placed(await harness.Controller.PlaceBookingOnBehalf(Request()));

        Assert.Equal(nameof(BookingStatus.Confirmed), placed.Status);
    }

    // ---- what it does NOT report back ---------------------------------------------------------

    [Fact]
    public void Spec_scenario_the_response_does_not_carry_the_booker_back()
    {
        // STRUCTURAL, over the whole type rather than against a list of forbidden names: the
        // member somebody adds will be called `Contact` or `Booker` or `CustomerEmail`, not
        // whatever a list here guessed. A write that reported a booker back would be a read of
        // personal data wearing a write's authorization.
        var members = typeof(PlacedBookingModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            new[] { "BookingId", "EndUtc", "Reference", "StartUtc", "Status", "TimeZoneId" },
            members);
    }

    [Fact]
    public void Spec_scenario_the_response_does_not_imitate_a_list_row()
    {
        Assert.DoesNotContain(
            typeof(PlacedBookingModel).GetProperties(),
            p => typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                && p.PropertyType != typeof(string));
    }

    [Fact]
    public async Task Spec_scenario_the_endpoint_is_not_an_oracle_over_existing_records()
    {
        // The guarantee KnownWrites' exemption rests on: the outcome must be decided by the
        // booking's own rules alone. Same address, once where no booking holds it and once
        // where one does — and the two differ in nothing but the time, which is the booking's
        // business rather than the booker's.
        var harness = Wire();

        var first = await harness.Controller.PlaceBookingOnBehalf(Request(start: "10:00"));
        var second = await harness.Controller.PlaceBookingOnBehalf(Request(start: "12:00"));

        var a = Placed(first);
        var b = Placed(second);

        Assert.Equal(nameof(BookingStatus.Confirmed), a.Status);
        Assert.Equal(nameof(BookingStatus.Confirmed), b.Status);
        Assert.NotEqual(a.Reference, b.Reference);
    }

    // ---- what there is to book ----------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_an_operator_who_may_place_a_booking_may_see_what_to_book()
    {
        var harness = Wire();

        var listed = Assert.IsType<BookableSubjectsModel>(
            Assert.IsType<OkObjectResult>(await harness.Controller.ListBookableSubjects()).Value);

        // The fixture's only resource WITHHOLDS direct booking from visitors, so its presence
        // here is the spec scenario: the permission does not bind an operator, and filtering it
        // out would state that rule in a second place and disagree with the placement.
        var resource = Assert.Single(listed.Resources);
        Assert.Equal(Id(1), resource.Id);
        Assert.Equal("Meeting Room A", resource.Name);
    }

    [Fact]
    public void Spec_scenario_it_carries_no_configuration_and_nothing_about_any_person()
    {
        // Structural, over the whole type: the thinness is the guarantee. A member carrying
        // open hours, constraints or capabilities would make this a way to read the
        // configuration without the verb for it; one carrying a booker would make it a
        // disclosure.
        var members = typeof(BookableSubjectModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "Id", "Name" }, members);

        var envelope = typeof(BookableSubjectsModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "Resources", "Services" }, envelope);
    }

    [Fact]
    public void The_bookable_read_is_gated_on_manage_and_not_on_sensitive_data()
    {
        // It discloses nothing about any person, so the sensitive-data gate would be a grant
        // nobody needs — and a gate applied where it is not warranted teaches that the gate is
        // decoration.
        var action = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.ListBookableSubjects));

        Assert.NotNull(action);

        var policies = action!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        Assert.Contains(UBookIt.Backoffice.Constants.VerbPolicies.BookingsManage, policies);
        Assert.DoesNotContain(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy, policies);
    }

    [Fact]
    public void The_bookable_read_takes_no_parameters_that_could_filter_it()
    {
        // Unpaged, and with no filter of any kind: a picker that silently omitted a resource
        // would send an operator to tell a customer the site cannot do something it can.
        var action = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.ListBookableSubjects))!;

        Assert.All(
            action.GetParameters(),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    // ---- the gates ----------------------------------------------------------------------------

    [Fact]
    public void Spec_scenario_the_sensitive_data_gate_is_the_endpoints_own_authorization()
    {
        // ON THE ACTION, not inside the handler — the sensitive-data capability requires it,
        // and a check in the handler cannot be seen from here or from its own guard.
        var action = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.PlaceBookingOnBehalf));

        Assert.NotNull(action);

        var policies = action!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        Assert.Contains(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy, policies);
        Assert.Contains(UBookIt.Backoffice.Constants.VerbPolicies.BookingsManage, policies);
    }

    /// <summary>
    /// A security accessor this endpoint never consults.
    /// </summary>
    /// <remarks>
    /// Placement carries no booker back, so there is no visibility decision to make — and that
    /// is a claim rather than an assumption, so every member throws. If the endpoint ever starts
    /// asking who the user is, these tests say so instead of quietly agreeing with whatever a
    /// stubbed answer happened to be.
    /// </remarks>
    private sealed class NoSecurity : IBackOfficeSecurityAccessor
    {
        public static NoSecurity Instance { get; } = new();

        public IBackOfficeSecurity? BackOfficeSecurity
            => throw new InvalidOperationException(
                "The placement endpoint asked who the current user is. It reports no personal "
                + "data, so it should have no visibility decision to make — if that has changed, "
                + "the response model has probably grown a member it must not have.");
    }

    /// <summary>A management store this endpoint never reaches.</summary>
    private sealed class UnusedManagementStore : IBookingManagementStore
    {
        public Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The placement endpoint does not list bookings.");

        public Task<BookingPage> FindByBookerEmailAsync(
            BookerEmailQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The placement endpoint does not search bookings.");
    }
}
