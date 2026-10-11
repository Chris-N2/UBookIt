using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The management contract for closure ranges and closed periods (closure-date-ranges: the
/// <c>site-closures</c> endpoint scenarios and the <c>resource-management</c> CRUD scenarios).
/// </summary>
public class ClosureRangeEndpointTests
{
    private static readonly DateOnly Start = new(2027, 2, 1);

    private static CancellationToken Ct => CancellationToken.None;

    private static (ClosuresController Controller, InMemorySiteClosureStore Store) WireClosures(DateTimeOffset? now = null)
    {
        var store = new InMemorySiteClosureStore();
        var settings = new SiteBookingSettings { TimeZoneId = "Europe/London" };
        return (new ClosuresController(store, new FixedTimeProvider(now ?? TestData.Now), settings,
            new HolidayPreviewService(store, null)), store);
    }

    private static (ResourcesController Controller, InMemorySiteClosureStore Closures) WireResources()
    {
        var (controller, closures, _) = WireResourcesWithStore();
        return (controller, closures);
    }

    private static (ResourcesController Controller, InMemorySiteClosureStore Closures, InMemoryResourceStore Store) WireResourcesWithStore()
    {
        var closures = new InMemorySiteClosureStore();
        var resources = new InMemoryResourceStore().Hydrating(closures);
        return (new ResourcesController(resources, resources, closures), closures, resources);
    }

    private static T Ok<T>(IActionResult result)
        => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static ApiErrorModel[] Errors(IActionResult result)
    {
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("ValidationFailed", problem.Type);
        return Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);
    }

    // ── Closures ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_body_without_an_end_date_is_a_single_day()
    {
        var (controller, _) = WireClosures();

        var created = Ok<SiteClosureModel>(await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, Label = "Christmas Day" }, Ct));

        Assert.Equal(Start, created.Date);
        Assert.Equal(Start, created.EndDate);
    }

    [Fact]
    public async Task A_range_round_trips_through_the_api()
    {
        var (controller, _) = WireClosures();

        var created = Ok<SiteClosureModel>(await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(58), Label = "Refurbishment" }, Ct));
        var listed = Assert.Single(Ok<List<SiteClosureModel>>(await controller.ListClosures(includePast: true, Ct)));

        Assert.Equal(created.Id, listed.Id);
        Assert.Equal(Start, listed.Date);
        Assert.Equal(Start.AddDays(58), listed.EndDate);
    }

    [Fact]
    public async Task Updating_without_an_end_date_makes_a_single_day()
    {
        var (controller, _) = WireClosures();
        var created = Ok<SiteClosureModel>(await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(9), Label = "Refurbishment" }, Ct));

        var updated = Ok<SiteClosureModel>(await controller.UpdateClosure(
            created.Id, new SiteClosureRequestModel { Date = Start, Label = "Refurbishment" }, Ct));

        Assert.Equal(Start, updated.EndDate);
    }

    [Fact]
    public async Task An_inverted_range_is_problem_details_with_its_code()
    {
        var (controller, _) = WireClosures();

        var result = await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(-1), Label = "Backwards" }, Ct);

        Assert.Equal(FailureCodes.ClosureRangeInvalid, Assert.Single(Errors(result)).Code);
    }

    [Fact]
    public async Task An_overlap_is_problem_details_with_the_duplicate_code()
    {
        var (controller, _) = WireClosures();
        await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(58), Label = "Refurbishment" }, Ct);

        var result = await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start.AddDays(52), EndDate = Start.AddDays(63), Label = "Spring" }, Ct);

        Assert.Equal(FailureCodes.DuplicateClosureDate, Assert.Single(Errors(result)).Code);
    }

    [Fact]
    public async Task Extending_a_closure_is_not_an_overlap_with_itself()
    {
        var (controller, _) = WireClosures();
        var created = Ok<SiteClosureModel>(await controller.CreateClosure(
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(58), Label = "Refurbishment" }, Ct));

        var extended = Ok<SiteClosureModel>(await controller.UpdateClosure(
            created.Id,
            new SiteClosureRequestModel { Date = Start, EndDate = Start.AddDays(88), Label = "Refurbishment" }, Ct));

        Assert.Equal(Start.AddDays(88), extended.EndDate);
    }

    [Fact]
    public async Task A_closure_in_progress_is_upcoming()
    {
        // "Now" is TestData.Now (1 September 2026): a closure from August to October is in progress.
        var (controller, store) = WireClosures();
        store.Add(SiteClosure.CreateRange(new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 31), "Summer works").Value);
        store.Add(SiteClosure.CreateRange(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 5), "Last month").Value);

        var upcoming = Ok<List<SiteClosureModel>>(await controller.ListClosures(includePast: false, Ct));

        Assert.Equal("Summer works", Assert.Single(upcoming).Label);
    }

    // ── Resources ────────────────────────────────────────────────────────────────────────

    private static ResourceRequestModel Request(List<ClosedPeriodModel>? periods, params AvailabilityExceptionModel[] exceptions)
        => new()
        {
            Type = "room",
            DisplayName = "Meeting Room A",
            Exceptions = [.. exceptions],
            ClosedPeriods = periods,
        };

    private static ClosedPeriodModel PeriodModel(int fromDay, int toDay, string? note = null)
        => new() { StartDate = Start.AddDays(fromDay), EndDate = Start.AddDays(toDay), Note = note };

    [Fact]
    public async Task Closed_periods_round_trip()
    {
        var (controller, _) = WireResources();

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request([PeriodModel(0, 13, "Being painted")]), Ct));

        var period = Assert.Single(created.ClosedPeriods);
        Assert.Equal(Start, period.StartDate);
        Assert.Equal(Start.AddDays(13), period.EndDate);
        Assert.Equal("Being painted", period.Note);
    }

    [Fact]
    public async Task An_update_carrying_the_collection_replaces_it()
    {
        var (controller, _) = WireResources();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request([PeriodModel(0, 5), PeriodModel(10, 12)]), Ct));

        var updated = Ok<ResourceResponseModel>(await controller.UpdateResource(
            created.Id, Request([PeriodModel(10, 12)]), Ct));

        Assert.Equal(Start.AddDays(10), Assert.Single(updated.ClosedPeriods).StartDate);
    }

    [Fact]
    public async Task An_update_omitting_the_collection_leaves_the_periods_unchanged()
    {
        var (controller, _) = WireResources();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request([PeriodModel(0, 5)]), Ct));

        var updated = Ok<ResourceResponseModel>(await controller.UpdateResource(
            created.Id, Request(periods: null), Ct));

        Assert.Equal(Start, Assert.Single(updated.ClosedPeriods).StartDate);
    }

    /// <summary>
    /// The production seam. Through this double both store paths leave the same stored result —
    /// so what is asserted is which one the controller CHOSE. Calling the plain update for an
    /// omitted collection would, against SQL Server, delete and rewrite the rows and skip the
    /// re-check under the lock.
    /// </summary>
    [Fact]
    public async Task An_omitted_collection_goes_through_the_update_that_keeps_closed_periods()
    {
        var (controller, _, store) = WireResourcesWithStore();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(Request([PeriodModel(0, 5)]), Ct));

        Ok<ResourceResponseModel>(await controller.UpdateResource(created.Id, Request(periods: null), Ct));
        Assert.True(store.LastUpdateKeptClosedPeriods);

        Ok<ResourceResponseModel>(await controller.UpdateResource(created.Id, Request([PeriodModel(0, 5)]), Ct));
        Assert.False(store.LastUpdateKeptClosedPeriods);
    }

    [Fact]
    public async Task An_empty_collection_clears_the_periods()
    {
        var (controller, _) = WireResources();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request([PeriodModel(0, 5)]), Ct));

        var updated = Ok<ResourceResponseModel>(await controller.UpdateResource(created.Id, Request([]), Ct));

        Assert.Empty(updated.ClosedPeriods);
    }

    [Fact]
    public async Task Omitting_the_collection_still_judges_new_exceptions_against_the_stored_periods()
    {
        var (controller, _) = WireResources();
        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request([PeriodModel(0, 13)]), Ct));

        var result = await controller.UpdateResource(
            created.Id,
            Request(periods: null, new AvailabilityExceptionModel { Date = Start.AddDays(4) }),
            Ct);

        Assert.Equal(FailureCodes.ClosedPeriodConflictsWithException, Assert.Single(Errors(result)).Code);
    }

    [Fact]
    public async Task Overlapping_periods_are_refused_at_the_endpoint()
    {
        var (controller, _) = WireResources();

        var result = await controller.CreateResource(Request([PeriodModel(0, 13), PeriodModel(9, 19)]), Ct);

        Assert.Equal(FailureCodes.ClosedPeriodOverlap, Assert.Single(Errors(result)).Code);
    }

    [Fact]
    public async Task Applied_closures_carry_their_range()
    {
        var (controller, closures) = WireResources();
        closures.Add(SiteClosure.CreateRange(Start, Start.AddDays(58), "Refurbishment").Value);

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(Request(periods: null), Ct));

        var applied = Assert.Single(created.Closures);
        Assert.Equal(Start, applied.Date);
        Assert.Equal(Start.AddDays(58), applied.EndDate);
    }

    [Fact]
    public async Task An_override_inside_a_closure_range_is_marked_superseded()
    {
        var (controller, closures) = WireResources();
        closures.Add(SiteClosure.CreateRange(Start, Start.AddDays(58), "Refurbishment").Value);

        var created = Ok<ResourceResponseModel>(await controller.CreateResource(
            Request(
                periods: null,
                new AvailabilityExceptionModel
                {
                    Date = Start.AddDays(20),
                    Windows = [new TimeWindowModel { Start = new TimeOnly(10, 0), End = new TimeOnly(14, 0) }],
                }),
            Ct));

        Assert.True(Assert.Single(created.Exceptions).Superseded);
    }
}
