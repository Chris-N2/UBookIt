using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The service configuration preview endpoint (resource-management spec,
/// "Service configuration preview endpoint").
/// <para>
/// Wired over the real <see cref="ServiceBookingService"/> rather than a
/// resolution double, because the guarantee under test is that the endpoint's
/// answer <em>is</em> Core's answer. A double would let the endpoint and the
/// assertion agree with each other while both disagreed with the booking path.
/// </para>
/// </summary>
public class ServicePreviewEndpointTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:x12}");

    private static Resource Room(int id, string name, int maxMinutes, params string[] capabilities)
        => Resource.Create(
            ResourceTypes.Room,
            name,
            capabilities: capabilities,
            availability: TestData.Config(
                TestData.Weekly("08:00", "20:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(30),
                    minDuration: TimeSpan.FromMinutes(30),
                    maxDuration: TimeSpan.FromMinutes(maxMinutes)).Value),
            id: Id(id)).Value;

    /// <summary>Ten rooms; three carry <c>projector</c>; one of those three can go four hours.</summary>
    private static Resource[] TenRooms() =>
    [
        Room(1, "Red Room", 120, "projector"),
        Room(2, "Blue Room", 120, "projector"),
        Room(3, "Green Room", 480, "projector"),
        .. Enumerable.Range(4, 7).Select(n => Room(n, $"Room {n}", 480)),
    ];

    private static (ServicesController Controller, InMemoryServiceStore Services) Wire(
        params Resource[] resources)
    {
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in resources)
        {
            resourceStore.Add(resource);
        }

        var services = new InMemoryServiceStore();

        return (
            new ServicesController(services, services, TestData.ServiceBooking(services, resourceStore)),
            services);
    }

    private static ServicePreviewRequestModel Request(
        string resourceType = ResourceTypes.Room,
        string[]? capabilities = null,
        ServiceDurationModel? duration = null)
        => Configuration(duration, Role(resourceType, capabilities));

    private static ServicePreviewRoleModel Role(
        string resourceType = ResourceTypes.Room, string[]? capabilities = null, int count = 1)
        => new()
        {
            ResourceType = resourceType,
            RequiredCapabilities = [.. capabilities ?? []],
            Count = count,
        };

    private static ServicePreviewRequestModel Configuration(
        ServiceDurationModel? duration, params ServicePreviewRoleModel[] roles)
        => new()
        {
            Roles = [.. roles],
            Duration = duration
                ?? new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
        };

    /// <summary>The one chain of a single-role configuration.</summary>
    private static ServiceRoleChainModel Single(ServicePreviewResponseModel response)
        => Assert.Single(response.Roles);

    private static ServiceDurationModel FixedMinutes(int minutes)
        => new() { Kind = ServiceDurationModel.FixedKind, Minutes = minutes };

    private static ServicePreviewResponseModel Ok(IActionResult result)
        => Assert.IsType<ServicePreviewResponseModel>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, string[] Codes) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
        return (obj.StatusCode!.Value, errors.Select(e => e.Code).ToArray());
    }

    private static string?[] Fields(IActionResult result)
    {
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);

        return Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]).Select(e => e.Field).ToArray();
    }

    private static Resource Therapist(int id, string name, params string[] capabilities)
        => Resource.Create(
            "therapist",
            name,
            capabilities: capabilities,
            availability: TestData.Config(TestData.Weekly("08:00", "20:00", Date.DayOfWeek)),
            id: Id(id)).Value;

    [Fact]
    public async Task Spec_scenario_the_chain_is_returned_for_a_configuration()
    {
        // "type `room` requiring `projector` with a fixed four-hour duration, and
        // ten rooms exist of which three carry `projector` and one of those can
        // provide four hours" → ten, three and one for the three stages, and the
        // two resources excluded by duration identified.
        var (controller, _) = Wire(TenRooms());

        var chain = Single(Ok(await controller.PreviewServiceConfiguration(
            Request(capabilities: ["projector"], duration: FixedMinutes(240)))));

        Assert.Equal(10, chain.OfType.Total);
        Assert.Equal(3, chain.WithCapabilities.Total);
        Assert.Equal(1, chain.CanProvide.Total);

        Assert.Equal(
            ["Red Room", "Blue Room"],
            chain.DurationExclusions.Select(e => e.DisplayName));
        Assert.All(chain.DurationExclusions, e =>
        {
            Assert.Equal(DurationExclusionModel.ResourceMaximumReason, e.Reason);
            Assert.Equal(120, e.BoundMinutes);
        });
    }

    [Fact]
    public async Task Spec_scenario_an_unsaved_and_incomplete_configuration_can_be_previewed()
    {
        // "a configuration no saved service uses and with no service name
        // supplied" → the request succeeds and reports the chain. The request
        // model has no name field at all: there is nothing to omit, so nothing
        // can start requiring it.
        var (controller, services) = Wire(TenRooms());

        var result = await controller.PreviewServiceConfiguration(
            Request(capabilities: ["projector"], duration: FixedMinutes(240)));

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, (await services.ListAsync(0, 50)).Total);
        Assert.DoesNotContain(
            "name",
            typeof(ServicePreviewRequestModel).GetProperties().Select(p => p.Name.ToLowerInvariant()));
    }

    [Fact]
    public async Task Spec_scenario_no_required_capabilities_matches_the_whole_type()
    {
        var (controller, _) = Wire(TenRooms());

        var chain = Single(Ok(await controller.PreviewServiceConfiguration(Request())));

        Assert.Equal(10, chain.OfType.Total);
        Assert.Equal(10, chain.WithCapabilities.Total);
    }

    [Fact]
    public async Task Spec_scenario_an_unused_type_key_yields_an_empty_first_stage()
    {
        // "a well-formed type key no resource uses" → successful, and its FIRST
        // stage is empty. That is what distinguishes it from a capability or
        // duration exclusion, and it must not be an error: naming a type before
        // creating resources of it is a legitimate setup order.
        var (controller, _) = Wire(TenRooms());

        var result = await controller.PreviewServiceConfiguration(
            Request("consulting-room", capabilities: ["projector"]));

        Assert.IsType<OkObjectResult>(result);

        var chain = Single(Ok(result));
        Assert.Equal(0, chain.OfType.Total);
        Assert.Equal(0, chain.WithCapabilities.Total);
        Assert.Equal(0, chain.CanProvide.Total);
        Assert.Empty(chain.DurationExclusions);
    }

    [Fact]
    public async Task Spec_scenario_preview_agrees_with_candidate_resolution()
    {
        // "the role and duration of a saved service whose candidates are also
        // resolved through Core" → the chain's final stage contains exactly the
        // candidates Core resolves. This is the property the endpoint exists to
        // preserve: a backoffice answer that diverges from the booking path's is
        // worse than none.
        var resourceStore = new InMemoryResourceStore();
        foreach (var room in TenRooms())
        {
            resourceStore.Add(room);
        }

        var duration = ServiceDuration.Fixed(TimeSpan.FromMinutes(240)).Value;
        var role = ServiceRole.Create(ResourceTypes.Room, ["projector"]).Value;
        var service = Service.Create("Workshop", duration, [role]).Value;

        var services = new InMemoryServiceStore().Add(service);
        var resolution = TestData.ServiceBooking(services, resourceStore);
        var controller = new ServicesController(services, services, resolution);

        var chain = Single(Ok(await controller.PreviewServiceConfiguration(
            Request(capabilities: ["projector"], duration: FixedMinutes(240)))));

        var candidates = await resolution.ResolveCandidatesAsync(service.Id);

        Assert.Equal(
            candidates.SingleRolePool().Select(c => c.ResourceId).OrderBy(id => id),
            chain.CanProvide.Items.Select(i => i.Id).OrderBy(id => id));
        Assert.NotEmpty(chain.CanProvide.Items);
    }

    [Fact]
    public async Task Spec_scenario_a_malformed_key_is_rejected_rather_than_ignored()
    {
        // "a capability key that is not a normalized key" → fails with
        // `capability-key-invalid` rather than reporting a chain for a silently
        // narrowed configuration.
        var (controller, _) = Wire(TenRooms());

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(
            Request(capabilities: ["Projector Screen"])));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task A_malformed_type_key_is_rejected_with_its_own_code()
    {
        var (controller, _) = Wire(TenRooms());

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(Request("Meeting Room")));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
    }

    [Fact]
    public async Task An_over_long_type_key_is_rejected_with_the_same_code()
    {
        var (controller, _) = Wire(TenRooms());

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(
            Request(new string('a', NormalizedKey.MaxLength + 1))));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
    }

    [Fact]
    public async Task A_malformed_duration_is_rejected_rather_than_defaulted()
    {
        // Guessing a duration would report a chain for a configuration nobody
        // asked about — and the duration stage is exactly what this endpoint
        // added.
        var (controller, _) = Wire(TenRooms());

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(
            Request(duration: new ServiceDurationModel { Kind = "whenever" })));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    [Fact]
    public async Task Every_failed_rule_is_reported_in_one_response()
    {
        var (controller, _) = Wire(TenRooms());

        var (_, codes) = Problem(await controller.PreviewServiceConfiguration(
            Request("Meeting Room", ["Projector Screen"], new ServiceDurationModel { Kind = "whenever" })));

        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    // --------------------------------------------------------------- several roles

    [Fact]
    public async Task Spec_scenario_a_chain_is_returned_for_each_role()
    {
        var (controller, _) = Wire([.. TenRooms(), Therapist(20, "Mary", "cert-x"), Therapist(21, "Frank")]);

        var response = Ok(await controller.PreviewServiceConfiguration(Configuration(
            FixedMinutes(60),
            Role(ResourceTypes.Room, ["projector"]),
            Role("therapist", ["cert-x"]))));

        // Two chains, in the order the roles were supplied, each identifying the
        // role it describes.
        Assert.Equal(2, response.Roles.Count);
        Assert.Equal([ResourceTypes.Room, "therapist"], response.Roles.Select(r => r.ResourceType));

        Assert.Equal(10, response.Roles[0].OfType.Total);
        Assert.Equal(3, response.Roles[0].WithCapabilities.Total);

        Assert.Equal(2, response.Roles[1].OfType.Total);
        Assert.Equal(1, response.Roles[1].WithCapabilities.Total);
    }

    [Fact]
    public async Task Chains_come_back_in_the_order_the_roles_were_supplied()
    {
        var (controller, _) = Wire([.. TenRooms(), Therapist(20, "Mary", "cert-x")]);

        var reversed = Ok(await controller.PreviewServiceConfiguration(Configuration(
            FixedMinutes(60),
            Role("therapist"),
            Role(ResourceTypes.Room))));

        Assert.Equal(["therapist", ResourceTypes.Room], reversed.Roles.Select(r => r.ResourceType));
        Assert.Equal(1, reversed.Roles[0].OfType.Total);
        Assert.Equal(10, reversed.Roles[1].OfType.Total);
    }

    [Fact]
    public async Task Spec_scenario_a_configuration_that_could_not_be_saved_can_still_be_previewed()
    {
        // Two roles of one resource type: saving is rejected, but previewing is
        // how an editor sees what each role resolves to while correcting it.
        // Refusing to answer would withhold exactly the information needed.
        var (controller, _) = Wire(TenRooms());

        var response = Ok(await controller.PreviewServiceConfiguration(Configuration(
            FixedMinutes(60),
            Role(ResourceTypes.Room, ["projector"]),
            Role(ResourceTypes.Room))));

        Assert.Equal(2, response.Roles.Count);

        // Reported independently and exactly as supplied — the second role is not
        // collapsed into the first, nor narrowed by it.
        Assert.Equal(3, response.Roles[0].WithCapabilities.Total);
        Assert.Equal(10, response.Roles[1].WithCapabilities.Total);
        Assert.Equal(["projector"], response.Roles[0].RequiredCapabilities);
        Assert.Empty(response.Roles[1].RequiredCapabilities);
    }

    [Fact]
    public async Task Spec_scenario_an_empty_role_list_is_rejected()
    {
        var (controller, _) = Wire(TenRooms());

        var (status, codes) = Problem(
            await controller.PreviewServiceConfiguration(Configuration(FixedMinutes(60))));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceRoleInvalid, codes);
    }

    [Fact]
    public async Task A_null_role_collection_is_a_validation_failure_not_a_crash()
    {
        // `{"roles": null}` binds straight past the property initializer, and a
        // null collection is not a ModelState error without [Required] — so
        // without a guard this leaves the controller as an unhandled
        // NullReferenceException where the endpoint promises 400 problem details.
        var (controller, _) = Wire(TenRooms());

        var request = Configuration(FixedMinutes(60));
        request.Roles = null!;

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceRoleInvalid, codes);
    }

    [Fact]
    public async Task A_null_role_entry_is_a_validation_failure_not_a_crash()
    {
        var (controller, _) = Wire(TenRooms());

        var request = Configuration(FixedMinutes(60));
        request.Roles = [null!];

        var (status, codes) = Problem(await controller.PreviewServiceConfiguration(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceRoleInvalid, codes);
    }

    [Fact]
    public async Task A_malformed_key_identifies_the_role_it_came_from()
    {
        var (controller, _) = Wire(TenRooms());

        var result = await controller.PreviewServiceConfiguration(Configuration(
            FixedMinutes(60),
            Role(ResourceTypes.Room),
            Role("Meeting Room", ["Projector Screen"])));

        var fields = Fields(result);

        // Both failures belong to the second row, and say so: with several roles
        // on screen, "the resource type is malformed" is not enough to know which
        // control to mark.
        Assert.Contains("Roles[1].ResourceType", fields);
        Assert.Contains("Roles[1].RequiredCapabilities", fields);
        Assert.DoesNotContain(fields, f => f is not null && f.StartsWith("Roles[0]", StringComparison.Ordinal));
    }

    // ------------------------------------------------------ start misalignment

    /// <summary>
    /// A resource of the given type opening at <paramref name="open"/> and
    /// stepping in <paramref name="granularityMinutes"/> — the two numbers the
    /// alignment finding is about.
    /// </summary>
    private static Resource Gridded(
        string type, int id, string name, string open, int granularityMinutes)
        => Resource.Create(
            type,
            name,
            availability: TestData.Config(
                TestData.Weekly(open, "20:00", Date.DayOfWeek),
                constraints: BookingConstraints.Create(
                    granularity: TimeSpan.FromMinutes(granularityMinutes),
                    minDuration: TimeSpan.FromMinutes(granularityMinutes),
                    maxDuration: TimeSpan.FromMinutes(granularityMinutes * 8)).Value),
            id: Id(id)).Value;

    /// <summary>The worked example: 09:00/30 against 09:15/20, which never meet.</summary>
    private static Resource[] MisalignedPair() =>
    [
        Gridded(ResourceTypes.Room, 1, "Red Room", "09:00", 30),
        Gridded("therapist", 2, "Mary", "09:15", 20),
    ];

    /// <summary>The same pair shifted so that gcd(30, 20) = 10 divides the offset.</summary>
    private static Resource[] AlignablePair() =>
    [
        Gridded(ResourceTypes.Room, 1, "Red Room", "09:00", 30),
        Gridded("therapist", 2, "Mary", "09:30", 20),
    ];

    private static ServicePreviewRequestModel TwoRoles()
        => Configuration(FixedMinutes(60), Role(ResourceTypes.Room), Role("therapist"));

    [Fact]
    public async Task Spec_scenario_a_misaligned_configuration_reports_the_finding()
    {
        var (controller, _) = Wire(MisalignedPair());

        var response = Ok(await controller.PreviewServiceConfiguration(TwoRoles()));

        // Both chains as before, PLUS the finding — not instead of them.
        Assert.Equal(2, response.Roles.Count);
        Assert.All(response.Roles, chain => Assert.Equal(1, chain.CanProvide.Total));

        var finding = response.StartMisalignment;
        Assert.NotNull(finding);

        Assert.Equal(ResourceTypes.Room, finding.First.ResourceType);
        Assert.Equal(Id(1), finding.First.ResourceId);
        Assert.Equal("Red Room", finding.First.DisplayName);
        Assert.Equal("09:00", finding.First.WindowStart);
        Assert.Equal(30, finding.First.GranularityMinutes);

        Assert.Equal("therapist", finding.Second.ResourceType);
        Assert.Equal(Id(2), finding.Second.ResourceId);
        Assert.Equal("Mary", finding.Second.DisplayName);
        Assert.Equal("09:15", finding.Second.WindowStart);
        Assert.Equal(20, finding.Second.GranularityMinutes);
    }

    [Fact]
    public async Task Spec_scenario_an_alignable_configuration_carries_no_finding()
    {
        // Same two roles, same pools, one opening time moved. Silence — not a
        // positive report that the roles align, which would be read as a promise
        // that the service is bookable.
        var (controller, _) = Wire(AlignablePair());

        var response = Ok(await controller.PreviewServiceConfiguration(TwoRoles()));

        Assert.Equal(2, response.Roles.Count);
        Assert.Null(response.StartMisalignment);
    }

    [Fact]
    public async Task Spec_scenario_a_single_role_configuration_never_reports_a_misalignment()
    {
        // There is no second grid to miss. The resource is the awkward one from
        // the misaligned pair, so the answer comes from there being one role.
        var (controller, _) = Wire(MisalignedPair());

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(FixedMinutes(60), Role(ResourceTypes.Room))));

        Assert.Single(response.Roles);
        Assert.Null(response.StartMisalignment);
    }

    [Fact]
    public async Task Spec_scenario_the_finding_is_separate_from_the_chains()
    {
        // The chains must be byte-for-byte what they would have been without the
        // finding — asserted by serialising the chain list for a misaligned
        // configuration and for an alignable one whose pools resolve identically,
        // rather than by inspecting fields the test chose.
        var (misaligned, _) = Wire(MisalignedPair());
        var (alignable, _) = Wire(AlignablePair());

        var withFinding = Ok(await misaligned.PreviewServiceConfiguration(TwoRoles()));
        var without = Ok(await alignable.PreviewServiceConfiguration(TwoRoles()));

        Assert.NotNull(withFinding.StartMisalignment);
        Assert.Null(without.StartMisalignment);

        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(without.Roles),
            System.Text.Json.JsonSerializer.Serialize(withFinding.Roles));

        // And no chain says anything about opening hours or start times: the
        // finding's numbers appear nowhere in the chains (design D6).
        var chains = System.Text.Json.JsonSerializer.Serialize(withFinding.Roles);
        Assert.DoesNotContain("09:00", chains, StringComparison.Ordinal);
        Assert.DoesNotContain("09:15", chains, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Spec_scenario_a_misaligned_configuration_is_still_previewed_not_rejected()
    {
        // Reported, never rejected: misalignment is a property of two resources'
        // opening hours, not of the service, and nothing about the service is
        // wrong.
        var (controller, services) = Wire(MisalignedPair());

        var result = await controller.PreviewServiceConfiguration(TwoRoles());

        Assert.IsType<OkObjectResult>(result);

        // Still read-only, as for any other configuration: nothing was saved.
        Assert.Equal(0, (await services.ListAsync(0, 50)).Total);
    }

    [Fact]
    public async Task The_finding_agrees_with_Core_rather_than_being_recomputed()
    {
        // The property the endpoint exists to preserve, applied to the new
        // member: the answer IS Core's answer, over the pools Core resolves.
        var resourceStore = new InMemoryResourceStore();
        foreach (var resource in MisalignedPair())
        {
            resourceStore.Add(resource);
        }

        var service = Service.Create(
            "Treatment",
            ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
            [ServiceRole.Create(ResourceTypes.Room, null).Value, ServiceRole.Create("therapist", null).Value]).Value;

        var services = new InMemoryServiceStore().Add(service);
        var resolution = TestData.ServiceBooking(services, resourceStore);
        var controller = new ServicesController(services, services, resolution);

        var response = Ok(await controller.PreviewServiceConfiguration(TwoRoles()));

        var pools = await resolution.ResolveCandidatesAsync(service.Id);
        Assert.True(pools.Succeeded);

        var core = StartAlignment.FindMisalignment(pools.Value);
        Assert.NotNull(core);
        Assert.NotNull(response.StartMisalignment);

        Assert.Equal(core.First.Resource.Id, response.StartMisalignment.First.ResourceId);
        Assert.Equal(core.Second.Resource.Id, response.StartMisalignment.Second.ResourceId);
        Assert.Equal(
            (int)core.First.Granularity.TotalMinutes, response.StartMisalignment.First.GranularityMinutes);
    }

    // ------------------------------------------------------------ contract shape

    /// <summary>
    /// Spec scenario "Authorization is required": asserted structurally — the section
    /// policy is on the shared base controller, so a request without backoffice
    /// authentication is rejected by the framework before the action runs. The verb
    /// policy the action itself names is covered where every action's is:
    /// <c>PermissionsTests</c>' recorded classification and real-pipeline checks.
    /// </summary>
    [Fact]
    public void Spec_scenario_authorization_is_required()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ServicesController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));

        // And the action carries no opt-out of its own.
        Assert.Null(
            typeof(ServicesController)
                .GetMethod(nameof(ServicesController.PreviewServiceConfiguration))!
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
    }

    [Fact]
    public void The_endpoint_is_a_post_on_a_literal_route()
    {
        // POST because a duration specification is a structured value carrying a
        // kind and whichever bounds apply; flattening it into query parameters
        // would reproduce the ambiguity ServiceDuration exists to prevent
        // (design D3).
        var route = typeof(ServicesController)
            .GetMethod(nameof(ServicesController.PreviewServiceConfiguration))!
            .GetCustomAttribute<HttpPostAttribute>();

        Assert.NotNull(route);
        Assert.Equal("services/preview", route.Template);
    }

    // ---------------------------------------------------------------------
    // The pool-sufficiency finding (resource-management spec, "The
    // configuration preview reports a structurally insufficient pool").
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Spec_scenario_an_insufficient_configuration_is_reported()
    {
        // One role of count 2 over a single eligible room.
        var (controller, _) = Wire(Room(1, "Red Room", 480));

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(null, Role(count: 2))));

        Assert.NotNull(response.PoolShortfall);
        Assert.Equal(2, response.PoolShortfall.Required);
        Assert.Equal(1, response.PoolShortfall.Eligible);
        Assert.Equal(
            [ResourceTypes.Room],
            response.PoolShortfall.Roles.Select(r => r.ResourceType));
        Assert.Equal(2, Assert.Single(response.PoolShortfall.Roles).Count);

        // And the chains come back unchanged beside it: the finding is a claim
        // about the roles together, not a correction to what either resolves to.
        Assert.Equal(1, Single(response).CanProvide.Total);
    }

    [Fact]
    public async Task Spec_scenario_a_sufficient_configuration_reports_nothing()
    {
        var (controller, _) = Wire(TenRooms());

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(null, Role(count: 2))));

        // Absence, not a zero-shortfall record — the endpoint adds no claim Core
        // declines to make.
        Assert.Null(response.PoolShortfall);
    }

    [Fact]
    public async Task Spec_scenario_two_roles_competing_for_one_resource_are_reported_together()
    {
        var (controller, _) = Wire(Therapist(1, "Mary", "cert-x"));

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(
                null,
                Role("therapist", ["cert-x"]),
                Role("therapist"))));

        Assert.NotNull(response.PoolShortfall);

        // One finding naming both roles, not one finding per role.
        Assert.Equal(2, response.PoolShortfall.Roles.Count);
        Assert.Equal(2, response.PoolShortfall.Required);
        Assert.Equal(1, response.PoolShortfall.Eligible);

        // In the order the request supplied them, and told apart by what each
        // requires — the roles are otherwise indistinguishable on screen.
        Assert.Equal(["cert-x"], response.PoolShortfall.Roles[0].RequiredCapabilities);
        Assert.Empty(response.PoolShortfall.Roles[1].RequiredCapabilities);
    }

    [Fact]
    public async Task Spec_scenario_each_named_role_carries_its_position_in_the_request()
    {
        // The healthy `room` sits first, so the finding names positions 1 and 2 —
        // its own array would say 0 and 1, and those are different rows.
        var (controller, _) = Wire(Room(1, "Red Room", 480), Therapist(2, "Mary", "cert-x"));

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(
                null,
                Role(),
                Role("therapist", ["cert-x"]),
                Role("therapist"))));

        Assert.NotNull(response.PoolShortfall);
        Assert.Equal([1, 2], response.PoolShortfall.Roles.Select(r => r.RoleIndex));
    }

    [Fact]
    public async Task Two_roles_alike_in_every_field_are_distinguishable_in_the_payload()
    {
        // Unsaveable, but reachable while an editor is correcting it — and the
        // only thing that tells the two apart on the wire is the position.
        var (controller, _) = Wire(Therapist(1, "Mary"));

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(null, Role("therapist"), Role("therapist"))));

        Assert.NotNull(response.PoolShortfall);

        var roles = response.PoolShortfall.Roles;

        Assert.Equal([0, 1], roles.Select(r => r.RoleIndex));
        Assert.Equal(roles[0].ResourceType, roles[1].ResourceType);
        Assert.Equal(roles[0].RequiredCapabilities, roles[1].RequiredCapabilities);
    }

    [Fact]
    public async Task Spec_scenario_the_chains_are_not_altered_by_the_finding()
    {
        // Two roles whose pools are the same two therapists, each needing two.
        // Each chain still reports two — it is true of the role it describes — and
        // the joint claim lives only in the new member.
        var (controller, _) = Wire(Therapist(1, "Mary", "cert-x"), Therapist(2, "Gwen", "cert-x"));

        var response = Ok(await controller.PreviewServiceConfiguration(
            Configuration(
                null,
                Role("therapist", ["cert-x"], count: 2),
                Role("therapist", count: 2))));

        Assert.All(response.Roles, chain => Assert.Equal(2, chain.CanProvide.Total));

        Assert.NotNull(response.PoolShortfall);
        Assert.Equal(4, response.PoolShortfall.Required);
        Assert.Equal(2, response.PoolShortfall.Eligible);
    }

    [Fact]
    public async Task Spec_scenario_an_insufficient_configuration_is_still_previewed()
    {
        // Reported, never rejected — exactly as duplicate resource types are.
        // Refusing to answer would withhold the information the fix needs.
        var (controller, _) = Wire(Room(1, "Red Room", 480));

        var result = await controller.PreviewServiceConfiguration(
            Configuration(null, Role(count: 5)));

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result).StatusCode);
        Assert.NotEmpty(Ok(result).Roles);
    }

    [Fact]
    public async Task An_omitted_count_is_one()
    {
        // Every request written before counts were carried means a count of 1, and
        // must keep reporting what it always did.
        var (controller, _) = Wire(Room(1, "Red Room", 480));

        var response = Ok(await controller.PreviewServiceConfiguration(new ServicePreviewRequestModel
        {
            Roles = [new ServicePreviewRoleModel { ResourceType = ResourceTypes.Room }],
            Duration = FixedMinutes(60),
        }));

        Assert.Null(response.PoolShortfall);
        Assert.Equal(1, Single(response).CanProvide.Total);
    }

    [Fact]
    public async Task Spec_scenario_a_count_does_not_change_any_chain()
    {
        // Count is not a filter resolution applies: a role of count 3 draws on
        // exactly the pool a role of count 1 does. Carried on the request only
        // because the sufficiency finding beside the chains cannot be asked
        // without it — and the chains must be provably untouched by it.
        var (controller, _) = Wire(TenRooms());

        async Task<int[]> StagesFor(int count)
        {
            var response = Ok(await controller.PreviewServiceConfiguration(
                Configuration(FixedMinutes(240), Role(capabilities: ["projector"], count: count))));

            var chain = Single(response);
            return [chain.OfType.Total, chain.WithCapabilities.Total, chain.CanProvide.Total];
        }

        var one = await StagesFor(1);

        Assert.Equal(one, await StagesFor(3));

        // And the count that changed nothing did change the finding, so this is
        // not passing because the count was ignored altogether.
        Assert.Equal([10, 3, 1], one);
        Assert.NotNull(Ok(await controller.PreviewServiceConfiguration(
            Configuration(FixedMinutes(240), Role(capabilities: ["projector"], count: 3)))).PoolShortfall);
    }

    [Fact]
    public async Task An_out_of_range_count_is_a_validation_failure_against_its_own_row()
    {
        // The count is now an input to part of the answer, so a preview cannot
        // silently narrow it — the same rule a malformed type key already follows,
        // reported through the same code the save reports, against the row that
        // carries it.
        var (controller, _) = Wire(Room(1, "Red Room", 480));

        var result = await controller.PreviewServiceConfiguration(
            Configuration(null, Role(), Role("therapist", count: 0)));

        var (status, codes) = Problem(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal([FailureCodes.ServiceRoleCountInvalid], codes);
        Assert.Equal(new string?[] { "Roles[1].Count" }, Fields(result));
    }

    [Fact]
    public async Task A_count_above_the_permitted_maximum_is_rejected_the_same_way()
    {
        // The scenario says "zero **or above the permitted maximum**", and the two
        // are different branches of the bound. Testing only zero would leave the
        // upper half resting on the assumption that one implementation covers
        // both — which is true today and is exactly the kind of assumption that
        // stops being true quietly.
        var (controller, _) = Wire(Room(1, "Red Room", 480));

        var result = await controller.PreviewServiceConfiguration(
            Configuration(null, Role(), Role("therapist", count: ServiceRole.MaxCount + 1)));

        var (status, codes) = Problem(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal([FailureCodes.ServiceRoleCountInvalid], codes);
        Assert.Equal(new string?[] { "Roles[1].Count" }, Fields(result));
    }

    [Fact]
    public async Task The_permitted_maximum_itself_is_accepted()
    {
        // The boundary on the other side, so the bound is pinned rather than
        // merely present: MaxCount must be allowed, or the rejection above could
        // pass with an off-by-one.
        var (controller, _) = Wire(TenRooms());

        var result = await controller.PreviewServiceConfiguration(
            Configuration(null, Role(count: ServiceRole.MaxCount)));

        Assert.IsType<OkObjectResult>(result);
    }
}
