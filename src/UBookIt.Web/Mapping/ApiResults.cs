using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Core.Common;
using UBookIt.Web.Models;

namespace UBookIt.Web.Mapping;

/// <summary>
/// Maps domain failures to RFC 7807 problem details for the delivery API
/// (design D3 — a Web-local mapper, deliberately independent of the backoffice
/// one; the shared contract is the stable <see cref="FailureCodes"/>, not the
/// HTTP projection). Status per code: <c>conflict</c> → 409;
/// <c>resource-not-found</c>/<c>booking-not-found</c> → 404; every other domain
/// code → 400. Every failed rule is echoed as {code, message, field} in the
/// "errors" extension, codes verbatim from the domain.
/// </summary>
internal static class ApiResults
{
    internal static IActionResult ToProblemResult(this IReadOnlyList<DomainFailure> failures)
    {
        var status = failures switch
        {
            _ when failures.Any(f => f.Code is FailureCodes.ResourceNotFound or FailureCodes.BookingNotFound)
                => StatusCodes.Status404NotFound,
            _ when failures.Any(f => f.Code == FailureCodes.Conflict)
                => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails
        {
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
}
