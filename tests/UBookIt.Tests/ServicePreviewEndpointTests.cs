using System.Reflection;
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
        string resourceType = ResourceTypes.Room, string[]? capabilities = null)
        => new() { ResourceType = resourceType, RequiredCapabilities = [.. capabilities ?? []] };

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

    // ------------------------------------------------------------ contract shape

    /// <summary>
    /// Spec scenario "Authorization is required": asserted structurally, as this
    /// repository asserts every other management endpoint's authorization — the
    /// policy is on the shared base controller, so a request without backoffice
    /// authentication is rejected by the framework before the action runs.
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
}
