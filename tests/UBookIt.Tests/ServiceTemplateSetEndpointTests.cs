using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The email template set through the service Management API (service-email-templates: the
/// <c>services</c> delta's "A service may name an email template set") — the round-trip, the
/// normalisation as a client sees it, and every rejection reported in the same 400 as every other
/// failure of the save.
/// </summary>
public class ServiceTemplateSetEndpointTests
{
    private static ServicesController Wire()
    {
        var store = new InMemoryServiceStore();
        return new ServicesController(store, store, TestData.ServiceBooking(store, new InMemoryResourceStore()));
    }

    private static ServiceRequestModel Request(string? templateSet = null, string name = "Online consultation") => new()
    {
        Name = name,
        Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
        Roles = [new ServiceRoleModel { ResourceType = "consultant", Count = 1 }],
        TemplateSet = templateSet,
    };

    private static T Ok<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, ApiErrorModel[] Errors) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        return (obj.StatusCode!.Value, Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]));
    }

    [Fact]
    public async Task A_template_set_round_trips_through_create_and_get()
    {
        var controller = Wire();

        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request("remote")));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        Assert.Equal("remote", created.TemplateSet);
        Assert.Equal("remote", fetched.TemplateSet);
    }

    [Fact]
    public async Task A_template_set_round_trips_through_update_and_the_list()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request()));

        Ok<ServiceResponseModel>(await controller.UpdateService(created.Id, Request("in-person")));

        var listed = Ok<PagedServicesModel>(await controller.ListServices());
        Assert.Equal("in-person", Assert.Single(listed.Items).TemplateSet);
    }

    [Fact]
    public async Task Upper_case_is_lowered_and_the_response_carries_the_stored_value()
    {
        var controller = Wire();

        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request("Remote-Consult")));

        Assert.Equal("remote-consult", created.TemplateSet);
        Assert.Equal("remote-consult", Ok<ServiceResponseModel>(await controller.GetService(created.Id)).TemplateSet);
    }

    [Fact]
    public async Task Surrounding_whitespace_is_trimmed()
        => Assert.Equal("remote", Ok<ServiceResponseModel>(await Wire().CreateService(Request("  remote  "))).TemplateSet);

    [Fact]
    public async Task Whitespace_alone_means_none_and_the_response_carries_null()
        => Assert.Null(Ok<ServiceResponseModel>(await Wire().CreateService(Request(" \t "))).TemplateSet);

    [Fact]
    public async Task A_service_without_a_set_reports_null_not_an_omission()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request()));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        // Serialised as the web defaults do, which never drop a null member.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fetched, JsonSerializerOptions.Web));

        Assert.True(json.RootElement.TryGetProperty("templateSet", out var member));
        Assert.Equal(JsonValueKind.Null, member.ValueKind);
    }

    [Theory]
    [InlineData("../partials")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData(".")]
    [InlineData("in person")]
    [InlineData("-remote")]
    [InlineData("remote-")]
    [InlineData("re--mote")]
    [InlineData("café")]
    public async Task An_invalid_name_is_a_400_with_its_code_and_field_and_nothing_is_stored(string templateSet)
    {
        var controller = Wire();

        var (status, errors) = Problem(await controller.CreateService(Request(templateSet)));

        Assert.Equal(400, status);
        var error = Assert.Single(errors);
        Assert.Equal(FailureCodes.ServiceTemplateSetInvalid, error.Code);
        Assert.Equal("templateSet", error.Field);
        Assert.Equal(0, Ok<PagedServicesModel>(await controller.ListServices()).Total);
    }

    [Fact]
    public async Task Sixty_four_characters_are_accepted_and_sixty_five_rejected()
    {
        var controller = Wire();

        var accepted = Ok<ServiceResponseModel>(await controller.CreateService(Request(new string('a', 64))));
        var (status, errors) = Problem(await controller.CreateService(Request(new string('a', 65))));

        Assert.Equal(new string('a', 64), accepted.TemplateSet);
        Assert.Equal(400, status);
        Assert.Equal(FailureCodes.ServiceTemplateSetInvalid, Assert.Single(errors).Code);
    }

    [Fact]
    public async Task A_blank_name_and_an_invalid_set_are_both_reported_at_once()
    {
        var (status, errors) = Problem(await Wire().CreateService(Request("../x", name: "")));

        Assert.Equal(400, status);
        Assert.Equal(
            new[] { FailureCodes.ServiceNameRequired, FailureCodes.ServiceTemplateSetInvalid },
            errors.Select(e => e.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Over_long_instructions_and_an_invalid_set_are_both_reported_at_once()
    {
        var request = Request("../x");
        request.BookerInstructions = new string('a', 2001);

        var (_, errors) = Problem(await Wire().CreateService(request));

        Assert.Equal(
            new[] { FailureCodes.ServiceBookerInstructionsTooLong, FailureCodes.ServiceTemplateSetInvalid },
            errors.Select(e => e.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_bad_duration_and_an_invalid_set_are_both_reported_at_once()
    {
        var request = Request("../x");
        request.Duration = null;

        var (_, errors) = Problem(await Wire().CreateService(request));

        Assert.Contains(errors, e => e.Code == FailureCodes.ServiceDurationInvalid);
        Assert.Contains(errors, e => e.Code == FailureCodes.ServiceTemplateSetInvalid);
    }

    [Fact]
    public async Task An_invalid_update_leaves_the_stored_set_alone()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request("remote")));

        var (status, _) = Problem(await controller.UpdateService(created.Id, Request("../x")));

        Assert.Equal(400, status);
        Assert.Equal("remote", Ok<ServiceResponseModel>(await controller.GetService(created.Id)).TemplateSet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_update_with_null_or_blank_removes_the_set(string? templateSet)
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request("remote")));

        var updated = Ok<ServiceResponseModel>(await controller.UpdateService(created.Id, Request(templateSet)));

        Assert.Null(updated.TemplateSet);
        Assert.Null(Ok<ServiceResponseModel>(await controller.GetService(created.Id)).TemplateSet);
    }

    [Fact]
    public async Task Two_services_may_share_a_set()
    {
        var controller = Wire();

        var first = Ok<ServiceResponseModel>(await controller.CreateService(Request("remote", "Online consultation")));
        var second = Ok<ServiceResponseModel>(await controller.CreateService(Request("remote", "Online follow-up")));

        Assert.Equal("remote", first.TemplateSet);
        Assert.Equal("remote", second.TemplateSet);
        Assert.All(Ok<PagedServicesModel>(await controller.ListServices()).Items, s => Assert.Equal("remote", s.TemplateSet));
    }

    [Fact]
    public async Task Instructions_and_a_set_are_saved_together()
    {
        var request = Request("remote");
        request.BookerInstructions = "Join on Teams";

        var created = Ok<ServiceResponseModel>(await Wire().CreateService(request));

        Assert.Equal("remote", created.TemplateSet);
        Assert.Equal("Join on Teams", created.BookerInstructions);
    }
}
