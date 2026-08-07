using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Core.Services;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Service management controller behaviour over an in-memory store: DTO
/// round-trip, paged list, not-found mapping, validation mapping, and the
/// containment guarantee. (Anonymous rejection is enforced by the shared base
/// controller's authorization policy — asserted structurally.)
/// </summary>
public class ServicesControllerTests
{
    private static (ServicesController Controller, InMemoryServiceStore Store) Wire()
    {
        var store = new InMemoryServiceStore();
        return (new ServicesController(store, store), store);
    }

    private static ServiceRequestModel ValidRequest(string name = "Massage") => new()
    {
        Name = name,
        DurationMinutes = 60,
        Roles = [new ServiceRoleModel { ResourceType = "person", Count = 1 }],
    };

    private static T Ok<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static (int Status, string[] Codes) Problem(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        var errors = Assert.IsType<UBookIt.Backoffice.Models.ApiErrorModel[]>(problem.Extensions["errors"]);
        return (obj.StatusCode!.Value, errors.Select(e => e.Code).ToArray());
    }

    [Fact]
    public async Task Create_then_get_round_trips()
    {
        var (controller, _) = Wire();

        var created = Ok<ServiceResponseModel>(await controller.CreateService(ValidRequest()));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        Assert.Equal("Massage", fetched.Name);
        Assert.Equal(60, fetched.DurationMinutes);
        Assert.Equal("person", Assert.Single(fetched.Roles).ResourceType);
    }

    [Fact]
    public async Task List_reports_page_and_total()
    {
        var (controller, _) = Wire();
        await controller.CreateService(ValidRequest("A"));
        await controller.CreateService(ValidRequest("B"));

        var page = Ok<PagedServicesModel>(await controller.ListServices(skip: 0, take: 1));

        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task Unknown_id_is_404()
    {
        var (controller, _) = Wire();

        var (status, codes) = Problem(await controller.GetService(Guid.NewGuid()));

        Assert.Equal(404, status);
        Assert.Contains(FailureCodes.ServiceNotFound, codes);
    }

    [Fact]
    public async Task Update_unknown_id_is_404()
    {
        var (controller, _) = Wire();

        var (status, codes) = Problem(await controller.UpdateService(Guid.NewGuid(), ValidRequest()));

        Assert.Equal(404, status);
        Assert.Contains(FailureCodes.ServiceNotFound, codes);
    }

    [Fact]
    public async Task Invalid_submit_is_400_with_codes()
    {
        var (controller, _) = Wire();
        var bad = new ServiceRequestModel
        {
            Name = "",
            Roles = [new ServiceRoleModel { ResourceType = "Bad Type", Count = 1 }],
        };

        var (status, codes) = Problem(await controller.CreateService(bad));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceNameRequired, codes);
        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
    }

    [Fact]
    public void Controller_inherits_the_authorized_backoffice_base()
    {
        Assert.True(typeof(UBookItBackofficeApiControllerBase).IsAssignableFrom(typeof(ServicesController)));
        Assert.NotNull(
            typeof(UBookItBackofficeApiControllerBase)
                .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>(inherit: true));
    }

    [Fact]
    public void Controller_depends_only_on_service_ports_not_booking_storage()
    {
        var paramTypes = typeof(ServicesController)
            .GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType)
            .ToArray();

        Assert.Equal([typeof(IServiceStore), typeof(IServiceManagementStore)], paramTypes);
        Assert.DoesNotContain(typeof(IBookingStore), paramTypes);
    }
}
