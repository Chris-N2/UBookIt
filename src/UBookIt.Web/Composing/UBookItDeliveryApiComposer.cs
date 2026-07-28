using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UBookIt.Web.Composing;

/// <summary>
/// Registers the delivery API's OpenAPI document — separate from the backoffice
/// document and, unlike it, with no backoffice security requirements: the
/// delivery API is anonymous (delivery-api spec). Controllers are routed into
/// this document by <c>[MapToApi(Constants.DeliveryApiName)]</c> on the shared
/// base controller.
/// </summary>
public sealed class UBookItDeliveryApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.Configure<SwaggerGenOptions>(options =>
        {
            options.SwaggerDoc(Constants.DeliveryApiName, new OpenApiInfo
            {
                Title = "uBookIt Delivery API",
                Version = "1.0",
                Description = "Public, anonymous booking delivery API: resource discovery, "
                    + "availability and slot queries, and booking placement.",
            });

            // Deliberately no security operation filter — the delivery API is
            // anonymous, so its operations carry no auth requirement.
        });
    }
}
