using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

public class ResourceModelMapperTests
{
    // ------------------------------------------------------------------
    // Direct bookability, both directions.
    //
    // QA proved this layer was unguarded: `ToDomain` could hardcode false and
    // `ToModel` could drop the member, with all 630 tests still green. The store
    // round-trip covers one layer below and the live pass covers the whole stack;
    // neither is a regression guard on the mapper itself.
    // ------------------------------------------------------------------

    [Fact]
    public void Spec_scenario_the_permission_round_trips_through_the_api()
    {
        var model = ValidModel();
        model.DirectlyBookable = true;

        var domain = ResourceModelMapper.ToDomain(model);

        Assert.True(domain.Succeeded);
        Assert.True(domain.Value.DirectlyBookable);

        // And back out again, which is the direction a dropped member breaks.
        Assert.True(ResourceModelMapper.ToModel(domain.Value).DirectlyBookable);
    }

    [Fact]
    public void Spec_scenario_an_omitted_permission_withholds_it()
    {
        // The request model's default, which is what an omitted JSON member
        // leaves behind. A caller that says nothing is saying no.
        var domain = ResourceModelMapper.ToDomain(ValidModel());

        Assert.True(domain.Succeeded);
        Assert.False(domain.Value.DirectlyBookable);
        Assert.False(ResourceModelMapper.ToModel(domain.Value).DirectlyBookable);
    }

    [Fact]
    public void Spec_scenario_a_full_update_can_withdraw_it()
    {
        // Full-replacement semantics, as the capability set has. A mapper that
        // could only ever turn the permission ON would pass the round-trip test
        // above and fail here, which is the point of having both.
        var granting = ValidModel();
        granting.DirectlyBookable = true;
        Assert.True(ResourceModelMapper.ToDomain(granting).Value.DirectlyBookable);

        var withdrawing = ValidModel();
        withdrawing.DirectlyBookable = false;

        Assert.False(ResourceModelMapper.ToDomain(withdrawing).Value.DirectlyBookable);
    }

    [Fact]
    public void The_permission_does_not_affect_whether_a_resource_is_valid()
    {
        // Neither answer is a validation rule, in either direction — so nothing
        // may start rejecting on its account.
        foreach (var answer in new[] { true, false })
        {
            var model = ValidModel();
            model.DirectlyBookable = answer;

            Assert.True(ResourceModelMapper.ToDomain(model).Succeeded);
        }
    }

    private static ResourceRequestModel ValidModel() => new()
    {
        Type = "room",
        DisplayName = "Mapped Room",
        Description = "A mapped room",
        OpeningHours =
        [
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(8, 0), End = new TimeOnly(12, 0) },
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(13, 0), End = new TimeOnly(17, 0) },
            new OpeningHoursModel { Day = DayOfWeek.Friday, Start = new TimeOnly(9, 0), End = new TimeOnly(13, 0) },
        ],
        Exceptions =
        [
            new AvailabilityExceptionModel { Date = new DateOnly(2026, 12, 24) },
            new AvailabilityExceptionModel
            {
                Date = new DateOnly(2026, 10, 5),
                Windows = [new TimeWindowModel { Start = new TimeOnly(10, 0), End = new TimeOnly(20, 0) }],
            },
        ],
        Constraints = new ConstraintsModel
        {
            GranularityMinutes = 30,
            MinDurationMinutes = 60,
            MaxDurationMinutes = 240,
            LeadTimeMinutes = 120,
            HorizonDays = 30,
        },
    };

    [Fact]
    public void Valid_model_round_trips_through_domain_and_back()
    {
        var result = ResourceModelMapper.ToDomain(ValidModel());

        Assert.True(result.Succeeded);
        var model = ResourceModelMapper.ToModel(result.Value);

        Assert.Equal("room", model.Type);
        Assert.Equal("Mapped Room", model.DisplayName);
        Assert.Equal(3, model.OpeningHours.Count);
        Assert.Equal(2, model.Exceptions.Count);
        Assert.Empty(model.Exceptions.Single(e => e.Date == new DateOnly(2026, 12, 24)).Windows);
        Assert.Single(model.Exceptions.Single(e => e.Date == new DateOnly(2026, 10, 5)).Windows);
        Assert.Equal(30, model.Constraints.GranularityMinutes);
        Assert.Equal(30, model.Constraints.HorizonDays);
    }

    [Fact]
    public void Explicit_id_is_applied()
    {
        var id = Guid.NewGuid();

        var result = ResourceModelMapper.ToDomain(ValidModel(), id);

        Assert.True(result.Succeeded);
        Assert.Equal(id, result.Value.Id);
    }

    [Fact]
    public void Overlapping_windows_and_blank_name_report_both_codes()
    {
        var model = ValidModel();
        model.DisplayName = "  ";
        model.OpeningHours =
        [
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(8, 0), End = new TimeOnly(12, 0) },
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(11, 0), End = new TimeOnly(14, 0) },
        ];

        var result = ResourceModelMapper.ToDomain(model);

        Assert.False(result.Succeeded);
        var codes = result.Failures.Select(f => f.Code).ToArray();
        Assert.Contains(FailureCodes.WindowsOverlap, codes);
        Assert.Contains(FailureCodes.DisplayNameRequired, codes);
    }

    [Fact]
    public void Duplicate_exception_dates_are_rejected()
    {
        var model = ValidModel();
        model.Exceptions =
        [
            new AvailabilityExceptionModel { Date = new DateOnly(2026, 12, 24) },
            new AvailabilityExceptionModel { Date = new DateOnly(2026, 12, 24) },
        ];

        var result = ResourceModelMapper.ToDomain(model);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, f => f.Code == FailureCodes.DuplicateExceptionDate);
    }

    [Fact]
    public void Null_constraints_apply_defaults()
    {
        var model = ValidModel();
        model.Constraints = null;

        var result = ResourceModelMapper.ToDomain(model);

        Assert.True(result.Succeeded);
        Assert.Equal(UBookIt.Core.Availability.BookingConstraints.Default, result.Value.Availability.Constraints);
    }

    [Fact]
    public void Overlapping_windows_and_duplicate_exception_dates_report_together()
    {
        var model = ValidModel();
        model.OpeningHours =
        [
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(8, 0), End = new TimeOnly(12, 0) },
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(11, 0), End = new TimeOnly(14, 0) },
        ];
        model.Exceptions =
        [
            new AvailabilityExceptionModel { Date = new DateOnly(2026, 12, 24) },
            new AvailabilityExceptionModel { Date = new DateOnly(2026, 12, 24) },
        ];

        var result = ResourceModelMapper.ToDomain(model);

        Assert.False(result.Succeeded);
        var codes = result.Failures.Select(f => f.Code).ToArray();
        Assert.Contains(FailureCodes.WindowsOverlap, codes);
        Assert.Contains(FailureCodes.DuplicateExceptionDate, codes);
    }

    [Fact]
    public void Invalid_window_and_invalid_constraints_accumulate()
    {
        var model = ValidModel();
        model.OpeningHours = [new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(12, 0), End = new TimeOnly(12, 0) }];
        model.Constraints!.MinDurationMinutes = 240;
        model.Constraints.MaxDurationMinutes = 60;

        var result = ResourceModelMapper.ToDomain(model);

        Assert.False(result.Succeeded);
        var codes = result.Failures.Select(f => f.Code).ToArray();
        Assert.Contains(FailureCodes.WindowInvalid, codes);
        Assert.Contains(FailureCodes.ConstraintsIncoherent, codes);
    }
}

public class ModelSerializationTests
{
    /// <summary>
    /// The wire contract sends day names ("Monday"), matching the swagger
    /// document, regardless of the host's global JSON options. Regression
    /// guard for the property-level string-enum converter.
    /// </summary>
    [Fact]
    public void Day_round_trips_as_a_name_with_default_json_options()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            new OpeningHoursModel { Day = DayOfWeek.Monday, Start = new TimeOnly(8, 0), End = new TimeOnly(12, 0) },
            System.Text.Json.JsonSerializerOptions.Web);

        Assert.Contains("\"Monday\"", json);

        var parsed = System.Text.Json.JsonSerializer.Deserialize<OpeningHoursModel>(
            """{"day":"Monday","start":"09:00","end":"17:00"}""",
            System.Text.Json.JsonSerializerOptions.Web);

        Assert.Equal(DayOfWeek.Monday, parsed!.Day);
        Assert.Equal(new TimeOnly(9, 0), parsed.Start);
    }
}

public class ApiResultsTests
{
    private static (int? Status, ApiErrorModel[] Errors) Unpack(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        return (objectResult.StatusCode, Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]));
    }

    [Fact]
    public void Validation_failures_map_to_400_with_all_codes()
    {
        var failures = new[]
        {
            new DomainFailure(FailureCodes.WindowsOverlap, "overlap"),
            new DomainFailure(FailureCodes.DisplayNameRequired, "name", "DisplayName"),
        };

        var (status, errors) = Unpack(failures.ToProblemResult());

        Assert.Equal(400, status);
        Assert.Equal(2, errors.Length);
        Assert.Equal("DisplayName", errors[1].Field);
    }

    [Fact]
    public void Resource_not_found_maps_to_404()
    {
        var (status, errors) = Unpack(
            new[] { new DomainFailure(FailureCodes.ResourceNotFound, "missing") }.ToProblemResult());

        Assert.Equal(404, status);
        Assert.Equal(FailureCodes.ResourceNotFound, Assert.Single(errors).Code);
    }

    [Fact]
    public void Resource_in_use_maps_to_409()
    {
        var (status, errors) = Unpack(
            new[] { new DomainFailure(FailureCodes.ResourceInUse, "claimed") }.ToProblemResult());

        Assert.Equal(409, status);
        Assert.Equal(FailureCodes.ResourceInUse, Assert.Single(errors).Code);
    }

    /// <summary>
    /// Regression guard. The backoffice's default error interceptor validates
    /// an error body with `isProblemDetailsLike`, which requires a `type`
    /// member; without one it discards the body — errors included — and
    /// substitutes a generic "A fatal server error occurred" problem. Dropping
    /// `Type` therefore silently turns every field-level validation failure
    /// into an unactionable server error in the editor, with nothing failing
    /// except the user's experience.
    /// </summary>
    [Theory]
    [InlineData(FailureCodes.DisplayNameRequired, "ValidationFailed")]
    [InlineData(FailureCodes.ResourceNotFound, "NotFound")]
    [InlineData(FailureCodes.ResourceInUse, "Conflict")]
    public void Problem_details_always_carry_a_type_member(string code, string expectedType)
    {
        var result = new[] { new DomainFailure(code, "message") }.ToProblemResult();
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);

        Assert.Equal(expectedType, problem.Type);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
    }
}
