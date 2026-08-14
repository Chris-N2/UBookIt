using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Capabilities across the resource management API's DTO → mapper → controller
/// seam (resource-management spec, "Resource CRUD endpoints").
/// <para>
/// The domain and the store each had capability coverage already; this seam did
/// not, so a mapper that dropped the collection would have left both green (QA
/// finding). Scenarios are taken from the spec text.
/// </para>
/// </summary>
public class ResourceCapabilityApiTests
{
    private static (ResourcesController Controller, InMemoryResourceStore Store) Wire()
    {
        var store = new InMemoryResourceStore();
        return (new ResourcesController(store, store), store);
    }

    private static ResourceRequestModel Request(params string[] capabilities) => new()
    {
        Type = "room",
        DisplayName = "Capability Room",
        Capabilities = [.. capabilities],
        Constraints = new ConstraintsModel
        {
            GranularityMinutes = 30,
            MinDurationMinutes = 30,
            MaxDurationMinutes = 240,
            LeadTimeMinutes = 0,
            HorizonDays = 30,
        },
    };

    private static ResourceResponseModel Ok(IActionResult result)
        => Assert.IsType<ResourceResponseModel>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, string[] Codes) Problem(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsAssignableFrom<ProblemDetails>(objectResult.Value);
        var errors = (IEnumerable<ApiErrorModel>)problem.Extensions["errors"]!;

        return (objectResult.StatusCode ?? 0, [.. errors.Select(e => e.Code)]);
    }

    [Fact]
    public async Task Spec_scenario_round_trip_through_the_api()
    {
        var (controller, _) = Wire();

        var created = Ok(await controller.CreateResource(Request("projector", "step-free")));
        var fetched = Ok(await controller.GetResource(created.Id));

        Assert.Equal(["projector", "step-free"], created.Capabilities);
        Assert.Equal(["projector", "step-free"], fetched.Capabilities);
    }

    [Fact]
    public async Task Spec_scenario_update_replaces_the_capability_set()
    {
        var (controller, _) = Wire();
        var created = Ok(await controller.CreateResource(Request("cert-x", "massage")));

        var updated = Ok(await controller.UpdateResource(created.Id, Request("massage")));
        var fetched = Ok(await controller.GetResource(created.Id));

        // Replaced, not merged: cert-x is gone rather than retained alongside.
        Assert.Equal(["massage"], updated.Capabilities);
        Assert.Equal(["massage"], fetched.Capabilities);
    }

    [Fact]
    public async Task Spec_scenario_omitted_capabilities_mean_none()
    {
        var (controller, _) = Wire();

        var created = Ok(await controller.CreateResource(Request()));

        Assert.NotNull(created.Capabilities);
        Assert.Empty(created.Capabilities);
    }

    [Fact]
    public async Task Spec_scenario_malformed_capability_key_is_rejected()
    {
        var (controller, _) = Wire();

        var (status, codes) = Problem(await controller.CreateResource(Request("Cert X")));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task A_malformed_capability_does_not_create_the_resource()
    {
        // A rejected request must leave nothing behind, or a retry would collide
        // with a half-made resource the caller never saw.
        var (controller, store) = Wire();

        await controller.CreateResource(Request("Cert X"));

        var page = await store.ListAsync(0, 50);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task Type_and_capability_failures_are_reported_separately()
    {
        var (controller, _) = Wire();
        var bad = Request("Cert X");
        bad.Type = "Meeting Room";

        var (status, codes) = Problem(await controller.CreateResource(bad));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task An_over_long_capability_key_is_a_validation_failure_not_a_storage_error()
    {
        // Well-formed but longer than the column. Without a domain length rule
        // this passes validation and fails at INSERT as a 500, where the spec
        // promises a stable code.
        var (controller, _) = Wire();
        var tooLong = new string('a', NormalizedKey.MaxLength + 1);

        var (status, codes) = Problem(await controller.CreateResource(Request(tooLong)));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task A_capability_key_at_the_length_limit_is_accepted()
    {
        var (controller, _) = Wire();
        var atLimit = new string('a', NormalizedKey.MaxLength);

        var created = Ok(await controller.CreateResource(Request(atLimit)));

        Assert.Equal([atLimit], created.Capabilities);
    }
}
