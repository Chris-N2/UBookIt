using Microsoft.OpenApi;
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
/// So these guards are deliberately of two kinds: one asks the transformer directly what it
/// does to a schema, and one asks the <b>committed generated client</b> whether the other
/// transformer's effect is actually present. A unit test cannot see a transformer that was
/// never registered; the artifact guard can.
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

        var methods = System.Text.RegularExpressions.Regex
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
        // it as optional, so these went `body?:` -> `body:` in the port. That is a tightening
        // and it is deliberate; pinning it means a future regeneration that loosens it back
        // fails here instead of letting a caller omit a body the server demands.
        var types = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/api/types.gen.ts");

        string[] writes =
        [
            "PlaceBookingOnBehalfData",
            "MoveBookingData",
            "CreateResourceData",
            "UpdateResourceData",
            "CreateServiceData",
            "UpdateServiceData",
            "PutSettingData",
        ];

        foreach (var write in writes)
        {
            var model = System.Text.RegularExpressions.Regex.Match(
                types,
                $@"export type {write} = \{{(?<members>.*?)\n\}};",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            Assert.True(model.Success, $"{write} is no longer generated.");
            Assert.Matches(@"\bbody:\s", model.Groups["members"].Value);
            Assert.DoesNotMatch(@"\bbody\?:", model.Groups["members"].Value);
        }
    }
}
