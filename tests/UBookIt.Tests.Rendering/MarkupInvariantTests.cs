using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;

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
    public void Every_in_scope_view_appears_in_some_document()
    {
        // Non-vacuity for rule 1, which is the one rule that lacked its own guard.
        // Its five clauses iterate documents; if a fixture change emptied that set,
        // or dropped a view from it, all five would pass over nothing. Rules 2 and 3
        // would still fail loudly, so the suite would not be silent — but rule 1
        // alone would be, and that asymmetry is the thing worth removing.
        var covered = ViewFixtures.Documents
            .SelectMany(document => document.Parts.Select(part => part.ViewPath))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(ViewFixtures.Documents);

        foreach (var view in ViewInventory.InScope)
        {
            Assert.True(
                covered.Contains(view),
                $"{view} is in scope but appears in no rendered document, so rule 1 never "
                + "asks anything of it.");
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
                    Assert.True(
                        document.GetElementById(id) is not null,
                        $"{rendered}: {attribute}=\"{id}\" resolves to nothing.");
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

            Assert.True(
                document.GetElementById(target) is not null,
                $"{rendered}: <a href=\"#{target}\"> points at no element in the document. "
                + "An error summary that links nowhere is worse than one that does not link.");
        }
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
    /// Whether a control has a name a screen reader can announce: an associated
    /// <c>label</c> (by <c>for</c> or by wrapping), an <c>aria-label</c>, or an
    /// <c>aria-labelledby</c> that resolves.
    /// </summary>
    private static bool HasAccessibleName(IDocument document, IElement control)
    {
        if (control.Id is { Length: > 0 } id
            && document.QuerySelector($"label[for=\"{id}\"]") is not null)
        {
            return true;
        }

        if (control.Closest("label") is not null)
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
                .All(target => document.GetElementById(target) is not null);
    }

    private async Task<IDocument> ParseAsync(DocumentCase rendered)
        => await Parser.ParseDocumentAsync(await RenderAsync(rendered));

    /// <summary>
    /// The whole document: every part rendered in the order a flow renders them,
    /// concatenated.
    /// <para>
    /// For a page that is one view. For the shared partials it is the four that
    /// only form a page together — `_DateAndLength` describes its length control
    /// with an id `_Times` owns, which dangles apart and resolves together, and the
    /// second is what the site serves (design D7).
    /// </para>
    /// </summary>
    private async Task<string> RenderAsync(DocumentCase rendered)
    {
        var parts = new List<string>(rendered.Parts.Count);

        foreach (var part in rendered.Parts)
        {
            parts.Add(await _renderer.RenderAsync(part.ViewPath, part.Model));
        }

        return string.Join(Environment.NewLine, parts);
    }
}
