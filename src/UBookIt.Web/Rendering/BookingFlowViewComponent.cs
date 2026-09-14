using Microsoft.AspNetCore.Mvc;

namespace UBookIt.Web.Rendering;

/// <summary>
/// The one component a site author places. It decides which flow the visitor is
/// in and renders it:
/// <code>
///   Component.InvokeAsync("BookingFlow")                     → catalogue, then either flow
///   Component.InvokeAsync("BookingFlow", new { serviceId })  → straight into the service flow
///   Component.InvokeAsync("BookingFlow", new { resourceId }) → straight into the resource flow
/// </code>
/// <para>
/// The catalogue links back to the host page with a query parameter, so the
/// dispatcher reads the same URL every other step reads and the author places
/// exactly one component. An explicitly supplied id wins over the query, which is
/// what makes "a site with one service" work: the author names it, and no
/// catalogue exists to be traversed. A site offering one service should not make a
/// visitor choose it from a list of one.
/// </para>
/// <para>
/// It holds no state and makes no decision of its own beyond that. Each flow is
/// the very class its own entry point uses, so reaching a flow here and reaching
/// it directly cannot diverge — the alternative is two implementations written to
/// match, which is how "the entry point does not change what follows" quietly
/// stops being true.
/// </para>
/// <para>
/// The existing <c>Booking</c> component keeps working unchanged for anyone
/// already using it.
/// </para>
/// </summary>
public sealed class BookingFlowViewComponent(
    ResourceBookingFlow resourceFlow,
    ServiceBookingFlow serviceFlow,
    BookingCatalogue catalogue,
    FrontendSettings frontendSettings) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid? serviceId = null, Guid? resourceId = null)
    {
        var entry = FlowEntry.Resolve(serviceId, resourceId, Request.ReadSubjectQuery());

        // Computed here, once, because the query string is the host's and only the
        // component sees it — the flows and models stay host-free (design D2).
        var preserved = PreservedQuery.Compute(Request.Query, frontendSettings.PreservedQueryParameters);

        if (entry.Subject is not { } chosen)
        {
            return View("Catalogue", await catalogue.BuildAsync(preserved));
        }

        return chosen.Kind == BookableKind.Service
            ? await ServiceAsync(chosen.Id, entry.Token, preserved)
            : await ResourceAsync(chosen.Id, entry.Token, preserved);
    }

    private async Task<IViewComponentResult> ServiceAsync(
        Guid serviceId, string? token, IReadOnlyList<PreservedQueryPair> preserved)
    {
        // A completed booking (PRG success) takes precedence over the form.
        if (TempData.Read<ServiceConfirmationModel>(BookingKeys.ServiceConfirmation) is { } confirmation)
        {
            return View("ServiceConfirmation", confirmation);
        }

        var outcome = await serviceFlow.BuildAsync(
            serviceId,
            new BookingFlowInput
            {
                Date = Request.ReadDateQuery(),
                DurationMinutes = Request.ReadDurationQuery(),
                Failed = TempData.Read<FailedSubmission>(BookingKeys.ServiceFailedSubmission),
                ChosenResourceId = Request.ReadResourceQuery(),
                FlowToken = token,
                PreservedQueryPairs = preserved,
            });

        return outcome.Unavailable is { } unavailable
            ? View("ServiceUnavailable", unavailable)
            : View("Service", outcome.Form);
    }

    private async Task<IViewComponentResult> ResourceAsync(
        Guid resourceId, string? token, IReadOnlyList<PreservedQueryPair> preserved)
    {
        if (TempData.Read<BookingConfirmationModel>(BookingKeys.Confirmation) is { } confirmation)
        {
            return View("Confirmation", confirmation);
        }

        var outcome = await resourceFlow.BuildAsync(
            resourceId,
            new BookingFlowInput
            {
                Date = Request.ReadDateQuery(),
                DurationMinutes = Request.ReadDurationQuery(),
                Failed = TempData.Read<FailedSubmission>(BookingKeys.FailedSubmission),
                FlowToken = token,
                PreservedQueryPairs = preserved,
            });

        return outcome.Unavailable is { } unavailable
            ? View("Unavailable", unavailable)
            : View("Default", outcome.Form);
    }
}
