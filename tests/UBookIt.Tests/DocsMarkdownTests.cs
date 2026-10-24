using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The documentation structure reader, guarded. <see cref="DocsMarkdown"/> decides whether every
/// link between the published pages resolves, so a fault in it either cries wolf or certifies
/// links that GitHub, and any site rendering the pages, will not resolve.
/// </summary>
/// <remarks>
/// <para>
/// <b>The identifiers below were read from GitHub, not written from the rule.</b> Source:
/// <c>gh api -H "Accept: application/vnd.github.html+json"
/// repos/Chris-N2/UBookIt/contents/docs/&lt;page&gt;.md?ref=17.6.0</c> for
/// <c>notifications.md</c>, <c>booking-page.md</c>, <c>backoffice.md</c> and
/// <c>configuration.md</c>, read on 2026-10-24. That endpoint renders the file as github.com
/// does, with an <c>id="user-content-…"</c> per heading. (<c>POST /markdown</c> no longer emits
/// heading identifiers at all, so it cannot be used for this.) Every heading on those four pages
/// with a character other than a letter, digit or space is here.
/// </para>
/// <para>
/// <b>Not observed, and so not pinned:</b> the <c>-1</c> suffix for a repeated heading. None of
/// the pages repeats one. It is GitHub's documented behaviour, and the next page that repeats a
/// heading should be checked against GitHub by hand.
/// </para>
/// </remarks>
public class DocsMarkdownTests
{
    [Theory]
    [InlineData("What uBookIt sends, and what it does not", "what-ubookit-sends-and-what-it-does-not")]
    [InlineData("`CancellationUrl` — and what happens if your view leaves it out", "cancellationurl--and-what-happens-if-your-view-leaves-it-out")]
    [InlineData("`BookerInstructions` — the service's instructions for a confirmed booker", "bookerinstructions--the-services-instructions-for-a-confirmed-booker")]
    [InlineData("HTML means no plain-text alternative", "html-means-no-plain-text-alternative")]
    [InlineData("What becomes yours, and what does not", "what-becomes-yours-and-what-does-not")]
    [InlineData("Replacing the whole message, delivery included", "replacing-the-whole-message-delivery-included")]
    [InlineData("If you store what a notification hands you, erasure will not reach it", "if-you-store-what-a-notification-hands-you-erasure-will-not-reach-it")]
    [InlineData("Do not install uBookIt with `RunSchemaAndContentMigrations` turned off", "do-not-install-ubookit-with-runschemaandcontentmigrations-turned-off")]
    [InlineData("Accessibility: what we hold, and what becomes yours", "accessibility-what-we-hold-and-what-becomes-yours")]
    [InlineData("Erasing a booker's details", "erasing-a-bookers-details")]
    [InlineData("Self-service cancellation", "self-service-cancellation")]
    [InlineData("The region is yours, not the package's", "the-region-is-yours-not-the-packages")]
    [InlineData("The uBookIt backoffice section", "the-ubookit-backoffice-section")]
    public void A_heading_gets_the_identifier_GitHub_gives_it(string heading, string github)
        => Assert.Equal(github, DocsMarkdown.Slug(heading));

    [Fact]
    public void A_heading_inside_a_blockquote_is_a_heading()
    {
        // Observed: GitHub gives backoffice.md's "> ### Turning this on erases your history
        // immediately" the identifier below. Missing it put every later heading on that page out
        // of step with GitHub's 27 identifiers by one.
        const string page = "## Erasing old bookings automatically\n\n> ### Turning this on erases your history immediately\n>\n> Text.\n";

        Assert.Equal(
            ["erasing-old-bookings-automatically", "turning-this-on-erases-your-history-immediately"],
            DocsMarkdown.HeadingIds(page));
    }

    [Fact]
    public void Nothing_in_a_fenced_block_is_a_heading_or_a_link()
    {
        const string page = "# Title\n\n```sh\n# a shell comment, not a heading\necho \"[x](not-a-link.md)\"\n```\n\n~~~\n## nor this\n~~~\n";

        Assert.Equal(["title"], DocsMarkdown.HeadingIds(page));
        Assert.Empty(DocsMarkdown.Links(page));
    }

    [Fact]
    public void Links_and_images_are_read_with_their_lines_and_inline_code_is_not()
    {
        const string page = "See [Email templates](email-templates.md#template-sets) and\n![A picture](images/x.png \"title\"), but not `[code](nope.md)`.\n";

        var links = DocsMarkdown.Links(page);

        Assert.Equal(
            [new DocsMarkdown.Link("email-templates.md#template-sets", 1), new DocsMarkdown.Link("images/x.png", 2)],
            links);
    }

    [Fact]
    public void A_code_span_that_wraps_neither_swallows_a_link_nor_exposes_one()
    {
        // Read line by line, each line holds one unpaired backtick, so neither line's span is
        // removed: line 2's link-shaped text inside the wrapped span is read as a link to
        // not-a-link.md. Read as a paragraph, the two backticks pair and the span is removed.
        const string page = "A span `that wraps\nacross [lines](not-a-link.md)` and then [a link](real.md).\n\nNext paragraph [here](next.md).\n";

        Assert.Equal(
            [new DocsMarkdown.Link("real.md", 2), new DocsMarkdown.Link("next.md", 4)],
            DocsMarkdown.Links(page));
    }

    [Fact]
    public void A_raw_html_link_or_image_is_reported_and_one_in_code_is_not()
    {
        Assert.Single(DocsMarkdown.UnsupportedSyntax("See <a href=\"mvp.md\">v1</a>.\n"));
        Assert.Single(DocsMarkdown.UnsupportedSyntax("<img src=\"images/x.png\" alt=\"x\">\n"));
        Assert.Single(DocsMarkdown.UnsupportedSyntax("See the old plan <a\nhref=\"mvp.md\">here</a>.\n"));
        Assert.Empty(DocsMarkdown.UnsupportedSyntax("An <abbr title=\"x\">abbr</abbr> and an <aside> are not links.\n"));
        Assert.Empty(DocsMarkdown.UnsupportedSyntax("Write `<a href=\"…\">` in your view.\n"));
        Assert.Empty(DocsMarkdown.UnsupportedSyntax("```html\n<a href=\"x\">x</a>\n```\n"));
    }

    // The ² and Ⅳ rows were OBSERVED on GitHub on 2026-10-24, through the same contents endpoint
    // (`gh api -H "Accept: application/vnd.github.html+json" repos/<repo>/readme`), on other
    // repositories' READMEs:
    // - ² is removed: "² Player Roles support…" →
    //   "-player-roles-support-is-only-available-since-version-20" (Awakened-Redstone/AutoWhitelist);
    // - Ⅳ is kept and lower-cased: "Ⅳ. usage:" → "ⅳ-usage" (ying32/govcl).
    // The other two rows are NOT observed; no current heading uses those characters.
    [Theory]
    [InlineData("Step 2 — the 3rd ²", "step-2--the-3rd-")]
    [InlineData("Part Ⅳ", "part-ⅳ")]
    [InlineData("snake_case and ‿", "snake_case-and-‿")]
    [InlineData("Café crème", "café-crème")]
    public void Only_word_characters_hyphens_and_spaces_survive(string heading, string expected)
        => Assert.Equal(expected, DocsMarkdown.Slug(heading));

    [Fact]
    public void A_repeated_heading_is_suffixed()
        => Assert.Equal(["notes", "notes-1", "notes-2"], DocsMarkdown.HeadingIds("## Notes\n## Notes\n## Notes\n"));

    [Fact]
    public void Setext_headings_and_reference_links_are_reported_and_a_thematic_break_is_not()
    {
        Assert.Single(DocsMarkdown.UnsupportedSyntax("Title\n=====\n"));
        Assert.Single(DocsMarkdown.UnsupportedSyntax("Title\n-----\n"));
        Assert.Single(DocsMarkdown.UnsupportedSyntax("[ref]: https://example.com\n"));
        Assert.Empty(DocsMarkdown.UnsupportedSyntax("Paragraph.\n\n---\n\nMore.\n"));
        Assert.Empty(DocsMarkdown.UnsupportedSyntax("| a | b |\n|---|---|\n| 1 | 2 |\n"));
    }
}
