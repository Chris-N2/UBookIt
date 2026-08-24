using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// Rule 1 — rendered markup resolves its own references (default-frontend spec).
/// <para>
/// One test per clause, each iterating every view and every state, because the
/// accessibility bar is stated once and a bar stated once should be asserted once
/// (design D5). Every failure names the view and the state, which is the
/// information needed to find it.
/// </para>
/// <para>
/// Checked against the <b>rendered document</b>, never against view source. A path
/// or an attribute appears in a <c>.cshtml</c> file whether the construct around it
/// renders or is emitted as literal text, so source scanning cannot tell a working
/// association from a dangling one — and the test that scanned source is itself one
/// of the defects this suite exists to replace.
/// </para>
/// </summary>
public class MarkupInvariantTests
{
    private readonly ViewRenderer _renderer = new();
    private static readonly HtmlParser Parser = new();

    public static TheoryData<DocumentCase> Cases()
    {
        var data = new TheoryData<DocumentCase>();

        foreach (var document in ViewFixtures.Documents)
        {
            data.Add(document);
        }

        return data;
    }

    [Fact]
    public void Every_page_the_package_ships_is_a_document()
    {
        // Non-vacuity for rule 1, which is the one rule that lacked its own guard.
        // Its clauses iterate documents; if a fixture change emptied that set, or
        // dropped a view from it, all of them would pass over nothing. Rules 2 and 3
        // would still fail loudly, so the suite would not be silent — but rule 1
        // alone would be, and that asymmetry is the thing worth removing.
        //
        // Every shipped view is either a page — and then it is a document here — or
        // a shared partial, which is a fragment that reaches rule 1 by being
        // rendered inside the flow page that includes it. That the partials really
        // are rendered that way is asserted in ViewInventoryTests, against the
        // rendered output rather than against view source.
        Assert.NotEmpty(ViewFixtures.Documents);

        var documents = ViewFixtures.Documents
            .Select(document => document.ViewPath)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var view in ViewInventory.All)
        {
            Assert.True(
                documents.Contains(view) || ViewFixtures.Partials.Contains(view),
                $"{view} is shipped but is neither a document nor a shared partial, so "
                + "rule 1 never asks anything of it.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_label_points_at_a_control_that_exists(DocumentCase rendered)
    {
        var document = await ParseAsync(rendered);

        foreach (var label in document.QuerySelectorAll("label[for]"))
        {
            var target = label.GetAttribute("for")!;

            Assert.True(
                document.GetElementById(target) is not null,
                $"{rendered}: <label for=\"{target}\"> points at no element in the document.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_aria_reference_resolves(DocumentCase rendered)
    {
        var document = await ParseAsync(rendered);

        foreach (var attribute in new[] { "aria-describedby", "aria-labelledby" })
        {
            foreach (var element in document.QuerySelectorAll($"[{attribute}]"))
            {
                var value = element.GetAttribute(attribute);

                // An empty attribute is its own defect: it reads as an association
                // and is none. The views emit null rather than "" for "no
                // description", so this must never be empty either.
                Assert.False(
                    string.IsNullOrWhiteSpace(value),
                    $"{rendered}: <{element.LocalName}> has an empty {attribute}, "
                    + "which reads as an association and is not one.");

                foreach (var id in value!.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var target = document.GetElementById(id);

                    Assert.True(
                        target is not null,
                        $"{rendered}: {attribute}=\"{id}\" resolves to nothing.");

                    // Resolving is not describing. An empty attribute was already a
                    // defect here; an attribute pointing at an EMPTY ELEMENT is the
                    // same defect one step along, and it was invisible — emptying any
                    // of the four described targets passed the whole suite. A screen
                    // reader announces the association and then has nothing to read.
                    //
                    // The message being in the error summary does not save it: the
                    // reporting rule is satisfied, and the visitor on the field still
                    // hears nothing.
                    Assert.True(
                        HasText(target!),
                        $"{rendered}: {attribute}=\"{id}\" resolves to an empty "
                        + $"<{target!.LocalName}>. The association is announced and "
                        + "describes nothing.");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_in_page_link_points_at_something(DocumentCase rendered)
    {
        // The error summary's links, and the omission QA found: rule 1 checked
        // `label[for]` and the aria references and left out the one the views
        // themselves worry about.
        //
        // `_DateAndLength.cshtml` carries the length control's id on a plain `div`
        // when the length is settled — a div that is not a control — for no reason
        // except that the summary links to it: "Without a target the summary link
        // would go nowhere, which is worse than the control it replaced." Nothing
        // asserted that, and pointing every summary link at nothing passed all 212
        // tests.
        //
        // This is the mechanism the whole "accessible failure handling" requirement
        // rests on: a summary that lists each problem and takes you to the field.
        var document = await ParseAsync(rendered);

        foreach (var link in document.QuerySelectorAll("a[href^=\"#\"]"))
        {
            var target = link.GetAttribute("href")![1..];

            // A bare "#" is a link to the top of the page, not a broken reference.
            if (target.Length == 0)
            {
                continue;
            }

            var destination = document.GetElementById(target);

            Assert.True(
                destination is not null,
                $"{rendered}: <a href=\"#{target}\"> points at no element in the document. "
                + "An error summary that links nowhere is worse than one that does not link.");

            // Resolving is not arriving. Following the link moves focus only if the
            // target can take it, and half of these targets are a plain `div` that
            // carries the control's id because the control itself was replaced by
            // settled text. A `div` without `tabindex` is not focusable, so the link
            // would resolve, the viewport would jump, and focus would stay in the
            // summary — which for a keyboard or screen-reader user is the failure the
            // link exists to prevent.
            //
            // Verified by hand once during ⑩-1's keyboard pass (focus landed on
            // `DIV#ubookit-who`); QA showed both `tabindex="-1"` attributes could be
            // deleted with the whole suite green. Announcement is not something a DOM
            // assertion can judge, but focusability is, so it should not rest on a
            // one-off observation.
            Assert.True(
                IsFocusable(destination!),
                $"{rendered}: <a href=\"#{target}\"> resolves to <{destination!.LocalName}>, "
                + "which cannot take focus. Following the link would move the viewport and "
                + "leave focus in the summary.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_problem_is_stated_in_the_document(DocumentCase rendered)
    {
        // The same blind spot as the linked-problem rule, one level up.
        //
        // Every rule about the summary asks something about LINKS — that they
        // resolve, that they exist where the control does. A problem whose message
        // never reaches the page at all satisfies all of them: no link, nothing to
        // resolve, nothing dangling. QA deleted the `else { @error.Message }` branch
        // and the suite stayed green, so an unlinked problem could vanish silently.
        //
        // This is the SHALL that justifies narrowing the association guarantee —
        // "and SHALL still be listed in text where it is not" — and it was the half
        // nothing checked. Replacing the link text with "click here" passed too: the
        // problem linked correctly and was never stated.
        var document = await ParseAsync(rendered);
        var text = document.Body?.TextContent ?? string.Empty;

        foreach (var form in rendered.Forms)
        {
            foreach (var error in form.Errors)
            {
                Assert.True(
                    text.Contains(error.Message, StringComparison.Ordinal),
                    $"{rendered}: '{error.Message}' is in the model and nowhere on the page. "
                    + "A problem the visitor is never told about is the worst outcome the "
                    + "summary has.");

                var links = document
                    .QuerySelectorAll("a[href]")
                    .Where(a => a.GetAttribute("href") == "#" + error.FieldId)
                    .ToList();

                if (links.Count == 0)
                {
                    continue;
                }

                // Where it IS linked, the message must be what the link says. A link
                // reading "click here" beside the message elsewhere satisfies the
                // check above and is exactly the anti-pattern the accessibility bar
                // exists to prevent: a link name that describes nothing.
                Assert.True(
                    links.Any(a => string.Equals(
                        a.TextContent.Trim(), error.Message, StringComparison.Ordinal)),
                    $"{rendered}: the link to '{error.FieldId}' does not say "
                    + $"'{error.Message}'. The link's own text is its accessible name.");
            }
        }
    }

    [Fact]
    public async Task Both_halves_of_the_reporting_rule_are_exercised()
    {
        // Non-vacuity, and it has to count the two halves separately: the linked and
        // the unlinked branch of the summary are different code, and a fixture set
        // that only ever produced one would leave the other's deletion invisible —
        // which is precisely how the deleted `else` survived.
        var linked = 0;
        var unlinked = 0;

        foreach (var rendered in ViewFixtures.Documents)
        {
            var document = await ParseAsync(rendered);

            foreach (var form in rendered.Forms)
            {
                foreach (var error in form.Errors)
                {
                    var isLinked = document
                        .QuerySelectorAll("a[href]")
                        .Any(a => a.GetAttribute("href") == "#" + error.FieldId);

                    if (isLinked)
                    {
                        linked++;
                    }
                    else
                    {
                        unlinked++;
                    }
                }
            }
        }

        Assert.True(linked >= 4, $"only {linked} problem(s) render as a link, so the "
            + "link-text half of the reporting rule is barely exercised.");

        Assert.True(unlinked >= 2, $"only {unlinked} problem(s) render unlinked, so the "
            + "'still listed in text' half of the reporting rule is barely exercised.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_problem_whose_control_is_on_the_page_is_linked_to_it(DocumentCase rendered)
    {
        // The other direction, and the one every rule so far was blind to.
        //
        // "Every link resolves" cannot see a link that was never emitted: dropping a
        // WORKING association is silent, and QA showed two arms of the summary's
        // guard could be flipped to drop one with all 277 tests green. That is the
        // opposite fault from the dangling link, and just as bad — the summary
        // states a problem and gives the visitor no way to it.
        //
        // The spec already required this ("a problem SHALL be associated with its
        // field where that field is on the page"); only the check was missing.
        var document = await ParseAsync(rendered);

        foreach (var form in rendered.Forms)
        {
            foreach (var error in form.Errors)
            {
                // No field, or a control the page does not render: legitimately
                // unlinked, and covered by the resolution rule above.
                if (error.FieldId is null || document.GetElementById(error.FieldId) is null)
                {
                    continue;
                }

                Assert.True(
                    document.QuerySelectorAll("a[href]")
                        .Any(a => a.GetAttribute("href") == "#" + error.FieldId),
                    $"{rendered}: '{error.Message}' names control '{error.FieldId}', which IS on "
                    + "the page, but the summary does not link to it. A problem the visitor can "
                    + "act on should take them there.");
            }
        }
    }

    [Fact]
    public async Task The_linked_problem_rule_is_exercised()
    {
        // Non-vacuity: the rule above skips errors whose control is absent, so a
        // fixture set in which no error ever names a rendered control would satisfy
        // it without checking anything.
        var exercised = 0;

        foreach (var rendered in ViewFixtures.Documents)
        {
            var document = await ParseAsync(rendered);

            foreach (var form in rendered.Forms)
            {
                exercised += form.Errors.Count(e =>
                    e.FieldId is not null && document.GetElementById(e.FieldId) is not null);
            }
        }

        Assert.True(exercised >= 4, $"only {exercised} error(s) name a control that is on the page, "
            + "so the linked-problem rule is barely exercised.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task No_id_is_emitted_twice(DocumentCase rendered)
    {
        var document = await ParseAsync(rendered);

        var duplicates = document
            .QuerySelectorAll("[id]")
            .Select(e => e.Id!)
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"{rendered}: id emitted more than once: {string.Join(", ", duplicates)}. "
            + "Every label[for] and aria reference to it resolves to the first only.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_control_has_an_accessible_name(DocumentCase rendered)
    {
        var document = await ParseAsync(rendered);

        foreach (var control in document.QuerySelectorAll("input, select, textarea"))
        {
            // A hidden field is not a control a visitor operates and has no name to
            // give. Everything else must be nameable.
            if (string.Equals(control.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Assert.True(
                HasAccessibleName(document, control),
                $"{rendered}: <{control.LocalName}"
                + (control.Id is { Length: > 0 } id ? $" id=\"{id}\"" : string.Empty)
                + "> has no associated label, aria-label, or resolving aria-labelledby.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_group_is_named_by_its_legend(DocumentCase rendered)
    {
        // The same fault as an empty label, on the construct the accessibility bar
        // names explicitly: the start times are a `fieldset` with a `legend`, and the
        // booker fields are a group. A `fieldset` whose `legend` is empty is a group
        // announced with no name — the structure is there and says nothing, which is
        // arguably worse than no grouping, because a screen reader will announce the
        // boundary either way.
        //
        // Emptying either legend passed the whole suite. Nothing here checked the
        // legend at all; the fieldset was verified by eye.
        var document = await ParseAsync(rendered);

        foreach (var group in document.QuerySelectorAll("fieldset"))
        {
            var legend = group.QuerySelector("legend");

            Assert.True(
                legend is not null,
                $"{rendered}: <fieldset"
                + (group.Id is { Length: > 0 } id ? $" id=\"{id}\"" : string.Empty)
                + "> has no <legend>, so the group it creates has no name.");

            Assert.True(
                HasText(legend!),
                $"{rendered}: <fieldset"
                + (group.Id is { Length: > 0 } named ? $" id=\"{named}\"" : string.Empty)
                + "> has an empty <legend>. The group is announced and unnamed.");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task No_tag_helper_reaches_the_page(DocumentCase rendered)
    {
        // Asserted against the RAW STRING, not the parsed document, and that is the
        // whole point of this one. `UBookIt.Web` registers no tag helpers and has no
        // _ViewImports.cshtml, so a tag helper is not a build error — it is emitted
        // as literal text onto the page. AngleSharp then parses `<partial …/>` into
        // a perfectly tidy unknown element, and a DOM check would sail past it.
        var html = await RenderAsync(rendered);

        Assert.DoesNotContain("<partial", html, StringComparison.OrdinalIgnoreCase);

        Assert.False(
            Regex.IsMatch(html, @"\sasp-[a-z-]+\s*="),
            $"{rendered}: an asp-* tag helper attribute reached the rendered page.");
    }

    /// <summary>
    /// Whether an element can receive focus: natively, or because it carries a
    /// <c>tabindex</c>. A negative <c>tabindex</c> counts — it is focusable
    /// programmatically and by a fragment link, and merely absent from the sequential
    /// tab order, which is what a wrapper around settled text should be.
    /// </summary>
    private static bool IsFocusable(IElement element)
    {
        if (element.HasAttribute("tabindex"))
        {
            return true;
        }

        return element.LocalName switch
        {
            "button" or "select" or "textarea" => true,
            "input" => !string.Equals(
                element.GetAttribute("type"), "hidden", StringComparison.OrdinalIgnoreCase),
            "a" or "area" => element.HasAttribute("href"),
            _ => false,
        };
    }

    /// <summary>
    /// Whether a control has a name a screen reader can announce: an associated
    /// <c>label</c> (by <c>for</c> or by wrapping), an <c>aria-label</c>, or an
    /// <c>aria-labelledby</c> that resolves.
    /// </summary>
    private static bool HasAccessibleName(IDocument document, IElement control)
    {
        // A name, not a labelling ELEMENT. The requirement says "accessible name";
        // this asked whether a `label` existed, and an empty `<label for="…"></label>`
        // gives a control no name at all while satisfying that. The `aria-label`
        // branch below always checked for content, which is what gave the
        // inconsistency away.
        if (control.Id is { Length: > 0 } id
            && document.QuerySelectorAll($"label[for=\"{id}\"]").Any(HasText))
        {
            return true;
        }

        if (control.Closest("label") is { } wrapping && HasText(wrapping))
        {
            return true;
        }

        if (control.GetAttribute("aria-label") is { Length: > 0 })
        {
            return true;
        }

        var labelledBy = control.GetAttribute("aria-labelledby");

        return labelledBy is { Length: > 0 }
            && labelledBy
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(target => document.GetElementById(target) is { } named && HasText(named));
    }

    /// <summary>
    /// Whether an element contributes text a screen reader can announce. Whitespace
    /// is not text: an element containing only a newline and indentation is as empty
    /// as one containing nothing, and Razor produces plenty of both.
    /// </summary>
    private static bool HasText(IElement element)
        => !string.IsNullOrWhiteSpace(element.TextContent);

    private async Task<IDocument> ParseAsync(DocumentCase rendered)
        => await Parser.ParseDocumentAsync(await RenderAsync(rendered));

    /// <summary>
    /// The whole document: one view, rendered.
    /// <para>
    /// Nothing is concatenated here any more. A flow view renders its own partials
    /// in its own order under its own conditions, so the document these rules judge
    /// is the one the site serves rather than one this file assembled — which is why
    /// `_DateAndLength` describing its length control with an id `_Times` owns
    /// resolves: the page includes both, and always did (design D3).
    /// </para>
    /// </summary>
    private async Task<string> RenderAsync(DocumentCase rendered)
        => await _renderer.RenderAsync(rendered.ViewPath, rendered.Model);
}
