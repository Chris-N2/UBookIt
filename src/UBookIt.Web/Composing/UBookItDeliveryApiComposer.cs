using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Core;
using UBookIt.Web.Controllers;
using UBookIt.Web.Mapping;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.DependencyInjection;
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

        // SELF-SERVICE CANCELLATION, on the same terms and for the same reason: off unless the
        // section says otherwise, and absent rather than refused when off. Bound here rather than
        // through the settings store because it is an exposure switch — see
        // SelfServiceCancellationSettings — and the application model is built once at startup,
        // which is why the settings screen shows it as needing a restart.
        var cancellation = builder.Config
            .GetSection(SelfServiceCancellationSettings.SectionKey)
            .Get<SelfServiceCancellationSettings>() ?? new SelfServiceCancellationSettings();

        builder.Services.Configure<MvcOptions>(options =>
            options.Conventions.Add(new CancellationExposureConvention(cancellation)));

        // Deliberately NOT AddBackOfficeOpenApiDocument, and deliberately no authentication:
        // the delivery API is anonymous by design, so its operations carry no auth requirement
        // and its document must not advertise one. That absence is a guarantee about a public
        // API rather than a line nobody typed — see the delivery-api capability. Do not add
        // authentication here "for consistency" with the backoffice document; the two differ
        // on purpose.
        //
        // Umbraco 18 generates OpenAPI through Microsoft.AspNetCore.OpenApi rather than
        // Swashbuckle, so this is the framework's own AddOpenApi rather than a Umbraco
        // backoffice helper — which is the right shape anyway, because this document describes
        // a public API that has nothing to do with the backoffice.
        builder.Services.AddOpenApi(Constants.DeliveryApiName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "uBookIt Delivery API";
                document.Info.Version = "1.0";
                document.Info.Description =
                    "Public, anonymous booking delivery API: resource discovery, "
                    + "availability and slot queries, and booking placement.";

                return Task.CompletedTask;
            });

            // Membership is decided by the SAME [MapToApi] attribute the controllers already
            // carry, rather than by a namespace check duplicated here. One source of truth:
            // move a controller out of the delivery API and this follows automatically.
            //
            // Endpoints hidden by DeliveryApiExposureConvention never reach this predicate —
            // it clears ApiExplorer.IsVisible, so no ApiDescription is produced at all. That
            // is why "a disabled endpoint does not appear in the OpenAPI document" survives
            // the move off Swashbuckle: the mechanism was never Swashbuckle's.
            options.ShouldInclude = api =>
                api.ActionDescriptor is ControllerActionDescriptor controller
                && controller.ControllerTypeInfo
                    .GetCustomAttributes(typeof(MapToApiAttribute), inherit: true)
                    .OfType<MapToApiAttribute>()
                    .Any(attribute => attribute.ApiName == Constants.DeliveryApiName);
        });

        builder.Services.AddOpenApiDocumentToUi(Constants.DeliveryApiName, "uBookIt Delivery API");

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
