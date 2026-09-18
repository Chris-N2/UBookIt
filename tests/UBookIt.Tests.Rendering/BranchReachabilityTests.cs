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

    public static TheoryData<string> ShippedViews()
    {
        var data = new TheoryData<string>();

        foreach (var view in ViewInventory.All)
        {
            data.Add(view);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedViews))]
    public async Task Every_literal_the_view_can_emit_appears_in_some_state(string view)
    {
        var states = ViewFixtures.For(view);

        // Every shipped view has states, delegating views included — asserted
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
        var shared = ViewInventory.All
            .SelectMany(view => DuplicatedLiteralsOf(view).Select(literal => $"{Name(view)}:{literal}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                // `ubookit-no-dates` is emitted by BOTH empty-window branches — the one that
                // names a shorter length that would work, and the one for a window with nothing
                // at any length. They are deliberately the same hook because they are the same
                // kind of statement to a site styling them; what distinguishes them is their
                // TEXT, which rule 3 does not read. Safe here because each is separately
                // asserted by wording in PrivacyNoticeTests' sibling, AvailableDatesTests, where
                // the two sentences are pinned apart.
                "_AvailableDates.cshtml:ubookit-no-dates",

                // Listed in ordinal order, which is the order the scan produces.
                //
                // _DateAndLength: both length branches render the same rejection
                // span (settled and chosen), the standing note under the choice
                // control comes from two branches, and the reset notice and choice
                // error each appear twice — inside the control, and in the wrapper
                // rendered when there is no control. Each is covered instead by
                // fixtures reaching both sides.
                "_DateAndLength.cshtml:ubookit-duration-error",

                // `ubookit-field` marks every field position in the view, and two of
                // those positions are the wrappers that stand in for a control replaced
                // by settled text. So it is emitted from mutually exclusive branches BY
                // DESIGN — that is what "every field position" means — and asking
                // whether those branches need distinguishing has a definite answer here:
                // no. The whole purpose of the class is that a field is a field whether
                // it holds a control or the text that replaced one, because the layout
                // rule must not skip the rows carrying the most conditional behaviour.
                // It also carries no behaviour, so a branch losing it is a layout
                // regression rather than an accessibility one, and it renders in every
                // state rather than in a reachable few.
                "_DateAndLength.cshtml:ubookit-field",
                "_DateAndLength.cshtml:ubookit-field-error",
                "_DateAndLength.cshtml:ubookit-hint",
                "_DateAndLength.cshtml:ubookit-notice",
                "_DateAndLength.cshtml:ubookit-who-error",
                "_DateAndLength.cshtml:ubookit-who-hint",
                "_DateAndLength.cshtml:ubookit-who-reset",

                // The two empty states of the time list, which differ in text
                // rather than in class.
                "_Times.cshtml:ubookit-no-times",

                // One field wrapper per booker field, and one error span per booker
                // field. Both are one-per-field rather than branch-dependent: all three
                // wrappers are unconditional, so neither can die while a sibling keeps
                // the literal alive on a path the fixtures do not reach.
                // `ubookit-email-hint` is emitted by BOTH branches of the email field's hint —
                // the one promising a confirmation and the one saying only that the site can make
                // contact — and it MUST be, because `aria-describedby` on the input names that id
                // unconditionally. Making the two branches emit different ids to satisfy this rule
                // would break the programmatic relationship between the field and its description,
                // trading a real accessibility guarantee for a proxy's convenience.
                //
                // The branches are distinguished by their TEXT instead, which PrivacyNoticeTests
                // asserts in both directions, so nothing here is going untested — only untested
                // BY THIS RULE, which is what this list is for.
                "_YourDetails.cshtml:ubookit-email-hint",
                "_YourDetails.cshtml:ubookit-field",
                "_YourDetails.cshtml:ubookit-field-error",
                // The hint's CLASS, duplicated for the same reason as its id and with even less
                // choice about it: both wordings are the same kind of thing and must look the same.
                "_YourDetails.cshtml:ubookit-hint",
            ],
            shared);
    }

    [Theory]
    [MemberData(nameof(ShippedViews))]
    public async Task Each_shared_literal_is_rendered_by_more_than_one_state(string view)
    {
        // The enumeration above records WHY each shared literal is safe — "covered
        // instead by fixtures reaching both sides" — and that was prose. This makes
        // it a check.
        //
        // Where two branches emit the same literal, rule 3 cannot tell which one
        // produced it. The mitigation is that fixtures reach both, and the evidence
        // for that is the literal appearing in more than one distinct rendered
        // state. One state alone would mean only one branch is exercised, and the
        // other could die unnoticed.
        //
        // It does not close the blind spot — QA showed a branch can be disabled
        // while a sibling keeps the literal alive — but it does convert the stated
        // reason into something that fails when it stops being true.
        var states = ViewFixtures.For(view);
        var duplicated = DuplicatedLiteralsOf(view);

        if (duplicated.Count == 0)
        {
            return;
        }

        var rendered = new List<string>(states.Count);

        foreach (var state in states)
        {
            rendered.Add(await _renderer.RenderAsync(view, state.Model));
        }

        foreach (var literal in duplicated)
        {
            var reached = rendered.Count(html => html.Contains(literal, StringComparison.Ordinal));

            Assert.True(
                reached > 1,
                $"{view}: '{literal}' is emitted from more than one branch but rendered in only "
                + $"{reached} state, so only one of those branches is exercised and the others "
                + "could die unnoticed. Add a state reaching the other.");
        }
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

        Assert.All(ViewInventory.All, view =>
        {
            if (!ModelReferences.DelegatingViews.ContainsKey(view) && !RendersNoHooks(view))
            {
                Assert.NotEmpty(LiteralsOf(view));
            }
        });

        // AND THE EXEMPTED SET IS ENUMERATED, like every other exemption in this file.
        //
        // Asserting that an exempted view has no literals would restate `RendersNoHooks`'
        // own definition and could not fail — the control shape this change has produced
        // repeatedly. What can fail, and is worth failing, is a FOURTH view quietly joining the
        // set: a flow view whose hooks were removed would stop being checked by rule 3 and nothing
        // else would say so.
        //
        // Scoped to non-delegating views, because the three BookingFlow entry views render no
        // hooks either — they delegate — and were already skipped by the clause above. Listing them
        // here would say the cancellation pages and the delegating views are the same kind of
        // thing, which they are not.
        Assert.Equal(
            [
                "~/Views/Cancellation/Cancelled.cshtml",
                "~/Views/Cancellation/Index.cshtml",
                "~/Views/Cancellation/Unusable.cshtml",
            ],
            ViewInventory.All
                .Where(view => !ModelReferences.DelegatingViews.ContainsKey(view) && RendersNoHooks(view))
                .Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Whether a view renders no <c>id</c> and no <c>class</c> — the two anchors the extraction
    /// reads. <b>Comments are stripped first, on the same terms as <see cref="LiteralsOf"/></b>, so
    /// a class named only in prose cannot make a view look hooked when the extraction sees nothing
    /// in it.
    /// </summary>
    /// <remarks>
    /// The cancellation pages. They are standalone documents no stylesheet can reach, so they carry
    /// no styling hooks, and <c>default-frontend</c>'s vocabulary requirement means they may not
    /// carry classes the published contract does not describe. Two of the three have no branches at
    /// all; the third's two branches emit distinct markup, so the branch rule found nothing in them
    /// before the classes went either.
    /// </remarks>
    private static bool RendersNoHooks(string view)
        => !Regex.IsMatch(
            Regex.Replace(
                RepoFiles.Read(ViewInventory.SourcePathOf(view)),
                @"@\*.*?\*@",
                string.Empty,
                RegexOptions.Singleline),
            @"\b(?:id|class)=""");
}
