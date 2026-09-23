using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Tests.Support;
using Constants = UBookIt.Backoffice.Constants;

namespace UBookIt.Tests;

/// <summary>
/// The closures endpoints, the verb split that guards them, and the resource contract's
/// closure members.
/// </summary>
public class ClosuresControllerTests
{
    private static readonly DateOnly Date = TestData.BaseDate;

    private static CancellationToken Ct => CancellationToken.None;

    private static (ClosuresController Controller, InMemorySiteClosureStore Store) Wire(
        string timeZoneId = "Europe/London", DateTimeOffset? nowUtc = null)
    {
        var store = new InMemorySiteClosureStore();
        var settings = new SiteBookingSettings { TimeZoneId = timeZoneId };
        var time = new FixedTimeProvider(nowUtc ?? TestData.Now);

        return (new ClosuresController(store, time, settings), store);
    }

    private static T Ok<T>(IActionResult result)
        => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static ApiErrorModel[] Errors(IActionResult result)
    {
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        return Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
    }

    // ---- the endpoints ----

    [Fact]
    public async Task A_closure_round_trips_through_the_api()
    {
        var (controller, _) = Wire();

        var created = Ok<SiteClosureModel>(await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Date, Label = "Christmas Day" }, Ct));

        var listed = Ok<List<SiteClosureModel>>(await controller.ListClosures(includePast: true, Ct));

        Assert.Equal(Date, created.Date);
        Assert.Equal("Christmas Day", created.Label);
        Assert.Equal(created.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task A_blank_label_is_a_validation_failure_carrying_the_code()
    {
        var (controller, _) = Wire();

        var result = await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Date, Label = "   " }, Ct);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);

        // `type` is what keeps the body alive through the backoffice's error interceptor;
        // without it the editor sees a generic fatal error instead of this failure.
        Assert.Equal("ValidationFailed", problem.Type);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(FailureCodes.ClosureLabelInvalid, Assert.Single(Errors(result)).Code);
    }

    [Fact]
    public async Task A_duplicate_date_is_refused_with_its_code()
    {
        var (controller, _) = Wire();
        await controller.CreateClosure(new SiteClosureRequestModel { Date = Date, Label = "First" }, Ct);

        var second = await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Date, Label = "Second" }, Ct);

        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(Errors(second)).Code);
    }

    [Fact]
    public async Task The_list_omits_past_closures_by_default()
    {
        var (controller, store) = Wire();
        var past = TestData.Closure(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30), "Last month");
        var future = TestData.Closure(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), "Next month");
        store.Add(past).Add(future);

        var byDefault = Ok<List<SiteClosureModel>>(await controller.ListClosures(cancellationToken: Ct));
        var withPast = Ok<List<SiteClosureModel>>(await controller.ListClosures(includePast: true, Ct));

        Assert.Equal(future.Id, Assert.Single(byDefault).Id);
        Assert.Equal(2, withPast.Count);
    }

    [Fact]
    public async Task An_unknown_closure_is_a_404_answerable_by_status_alone()
    {
        var (controller, _) = Wire();

        var updated = await controller.UpdateClosure(
            Guid.NewGuid(), new SiteClosureRequestModel { Date = Date, Label = "Ghost" }, Ct);
        var deleted = await controller.DeleteClosure(Guid.NewGuid(), Ct);

        // Umbraco's interceptor discards `errors` on a 404, so the status must carry the
        // meaning on its own — measured during find-booking, recorded in ApiResults.
        foreach (var result in new[] { updated, deleted })
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(result).Value);
            Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
            Assert.Equal("NotFound", problem.Type);
        }
    }

    [Fact]
    public async Task Editing_a_closure_keeps_its_id()
    {
        var (controller, store) = Wire();
        var closure = TestData.Closure(Date, "Stocktake");
        store.Add(closure);

        var moved = Ok<SiteClosureModel>(await controller.UpdateClosure(
            closure.Id, new SiteClosureRequestModel { Date = Date.AddDays(1), Label = "Stocktake" }, Ct));

        Assert.Equal(closure.Id, moved.Id);
        Assert.Equal(Date.AddDays(1), moved.Date);
    }

    // ---- the verb split, read off the actions themselves ----

    private static string PolicyOf(string action)
        => typeof(ClosuresController)
            .GetMethod(action)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .Single()!;

    /// <summary>
    /// The attributes name the policies they should. <b>This asserts the wiring, not the
    /// decision</b> — whether those policies then admit the right verbs is evaluated through the
    /// real policy engine in <c>PermissionsTests</c>, because a test that compares attribute
    /// strings would stay green if <c>ClosuresRead</c> were registered with the wrong verbs.
    /// </summary>
    [Fact]
    public void Each_closure_action_names_the_policy_it_should()
    {
        Assert.Equal(Constants.VerbPolicies.ClosuresRead, PolicyOf(nameof(ClosuresController.ListClosures)));

        Assert.Equal(Constants.VerbPolicies.Settings, PolicyOf(nameof(ClosuresController.CreateClosure)));
        Assert.Equal(Constants.VerbPolicies.Settings, PolicyOf(nameof(ClosuresController.UpdateClosure)));
        Assert.Equal(Constants.VerbPolicies.Settings, PolicyOf(nameof(ClosuresController.DeleteClosure)));
    }

    [Fact]
    public void Every_closure_action_names_a_verb_policy()
    {
        // The section gate comes from the base controller and applies to everything; a verb
        // policy is what refines within it, and an action carrying none would be reachable by
        // anyone who can see the section.
        var actions = typeof(ClosuresController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
            Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>()));
    }

    // ---- the resource contract ----

    private static Resource ResourceWith(
        IEnumerable<SiteClosure>? applicable = null,
        IEnumerable<Guid>? optOuts = null,
        IEnumerable<DateException>? exceptions = null)
        => Resource.Create(
            type: ResourceTypes.Room,
            displayName: "Meeting Room A",
            availability: TestData.Config(
                TestData.Weekly("09:00", "17:00", Date.DayOfWeek),
                exceptions: exceptions,
                closures: applicable),
            closureOptOuts: optOuts).Value;

    [Fact]
    public void The_response_projects_every_closure_with_its_excluded_flag()
    {
        var inherited = TestData.Closure(Date, "Christmas Day");
        var exempted = TestData.Closure(Date.AddDays(1), "Boxing Day");

        // As hydration produces it: the exempted closure is absent from the resource's
        // availability, and present in the site list the mapper is given.
        var model = ResourceModelMapper.ToModel(
            ResourceWith(applicable: [inherited], optOuts: [exempted.Id]),
            [inherited, exempted]);

        Assert.Equal(2, model.Closures.Count);
        Assert.False(model.Closures.Single(c => c.Id == inherited.Id).Excluded);
        Assert.True(model.Closures.Single(c => c.Id == exempted.Id).Excluded);

        // The label travels with it: a bare date asks an editor to exempt something they
        // cannot identify.
        Assert.Equal("Boxing Day", model.Closures.Single(c => c.Id == exempted.Id).Label);
    }

    [Fact]
    public void An_override_exception_under_a_closure_is_reported_superseded()
    {
        var closure = TestData.Closure(Date, "Christmas Day");
        var model = ResourceModelMapper.ToModel(
            ResourceWith(
                applicable: [closure],
                exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value]),
            [closure]);

        Assert.True(Assert.Single(model.Exceptions).Superseded);
    }

    [Fact]
    public void A_closure_exception_under_a_closure_is_not_reported_superseded()
    {
        // Closed either way, so there is no difference to report.
        var closure = TestData.Closure(Date, "Christmas Day");
        var model = ResourceModelMapper.ToModel(
            ResourceWith(applicable: [closure], exceptions: [DateException.Closure(Date)]),
            [closure]);

        Assert.False(Assert.Single(model.Exceptions).Superseded);
    }

    [Fact]
    public void An_exempted_resources_exception_is_not_reported_superseded()
    {
        var closure = TestData.Closure(Date, "Christmas Day");

        // Exempted, so hydration hands it no closure at all.
        var model = ResourceModelMapper.ToModel(
            ResourceWith(
                optOuts: [closure.Id],
                exceptions: [DateException.Override(Date, [TestData.Win("10:00", "14:00")]).Value]),
            [closure]);

        Assert.False(Assert.Single(model.Exceptions).Superseded);
    }

    [Fact]
    public void Opt_outs_carry_from_the_request_into_the_domain()
    {
        var closureId = Guid.NewGuid();

        var resource = ResourceModelMapper.ToDomain(new ResourceRequestModel
        {
            Type = "room",
            DisplayName = "Meeting Room A",
            ClosureOptOuts = [closureId],
        });

        Assert.True(resource.Succeeded);
        Assert.Equal(closureId, Assert.Single(resource.Value.ClosureOptOuts));
    }

    [Fact]
    public void An_omitted_opt_out_collection_means_none()
    {
        var resource = ResourceModelMapper.ToDomain(new ResourceRequestModel
        {
            Type = "room",
            DisplayName = "Meeting Room A",
        });

        Assert.True(resource.Succeeded);
        Assert.Empty(resource.Value.ClosureOptOuts);
    }

    // ---- the resource endpoints' own handling of opt-outs ----

    private static (ResourcesController Controller, InMemorySiteClosureStore Closures) WireResources()
    {
        var closures = new InMemorySiteClosureStore();

        // Hydrating, like the shipped store: a double that returns exactly what was written
        // cannot see a response computed from an un-hydrated aggregate.
        var resources = new InMemoryResourceStore().Hydrating(closures);
        return (new ResourcesController(resources, resources, closures), closures);
    }

    private static ResourceRequestModel RequestWith(params Guid[] optOuts) => new()
    {
        Type = "room",
        DisplayName = "Meeting Room A",
        ClosureOptOuts = [.. optOuts],
    };

    [Fact]
    public async Task An_opt_out_naming_no_closure_is_refused_by_the_endpoint()
    {
        var (controller, _) = WireResources();

        var result = await controller.CreateResource(RequestWith(Guid.NewGuid()), Ct);

        // A 400, not a 404: the field-level error survives only on a 400, and this is a
        // validation failure about one member of a resource write rather than a missing
        // resource.
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);

        var error = Assert.Single(Errors(result));
        Assert.Equal(FailureCodes.ClosureNotFound, error.Code);
        Assert.Equal(nameof(ResourceRequestModel.ClosureOptOuts), error.Field);
    }

    [Fact]
    public async Task An_opt_out_naming_a_real_closure_is_accepted_and_reported()
    {
        var (controller, closures) = WireResources();
        var closure = TestData.Closure(Date, "Christmas Day");
        closures.Add(closure);

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(RequestWith(closure.Id), Ct));

        Assert.True(Assert.Single(created.Closures).Excluded);
    }

    [Fact]
    public async Task A_resource_write_reports_its_other_failures_alongside_an_unknown_closure()
    {
        // One save reports every failed rule — the resource-management spec's rule, and the
        // reason the closure check does not return early ahead of the domain's own.
        var (controller, _) = WireResources();

        var result = await controller.CreateResource(new ResourceRequestModel
        {
            Type = "room",
            DisplayName = "   ",
            ClosureOptOuts = [Guid.NewGuid()],
        }, Ct);

        var codes = Errors(result).Select(e => e.Code).ToList();

        Assert.Contains(FailureCodes.DisplayNameRequired, codes);
        Assert.Contains(FailureCodes.ClosureNotFound, codes);
    }

    [Fact]
    public async Task A_resource_response_lists_closures_it_is_not_exempt_from()
    {
        var (controller, closures) = WireResources();
        closures.Add(TestData.Closure(Date, "Christmas Day"));

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(RequestWith(), Ct));

        Assert.False(Assert.Single(created.Closures).Excluded);
    }

    /// <summary>
    /// <b>FOUND IN THE RUNNING BACKOFFICE, not by any test here.</b> The save response was built
    /// from the aggregate the REQUEST mapped to — and a request carries no closures, only
    /// opt-outs — so <c>superseded</c> came back false however the site was configured, while the
    /// GET of the same resource said true. The editor therefore lost the statement the moment
    /// somebody saved, and got it back on reload.
    /// <para>
    /// The fix re-reads the resource through the read port after the write, so the save response
    /// is the same projection a subsequent GET produces. Asserted here as "the two agree", rather
    /// than as "the flag is true", because the defect was the two DISAGREEING.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_save_response_reports_the_superseded_state_the_next_read_would()
    {
        var (controller, closures) = WireResources();
        var closure = TestData.Closure(Date, "Christmas Day");
        closures.Add(closure);

        var request = RequestWith();
        request.OpeningHours = [new OpeningHoursModel { Day = Date.DayOfWeek, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0) }];
        request.Exceptions =
        [
            new AvailabilityExceptionModel
            {
                Date = Date,
                Windows = [new TimeWindowModel { Start = new TimeOnly(10, 0), End = new TimeOnly(14, 0) }],
            },
        ];

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(request, Ct));
        var fetched = Ok<ResourceResponseModel>(await controller.GetResource(created.Id, Ct));

        Assert.Equal(
            Assert.Single(fetched.Exceptions).Superseded,
            Assert.Single(created.Exceptions).Superseded);

        // And it is the TRUE answer, not merely a consistent one: the closure applies.
        Assert.True(Assert.Single(created.Exceptions).Superseded);
    }

    [Fact]
    public async Task An_update_response_reports_the_superseded_state_the_next_read_would()
    {
        var (controller, closures) = WireResources();
        var closure = TestData.Closure(Date, "Christmas Day");
        closures.Add(closure);

        var request = RequestWith();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(request, Ct));

        request.OpeningHours = [new OpeningHoursModel { Day = Date.DayOfWeek, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0) }];
        request.Exceptions =
        [
            new AvailabilityExceptionModel
            {
                Date = Date,
                Windows = [new TimeWindowModel { Start = new TimeOnly(10, 0), End = new TimeOnly(14, 0) }],
            },
        ];

        var updated = Ok<ResourceResponseModel>(await controller.UpdateResource(created.Id, request, Ct));
        var fetched = Ok<ResourceResponseModel>(await controller.GetResource(created.Id, Ct));

        Assert.Equal(
            Assert.Single(fetched.Exceptions).Superseded,
            Assert.Single(updated.Exceptions).Superseded);
        Assert.True(Assert.Single(updated.Exceptions).Superseded);
    }

    /// <summary>
    /// The PAGED LIST reports the same superseded state a single read does.
    /// </summary>
    /// <remarks>
    /// The list maps from the management store rather than the read port, so it is a second
    /// route to the same statement — and it was the route no test covered while the double
    /// hydrated only <c>GetAsync</c>. A readout that disagreed with itself between a list and a
    /// detail view is the same defect the save response had, on a different surface.
    /// </remarks>
    [Fact]
    public async Task The_paged_list_reports_the_superseded_state_a_single_read_does()
    {
        var (controller, closures) = WireResources();
        closures.Add(TestData.Closure(Date, "Christmas Day"));

        var request = RequestWith();
        request.OpeningHours = [new OpeningHoursModel { Day = Date.DayOfWeek, Start = new TimeOnly(9, 0), End = new TimeOnly(17, 0) }];
        request.Exceptions =
        [
            new AvailabilityExceptionModel
            {
                Date = Date,
                Windows = [new TimeWindowModel { Start = new TimeOnly(10, 0), End = new TimeOnly(14, 0) }],
            },
        ];

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(request, Ct));
        var listed = Ok<PagedResourcesModel>(await controller.ListResources(0, 50, Ct));
        var fetched = Ok<ResourceResponseModel>(await controller.GetResource(created.Id, Ct));

        var fromList = Assert.Single(listed.Items, r => r.Id == created.Id);

        Assert.Equal(
            Assert.Single(fetched.Exceptions).Superseded,
            Assert.Single(fromList.Exceptions).Superseded);
        Assert.True(Assert.Single(fromList.Exceptions).Superseded);
    }

    /// <summary>
    /// "Today" for the default filter is today in the SITE's time zone.
    /// </summary>
    /// <remarks>
    /// On a site west of UTC, a UTC "today" drops the current day's closure out of the default
    /// list from the local afternoon — the operator loses the row for the day they are standing
    /// in. Asserted with a fixed clock at an instant where the two dates genuinely differ, so
    /// the test cannot pass by the zones happening to agree.
    /// </remarks>
    [Fact]
    public async Task The_default_filter_uses_the_sites_date_not_the_servers()
    {
        // 03:00 UTC on the 2nd is still 22:00 on the 1st in New York.
        var nowUtc = new DateTimeOffset(2026, 10, 2, 3, 0, 0, TimeSpan.Zero);
        var (controller, store) = Wire("America/New_York", nowUtc);

        var localToday = TestData.Closure(new DateOnly(2026, 10, 1), "Still today in New York");
        store.Add(localToday);

        var listed = Ok<List<SiteClosureModel>>(await controller.ListClosures(cancellationToken: Ct));

        Assert.Equal(localToday.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task An_unreadable_time_zone_still_serves_the_list()
    {
        // The setting has its own validation and its own code; a list is not where that is
        // enforced, and failing here would show nothing at all.
        var (controller, store) = Wire("Not/AZone", new DateTimeOffset(2026, 10, 2, 3, 0, 0, TimeSpan.Zero));
        store.Add(TestData.Closure(new DateOnly(2026, 10, 5), "Later"));

        var listed = Ok<List<SiteClosureModel>>(await controller.ListClosures(cancellationToken: Ct));

        Assert.Single(listed);
    }
}
