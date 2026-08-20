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

        // Every in-scope view has states, delegating views included — asserted
        // rather than escaped. An `if (states.Count == 0) return;` here was
        // unreachable today and would have let a fixture-less view pass this rule
        // vacuously the moment it stopped being.
        Assert.NotEmpty(states);

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
    public void The_literals_that_cannot_distinguish_a_branch_are_enumerated()
    {
        // Rule 3's known blind spot, made visible instead of silent.
        //
        // The check is a proxy: it asks whether a literal a view can emit ever
        // appears. Where two branches emit the SAME literal, one can die while the
        // other keeps it alive, and the rule cannot tell. QA demonstrated it —
        // deleting the duration-error span from the settled-length branch left all
        // 212 tests green, because the sibling branch emits an identical span.
        //
        // The blind spot cannot be removed without parsing Razor's branch structure,
        // which is a great deal of machinery for a proxy. It can be BOUNDED: every
        // literal emitted more than once in a view is listed here with the reason it
        // is safe, so the set is reviewed rather than discovered. A new shared
        // literal fails this test and has to be justified — which is the moment to
        // ask whether the branches need distinguishing.
        var shared = ViewInventory.InScope
            .SelectMany(view => DuplicatedLiteralsOf(view).Select(literal => $"{Name(view)}:{literal}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                // Listed in ordinal order, which is the order the scan produces.
                //
                // _DateAndLength: both length branches render the same rejection
                // span (settled and chosen), the standing note under the choice
                // control comes from two branches, and the reset notice and choice
                // error each appear twice — inside the control, and in the wrapper
                // rendered when there is no control. Each is covered instead by
                // fixtures reaching both sides.
                "_DateAndLength.cshtml:ubookit-duration-error",
                "_DateAndLength.cshtml:ubookit-field-error",
                "_DateAndLength.cshtml:ubookit-hint",
                "_DateAndLength.cshtml:ubookit-notice",
                "_DateAndLength.cshtml:ubookit-who-error",
                "_DateAndLength.cshtml:ubookit-who-hint",
                "_DateAndLength.cshtml:ubookit-who-reset",

                // The two empty states of the time list, which differ in text
                // rather than in class.
                "_Times.cshtml:ubookit-no-times",

                // One error span per booker field.
                "_YourDetails.cshtml:ubookit-field-error",
            ],
            shared);
    }

    /// <summary>Literals a view's markup emits from more than one place.</summary>
    private static IReadOnlyList<string> DuplicatedLiteralsOf(string view)
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
                .GroupBy(value => value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string Name(string viewPath) => viewPath[(viewPath.LastIndexOf('/') + 1)..];

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
