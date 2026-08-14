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
        Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
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
        Assert.Equal(ServiceDurationModel.FixedKind, fetched.Duration.Kind);
        Assert.Equal(60, fetched.Duration.Minutes);
        Assert.Equal("person", Assert.Single(fetched.Roles).ResourceType);
    }

    [Fact]
    public async Task Bounded_variable_duration_round_trips()
    {
        var (controller, _) = Wire();
        var request = ValidRequest("Room hire");
        request.Duration = new ServiceDurationModel
        {
            Kind = ServiceDurationModel.VariableKind, MinMinutes = 45, MaxMinutes = 120,
        };

        var created = Ok<ServiceResponseModel>(await controller.CreateService(request));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        Assert.Equal(ServiceDurationModel.VariableKind, fetched.Duration.Kind);
        Assert.Equal(45, fetched.Duration.MinMinutes);
        Assert.Equal(120, fetched.Duration.MaxMinutes);
        Assert.Null(fetched.Duration.Minutes);
    }

    [Fact]
    public async Task Unbounded_variable_duration_round_trips()
    {
        var (controller, _) = Wire();
        var request = ValidRequest("Hot desk");
        request.Duration = new ServiceDurationModel { Kind = ServiceDurationModel.VariableKind };

        var created = Ok<ServiceResponseModel>(await controller.CreateService(request));
        var fetched = Ok<ServiceResponseModel>(await controller.GetService(created.Id));

        Assert.Equal(ServiceDurationModel.VariableKind, fetched.Duration.Kind);
        Assert.Null(fetched.Duration.MinMinutes);
        Assert.Null(fetched.Duration.MaxMinutes);
    }

    [Fact]
    public async Task Inverted_bounds_are_rejected()
    {
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Duration = new ServiceDurationModel
        {
            Kind = ServiceDurationModel.VariableKind, MinMinutes = 120, MaxMinutes = 45,
        };

        var (status, codes) = Problem(await controller.CreateService(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    [Fact]
    public async Task An_unknown_duration_kind_is_rejected_rather_than_defaulted()
    {
        // Guessing a kind would store something the caller never asked for.
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Duration = new ServiceDurationModel { Kind = "whenever" };

        var (status, codes) = Problem(await controller.CreateService(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    [Fact]
    public async Task An_absent_duration_is_rejected_rather_than_defaulted()
    {
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Duration = null;

        var (status, codes) = Problem(await controller.CreateService(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    [Fact]
    public async Task A_fixed_duration_without_a_length_is_rejected()
    {
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind };

        var (status, codes) = Problem(await controller.CreateService(request));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.ServiceDurationInvalid, codes);
    }

    [Fact]
    public async Task Duration_failures_identify_the_offending_input()
    {
        // The editor associates the message with the specific bound control, so
        // the field has to survive the HTTP edge.
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Duration = new ServiceDurationModel
        {
            Kind = ServiceDurationModel.VariableKind, MinMinutes = 120, MaxMinutes = 45,
        };

        var obj = Assert.IsType<ObjectResult>(await controller.CreateService(request));
        var problem = Assert.IsType<ProblemDetails>(obj.Value);
        var errors = Assert.IsType<UBookIt.Backoffice.Models.ApiErrorModel[]>(problem.Extensions["errors"]);

        Assert.Contains(errors, e => e.Field == ServiceDuration.MinField);
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
    public async Task A_malformed_required_capability_is_rejected_with_its_own_code()
    {
        var (controller, _) = Wire();
        var bad = new ServiceRequestModel
        {
            Name = "Massage",
            Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
            Roles = [new ServiceRoleModel
            {
                ResourceType = "therapist",
                RequiredCapabilities = ["Cert X"],
                Count = 1,
            }],
        };

        var (status, codes) = Problem(await controller.CreateService(bad));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task Type_and_capability_failures_are_reported_separately_in_one_response()
    {
        // Distinct codes are what let the editor put each message against the
        // control it belongs to. One save reports every failed rule, so a user
        // fixing both does not need two round trips to discover the second.
        var (controller, _) = Wire();
        var bad = new ServiceRequestModel
        {
            Name = "Massage",
            Duration = new ServiceDurationModel { Kind = ServiceDurationModel.FixedKind, Minutes = 60 },
            Roles = [new ServiceRoleModel
            {
                ResourceType = "Bad Type",
                RequiredCapabilities = ["Cert X"],
                Count = 1,
            }],
        };

        var (status, codes) = Problem(await controller.CreateService(bad));

        Assert.Equal(400, status);
        Assert.Contains(FailureCodes.TypeKeyInvalid, codes);
        Assert.Contains(FailureCodes.CapabilityKeyInvalid, codes);
    }

    [Fact]
    public async Task Required_capabilities_round_trip_through_the_api()
    {
        var (controller, _) = Wire();
        var request = ValidRequest();
        request.Roles[0].RequiredCapabilities = ["cert-x", "welsh"];

        var created = Assert.IsType<OkObjectResult>(await controller.CreateService(request));
        var model = Assert.IsType<ServiceResponseModel>(created.Value);

        Assert.Equal(["cert-x", "welsh"], model.Roles[0].RequiredCapabilities);
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
