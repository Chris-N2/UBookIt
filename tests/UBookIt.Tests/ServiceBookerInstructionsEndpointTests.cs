using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Instructions for the booker through the service Management API (service-booker-instructions:
/// the <c>services</c> delta) — the round-trip, the normalisation as a client sees it, and the
/// length failure reported in the same 400 as every other failure of the save.
/// </summary>
public class ServiceBookerInstructionsEndpointTests
{
    private const string TwoLines = "Join on Teams:\nhttps://example.test/meet";

    private static ServicesController Wire()
    {
        var store = new InMemoryServiceStore();
        return new ServicesController(store, store, TestData.ServiceBooking(store, new InMemoryResourceStore()));
    }

    private static ServiceRequestModel Request(string? instructions = null, string name = "Online consultation") => new()
    {
        Name = name,
        Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
        Roles = [new ServiceRoleModel { ResourceType = "consultant", Count = 1 }],
        BookerInstructions = instructions,
    };

    private static T Ok<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, ApiErrorModel[] Errors) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        return (obj.StatusCode!.Value, Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]));
    }

    [Fact]
    public async Task Instructions_round_trip_through_create_and_get_with_their_line_break()
    {
        var controller = Wire();

        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request(TwoLines)));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        Assert.Equal(TwoLines, created.BookerInstructions);
        Assert.Equal(TwoLines, fetched.BookerInstructions);
    }

    [Fact]
    public async Task Instructions_round_trip_through_update_and_the_list()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request()));

        Ok<ServiceResponseModel>(await controller.UpdateService(created.Id, Request(TwoLines)));

        var listed = Ok<PagedServicesModel>(await controller.ListServices());
        Assert.Equal(TwoLines, Assert.Single(listed.Items).BookerInstructions);
    }

    [Fact]
    public async Task Surrounding_whitespace_is_trimmed()
    {
        var created = Ok<ServiceResponseModel>(await Wire().CreateService(Request($"  \n {TwoLines} \n\n")));

        Assert.Equal(TwoLines, created.BookerInstructions);
    }

    [Fact]
    public async Task Whitespace_alone_means_none_and_the_response_carries_null()
    {
        var created = Ok<ServiceResponseModel>(await Wire().CreateService(Request(" \r\n\t ")));

        Assert.Null(created.BookerInstructions);
    }

    [Fact]
    public async Task A_service_without_instructions_reports_null_not_an_omission()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request()));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        // Serialised as the web defaults do, which never drop a null member.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fetched, JsonSerializerOptions.Web));

        Assert.True(json.RootElement.TryGetProperty("bookerInstructions", out var member));
        Assert.Equal(JsonValueKind.Null, member.ValueKind);
    }

    [Fact]
    public async Task Over_long_instructions_are_a_400_with_their_code_and_field_and_nothing_is_stored()
    {
        var controller = Wire();

        var (status, errors) = Problem(await controller.CreateService(Request(new string('a', 2001))));

        Assert.Equal(400, status);
        var error = Assert.Single(errors);
        Assert.Equal(FailureCodes.ServiceBookerInstructionsTooLong, error.Code);
        Assert.Equal("bookerInstructions", error.Field);
        Assert.Equal(0, Ok<PagedServicesModel>(await controller.ListServices()).Total);
    }

    [Fact]
    public async Task Exactly_the_limit_after_trimming_is_accepted()
    {
        var text = new string('a', 2000);

        var created = Ok<ServiceResponseModel>(await Wire().CreateService(Request($" {text}\n")));

        Assert.Equal(text, created.BookerInstructions);
    }

    [Fact]
    public async Task A_blank_name_and_over_long_instructions_are_both_reported_at_once()
    {
        var (status, errors) = Problem(await Wire().CreateService(Request(new string('a', 2001), name: " ")));

        Assert.Equal(400, status);
        Assert.Equal(
            new[] { FailureCodes.ServiceBookerInstructionsTooLong, FailureCodes.ServiceNameRequired },
            errors.Select(e => e.Code).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_bad_duration_and_over_long_instructions_are_both_reported_at_once()
    {
        var request = Request(new string('a', 2001));
        request.Duration = null;

        var (_, errors) = Problem(await Wire().CreateService(request));

        Assert.Contains(errors, e => e.Code == FailureCodes.ServiceDurationInvalid);
        Assert.Contains(errors, e => e.Code == FailureCodes.ServiceBookerInstructionsTooLong);
    }

    [Fact]
    public async Task An_over_long_update_leaves_the_stored_instructions_alone()
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request(TwoLines)));

        var (status, _) = Problem(await controller.UpdateService(created.Id, Request(new string('a', 2001))));

        Assert.Equal(400, status);
        Assert.Equal(TwoLines, Ok<ServiceResponseModel>(await controller.GetService(created.Id)).BookerInstructions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_update_with_null_or_blank_removes_the_instructions(string? instructions)
    {
        var controller = Wire();
        var created = Ok<ServiceResponseModel>(await controller.CreateService(Request(TwoLines)));

        var updated = Ok<ServiceResponseModel>(await controller.UpdateService(created.Id, Request(instructions)));

        Assert.Null(updated.BookerInstructions);
        Assert.Null(Ok<ServiceResponseModel>(await controller.GetService(created.Id)).BookerInstructions);
    }
}
