using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace UBookIt.Web.Composing;

/// <summary>
/// Puts back the plain <c>integer</c> that Umbraco 17's OpenAPI document described, after
/// Umbraco 18's generator widened every numeric property to <c>["integer", "string"]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the widening happens.</b> <c>Microsoft.AspNetCore.OpenApi</c> generates schemas from
/// the <b>global HTTP <c>JsonOptions</c></b> — Microsoft documents these as the only JSON
/// options that influence OpenAPI, MVC's having none — and Umbraco configures those globally
/// with <c>JsonNumberHandling.AllowReadingFromString</c>. That is a true statement about a
/// minimal API reading those options, and a <b>false</b> one about the delivery API's
/// controllers, which are MVC and do not. Measured: the Umbraco 17 document emitted
/// <b>zero</b> such unions, so this restores the contract rather than inventing one.
/// </para>
/// <para>
/// The backoffice document does not need this — it names its JSON options through
/// <c>BackOfficeOpenApiDocumentBuilder.WithJsonOptions</c>, the seam Umbraco provides for
/// exactly this problem. That seam is Umbraco-internal for a non-backoffice document and
/// <c>OpenApiOptions</c> exposes no serializer of its own, which is why the delivery document
/// has to correct the generated schema instead.
/// </para>
/// <para>
/// <b>It narrows ONLY what the serializer widened, and that is enforced rather than asserted.</b>
/// An earlier version of this claimed the property in a comment while the code matched on
/// nothing more than "has String and has Integer" — which would equally have rewritten a union
/// somebody wrote on purpose, and discarded that schema's <c>Pattern</c> with it. The signature
/// is narrower than that: the serializer emits the numeric-string <see cref="NumericStringPattern"/>
/// alongside the widened type, every time and identically (measured across both documents: 60
/// occurrences, one distinct pattern). A string/number union carrying any other pattern, or
/// none, is somebody's intent and is left exactly as it is.
/// </para>
/// <para>
/// <b>Two things this does NOT cover, stated because the paragraph they replace claimed the
/// opposite.</b> That paragraph said a changed host pattern would "break the TypeScript build
/// loudly". It would not: <b>nothing generates TypeScript from the delivery document.</b> The
/// only <c>generate-client</c> target is the backoffice document, and that one is fixed by
/// <c>WithJsonOptions</c> rather than by this. The fourteen TypeScript errors that started this
/// investigation came from the backoffice client. So the failure here is silent, and the guard
/// is <c>OpenApiTransformerTests</c> rather than a build.
/// </para>
/// <para>
/// And <see cref="NumericStringPattern"/> is <c>System.Text.Json</c>'s <b>integral</b> pattern.
/// A <c>decimal</c>, <c>double</c> or <c>float</c> on a delivery model would be widened with a
/// different one, would not match here, and would publish as <c>["number", "string"]</c> with
/// nothing to notice. <b>No delivery model carries a non-integral number today</b>, so this is
/// latent rather than live — but the first one that does needs its pattern added here, and
/// knowing that is cheaper than rediscovering it.
/// </para>
/// </remarks>
internal sealed class SerializerWidenedNumberSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>
    /// The pattern <c>System.Text.Json</c>'s schema exporter attaches to a number widened by
    /// <c>JsonNumberHandling.AllowReadingFromString</c>. Its presence is what identifies the
    /// union as the serializer's rather than the author's.
    /// </summary>
    internal const string NumericStringPattern = @"^-?(?:0|[1-9]\d*)$";

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Narrow(schema);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes the <c>string</c> member from a serializer-widened numeric union, preserving
    /// every other member — <c>null</c> included, so a nullable int stays nullable.
    /// </summary>
    /// <returns><c>true</c> if the schema was narrowed.</returns>
    internal static bool Narrow(IOpenApiSchema schema)
    {
        if (schema is not OpenApiSchema writable
            || writable.Type is not { } type
            || type.HasFlag(JsonSchemaType.String) is false
            || (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)) is false
            || writable.Pattern != NumericStringPattern)
        {
            return false;
        }

        writable.Type = type & ~JsonSchemaType.String;
        writable.Pattern = null;
        return true;
    }
}
