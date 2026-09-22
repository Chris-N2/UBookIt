using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
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
    /// <b>This was forty lines against Umbraco 17 and is eight against 18, because the host now
    /// does the work itself.</b> Umbraco 17 exposed OpenAPI through Swashbuckle, so a package had
    /// to configure <c>SwaggerGenOptions</c>, register its own document, add an operation filter
    /// to attach backoffice authentication, and supply an <c>IOperationIdHandler</c> to stop the
    /// generated client's method names being unusably verbose. Umbraco 18 generates through
    /// <c>Microsoft.AspNetCore.OpenApi</c> and <c>AddBackOfficeOpenApiDocument</c> applies the
    /// authentication, the schema conventions and the operation-ID conventions itself.
    /// </para>
    /// <para>
    /// <b>The custom operation-ID handler is PORTED, not deleted — and the first attempt at this
    /// change deleted it on the strength of the documentation saying the new registration
    /// "applies the schema and operation ID conventions".</b> It does, but not the same ones.
    /// Regenerating the client proved it: the thirty exported method names went from
    /// <c>cancelBooking</c>, <c>listBookings</c> and <c>placeBookingOnBehalf</c> to
    /// <c>postBookingsByIdCancel</c>, <c>getBookings</c> and <c>postBookings</c> — the verbose,
    /// route-derived naming this handler has always existed to prevent.
    /// </para>
    /// <para>
    /// The lesson is the one the task had already written down and the implementation then
    /// ignored: <b>decide by regenerating and comparing, never by what the docs imply.</b>
    /// </para>
    /// </remarks>
    public class UBookItBackofficeApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
            => builder.AddBackOfficeOpenApiDocument(
                Constants.ApiName,
                document => document
                    .WithTitle("uBookIt Backoffice API")
                    .WithBackOfficeAuthentication()
                    .WithJsonOptions(Umbraco.Cms.Core.Constants.JsonOptionsNames.BackOffice)
                    .ConfigureOpenApiOptions(options =>
                        options.AddOperationTransformer<ActionNameOperationIdTransformer>()));

        /// <summary>
        /// Names each operation after its action method, so the generated TypeScript client reads
        /// <c>listBookings()</c> rather than <c>getBookings()</c> or worse.
        /// </summary>
        /// <remarks>
        /// The Umbraco 18 replacement for the <c>IOperationIdHandler</c> this package used on
        /// Umbraco 17. Same rule, same source of truth — the action's route value — expressed
        /// through the transformer pipeline that <c>Microsoft.AspNetCore.OpenApi</c> uses instead
        /// of Swashbuckle's operation filters.
        /// </remarks>
        internal sealed class ActionNameOperationIdTransformer : IOpenApiOperationTransformer
        {
            public Task TransformAsync(
                OpenApiOperation operation,
                OpenApiOperationTransformerContext context,
                CancellationToken cancellationToken)
            {
                if (context.Description.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
                    && string.IsNullOrWhiteSpace(action) is false)
                {
                    operation.OperationId = action;
                }

                return Task.CompletedTask;
            }
        }
    }
}
