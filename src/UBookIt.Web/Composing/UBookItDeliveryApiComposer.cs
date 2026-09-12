using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using UBookIt.Web.Controllers;
using UBookIt.Web.Mapping;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UBookIt.Web.Composing;

/// <summary>
/// Composes the delivery API's exposure and its OpenAPI document. Exposure first,
/// because it is the change that matters: the two direction switches are bound here
/// (both off when unconfigured) and <see cref="DeliveryApiExposureConvention"/> is
/// registered into MVC, which is the single line the off-by-default guarantee rides
/// on — pinned by its own tests precisely because every other exposure test
/// registers the convention itself and cannot see this one. The OpenAPI document is
/// separate from the backoffice document and, unlike it, carries no backoffice
/// security requirements: the delivery API, where a direction is enabled, is
/// anonymous (delivery-api spec). Controllers are routed into the document by
/// <c>[MapToApi(Constants.DeliveryApiName)]</c> on the shared base controller.
/// </summary>
public sealed class UBookItDeliveryApiComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // EXPOSURE IS OFF BY DEFAULT, per direction, decided once at startup. Binding
        // yields the all-off record when the section is absent, so an untouched install
        // serves no anonymous endpoint — the convention below removes the disabled
        // directions' selectors and ApiExplorer visibility while the application model
        // is being built, which is what makes "absent, not refused" structural.
        var settings = builder.Config
            .GetSection(DeliveryApiSettings.SectionKey)
            .Get<DeliveryApiSettings>() ?? new DeliveryApiSettings();

        builder.Services.AddSingleton(settings);
        builder.Services.Configure<MvcOptions>(options =>
            options.Conventions.Add(new DeliveryApiExposureConvention(settings)));

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

        // Transport/model-binding failures (malformed body, unparseable or
        // missing parameter) must use the same errors[] envelope as domain
        // failures (design D7). ApiBehaviorOptions is global, so this is scoped
        // to delivery controllers and delegates everything else to the built-in
        // factory, leaving the backoffice API's responses untouched.
        // PostConfigure runs after all Configure actions, so this wrapper wins
        // regardless of composer ordering and captures the final built-in.
        builder.Services.PostConfigure<ApiBehaviorOptions>(options =>
        {
            var builtIn = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
                context.ActionDescriptor is ControllerActionDescriptor descriptor
                && typeof(UBookItDeliveryApiControllerBase).IsAssignableFrom(descriptor.ControllerTypeInfo)
                    ? ApiResults.ToValidationProblemResult(context.ModelState)
                    : builtIn(context);
        });
    }
}
