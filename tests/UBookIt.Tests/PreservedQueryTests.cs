using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    /// The doc's own-keys list is tied to the DERIVED set (QA round 1's nit): the
    /// code derives the exclusion from BookingKeys, but the sentence naming the five
    /// keys was hand-kept, so a sixth key would leave it silently incomplete.
    /// </summary>
    [Fact]
    public void The_docs_name_every_own_key()
        => Assert.All(
            PreservedQuery.OwnKeys,
            key => Assert.Contains($"`{key}`", BookingPageDocs(), StringComparison.Ordinal));

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

    // ---- the whole redirect decision, exercised as a value (QA round 1's MAJOR 2) ----
    //
    // The first guard here was source-level and pinned only the subjectless branch's
    // mechanism: QA deleted the preserved argument from the branch every normal
    // submission takes and 2684 tests stayed green. The decision now lives in
    // AfterSubmission and is asserted by OUTPUT, branch by branch; the controllers'
    // remaining wiring is held by two things — AfterSubmission's preserved parameter
    // is REQUIRED (a call without it does not compile), and the source guard below
    // pins that what is passed is computed from the request, not an empty stand-in.

    private static readonly IReadOnlyList<PreservedQueryPair> Tail =
        [new PreservedQueryPair("utm_source", "newsletter")];

    [Fact]
    public void A_subject_ful_submission_redirect_carries_flow_state_and_the_tail()
        => Assert.Equal(
            BookingFlowLink.For(
                BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")),
                new DateOnly(2026, 9, 15), 60, preserved: Tail).ToUriComponent(),
            BookingFlowLink.AfterSubmission(
                BookingSubject.Resource(new Guid("00000000-0000-0000-0000-000000000001")),
                new DateOnly(2026, 9, 15), 60, chosenResourceId: null, Tail)!.Value.ToUriComponent());

    [Fact]
    public void The_subject_ful_redirect_still_ends_with_the_tail()
        // Not implied by the equality above alone: if For() itself lost the tail,
        // both sides would agree on the wrong answer. This pins the guarantee.
        => Assert.EndsWith(
            "&utm_source=newsletter",
            BookingFlowLink.AfterSubmission(
                BookingSubject.Service(new Guid("00000000-0000-0000-0000-000000000900")),
                new DateOnly(2026, 9, 15), 60, chosenResourceId: null, Tail)!.Value.ToUriComponent(),
            StringComparison.Ordinal);

    [Fact]
    public void A_component_named_submission_redirect_is_the_tail_alone()
        => Assert.Equal(
            "?utm_source=newsletter",
            BookingFlowLink.AfterSubmission(
                subject: null, new DateOnly(2026, 9, 15), 60, chosenResourceId: null, Tail)!
                .Value.ToUriComponent());

    [Fact]
    public void A_component_named_submission_with_nothing_to_preserve_redirects_with_no_query()
        // Null, not an empty QueryString: the controller redirects plain, so an
        // unconfigured site's redirect stays byte-for-byte what it always was.
        => Assert.Null(BookingFlowLink.AfterSubmission(
            subject: null, new DateOnly(2026, 9, 15), 60, chosenResourceId: null, []));

    [Fact]
    public void The_chosen_resource_still_travels_through_the_submission_redirect()
        => Assert.Contains(
            "ubWho=00000000-0000-0000-0000-000000000002",
            BookingFlowLink.AfterSubmission(
                BookingSubject.Service(new Guid("00000000-0000-0000-0000-000000000900")),
                new DateOnly(2026, 9, 15), 60,
                new Guid("00000000-0000-0000-0000-000000000002"), Tail)!.Value.ToUriComponent(),
            StringComparison.Ordinal);

    /// <summary>
    /// The last unprovable-by-output hop: that each controller passes AfterSubmission
    /// the pairs computed from THE REQUEST'S QUERY against THE CONFIGURED ALLOW-LIST.
    /// One regex per controller, spanning the call and pinning BOTH Compute
    /// arguments — QA round 2 proved the query-only version green with
    /// <c>Compute(Request.Query, [])</c>, an empty stand-in laundered through the
    /// pinned function, so the allow-list source is pinned too. The sibling seam
    /// guard forbids any other link call.
    /// </summary>
    [Fact]
    public void Both_controllers_hand_the_computed_pairs_to_the_submission_decision()
    {
        foreach (var controller in new[]
        {
            "src/UBookIt.Web/Rendering/BookingSurfaceController.cs",
            "src/UBookIt.Web/Rendering/ServiceBookingSurfaceController.cs",
        })
        {
            Assert.Matches(
                new System.Text.RegularExpressions.Regex(
                    @"BookingFlowLink\.AfterSubmission\((?:(?!;).)*?PreservedQuery\.Compute\("
                    + @"Request\.Query,\s*_frontendSettings\.PreservedQueryParameters\)",
                    System.Text.RegularExpressions.RegexOptions.Singleline),
                RepoFiles.Read(controller));
        }
    }

    // ---- the composer's registration and binding, by effect (QA round 1's MAJOR 1) ----

    /// <summary>
    /// QA deleted the <c>AddSingleton(frontendSettings)</c> line and 2684 tests
    /// stayed green while production would fail every booking render at DI
    /// resolution. So: compose the REAL composer over a REAL in-memory configuration
    /// and assert the registered instance by effect — the registration exists, and
    /// the section key and binder actually delivered the configured names. A typo in
    /// "UBookIt:Frontend" now fails here instead of silently giving every configured
    /// site the empty default.
    /// </summary>
    [Fact]
    public void The_rendering_composer_registers_frontend_settings_bound_from_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UBookIt:Frontend:PreservedQueryParameters:0"] = "utm_source",
                ["UBookIt:Frontend:PreservedQueryParameters:1"] = "culture",
            })
            .Build();

        var services = new ServiceCollection();
        new UBookIt.Web.Composing.UBookItRenderingComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(services, configuration));

        using var provider = services.BuildServiceProvider();
        var settings = provider.GetService<UBookIt.Web.FrontendSettings>();

        Assert.NotNull(settings);
        Assert.Equal(["utm_source", "culture"], settings.PreservedQueryParameters);
    }

    /// <summary>An absent section binds to the empty default rather than failing resolution.</summary>
    [Fact]
    public void An_unconfigured_site_composes_the_empty_default()
    {
        var services = new ServiceCollection();
        new UBookIt.Web.Composing.UBookItRenderingComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(services, new ConfigurationBuilder().Build()));

        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<UBookIt.Web.FrontendSettings>().PreservedQueryParameters);
    }
}
