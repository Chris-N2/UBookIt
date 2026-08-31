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
            // `BookingNotFound` joins the other two rather than falling through to 400.
            // Cancelling an id nothing has and cancelling a booking that cannot move are
            // different problems calling for different actions — one is a stale list, the
            // other is a booking somebody already dealt with — and a caller that meets 400
            // for both has to read the code to tell them apart. The delivery API already
            // maps it this way; this mapping simply had no endpoint that produced it until
            // now.
            _ when failures.Any(f => f.Code is FailureCodes.ResourceNotFound
                or FailureCodes.ServiceNotFound
                or FailureCodes.BookingNotFound)
                => StatusCodes.Status404NotFound,
            _ when failures.Any(f => f.Code == FailureCodes.ResourceInUse) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails
        {
            // `Type` is not decoration. The backoffice's default error
            // interceptor keeps our body only if `isProblemDetailsLike` passes,
            // and that check requires a `type` member; without it the
            // interceptor discards the payload — errors and all — and
            // substitutes a generic "A fatal server error occurred" problem, so
            // every validation failure reached the editor as an unactionable
            // server error.
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
}
