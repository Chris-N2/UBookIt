using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the suite covers — and, now, the claim that it covers everything.
/// <para>
/// The inventory is scanned rather than listed, so these tests are what stop the
/// scan decaying: a scan that silently found four views would let every rule
/// elsewhere pass over four views and report green.
/// </para>
/// </summary>
public class ViewInventoryTests
{
    private readonly ViewRenderer _renderer = new();

    [Fact]
    public void The_scan_finds_every_shipped_view()
    {
        // Counted, so a scan that quietly stopped finding things fails here rather
        // than making the rules vacuous. If a view is added this fails, and someone
        // decides deliberately that it belongs in the suite — which is the point.
        //
        // Both sets are counted. Counting only the governed set would let a view be
        // added AND excluded in one change without anything failing, which is the
        // decay this guard exists to prevent.
        Assert.Equal(15, ViewInventory.Shipped.Count);
        Assert.Equal(14, ViewInventory.All.Count);
    }

    [Fact]
    public void The_only_view_outside_the_rendering_rules_is_the_style_emitter()
    {
        // The exclusion, asserted rather than trusted. A second exclusion — or a
        // different one — fails here, so "excluded by name with a reason" cannot decay
        // into "excluded because someone filtered it once".
        var excluded = ViewInventory.Shipped
            .Where(view => !ViewInventory.All.Contains(view, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["~/Views/Shared/UBookIt/_Styles.cshtml"], excluded);
    }

    [Fact]
    public void Exactly_one_view_emits_the_package_stylesheet()
    {
        // The rule that covers the excluded view, and the spec's "there is one
        // emission route" — which exists because a future theme suppresses package
        // CSS by replacing that one partial, and a second link element somewhere
        // else would survive the theme and fight it.
        var emitting = ViewInventory.Shipped
            .Where(view => RepoFiles.Read(ViewInventory.SourcePathOf(view)) is var source
                && (Regex.IsMatch(source, @"<link\b", RegexOptions.IgnoreCase)
                    || Regex.IsMatch(source, @"<style\b", RegexOptions.IgnoreCase)))
            .ToList();

        Assert.Equal(["~/Views/Shared/UBookIt/_Styles.cshtml"], emitting);

        var emitter = RepoFiles.Read(ViewInventory.SourcePathOf(ViewInventory.NotRendered[0]));

        // Names the shipped asset. This asserts the href is the one the package
        // intends — it does NOT tie the href to the file's real name, so it is not
        // what would catch a rename; `StylesheetContractTests` and `PackagingTests`
        // both read the file by path and fail if it moves.
        Assert.Contains("_content/UBookIt.Web/ubookit.css", emitter, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(emitter, @"<link\b", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Every_shipped_view_is_exercised()
    {
        // The completeness guarantee. Nothing is deferred: the shipped set and the
        // exercised set are the same set.
        //
        // Three views once sat outside the suite because they call
        // `Html.BeginUmbracoForm` and the rig could not host it. They were excluded
        // by a phrase — "in scope" — that named a set the spec never defined, which
        // is how the exclusion stayed invisible. There is no such phrase now.
        var exercised = ViewFixtures.All
            .Select(rendered => rendered.ViewPath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var view in ViewInventory.All)
        {
            Assert.True(
                exercised.Contains(view),
                $"{view} is shipped and no fixture renders it, so every rule passes "
                + "over it while reporting green.");
        }
    }

    [Fact]
    public void The_completeness_check_cannot_pass_by_checking_nothing()
    {
        // The vacuity guard the spec requires. A completeness rule satisfied by an
        // empty set is the rule's own failure mode: it finds no counter-example
        // because it looked at nothing.
        Assert.NotEmpty(ViewFixtures.All);
        Assert.NotEmpty(ViewInventory.All);

        var exercised = ViewFixtures.All
            .Select(rendered => rendered.ViewPath)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(
            exercised.Count >= ViewInventory.All.Count,
            $"{exercised.Count} view(s) exercised against {ViewInventory.All.Count} shipped: "
            + "the exercised set has shrunk below the shipped set.");
    }

    [Fact]
    public void A_view_that_reaches_the_umbraco_form_helper_is_exercised_like_any_other()
    {
        // The three views that call `Html.BeginUmbracoForm`, directly or through a
        // delegate, hold no special status. Asserted as a property of whichever
        // views reach the helper rather than by name, so it keeps meaning something
        // if a fourth appears.
        //
        // Non-vacuity below: some view must actually reach it, or this passes by
        // asking nothing.
        var reaching = ViewInventory.All.Where(view => ReachesUmbraco(view, [])).ToList();

        Assert.True(
            reaching.Count >= 3,
            $"only {reaching.Count} view(s) reach Html.BeginUmbracoForm; this check is "
            + "asserting almost nothing.");

        var exercised = ViewFixtures.All
            .Select(rendered => rendered.ViewPath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var view in reaching)
        {
            Assert.True(
                exercised.Contains(view),
                $"{view} reaches Html.BeginUmbracoForm and is not exercised.");
        }
    }

    [Fact]
    public async Task Each_shared_partial_is_rendered_by_a_flow_page_that_includes_it()
    {
        // Rule 1 is asked of pages, so a shared partial reaches it only by being
        // rendered inside one. This asserts that it is — against the RENDERED
        // OUTPUT, not against view source.
        //
        // The distinction is the requirement. A source scan sees that a flow view
        // NAMES a partial; it cannot see the order, the condition, or that one is
        // included twice. The previous suite relied on exactly such a scan, and the
        // composition it was standing in for had already drifted.
        var flow = await _renderer.RenderAsync(
            ViewInventory.ServiceFlow,
            ViewFixtures.For(ViewInventory.ServiceFlow)
                .First(c => c.State == "service: times, no errors")
                .Model);

        // A marker each partial emits and its siblings do not, so presence in the
        // flow's output is evidence that partial rendered.
        foreach (var (partial, marker) in new[]
        {
            (ViewInventory.DateAndLength, "ubookit-date-form"),
            (ViewInventory.Times, "ubookit-time-0"),
            (ViewInventory.YourDetails, "ubookit-name"),
        })
        {
            Assert.True(
                flow.Contains(marker, StringComparison.Ordinal),
                $"{partial} did not render inside {ViewInventory.ServiceFlow}: no '{marker}'.");
        }

        // The error summary renders only when there are errors, so it is evidenced
        // from a state that has some.
        var withErrors = await _renderer.RenderAsync(
            ViewInventory.ServiceFlow,
            ViewFixtures.For(ViewInventory.ServiceFlow)
                .First(c => c.State == "service: with errors")
                .Model);

        Assert.True(
            withErrors.Contains("ubookit-errors", StringComparison.Ordinal),
            $"{ViewInventory.ErrorSummary} did not render inside {ViewInventory.ServiceFlow}.");
    }

    [Fact]
    public async Task An_exempted_delegate_adds_no_markup_to_the_view_it_delegates_to()
    {
        // The delegate exemption in ModelReferences.DelegatingViews switches off two
        // rules, and its stated reason is "checked itself" — true only if the
        // delegate contributes nothing of its own. That half was asserted from
        // source shape, which cannot see it: a view can name one partial and still
        // render arbitrary markup around it.
        //
        // Asked of EVERY exempted view rather than the one that prompted it. Only
        // the dispatcher had this check; its two siblings were exempted on the same
        // stated reason with nothing testing it.
        //
        // Compared by TAG SEQUENCE rather than by bytes, because a delegate into a
        // form view carries a per-render GUID id and two fresh tokens. (Measured for
        // the dispatcher: the two outputs differ by exactly one trailing "\r\n" —
        // the newline after the PartialAsync line — 3460 bytes against 3458, and are
        // identical once trimmed.)
        Assert.NotEmpty(ModelReferences.DelegatingViews);

        foreach (var (view, _) in ModelReferences.DelegatingViews)
        {
            var target = ModelReferences.DelegationTargetOf(view);

            Assert.True(
                target is not null,
                $"{view} is exempted as a delegate but its source does not hand its "
                + "model to exactly one view.");

            var model = ViewFixtures.For(view)[0].Model;

            var direct = await Structure(target!, model);
            var viaDelegate = await Structure(view, model);

            Assert.NotEmpty(direct);
            Assert.Equal(direct, viaDelegate);
        }
    }

    /// <summary>The rendered document's element names, in document order.</summary>
    private async Task<IReadOnlyList<string>> Structure(string viewPath, object model)
    {
        var html = await _renderer.RenderAsync(viewPath, model);
        var document = await new HtmlParser().ParseDocumentAsync(html);

        return
        [
            .. document.Body!.QuerySelectorAll("*").Select(element => element.LocalName),
        ];
    }

    /// <summary>
    /// Whether a view uses <c>Html.BeginUmbracoForm</c>, following partial
    /// includes so a one-line delegate is judged by what it delegates to.
    /// </summary>
    private static bool ReachesUmbraco(string viewPath, HashSet<string> seen)
    {
        if (!seen.Add(viewPath))
        {
            return false;
        }

        var source = RepoFiles.Read(ViewInventory.SourcePathOf(viewPath));

        if (source.Contains("BeginUmbracoForm", StringComparison.Ordinal))
        {
            return true;
        }

        return Regex
            .Matches(source, @"PartialAsync\(""(~/[^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Any(included => ReachesUmbraco(included, seen));
    }
}
