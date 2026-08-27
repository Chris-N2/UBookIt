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
    /// `ubookit-catalogue-choice`), and treating each radio as a field would stack them
    /// vertically — the opposite of what the layout rule for start times exists to do.
    /// </summary>
    private static bool IsInsideAChoiceGroup(IElement label)
        => label.Closest(".ubookit-times-option, .ubookit-catalogue-choice") is not null;

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
        // package. Counted per document rather than in total, so one document going
        // empty cannot be masked by another.
        Assert.NotEmpty(ViewFixtures.Documents);

        var examined = 0;

        foreach (var rendered in ViewFixtures.Documents)
        {
            var document = await ParseAsync(rendered);

            examined += document.QuerySelectorAll("label[for]")
                .Count(label => !IsInsideAChoiceGroup(label));
        }

        // The flows render seven `for=`-based labels between them across the fixture
        // states; the exact number is not the point, but zero would make the rule a
        // no-op and a handful would mean the fixtures stopped reaching the forms.
        Assert.True(
            examined >= 7,
            $"Only {examined} non-choice labels were examined across all documents, so "
            + "the field rule is close to vacuous. Either the fixtures stopped reaching "
            + "the forms or the selector stopped matching.");
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
