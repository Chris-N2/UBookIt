using System.Text.RegularExpressions;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Tests.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// Rule 3 — every branch a view carries can actually be taken.
/// <para>
/// Added during apply, and the reason is worth keeping (design D9). The property
/// rule catches a model member that changes nothing; it does <b>not</b> catch a
/// dead <em>branch</em>, because a member can be perfectly live while one of the
/// branches it selects is unreachable. Proved by mutation: forcing the
/// catalogue's <c>@if (Model.HasEntries)</c> to <c>true ||</c> leaves both
/// <c>HasEntries</c> and <c>Entries</c> live — the two states still render
/// differently — while "there is nothing available to book" becomes unreachable.
/// The property rule passed. This one does not.
/// </para>
/// <para>
/// A branch is identified by the literal <c>id</c> and <c>class</c> values in its
/// markup, which is a proxy rather than instrumentation of the Razor itself: if a
/// literal a view can emit never appears in any state, the code emitting it cannot
/// be reached. Being a proxy, it is deliberately paired with the property rule
/// rather than replacing it — neither catches the other's case.
/// </para>
/// </summary>
public class BranchReachabilityTests
{
    private readonly ViewRenderer _renderer = new();

    /// <summary>
    /// Literals a view can emit that no state is expected to render, with the
    /// reason. Explicit, so an exemption is a decision someone made rather than a
    /// hole nobody noticed.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
    };

    public static TheoryData<string> InScopeViews()
    {
        var data = new TheoryData<string>();

        foreach (var view in ViewInventory.InScope)
        {
            data.Add(view);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(InScopeViews))]
    public async Task Every_literal_the_view_can_emit_appears_in_some_state(string view)
    {
        var states = ViewFixtures.For(view);

        if (states.Count == 0)
        {
            // A delegating view emits nothing of its own; the view it hands to is
            // checked in its own right.
            Assert.Contains(view, ModelReferences.DelegatingViews.Keys);
            return;
        }

        var rendered = new List<string>(states.Count);

        foreach (var state in states)
        {
            rendered.Add(await _renderer.RenderAsync(view, state.Model));
        }

        foreach (var literal in LiteralsOf(view))
        {
            if (Exempt.ContainsKey(literal))
            {
                continue;
            }

            Assert.True(
                rendered.Any(html => html.Contains(literal, StringComparison.Ordinal)),
                $"{view}: '{literal}' appears in the view's markup but in none of its "
                + $"{states.Count} rendered states. Either the branch emitting it cannot be "
                + "taken, or no fixture reaches it — and the first is a message placed where "
                + "it can never appear.");
        }
    }

    /// <summary>
    /// The literal <c>id</c> and <c>class</c> values a view's markup can emit.
    /// <para>
    /// Razor-interpolated values are skipped: <c>id="@BookingFieldIds.Duration"</c>
    /// is not a literal the source can be compared against, and the property rule
    /// covers what drives it. Comments are stripped first, so a class named only in
    /// prose is not demanded of the output.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> LiteralsOf(string view)
    {
        var source = Regex.Replace(
            RepoFiles.Read(ViewInventory.SourcePathOf(view)),
            @"@\*.*?\*@",
            string.Empty,
            RegexOptions.Singleline);

        return
        [
            .. Regex
                .Matches(source, @"(?:id|class)=""(?<value>[^""@]+)""")
                .Select(m => m.Groups["value"].Value.Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void The_literal_extraction_is_not_vacuous()
    {
        // Non-vacuity, on the same terms as the property rule's: a view whose
        // literals cannot be found passes this rule trivially. `_DateAndLength` is
        // the branch-densest view in the package, so if the extraction stops
        // finding things it fails here first.
        var literals = LiteralsOf(ViewInventory.DateAndLength);

        Assert.Contains("ubookit-who-reset", literals);
        Assert.Contains("ubookit-who-hint", literals);
        Assert.Contains("ubookit-date-form", literals);

        Assert.All(ViewInventory.InScope, view =>
        {
            if (!ModelReferences.DelegatingViews.ContainsKey(view))
            {
                Assert.NotEmpty(LiteralsOf(view));
            }
        });
    }
}
