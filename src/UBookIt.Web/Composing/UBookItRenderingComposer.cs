using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core.Notifications;
using UBookIt.Web.Emails;
using UBookIt.Web.Rendering;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace UBookIt.Web.Composing;

/// <summary>
/// Registers the default front-end's flow classes — what each ViewComponent
/// decides, held apart from the host so it can be exercised without one.
/// <para>
/// Scoped, matching the Core ports they compose (the stores and the availability
/// and service-booking services are all scoped). A singleton over scoped
/// dependencies would capture a disposed scope's stores on the second request.
/// </para>
/// </summary>
public sealed class UBookItRenderingComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<ResourceBookingFlow>();
        builder.Services.AddScoped<ServiceBookingFlow>();
        builder.Services.AddScoped<BookingCatalogue>();

        // SITE-SUPPLIED EMAIL CONTENT, and the registration is the whole mechanism.
        //
        // Deliberately NOT AddUnique, and deliberately no [ComposeAfter]: nothing is being
        // replaced. Core's port has no default implementation and UBookIt.Persistence registers
        // none, so the composer that consumes it resolves null on a site without this assembly
        // and uses the package's own wording. Adding this registration is the only thing that
        // turns the feature on, which means no composer ordering can get it wrong — and in this
        // package an ordering assumption has already silently disabled a feature once (see
        // UBookItThemeRegistration). Umbraco's own EmailSender detects a registered handler by
        // exactly this presence-or-absence test.
        //
        // Scoped, matching BookingMessageComposer, which is the only thing that resolves it.
        // ONE registration, resolvable two ways. The boot check needs the concrete type (it asks
        // what is supplied, which is not on the port), and the composer needs the port. Two
        // independent AddScoped calls would build two instances per scope — harmless today
        // because the renderer is stateless, and exactly the kind of harmless that stops being
        // so when somebody adds a cache to it.
        builder.Services.AddScoped<RazorBookingTemplateRenderer>();
        builder.Services.AddScoped<IBookingTemplateRenderer>(
            sp => sp.GetRequiredService<RazorBookingTemplateRenderer>());

        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, UBookItEmailTemplateBootCheck>();
    }
}
