using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Common;
using UBookIt.Persistence.Responsibility;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// Reads and writes the responsible parties of a resource or service. Authorization comes
/// from the shared base controller: the package's own section, and nothing weaker —
/// deliberately the SAME authorization as every other endpoint, because responsibility is
/// who is emailed about bookings and grants nothing, so there is nothing here for a finer
/// policy to protect.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writes replace wholesale and accept dangling party keys.</b> A save that races the
/// deletion of a user must not fail the whole editor save; a party that no longer
/// resolves is an expected state, marked by the read and skipped by sending. Writing to a
/// subject that does not exist IS refused — that is a stale editor, not a race worth
/// absorbing.
/// </para>
/// <para>
/// <b>The read does not require the subject to exist.</b> Assignments for a concurrently
/// deleted subject read back as whatever remains (the delete removes them, so ordinarily
/// nothing), which is an empty list rather than an error — the editor showing them is
/// about to find out its subject is gone from the subject's own endpoint.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ResponsibilityController(
    IResponsibilityStore store,
    IResponsibleRecipientResolver resolver) : UBookItBackofficeApiControllerBase
{
    /// <summary>Not in Core's <c>FailureCodes</c>: the code names a contract-level shape error this controller owns.</summary>
    internal const string UnknownPartyKind = "responsibility-party-kind-unknown";

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("resources/{id:guid}/responsibility")]
    [ProducesResponseType<ResponsibilityResponseModel>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetResourceResponsibility(Guid id, CancellationToken cancellationToken = default)
        => GetAsync(ResponsibilitySubject.Resource, id, cancellationToken);

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpGet("services/{id:guid}/responsibility")]
    [ProducesResponseType<ResponsibilityResponseModel>(StatusCodes.Status200OK)]
    public Task<IActionResult> GetServiceResponsibility(Guid id, CancellationToken cancellationToken = default)
        => GetAsync(ResponsibilitySubject.Service, id, cancellationToken);

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpPut("resources/{id:guid}/responsibility")]
    [ProducesResponseType<ResponsibilityResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> PutResourceResponsibility(
        Guid id, ResponsibilityRequestModel model, CancellationToken cancellationToken = default)
        => PutAsync(ResponsibilitySubject.Resource, id, model, cancellationToken);

    [Authorize(Policy = Constants.VerbPolicies.Configure)]
    [HttpPut("services/{id:guid}/responsibility")]
    [ProducesResponseType<ResponsibilityResponseModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IActionResult> PutServiceResponsibility(
        Guid id, ResponsibilityRequestModel model, CancellationToken cancellationToken = default)
        => PutAsync(ResponsibilitySubject.Service, id, model, cancellationToken);

    private async Task<IActionResult> GetAsync(
        ResponsibilitySubject subject, Guid id, CancellationToken cancellationToken)
    {
        var assignments = await store.GetAsync(subject, id, cancellationToken);

        return Ok(await DescribeAsync(assignments, cancellationToken));
    }

    private async Task<IActionResult> PutAsync(
        ResponsibilitySubject subject, Guid id, ResponsibilityRequestModel model,
        CancellationToken cancellationToken)
    {
        var assignments = new List<ResponsibilityAssignment>(model.Assignments.Count);
        var failures = new List<DomainFailure>();

        foreach (var assignment in model.Assignments)
        {
            // Exact and case-sensitive, matching how the package treats its other
            // normalized keys: "User" is not silently accepted as "user", because a
            // caller whose casing is wrong once is wrong everywhere and should hear so
            // at the first call.
            switch (assignment.Kind)
            {
                case "user":
                    assignments.Add(new ResponsibilityAssignment(ResponsibilityPartyKind.User, assignment.Key));
                    break;
                case "group":
                    assignments.Add(new ResponsibilityAssignment(ResponsibilityPartyKind.Group, assignment.Key));
                    break;
                default:
                    failures.Add(new DomainFailure(
                        UnknownPartyKind,
                        $"'{assignment.Kind}' is not a responsibility party kind. Use 'user' or 'group'.",
                        nameof(ResponsibilityAssignmentModel.Kind)));
                    break;
            }
        }

        if (failures.Count > 0)
        {
            return failures.ToProblemResult();
        }

        var result = await store.ReplaceAsync(subject, id, assignments, cancellationToken);

        if (!result.Succeeded)
        {
            return result.Failures.ToProblemResult();
        }

        // Read back through the same path the GET uses, annotations included, so what the
        // editor holds after a save is exactly what a reload would show it.
        var stored = await store.GetAsync(subject, id, cancellationToken);

        return Ok(await DescribeAsync(stored, cancellationToken));
    }

    private async Task<ResponsibilityResponseModel> DescribeAsync(
        IReadOnlyList<ResponsibilityAssignment> assignments, CancellationToken cancellationToken)
    {
        var statuses = await resolver.DescribeAsync(assignments, cancellationToken);

        return new ResponsibilityResponseModel
        {
            Assignments = statuses.Select(status => new ResponsibilityPartyModel
            {
                Kind = status.Assignment.Kind == ResponsibilityPartyKind.User ? "user" : "group",
                Key = status.Assignment.Key,
                Exists = status.Exists,
                DisplayName = status.DisplayName,
                UserState = status.UserState,
            }).ToList(),
        };
    }
}
