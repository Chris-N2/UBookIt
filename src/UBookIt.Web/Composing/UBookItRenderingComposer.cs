using Microsoft.Extensions.DependencyInjection;
using UBookIt.Web.Rendering;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

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
    }
}
