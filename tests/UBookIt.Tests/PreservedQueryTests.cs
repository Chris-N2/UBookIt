using System.Reflection;
using Microsoft.Extensions.Primitives;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The preservation computation (default-frontend, "The GET forms preserve configured
/// host-page query parameters") — the pure half. What the forms do with the pairs is
/// the rendering suite's; every rule about WHICH pairs exist is decided here, once.
/// </summary>
public class PreservedQueryTests
{
    private static IReadOnlyList<PreservedQueryPair> Compute(
        IReadOnlyCollection<string> allowList,
        params (string Name, StringValues Values)[] query)
        => PreservedQuery.Compute(
            query.Select(parameter => new KeyValuePair<string, StringValues>(
                parameter.Name, parameter.Values)),
            allowList);

    [Fact]
    public void A_listed_parameter_is_preserved_and_an_unlisted_one_is_dropped()
    {
        var pairs = Compute(
            ["utm_source"],
            ("utm_source", "newsletter"),
            ("session_hint", "abc"));

        Assert.Equal([new PreservedQueryPair("utm_source", "newsletter")], pairs);
    }

    [Fact]
    public void An_empty_allow_list_preserves_nothing()
        => Assert.Empty(Compute([], ("utm_source", "newsletter")));

    [Fact]
    public void Name_matching_is_case_insensitive_and_keeps_the_request_spelling()
    {
        // The requirement: a site configuring "Culture" must not silently lose
        // "culture". The pair keeps the REQUEST's spelling, because that is the name
        // the page's own code reads back after the round trip.
        var pairs = Compute(["Culture"], ("culture", "en-GB"));

        Assert.Equal([new PreservedQueryPair("culture", "en-GB")], pairs);
    }

    [Fact]
    public void A_multi_valued_parameter_yields_one_pair_per_value_in_order()
        => Assert.Equal(
            [new PreservedQueryPair("tag", "a"), new PreservedQueryPair("tag", "b")],
            Compute(["tag"], ("tag", new StringValues(["a", "b"]))));

    [Fact]
    public void A_null_value_becomes_an_empty_string()
        // A single null string is StringValues.Empty and yields nothing — the null
        // ELEMENT case is an array carrying one, which is the shape that would
        // otherwise render the literal absence of a value as a null attribute.
        => Assert.Equal(
            [new PreservedQueryPair("flag", "")],
            Compute(["flag"], ("flag", new StringValues([null]))));

    [Fact]
    public void Request_order_is_kept_across_parameters()
        => Assert.Equal(
            [
                new PreservedQueryPair("b", "2"),
                new PreservedQueryPair("a", "1"),
            ],
            Compute(["a", "b"], ("b", "2"), ("a", "1")));

    [Fact]
    public void A_listed_uBookIt_key_is_never_preserved()
    {
        // Every own key, not a sample: a hidden input duplicating a live control's
        // name would submit both values and leave the winner to model binding.
        foreach (var ownKey in PreservedQuery.OwnKeys)
        {
            Assert.Empty(Compute([ownKey], (ownKey, "smuggled")));

            // Case variance must not re-admit it.
            Assert.Empty(Compute([ownKey.ToUpperInvariant()], (ownKey, "smuggled")));
        }
    }

    [Fact]
    public void The_own_key_set_is_total_over_BookingKeys()
    {
        // The totality guard (design D2): the exclusion is DERIVED from BookingKeys'
        // *Query constants, and this asserts the derivation against an independent
        // reflection — so a query key added to BookingKeys joins the exclusion, or
        // this names it. TempData keys are not query parameters and stay out.
        var declared = typeof(BookingKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)
                && field.Name.EndsWith("Query", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(declared.SetEquals(PreservedQuery.OwnKeys));

        // And the derivation is not vacuous: the five keys that exist today are all
        // present by VALUE, so a rename of the constants cannot quietly empty the set.
        Assert.Superset(
            new HashSet<string>(["ubBook", "ubDate", "ubDateOther", "ubMins", "ubWho"]),
            declared);
    }
}
