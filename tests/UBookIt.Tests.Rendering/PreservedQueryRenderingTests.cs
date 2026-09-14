using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// The GET forms carry the host page's listed query parameters as hidden inputs
/// (default-frontend, "The GET forms preserve configured host-page query parameters").
/// </summary>
/// <remarks>
/// <para>
/// <b>Both forms, independently, in every test.</b> The hidden-input loop is inlined in
/// the catalogue and the date form rather than shared (design D3: a shared partial in
/// that folder joins the public theming contract), so evidence about one form is no
/// evidence about the other — the date form once preserved the subject token while the
/// catalogue preserved nothing, which is half of how this defect class existed.
/// </para>
/// <para>
/// Rendered and parsed, not inspected as source: the encoding scenario in particular is
/// a claim about what reaches the document, and reading the attribute back through a
/// real HTML parser proves the value round-trips — a source scan would pass on markup
/// that never encoded anything.
/// </para>
/// </remarks>
public class PreservedQueryRenderingTests
{
    private static readonly HtmlParser Parser = new();

    private readonly ViewRenderer _renderer = new();

    private static readonly IReadOnlyList<PreservedQueryPair> Pairs =
    [
        new("utm_source", "newsletter"),
        new("tag", "a"),
        new("tag", "b"),
        new("note", "\"><script>alert(1)</script>"),
    ];

    /// <summary>Both GET forms, each rendered with the given pairs.</summary>
    private async Task<IReadOnlyList<(string Form, IDocument Document)>> RenderBothAsync(
        IReadOnlyList<PreservedQueryPair> pairs)
    {
        var catalogue = await _renderer.RenderAsync(ViewInventory.Catalogue, new CatalogueModel
        {
            Entries = [new(BookingSubject.Service(Guid.NewGuid()), "Massage")],
            PreservedQuery = pairs,
        });

        var dateForm = await _renderer.RenderAsync(ViewInventory.DateAndLength, new BookingFormModel
        {
            PrivacyNotice = new PrivacyNoticeView(null, null, false),
            PreservedQuery = pairs,
            ResourceId = Guid.NewGuid(),
            ResourceName = "Meeting Room A",
            SelectedDate = new DateOnly(2026, 9, 15),
            MinDate = new DateOnly(2026, 9, 10),
            MaxDate = new DateOnly(2026, 12, 10),
            DurationMinutes = 60,
            DurationOptions = [30, 60],
            Times = [],
            Errors = [],
        });

        return
        [
            ("catalogue", Parser.ParseDocument(catalogue)),
            ("date form", Parser.ParseDocument(dateForm)),
        ];
    }

    private static IReadOnlyList<(string Name, string Value)> HiddenInputs(IDocument document)
        => [.. document
            .QuerySelectorAll("form input[type='hidden']")
            .Select(input => (input.GetAttribute("name") ?? "", input.GetAttribute("value") ?? ""))

            // The date form's own subject token is not preservation and not under test.
            .Where(pair => pair.Item1 != BookingKeys.SubjectQuery)];

    [Fact]
    public async Task Listed_parameters_render_as_hidden_inputs_in_both_forms()
    {
        foreach (var (form, document) in await RenderBothAsync(Pairs))
        {
            var inputs = HiddenInputs(document);

            Assert.True(
                Pairs.Select(pair => (pair.Name, pair.Value)).SequenceEqual(inputs),
                $"The {form} should carry every preserved pair, in order. Rendered: "
                + string.Join(", ", inputs.Select(pair => $"{pair.Name}={pair.Value}")));
        }
    }

    [Fact]
    public async Task An_empty_list_renders_no_preservation_input_in_either_form()
    {
        // The default install: the requirement's "an unconfigured site is unchanged".
        foreach (var (form, document) in await RenderBothAsync([]))
        {
            Assert.True(
                HiddenInputs(document).Count == 0,
                $"The {form} rendered a preservation input with nothing configured.");
        }
    }

    [Fact]
    public async Task A_multi_valued_parameter_keeps_every_value_in_order()
    {
        foreach (var (form, document) in await RenderBothAsync(Pairs))
        {
            Assert.Equal(
                ["a", "b"],
                HiddenInputs(document).Where(pair => pair.Name == "tag").Select(pair => pair.Value));
        }
    }

    [Fact]
    public async Task A_markup_hostile_value_round_trips_encoded()
    {
        foreach (var (form, document) in await RenderBothAsync(Pairs))
        {
            // Read back through the parser: equality here means the value was encoded on
            // the way out and decodes to itself — the whole encoding claim in one check.
            Assert.Equal(
                "\"><script>alert(1)</script>",
                Assert.Single(HiddenInputs(document), pair => pair.Name == "note").Value);

            // And it never became markup: no script element exists in the document.
            Assert.True(
                document.QuerySelector("script") is null,
                $"The {form} turned a preserved value into a live element.");
        }
    }
}
