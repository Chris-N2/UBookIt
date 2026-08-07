using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;

namespace UBookIt.Backoffice.Mapping;

/// <summary>
/// Maps domain failures to RFC 7807 problem details (design D6): 404 for
/// resource-not-found, 409 for resource-in-use, 400 for validation — always
/// carrying every failed rule as {code, message, field} in the "errors"
/// extension, codes verbatim from the domain.
/// </summary>
internal static class ApiResults
{
    internal static IActionResult ToProblemResult(this IReadOnlyList<DomainFailure> failures)
    {
        var status = failures switch
        {
            _ when failures.Any(f => f.Code is FailureCodes.ResourceNotFound or FailureCodes.ServiceNotFound)
                => StatusCodes.Status404NotFound,
            _ when failures.Any(f => f.Code == FailureCodes.ResourceInUse) => StatusCodes.Status409Conflict,
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
}
