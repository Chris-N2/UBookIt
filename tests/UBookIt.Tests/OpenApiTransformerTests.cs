using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UBookIt.Web;
using UBookIt.Tests.Support;
using UBookIt.Web.Composing;

namespace UBookIt.Tests;

/// <summary>
/// The two OpenAPI transformers the Umbraco 18 port introduced, and the generated artifacts
/// that are the only evidence either of them ran.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both exist because a generated artifact was wrong and nothing could have said so.</b>
/// The operation-ID transformer was deleted once on the strength of documentation implying it
/// was redundant, and the regression — thirty client method names turning route-derived —
/// reached a human only because somebody regenerated the client by hand. The schema
/// transformer rewrites a published contract and shipped with no test at all.
/// </para>
/// <para>
/// So these guards are of <b>three</b> kinds, and the third exists because QA round 2 deleted
/// the schema transformer's registration line and watched the whole suite stay green. Asking a
/// transformer what it does to a schema says nothing about whether it runs; asking the
/// committed client covers the operation-ID transformer, because that one's effect is baked
/// into a committed artifact. <b>Nothing is generated from the delivery document</b>, so the
/// schema transformer has no artifact to be read out of and needs its <i>registration</i>
/// asserted directly.
/// </para>
/// <para>
/// The paragraph this replaces claimed the two-kinds design covered both transformers. It
/// covered one. That is the same fault — a comment asserting a property the code does not
/// have — that the schema transformer was extracted to fix, committed in the doc block written
/// to announce the fix.
/// </para>
/// </remarks>
public class OpenApiTransformerTests
{
    private const string Numeric = @"^-?(?:0|[1-9]\d*)$";

    private static OpenApiSchema Schema(JsonSchemaType type, string? pattern = null)
        => new() { Type = type, Pattern = pattern };

    [Fact]
    public void A_serializer_widened_integer_becomes_a_plain_integer()
    {
        var schema = Schema(JsonSchemaType.Integer | JsonSchemaType.String, Numeric);

        Assert.True(SerializerWidenedNumberSchemaTransformer.Narrow(schema));
        Assert.Equal(JsonSchemaType.Integer, schema.Type);

        // The pattern described the STRING member. With the string gone it describes nothing,
        // and left behind it would constrain an integer to a regex.
        Assert.Null(schema.Pattern);
    }

    [Fact]
    public void A_widened_nullable_integer_stays_nullable()
    {
        // The defect this prevents is specific: narrowing by assignment rather than by
        // clearing one flag turns `int?` into `int`, and an absent null member is not
        // something the TypeScript build would notice.
        var schema = Schema(JsonSchemaType.Null | JsonSchemaType.Integer | JsonSchemaType.String, Numeric);

        Assert.True(SerializerWidenedNumberSchemaTransformer.Narrow(schema));
        Assert.Equal(JsonSchemaType.Null | JsonSchemaType.Integer, schema.Type);
        Assert.True(schema.Type!.Value.HasFlag(JsonSchemaType.Null));
    }

    [Fact]
    public void A_widened_number_is_narrowed_the_same_way()
    {
        var schema = Schema(JsonSchemaType.Number | JsonSchemaType.String, Numeric);

        Assert.True(SerializerWidenedNumberSchemaTransformer.Narrow(schema));
        Assert.Equal(JsonSchemaType.Number, schema.Type);
    }

    [Fact]
    public void An_authored_string_integer_union_is_left_alone()
    {
        // THE POINT OF THE PATTERN CHECK. A union somebody wrote on purpose looks identical
        // to a widened one if you only ask "has String and has Integer" — which is what the
        // first version of this transformer asked, while its comment claimed it narrowed only
        // the serializer's. This is the case that separates the claim from the code.
        var authored = Schema(JsonSchemaType.Integer | JsonSchemaType.String, "^[0-9]{4}$");

        Assert.False(SerializerWidenedNumberSchemaTransformer.Narrow(authored));
        Assert.Equal(JsonSchemaType.Integer | JsonSchemaType.String, authored.Type);
        Assert.Equal("^[0-9]{4}$", authored.Pattern);

        var unpatterned = Schema(JsonSchemaType.Integer | JsonSchemaType.String);

        Assert.False(SerializerWidenedNumberSchemaTransformer.Narrow(unpatterned));
        Assert.Equal(JsonSchemaType.Integer | JsonSchemaType.String, unpatterned.Type);
    }

    [Fact]
    public void A_plain_string_keeps_its_pattern()
    {
        // A schema with no numeric member must not be touched at all — including a string
        // whose pattern happens to be the numeric one, which is a legitimate way to say
        // "a string of digits".
        var schema = Schema(JsonSchemaType.String, Numeric);

        Assert.False(SerializerWidenedNumberSchemaTransformer.Narrow(schema));
        Assert.Equal(JsonSchemaType.String, schema.Type);
        Assert.Equal(Numeric, schema.Pattern);
    }

    [Fact]
    public void A_plain_integer_is_unchanged()
    {
        var schema = Schema(JsonSchemaType.Integer);

        Assert.False(SerializerWidenedNumberSchemaTransformer.Narrow(schema));
        Assert.Equal(JsonSchemaType.Integer, schema.Type);
    }

    [Fact]
    public void The_generated_client_keeps_action_shaped_method_names()
    {
        // THE GUARD THAT WAS MISSING WHEN THIS EXACT DEFECT SHIPPED. Umbraco 18's built-in
        // operation-ID conventions are route-derived; ActionNameOperationIdTransformer puts
        // the action-derived names back. Delete that transformer, regenerate, and the client's
        // method names change — which no C# test could see, because the client is TypeScript
        // and committed rather than built.
        var sdk = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/api/sdk.gen.ts");

        var methods = Regex
            .Matches(sdk, @"public static (?<name>[A-Za-z0-9_]+)")
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        // NON-EMPTY FIRST. An extraction that matches nothing satisfies every "absent" check
        // below and reports a healthy client — the precise failure that made this change's
        // own investigation report "names unchanged" twice from an empty list.
        Assert.True(
            methods.Count >= 30,
            $"Extracted {methods.Count} method names from sdk.gen.ts; the pattern is broken, not the client.");

        // Action-derived, as the transformer produces.
        Assert.Contains("listBookings", methods);
        Assert.Contains("cancelBooking", methods);
        Assert.Contains("placeBookingOnBehalf", methods);

        // Route-derived, as Umbraco 18 produces WITHOUT the transformer. Naming the wrong
        // names as well as the right ones is what makes this fire on the real regression
        // rather than merely on a rename.
        Assert.DoesNotContain("getBookings", methods);
        Assert.DoesNotContain("postBookings", methods);
        Assert.DoesNotContain("postBookingsByIdCancel", methods);
    }

    [Fact]
    public void The_generated_client_requires_a_body_on_every_write()
    {
        // Umbraco 18 describes a [FromBody] parameter as required where Swashbuckle described
        // it as optional, so eleven of these went `body?:` -> `body:` in the port. That is a
        // tightening and it is deliberate; pinning it means a future regeneration that loosens
        // it back fails here instead of letting a caller omit a body the server demands.
        //
        // ASSERTED OVER THE POPULATION, NOT A LIST. The first version of this named seven of
        // the eleven, which is "a finding enumerates a sample" — and a hand-kept list cannot
        // cover the twelfth write endpoint nobody has added yet. Every `body` member in the
        // file is either required or the generated `body?: never` that marks an operation
        // taking no body at all, so the absence of any OTHER optional body is the real claim.
        var types = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/api/types.gen.ts");

        var required = Regex.Matches(types, @"^\s*body:\s*(?<model>\S+);\s*$", RegexOptions.Multiline);
        var optional = Regex.Matches(types, @"^\s*body\?:\s*(?<model>\S+);\s*$", RegexOptions.Multiline);

        // NON-EMPTY FIRST, for the same reason as the method-name guard: an extraction that
        // matches nothing satisfies the "no optional bodies" claim perfectly.
        Assert.True(
            required.Count >= 11,
            $"Found {required.Count} required body members in types.gen.ts; the pattern is broken, not the client.");

        var loosened = optional
            .Select(match => match.Groups["model"].Value)
            .Where(model => model != "never")
            .ToArray();

        Assert.True(
            loosened.Length == 0,
            $"These request bodies are optional again: {string.Join(", ", loosened)}");
    }

    /// <summary>
    /// That the schema transformer is actually REGISTERED on the delivery document.
    /// </summary>
    /// <remarks>
    /// QA round 2 deleted <c>options.AddSchemaTransformer&lt;…&gt;()</c> from the composer,
    /// rebuilt in Release and ran everything: <b>0 warnings, every test green.</b> Six unit
    /// tests asked the transformer what it does to a schema and not one asked whether it runs.
    /// <para>
    /// The operation-ID transformer is covered by the committed client, which is downstream of
    /// it. <b>Nothing is generated from the delivery document</b>, so there is no artifact to
    /// read this one out of — which also means the "it would break the TypeScript build"
    /// failure direction claimed for it does not exist. Registration has to be asserted
    /// directly, through the real composer, the way <c>DeliveryApiExposureTests</c> asserts the
    /// exposure convention.
    /// </para>
    /// <para>
    /// Reaches an internal field by reflection, deliberately: <c>OpenApiOptions</c> exposes no
    /// public transformer collection, and the alternative is no guard at all over a line whose
    /// removal is otherwise silent. If a future ASP.NET renames the field this test fails
    /// loudly rather than passing vacuously — which is what the non-null assertion below is for.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_schema_transformer_is_registered_on_the_delivery_document()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        new UBookItDeliveryApiComposer().Compose(new ServicesOnlyUmbracoBuilder(services, config));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>()
            .Get(Constants.DeliveryApiName);

        var field = typeof(OpenApiOptions)
            .GetField("SchemaTransformers", BindingFlags.Instance | BindingFlags.NonPublic);

        // THE INSTRUMENT FIRST. A renamed field would make `registered` empty, and an empty
        // list would fail the assertion below for entirely the wrong reason.
        Assert.True(field is not null, "OpenApiOptions.SchemaTransformers no longer exists; this guard cannot see anything.");

        var registered = ((System.Collections.IEnumerable)field!.GetValue(options)!)
            .Cast<object>()
            .ToArray();

        Assert.True(
            registered.Length > 0,
            "No schema transformer is registered on the delivery document at all.");

        // Registered by TYPE, so the entry in the list is the framework's own
        // TypeBasedOpenApiSchemaTransformer wrapper and our type is a Type-valued field on it.
        // Read the field rather than the wrapper's ToString(), which names neither.
        static IEnumerable<Type> WrappedTypes(object transformer) =>
            transformer.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.GetValue(transformer))
                .OfType<Type>();

        var registeredTypes = registered
            .SelectMany(transformer => WrappedTypes(transformer).Append(transformer.GetType()))
            .ToArray();

        Assert.Contains(typeof(SerializerWidenedNumberSchemaTransformer), registeredTypes);
    }
}
