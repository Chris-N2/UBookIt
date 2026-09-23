using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Backoffice.Controllers;

/// <summary>
/// The site's closure dates: the list every resource inherits unless it is exempted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read and write are guarded by different verbs, and the server holds the line.</b>
/// Reading names <see cref="Constants.VerbPolicies.ClosuresRead"/>, satisfied by
/// <c>UBookIt.Configure</c> or <c>UBookIt.Settings</c> — an operator editing a resource must
/// see what it is inheriting in order to exempt it. Writing names
/// <see cref="Constants.VerbPolicies.Settings"/> alone: one entry shuts every resource the
/// site has, including those created after it, and a grant meaning "may add a meeting room"
/// does not carry that.
/// </para>
/// <para>
/// <b>Closures change what is OFFERED, never what is booked.</b> Nothing here touches a
/// booking: one already placed on a date that becomes closed keeps its interval, its status
/// and its reference, and goes on blocking that time. The screen states this unconditionally
/// rather than counting anything — it is true of every site whatever its data.
/// </para>
/// <para>
/// <b>A 404 is answered by status, not by code.</b> Umbraco's error interceptor discards the
/// <c>errors</c> extension on a 404 (measured, see <c>ApiResults</c>), so an unknown closure
/// is returned as its own not-found result and a client reads the status. The same code on a
/// resource write stays a 400 validation failure, where the field-level error survives and
/// the editor can land it on the control it concerns.
/// </para>
/// </remarks>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UBookIt.Backoffice")]
public class ClosuresController(
    ISiteClosureManagementStore store,
    TimeProvider timeProvider,
    SiteBookingSettings settings) : UBookItBackofficeApiControllerBase
{
    /// <summary>
    /// The site's closures, newest-relevant first by date. <paramref name="includePast"/>
    /// false — the default — returns only today's and later.
    /// </summary>
    /// <remarks>
    /// The filter is applied by the store rather than by the client hiding rows, so the
    /// view's default does not depend on fetching every closure a site has ever recorded.
    /// <para>
    /// <b>"Today" is today in the SITE's time zone</b>, not the server's and not the viewer's.
    /// A closure list is a site-wide statement about the site's own calendar, and UTC would
    /// drop today's closure out of the default list from the local afternoon on any site west
    /// of it. An unreadable zone id falls back to UTC rather than failing the read — the
    /// setting has its own validation, and a list is not the place to enforce it.
    /// </para>
    /// </remarks>
    [Authorize(Policy = Constants.VerbPolicies.ClosuresRead)]
    [HttpGet("closures")]
    [ProducesResponseType<IEnumerable<SiteClosureModel>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListClosures(
        bool includePast = false, CancellationToken cancellationToken = default)
    {
        var closures = await store.ListAsync(includePast ? null : Today(), cancellationToken);

        return Ok(closures.Select(ToModel).ToList());
    }

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpPost("closures")]
    [ProducesResponseType<SiteClosureModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClosure(
        SiteClosureRequestModel model, CancellationToken cancellationToken = default)
    {
        var closure = SiteClosure.Create(model.Date, model.Label);
        if (!closure.Succeeded)
        {
            return closure.Failures.ToProblemResult();
        }

        var created = await store.CreateAsync(closure.Value, cancellationToken);

        return created.Succeeded
            ? Ok(ToModel(created.Value))
            : created.Failures.ToProblemResult();
    }

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpPut("closures/{id:guid}")]
    [ProducesResponseType<SiteClosureModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClosure(
        Guid id, SiteClosureRequestModel model, CancellationToken cancellationToken = default)
    {
        var closure = SiteClosure.Create(model.Date, model.Label, id);
        if (!closure.Succeeded)
        {
            return closure.Failures.ToProblemResult();
        }

        var updated = await store.UpdateAsync(closure.Value, cancellationToken);

        if (updated.Succeeded)
        {
            // The id is kept, so every resource exempted from this closure stays exempted
            // from it — including when its date moved.
            return Ok(ToModel(updated.Value));
        }

        return updated.Failures.Any(f => f.Code == FailureCodes.ClosureNotFound)
            ? NotFoundProblem(id)
            : updated.Failures.ToProblemResult();
    }

    [Authorize(Policy = Constants.VerbPolicies.Settings)]
    [HttpDelete("closures/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClosure(Guid id, CancellationToken cancellationToken = default)
    {
        // Deleting a closure takes its exemptions with it, by cascade — an exemption cannot
        // outlive the thing it exempts from. No booking is touched.
        var deleted = await store.DeleteAsync(id, cancellationToken);

        return deleted.Succeeded ? Ok() : NotFoundProblem(id);
    }

    private DateOnly Today()
    {
        var now = timeProvider.GetUtcNow();

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // The setting has its own validation and its own failure code; a LIST is not the
            // place to enforce it. Falling back to UTC shows a closure a day early at worst,
            // where failing would show nothing at all.
            return DateOnly.FromDateTime(now.UtcDateTime);
        }
    }

    private static SiteClosureModel ToModel(SiteClosure closure)
        => new() { Id = closure.Id, Date = closure.Date, Label = closure.Label };

    private IActionResult NotFoundProblem(Guid id)
        => NotFound(new ProblemDetails
        {
            // `Type` is required by the backoffice's error interceptor, which discards a body
            // without one; see ApiResults for the measurement behind that.
            Type = "NotFound",
            Title = $"No closure exists with id {id}.",
            Status = StatusCodes.Status404NotFound,
        });
}
