using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UBookIt.Tests.Rendering.Support;

/// <summary>
/// Produces a model that differs from another in exactly one member, so "does
/// varying this change the output" can be asked with the answer attributable to
/// that member and nothing else.
/// <para>
/// The view models carry init-only properties, so a copy cannot be made by
/// assignment. It is made by a JSON round trip instead: serialize, change one
/// value, deserialize. That keeps the variation <b>mechanical</b> — there is no
/// per-property list for someone to forget to extend, which is the whole reason
/// the member set is derived rather than declared.
/// </para>
/// </summary>
public static class ModelVariation
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>
    /// A copy of <paramref name="model"/> with <paramref name="member"/> changed to
    /// a different value, or null when this member cannot be varied from this state
    /// — an empty collection has no smaller value, and a null has nothing to vary.
    /// The caller then tries another state rather than concluding anything.
    /// </summary>
    public static object? Vary(object model, string member)
    {
        var property = model.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance);

        // Computed properties and methods have no value to set. They are checked a
        // different way — see ModelPropertyTests — because the thing that varies
        // them is the settable member they are derived from.
        if (property is null || !property.CanWrite)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(model, model.GetType(), Options)!.AsObject();

        var name = node.Select(pair => pair.Key)
            .FirstOrDefault(key => string.Equals(key, member, StringComparison.OrdinalIgnoreCase));

        if (name is null)
        {
            return null;
        }

        if (!TryDifferent(property, node[name], out var varied))
        {
            return null;
        }

        node[name] = varied;

        return JsonSerializer.Deserialize(node.ToJsonString(), model.GetType(), Options);
    }

    /// <summary>
    /// A value of the property's own type that is not the one it currently holds.
    /// <para>
    /// Driven by the <b>declared type and its nullability</b>, not by the shape of
    /// the serialized value. A type-blind variation reads a Guid as a string and
    /// appends to it, or nulls a collection the model promises is never null — and
    /// the round trip then throws, or the view throws, which reads as a failing
    /// rule rather than a broken varier. Both happened on the first run.
    /// </para>
    /// <para>
    /// False means "no different value is available from this state" — an empty
    /// collection has nowhere to go — and the caller tries another state rather than
    /// concluding the member is dead.
    /// </para>
    /// </summary>
    private static bool TryDifferent(PropertyInfo property, JsonNode? current, out JsonNode? varied)
    {
        varied = null;

        var type = property.PropertyType;
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        // Only a member the model says may be null is varied to null. Nulling one
        // that may not is not a variation — it is an invalid model, and the view
        // rightly throws on it.
        if (IsNullable(property))
        {
            if (current is not null)
            {
                return true; // vary to null
            }

            varied = Sample(underlying);
            return varied is not null;
        }

        if (current is null)
        {
            return false;
        }

        if (underlying == typeof(bool))
        {
            varied = JsonValue.Create(!current.GetValue<bool>());
            return true;
        }

        if (underlying == typeof(string))
        {
            varied = JsonValue.Create(current.GetValue<string>() + "-varied");
            return true;
        }

        if (underlying == typeof(int))
        {
            varied = JsonValue.Create(current.GetValue<int>() + 1);
            return true;
        }

        if (underlying == typeof(Guid))
        {
            varied = JsonValue.Create(
                current.GetValue<string>() == EmptyGuid ? OtherGuid : EmptyGuid);
            return true;
        }

        if (underlying == typeof(DateOnly))
        {
            varied = JsonValue.Create(
                DateOnly.Parse(current.GetValue<string>()).AddDays(1).ToString("yyyy-MM-dd"));
            return true;
        }

        // A populated collection empties; an empty one has nowhere to go from here.
        if (current is JsonArray array)
        {
            if (array.Count == 0)
            {
                return false;
            }

            varied = new JsonArray();
            return true;
        }

        return false;
    }

    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";

    private const string OtherGuid = "00000000-0000-0000-0000-0000000000ff";

    /// <summary>
    /// Whether the model declares this member as possibly null — a
    /// <c>Nullable&lt;T&gt;</c>, or a reference type annotated nullable.
    /// </summary>
    private static bool IsNullable(PropertyInfo property)
        => Nullable.GetUnderlyingType(property.PropertyType) is not null
            || new NullabilityInfoContext().Create(property).WriteState == NullabilityState.Nullable;

    /// <summary>A value to give a member that is currently null.</summary>
    private static JsonNode? Sample(Type type)
        => type == typeof(string) ? JsonValue.Create("varied")
            : type == typeof(Guid) ? JsonValue.Create(OtherGuid)
            : type == typeof(int) ? JsonValue.Create(42)
            : type == typeof(DateOnly) ? JsonValue.Create("2026-12-25")
            : null;

    /// <summary>
    /// What a member evaluates to on a model, as a comparable string — including
    /// members that cannot be set: computed properties, and the <c>ErrorFor</c>
    /// method the form partials use as their error channel.
    /// </summary>
    public static string? Evaluate(object model, string member)
    {
        var type = model.GetType();

        if (type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance) is { } property)
        {
            var value = property.GetValue(model);

            return value is System.Collections.IEnumerable items and not string
                ? string.Join("|", items.Cast<object?>().Select(i => i?.ToString()))
                : value?.ToString();
        }

        var method = type.GetMethod(member, BindingFlags.Public | BindingFlags.Instance);

        if (method is null || method.GetParameters().Length != 1)
        {
            return null;
        }

        // The error channel: what the view would show against each control it can
        // report on. Two states whose errors differ evaluate differently here.
        return string.Join("|", new[]
            {
                UBookIt.Web.Rendering.BookingFieldIds.Name,
                UBookIt.Web.Rendering.BookingFieldIds.Email,
                UBookIt.Web.Rendering.BookingFieldIds.Times,
                UBookIt.Web.Rendering.BookingFieldIds.Duration,
                UBookIt.Web.Rendering.BookingFieldIds.Resource,
            }
            .Select(field => method.Invoke(model, [field])?.ToString() ?? string.Empty));
    }
}
