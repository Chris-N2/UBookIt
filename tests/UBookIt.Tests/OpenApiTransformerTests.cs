using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using UBookIt.Web;
using UBookIt.Tests.Support;
using UBookIt.Backoffice.Composers;
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

        var required = Regex.Matches(types, @"^\s*body:\s*(?<model>[^;]+);\s*$", RegexOptions.Multiline);
        var optional = Regex.Matches(types, @"^\s*body\?:\s*(?<model>[^;]+);\s*$", RegexOptions.Multiline);

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
    /// That each transformer is actually REGISTERED on the document it belongs to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// QA round 2 deleted the schema transformer's registration, rebuilt in Release and ran
    /// everything: <b>0 warnings, every test green.</b> Round 3 did the same to the
    /// operation-ID transformer's registration, with the same result — because the
    /// committed-client guard covers that transformer's <i>effect</i> and only once somebody
    /// regenerates, which a person deleting a registration has no reason to do. **That is
    /// exactly how this change shipped its original defect.** So both registrations are
    /// asserted, not one.
    /// </para>
    /// <para>
    /// Reaches an internal field by reflection. The trade is a real one rather than the
    /// absence of an alternative: a source scan could assert that the call is <i>written</i>,
    /// while this asserts that it <i>took effect</i> on the options object the framework will
    /// consume — a scan would pass happily if the call moved into a branch that never runs.
    /// The cost is a dependency on an ASP.NET internal, in a test that ships in the repository
    /// and in no package. If the field is ever renamed these fail by name, which the
    /// instrument assertion below exists to guarantee.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("SchemaTransformers")]
    [InlineData("OperationTransformers")]
    public void The_transformer_lists_are_reachable(string field)
    {
        // THE INSTRUMENT, ASSERTED BEFORE ANYTHING IS ASSERTED WITH IT. Both guards below read
        // these internal fields; a rename would make each return nothing, and "nothing" reads
        // as "no unexpected transformer" rather than as a broken test.
        Assert.NotNull(typeof(OpenApiOptions).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic));
    }

    [Fact]
    public void The_schema_transformer_is_registered_on_the_delivery_document()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        new UBookItDeliveryApiComposer().Compose(new ServicesOnlyUmbracoBuilder(services, config));

        Assert.Contains(
            typeof(SerializerWidenedNumberSchemaTransformer),
            RegisteredTransformers(services, UBookIt.Web.Constants.DeliveryApiName, "SchemaTransformers"));
    }

    [Fact]
    public void The_operation_id_transformer_is_registered_on_the_backoffice_document()
    {
        // Deleting this registration is the defect this change shipped in its first pass: the
        // thirty client method names turned route-derived and nothing failed. The committed
        // client catches it only after a regeneration; this catches it at the source.
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        new UBookItBackofficeApiComposer().Compose(new ServicesOnlyUmbracoBuilder(services, config));

        Assert.Contains(
            typeof(UBookItBackofficeApiComposer.ActionNameOperationIdTransformer),
            RegisteredTransformers(services, UBookIt.Backoffice.Constants.ApiName, "OperationTransformers"));
    }

    /// <summary>
    /// The transformer types registered on one document, read off the options object the
    /// framework will actually consume.
    /// </summary>
    /// <remarks>
    /// Registration by type stores the framework's own <c>TypeBased…Transformer</c> wrapper,
    /// which names neither itself nor the wrapped type in its rendering — a first version of
    /// this guard asserted on <c>ToString()</c> and failed for that reason. The wrapped type is
    /// a <see cref="Type"/>-valued field on the wrapper, so that is what is read. The wrapper's
    /// own type is included too, so a transformer registered as an instance is still seen.
    /// </remarks>
    private static Type[] RegisteredTransformers(IServiceCollection services, string documentName, string fieldName)
    {
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenApiOptions>>().Get(documentName);

        var field = typeof(OpenApiOptions).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(field is not null, $"OpenApiOptions.{fieldName} no longer exists; this guard cannot see anything.");

        var registered = ((System.Collections.IEnumerable)field!.GetValue(options)!).Cast<object>().ToArray();

        Assert.True(registered.Length > 0, $"No transformer is registered in {fieldName} for '{documentName}' at all.");

        return registered
            .SelectMany(transformer => transformer.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(f => f.GetValue(transformer))
                .OfType<Type>()
                .Append(transformer.GetType()))
            .ToArray();
    }
}
