using Microsoft.AspNetCore.Mvc;

namespace UBookIt.Web.Rendering;

/// <summary>
/// Renders the default booking flow for one resource (invoked as
/// <c>@await Component.InvokeAsync("Booking", new { resourceId })</c>). Reads
/// availability from Core in-process (never the delivery API). After a PRG
/// redirect it renders either the confirmation (on success) or the redrawn
/// form with preserved input and errors (on failure), both carried in TempData.
/// <para>
/// The decision itself lives in <see cref="ResourceBookingFlow"/>, which the
/// dispatcher also uses, so there is one implementation of this flow rather than
/// two written to match. What stays here is what genuinely needs the host: the
/// TempData handoffs and the query string.
/// </para>
/// </summary>
public sealed class BookingViewComponent(ResourceBookingFlow flow) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid resourceId)
    {
        // A completed booking (PRG success) takes precedence over the form.
        if (TempData.Read<BookingConfirmationModel>(BookingKeys.Confirmation) is { } confirmation)
        {
            return View("Confirmation", confirmation);
        }

        var outcome = await flow.BuildAsync(
            resourceId,
            new BookingFlowInput
            {
                Date = Request.ReadDateQuery(),
                DurationMinutes = Request.ReadDurationQuery(),
                Failed = TempData.Read<FailedSubmission>(BookingKeys.FailedSubmission),

                // Deliberately absent. A site author naming the resource on the
                // component has no flow state in the URL to carry, and emitting a
                // token here would change this flow's redirect target for every
                // site already using it.
                FlowToken = null,
            });

        return outcome.Unavailable is { } unavailable
            ? View("Unavailable", unavailable)
            : View(outcome.Form);
    }
}
