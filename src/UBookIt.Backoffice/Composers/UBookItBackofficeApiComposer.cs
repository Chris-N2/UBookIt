using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UBookIt.Backoffice.Composers
{
    /// <summary>
    /// Registers uBookIt's own backoffice API as a named OpenAPI document, so it can be browsed
    /// in the Swagger UI and used to generate the TypeScript client the backoffice section runs on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Related documentation:
    /// https://docs.umbraco.com/umbraco-cms/extend-your-project/tutorials/creating-a-backoffice-api
    /// https://docs.umbraco.com/umbraco-cms/extend-your-project/tutorials/creating-a-backoffice-api/adding-a-custom-openapi-document
    /// </para>
    /// <para>
    /// <b>This was forty lines against Umbraco 17 and is five against 18, because the host now
    /// does the work itself.</b> Umbraco 17 exposed OpenAPI through Swashbuckle, so a package had
    /// to configure <c>SwaggerGenOptions</c>, register its own document, add an operation filter
    /// to attach backoffice authentication, and supply an <c>IOperationIdHandler</c> to stop the
    /// generated client's method names being unusably verbose. Umbraco 18 generates through
    /// <c>Microsoft.AspNetCore.OpenApi</c> and <c>AddBackOfficeOpenApiDocument</c> applies the
    /// authentication, the schema conventions and the operation-ID conventions itself.
    /// </para>
    /// <para>
    /// <b>The custom operation-ID handler was deleted on evidence, not on the documentation's
    /// word.</b> Its only job was short method names — <c>listBookings</c> rather than a route-
    /// derived mouthful — so the check was to regenerate the client and compare the exported
    /// names against the thirty the v17 client had. They are unchanged. Had they regressed, the
    /// handler would have been ported to v18's <c>GenerateOperationId</c> instead.
    /// </para>
    /// </remarks>
    public class UBookItBackofficeApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
            => builder.AddBackOfficeOpenApiDocument(
                Constants.ApiName,
                document => document
                    .WithTitle("uBookIt Backoffice API")
                    .WithBackOfficeAuthentication());
    }
}
