using System.Text.RegularExpressions;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// What the suite covers, and what it admits it does not.
/// <para>
/// The inventory is scanned rather than listed, so these tests are what stop the
/// scan decaying: a scan that silently found four views would let every rule below
/// pass over four views and report green.
/// </para>
/// </summary>
public class ViewInventoryTests
{
    [Fact]
    public void The_scan_finds_every_shipped_view()
    {
        // Counted, so a scan that quietly stopped finding things fails here rather
        // than making the rules vacuous. The numbers are the ones the change was
        // scoped against; if a view is added, this fails and someone decides
        // whether it is in scope — which is the point.
        Assert.Equal(14, ViewInventory.All.Count);
        Assert.Equal(3, ViewInventory.Deferred.Count);
        Assert.Equal(11, ViewInventory.InScope.Count);
    }

    [Fact]
    public void Every_deferred_view_transitively_reaches_the_umbraco_form_helper()
    {
        // The reason, not the names. `BookingFlow/Default.cshtml` reads as
        // Umbraco-free and is not — it is a one-line delegate into
        // `Booking/Default.cshtml` — so a by-name list would record the fact and
        // lose the reason, and the next person would not know which views could
        // join the suite.
        foreach (var deferred in ViewInventory.Deferred)
        {
            Assert.True(
                ReachesUmbraco(deferred, []),
                $"{deferred} is deferred but does not reach Html.BeginUmbracoForm");
        }
    }

    [Fact]
    public void No_view_in_scope_reaches_the_umbraco_form_helper()
    {
        // The other half, and the one that makes the suite honest: if an in-scope
        // view did reach it, the suite would be failing to render something it
        // claims to cover — or, worse, rendering it in a way the site does not.
        foreach (var view in ViewInventory.InScope)
        {
            Assert.False(
                ReachesUmbraco(view, []),
                $"{view} is in scope but reaches Html.BeginUmbracoForm");
        }
    }

    [Fact]
    public void The_shared_partials_are_included_only_by_the_deferred_views()
    {
        // Design D2 rests on this and nothing asserted it. The partials are rendered
        // here as a composed document rather than through a flow view, which is
        // sound only while the deferred three are their only includers. If an
        // in-scope view included one, this suite would be rendering that partial in
        // a composition the site never produces — and would be claiming coverage it
        // does not have.
        foreach (var view in ViewInventory.InScope)
        {
            var source = RepoFiles.Read(ViewInventory.SourcePathOf(view));

            foreach (var partial in ViewFixtures.Partials)
            {
                Assert.DoesNotContain(
                    partial.TrimStart('~', '/'),
                    source,
                    StringComparison.Ordinal);
            }
        }

        // Non-vacuity: the deferred views really do include them, so the absence
        // above is a property of the in-scope set rather than of the search.
        var service = RepoFiles.Read(
            ViewInventory.SourcePathOf("~/Views/Shared/Components/BookingFlow/Service.cshtml"));

        Assert.All(ViewFixtures.Partials, partial =>
            Assert.Contains(partial.TrimStart('~', '/'), service, StringComparison.Ordinal));
    }

    [Fact]
    public void The_deferred_views_are_the_only_ones_left_out()
    {
        // Scanned minus deferred equals in scope, with nothing quietly dropped in
        // between.
        Assert.Equal(
            ViewInventory.All.Order(StringComparer.Ordinal),
            ViewInventory.InScope.Concat(ViewInventory.Deferred).Order(StringComparer.Ordinal));
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
