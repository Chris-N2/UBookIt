using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using UBookIt.Core.Common;
using UBookIt.Web.Models;

namespace UBookIt.Web.Mapping;

/// <summary>
/// Maps domain failures to RFC 7807 problem details for the delivery API
/// (design D3 — a Web-local mapper, deliberately independent of the backoffice
/// one; the shared contract is the stable <see cref="FailureCodes"/>, not the
/// HTTP projection). Status per code: <c>conflict</c> → 409;
/// <c>resource-not-found</c>/<c>booking-not-found</c>/<c>service-not-found</c>
/// → 404; every other domain code → 400. Every failed rule is echoed as
/// {code, message, field} in the "errors" extension, codes verbatim from the
/// domain.
/// <para>
/// <c>service-unavailable</c> takes the default 400 deliberately: it reports
/// that no eligible resource can fulfil the request as stated, the same category
/// as <c>outside-open-hours</c>. A distinct status would put meaning on the
/// status line that the stable code already carries (book-via-service D10).
/// </para>
/// </summary>
internal static class ApiResults
{
    internal static IActionResult ToProblemResult(this IReadOnlyList<DomainFailure> failures)
    {
        var status = failures switch
        {
            _ when failures.Any(f => f.Code is FailureCodes.ResourceNotFound
                or FailureCodes.BookingNotFound
                or FailureCodes.ServiceNotFound)
                => StatusCodes.Status404NotFound,
            _ when failures.Any(f => f.Code == FailureCodes.Conflict)
                => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails
        {
            // Kept deliberately in step with the backoffice mapper's `Type`
            // (see its note): the two projections are independent but must not
            // drift in envelope shape. Additive — previously absent.
            Type = status switch
            {
                StatusCodes.Status404NotFound => "NotFound",
                StatusCodes.Status409Conflict => "Conflict",
                _ => "ValidationFailed",
            },
            Title = status == StatusCodes.Status400BadRequest ? "Validation failed" : failures[0].Message,
            Status = status,
        };
        problem.Extensions["errors"] = failures
            .Select(f => new ApiErrorModel { Code = f.Code, Message = f.Message, Field = f.Field })
            .ToArray();

        return new ObjectResult(problem) { StatusCode = status };
    }

    /// <summary>Convenience for a single-failure problem result.</summary>
    internal static IActionResult ToProblemResult(this DomainFailure failure)
        => new[] { failure }.ToProblemResult();

    /// <summary>
    /// Projects model-binding/transport failures into the same RFC 7807
    /// envelope as domain failures (design D7): 400 with every entry as
    /// {code, message, field} under "errors", using the stable transport code
    /// <see cref="Constants.InvalidRequestCode"/> and the model-state key as the
    /// field. Wired via the delivery composer's <c>InvalidModelStateResponseFactory</c>.
    /// </summary>
    internal static IActionResult ToValidationProblemResult(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiErrorModel
            {
                Code = Constants.InvalidRequestCode,
                Message = string.IsNullOrEmpty(error.ErrorMessage)
                    ? "The request could not be read."
                    : error.ErrorMessage,
                Field = string.IsNullOrEmpty(entry.Key) ? null : entry.Key,
            }))
            .ToArray();

        if (errors.Length == 0)
        {
            errors = [new ApiErrorModel { Code = Constants.InvalidRequestCode, Message = "The request is invalid." }];
        }

        var problem = new ProblemDetails
        {
            Type = "ValidationFailed",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
        };
        problem.Extensions["errors"] = errors;

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
