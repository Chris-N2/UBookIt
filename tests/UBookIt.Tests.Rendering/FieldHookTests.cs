using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// That every field the flows render carries the layout hook a site styles it by.
/// <para>
/// Asserted over the <b>rendered document</b> and structurally, rather than as a source
/// count. A pinned count of the class in view source catches a hook being *removed*,
/// which was the risk that occurred to the author; it cannot catch a field being
/// *added* without one, because the count simply stays where it was. QA found that gap,
/// and it matters because the spec's claim is the broader one — that each
/// label-and-control group carries the class — not that there happen to be eight.
/// </para>
/// </summary>
public class FieldHookTests
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

    /// <summary>
    /// The groups that are deliberately not fields: a radio group's own options. Those
    /// are items inside a group that has its own hooks (`ubookit-times-option`,
    /// `ubookit-catalogue-choice`, `ubookit-dates-option`), and treating each radio as a
    /// field would stack them vertically — the opposite of what the layout rule for start
    /// times exists to do.
    /// <para>
    /// <b>Enumerated rather than matched by a `-option` suffix.</b> A pattern would exempt
    /// anything somebody happened to name that way, including a genuine field; naming each
    /// group means adding one is a decision that shows up in a diff. The list grew by one
    /// for the available-dates list, which is a choice group on exactly the same terms as
    /// the start times it sits above.
    /// </para>
    /// </summary>
    private static bool IsInsideAChoiceGroup(IElement label)
        => label.Closest(".ubookit-times-option, .ubookit-catalogue-choice, .ubookit-dates-option") is not null;

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Every_labelled_control_sits_in_a_field(DocumentCase rendered)
    {
        var document = await ParseAsync(rendered);

        foreach (var label in document.QuerySelectorAll("label[for]"))
        {
            if (IsInsideAChoiceGroup(label))
            {
                continue;
            }

            Assert.True(
                label.Closest(".ubookit-field") is not null,
                $"{rendered.Page.ViewPath} ({rendered.Page.State}): the label "
                + $"\"{label.TextContent.Trim()}\" is not inside a .ubookit-field, so the "
                + "layout rule skips it and a site styling fields cannot reach it. Every "
                + "label-and-control group is a field; only a choice group's own options "
                + "are not.");
        }
    }

    [Fact]
    public async Task The_rule_examines_labels_in_every_document()
    {
        // Non-vacuity, and the specific vacuity that would matter: a selector change
        // that stopped finding labels would let the rule pass over every field in the
        // package.
        //
        // Counted PER DOCUMENT, not summed. The first version of this guard accumulated
        // into one total and asserted on that, while its comment claimed per-document
        // — so one document going empty could be masked by another, and the comment
        // said it could not. QA caught the discrepancy. A false reassurance in the
        // vacuity guard of a rule written to close an earlier finding is about the
        // worst place to put one, so the code now does what the comment promised
        // rather than the comment being softened to match the code.
        // Scoped to the documents that render fields at all — and that scoping is a
        // correction, not a convenience. The first attempt required every document to
        // examine a label, which is simply false: the catalogue, the confirmations and
        // the unavailable pages render no form, and the catalogue's only labels are
        // choice options. The test failed and was right to; the assumption was wrong.
        //
        // What remains is the failure mode actually worth catching, and it is not
        // tautological: if `IsInsideAChoiceGroup` ever over-matched — excluding the
        // booker fields, say — a document would carry `.ubookit-field` elements while
        // the rule examined none of them, and `Every_labelled_control_sits_in_a_field`
        // would pass by asking nothing.
        Assert.NotEmpty(ViewFixtures.Documents);

        var barren = new List<string>();
        var examining = 0;

        foreach (var rendered in ViewFixtures.Documents)
        {
            var document = await ParseAsync(rendered);

            if (!document.QuerySelectorAll(".ubookit-field").Any())
            {
                continue;
            }

            var examined = document.QuerySelectorAll("label[for]")
                .Count(label => !IsInsideAChoiceGroup(label));

            if (examined == 0)
            {
                barren.Add($"{rendered.Page.ViewPath} [{rendered.Page.State}]");
            }
            else
            {
                examining++;
            }
        }

        Assert.True(
            barren.Count == 0,
            "These documents render .ubookit-field elements but the rule examined no "
            + "label in them: " + string.Join(", ", barren)
            + ". The choice-group exclusion is over-matching, so the field rule passes "
            + "by asking nothing.");

        // And that the scoping did not quietly reduce the rule to nothing: the three
        // flow pages render fields in every state their fixtures cover.
        Assert.True(
            examining >= 3,
            $"Only {examining} document(s) exercised the field rule, so it is close to "
            + "vacuous. The flow pages should all reach it.");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_field_never_wraps_a_choice_group(DocumentCase rendered)
    {
        // The other direction of the same rule. `ubookit-field` is `display: flex` in a
        // column, so wrapping the start-times fieldset in one would stack thirty-five
        // options vertically — the exact layout the wrapping run replaced. Nothing else
        // would report it.
        var document = await ParseAsync(rendered);

        foreach (var group in document.QuerySelectorAll(".ubookit-times, .ubookit-catalogue-choices"))
        {
            Assert.True(
                group.Closest(".ubookit-field") is null,
                $"{rendered.Page.ViewPath} ({rendered.Page.State}): a choice group is "
                + "inside a .ubookit-field, which lays its children out in a column and "
                + "would stack every option vertically.");
        }
    }

    private async Task<IDocument> ParseAsync(DocumentCase rendered)
        => Parser.ParseDocument(await _renderer.RenderAsync(rendered.Page.ViewPath, rendered.Page.Model));
}
