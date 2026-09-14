using System.Reflection;
using Microsoft.Extensions.Primitives;
using UBookIt.Tests.Support;
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

    // ---- the documentation's load-bearing claims (task 1.8) ----

    private static string BookingPageDocs() => RepoFiles.Read("docs/booking-page.md");

    /// <summary>
    /// The empty default and the refusal to preserve unknown parameters — the two
    /// claims a site owner acts on when deciding whether this setting is safe.
    /// </summary>
    [Fact]
    public void The_docs_state_the_empty_default_and_the_bound()
    {
        DocumentationAssert.Says(
            BookingPageDocs(),
            "The list is empty by default, so nothing changes until you configure it");
        DocumentationAssert.Says(
            BookingPageDocs(),
            "Only listed names are preserved");
        DocumentationAssert.Says(
            BookingPageDocs(),
            "the list you configure is the bound that keeps that safe");
    }

    /// <summary>The own-keys exclusion, stated where the setting is documented.</summary>
    [Fact]
    public void The_docs_state_the_own_key_exclusion()
        => DocumentationAssert.Says(
            BookingPageDocs(),
            "are never preserved through this mechanism, even if you list them");

    /// <summary>
    /// The whole-flow claim. This guard's first version pinned the OPPOSITE sentence
    /// ("but not the redirect after it") while the redirect genuinely dropped the
    /// parameters; the redirect was then taught to carry them (Chris, 2026-09-14)
    /// and the doc and guard flipped together — which is the point of pairing them.
    /// </summary>
    [Fact]
    public void The_docs_state_the_whole_flow_claim()
        => DocumentationAssert.Says(
            BookingPageDocs(),
            "every step's URL, the booking submission, and the redirect after it");

    // ---- the redirect's preserved tail (the submission-redirect scenarios) ----

    [Fact]
    public void The_flow_link_appends_preserved_pairs_encoded_and_in_order()
    {
        var query = BookingFlowLink.For(
            BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")),
            new DateOnly(2026, 9, 15),
            60,
            preserved:
            [
                new PreservedQueryPair("utm_source", "newsletter"),
                new PreservedQueryPair("tag", "a"),
                new PreservedQueryPair("tag", "b"),
                new PreservedQueryPair("note", "a&b=c"),
            ]).ToUriComponent();

        // The flow's own parameters first, then the preserved tail in request order,
        // values percent-encoded so a reserved character cannot become structure.
        Assert.Contains("ubBook=", query, StringComparison.Ordinal);
        Assert.EndsWith(
            "&utm_source=newsletter&tag=a&tag=b&note=a%26b%3Dc", query, StringComparison.Ordinal);
    }

    [Fact]
    public void No_preserved_pairs_leaves_the_flow_link_byte_identical()
        => Assert.Equal(
            BookingFlowLink.For(
                BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")),
                new DateOnly(2026, 9, 15), 60).ToUriComponent(),
            BookingFlowLink.For(
                BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")),
                new DateOnly(2026, 9, 15), 60, preserved: []).ToUriComponent());

    [Fact]
    public void A_preserved_only_query_is_the_pairs_and_nothing_else()
    {
        // The component-named flow's redirect: no flow state, just the page's own
        // parameters — and empty pairs produce an empty query, so an unconfigured
        // site's redirect is byte-for-byte what it always was.
        Assert.Equal(
            "?utm_source=newsletter",
            BookingFlowLink.Carrying([new PreservedQueryPair("utm_source", "newsletter")]).ToUriComponent());
        Assert.Equal(string.Empty, BookingFlowLink.Carrying([]).ToUriComponent());
    }

    /// <summary>
    /// The wiring, since <c>BackToFlow</c> is private and needs an Umbraco host: both
    /// surface controllers compute the preserved pairs from the request and hand them
    /// to the one link-building vocabulary. Source-level, like the sibling guard that
    /// already pins "every query string the controllers produce is BookingFlowLink's"
    /// — this narrows it to "and the preserved tail rides through it".
    /// </summary>
    [Fact]
    public void Both_surface_controllers_carry_the_preserved_tail_through_the_link()
    {
        foreach (var controller in new[]
        {
            "src/UBookIt.Web/Rendering/BookingSurfaceController.cs",
            "src/UBookIt.Web/Rendering/ServiceBookingSurfaceController.cs",
        })
        {
            var source = RepoFiles.Read(controller);

            Assert.Contains(
                "PreservedQuery.Compute(", source, StringComparison.Ordinal);
            Assert.Contains(
                "BookingFlowLink.Carrying(preserved)", source, StringComparison.Ordinal);
            Assert.Contains("preserved", source, StringComparison.Ordinal);
        }
    }
}
