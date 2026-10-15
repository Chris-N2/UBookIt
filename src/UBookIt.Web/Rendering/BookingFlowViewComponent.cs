using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;

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
/// exactly one component. An explicitly supplied id wins over the query. A site
/// offering one thing should not make a visitor choose it from a list of one, and
/// there are two ways it does not have to: the author names it, or — with no
/// subject at all — a catalogue that would offer exactly one choice is entered as if
/// the visitor had chosen it, unless the site has turned
/// <see cref="SiteBookingSettings.SkipSingleChoiceCatalogue"/> off.
/// </para>
/// <para>
/// It holds no state and makes no decision of its own beyond those. Each flow is
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
public sealed class BookingFlowViewComponent : ViewComponent
{
    private readonly ResourceBookingFlow resourceFlow;
    private readonly ServiceBookingFlow serviceFlow;
    private readonly BookingCatalogue catalogue;
    private readonly FrontendSettings frontendSettings;
    private readonly bool skipSingleChoice;

    /// <summary>
    /// The constructor every published version had, kept so that its shape stays a promise.
    /// </summary>
    /// <remarks>
    /// It has no site settings, so it skips a catalogue of one — the setting's default, and what a
    /// site that has stored and configured nothing gets anyway. Dependency injection does not use
    /// it: the constructor below is marked as the one to activate.
    /// </remarks>
    public BookingFlowViewComponent(
        ResourceBookingFlow resourceFlow,
        ServiceBookingFlow serviceFlow,
        BookingCatalogue catalogue,
        FrontendSettings frontendSettings)
        : this(resourceFlow, serviceFlow, catalogue, frontendSettings, skipSingleChoice: true)
    {
    }

    /// <summary>
    /// The constructor dependency injection activates, with the site's settings so that the
    /// single-choice skip follows <see cref="SiteBookingSettings.SkipSingleChoiceCatalogue"/>.
    /// </summary>
    /// <remarks>
    /// A second constructor rather than a changed one: the four-argument shape above is published,
    /// and the public surface is frozen. <see cref="ActivatorUtilitiesConstructorAttribute"/> is
    /// what makes the ViewComponent activator choose this one; a rendering test with the setting
    /// off and a catalogue of one fails if it does not.
    /// </remarks>
    [ActivatorUtilitiesConstructor]
    public BookingFlowViewComponent(
        ResourceBookingFlow resourceFlow,
        ServiceBookingFlow serviceFlow,
        BookingCatalogue catalogue,
        FrontendSettings frontendSettings,
        SiteBookingSettings siteSettings)
        : this(resourceFlow, serviceFlow, catalogue, frontendSettings,
            skipSingleChoice: (siteSettings ?? throw new ArgumentNullException(nameof(siteSettings)))
                .SkipSingleChoiceCatalogue)
    {
    }

    private BookingFlowViewComponent(
        ResourceBookingFlow resourceFlow,
        ServiceBookingFlow serviceFlow,
        BookingCatalogue catalogue,
        FrontendSettings frontendSettings,
        bool skipSingleChoice)
    {
        this.resourceFlow = resourceFlow;
        this.serviceFlow = serviceFlow;
        this.catalogue = catalogue;
        this.frontendSettings = frontendSettings;
        this.skipSingleChoice = skipSingleChoice;
    }

    public async Task<IViewComponentResult> InvokeAsync(Guid? serviceId = null, Guid? resourceId = null)
    {
        var entry = FlowEntry.Resolve(serviceId, resourceId, Request.ReadSubjectQuery());

        // Computed here, once, because the query string is the host's and only the
        // component sees it — the flows and models stay host-free (design D2).
        var preserved = PreservedQuery.Compute(Request.Query, frontendSettings.PreservedQueryParameters);

        if (entry.Subject is not { } chosen)
        {
            var choices = await catalogue.BuildAsync(preserved);

            // A catalogue of one is entered AS IF THE VISITOR HAD CHOSEN IT, so the token is
            // carried exactly as a `ubBook` query would carry it: every later step's URL then names
            // what is being booked, and stays linkable if the site comes to offer a second thing.
            // Not as if the author had named it — that would leave the steps' URLs bare, and a
            // bookmarked step would fall back to the catalogue and lose its date the day a second
            // thing was added.
            if (SingleChoice.Sole(choices, skipSingleChoice) is not { } sole)
            {
                return View("Catalogue", choices);
            }

            chosen = sole;
            entry = new FlowEntry(sole, NamedByAuthor: false);
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
