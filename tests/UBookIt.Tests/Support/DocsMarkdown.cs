using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UBookIt.Tests.Support;

/// <summary>
/// Reads the structure of the documentation pages — their headings, the identifiers GitHub gives
/// those headings, and their links — for the guards in <c>DocumentationManifestTests</c> and
/// <c>DocumentationLinkTests</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a Markdown parser.</b> It understands the subset these pages are written
/// in: ATX headings (<c>#</c> to <c>######</c>), including inside blockquotes; fenced code blocks,
/// whose contents are neither headings nor links; and inline links and images. Anything outside
/// that subset that would change what a heading or a link is — a setext heading, a
/// reference-style link — is reported by <see cref="UnsupportedSyntax"/>, so a page that started
/// using one fails loudly instead of being silently mis-read.
/// </para>
/// <para>
/// <b>The identifier rule is GitHub's, and it is pinned against GitHub rather than written from
/// memory.</b> <c>DocsMarkdownTests</c> holds identifiers read from GitHub's own rendering of the
/// pages. A guard that decides whether links resolve is itself a thing that can be wrong, which is
/// the lesson <c>DocumentationAssertTests</c> was written for.
/// </para>
/// </remarks>
public static class DocsMarkdown
{
    /// <summary>A heading: its level, its text as written, and the 1-based line it is on.</summary>
    public sealed record Heading(int Level, string Text, int Line);

    /// <summary>A link or image target as written, and the 1-based line it is on.</summary>
    public sealed record Link(string Target, int Line);

    private static readonly Regex Fence = new(@"^\s{0,3}(```|~~~)", RegexOptions.CultureInvariant);

    // Blockquote markers come off before a line is read as a heading: GitHub gives
    // "> ### Turning this on erases your history immediately" an identifier like any other
    // heading, and a reader that missed it would mis-number every heading after it.
    private static readonly Regex Quote = new(@"^(\s{0,3}>\s?)+", RegexOptions.CultureInvariant);

    private static readonly Regex Atx = new(@"^\s{0,3}(#{1,6})\s+(.*?)(\s+#+)?\s*$", RegexOptions.CultureInvariant);

    private static readonly Regex InlineCode = new("`[^`]*`", RegexOptions.CultureInvariant);

    // `](target)` or `](target "title")`: the target is everything up to whitespace or the
    // closing parenthesis.
    private static readonly Regex InlineLink = new(@"\]\(\s*<?([^)\s>]+)>?(?:\s+""[^""]*"")?\s*\)", RegexOptions.CultureInvariant);

    private static readonly Regex Setext = new(@"^\s{0,3}(=+|-+)\s*$", RegexOptions.CultureInvariant);

    // The tag name may end the line: the check runs a line at a time, so a tag wrapped straight
    // after its name has no whitespace after it here.
    private static readonly Regex RawHtmlLink = new(@"<(a|img)(\s|>|/|$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ReferenceDefinition = new(@"^\s{0,3}\[[^\]]+\]:\s", RegexOptions.CultureInvariant);

    /// <summary>The page's lines outside fenced code blocks, with their 1-based numbers.</summary>
    private static IEnumerable<(string Text, int Line)> ProseLines(string markdown)
    {
        var inFence = false;
        var number = 0;

        foreach (var raw in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            number++;
            var line = Quote.Replace(raw, string.Empty);

            if (Fence.IsMatch(line))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence)
            {
                yield return (line, number);
            }
        }
    }

    /// <summary>Every heading on the page, in order.</summary>
    public static IReadOnlyList<Heading> Headings(string markdown)
        => ProseLines(markdown)
            .Select(line => (Match: Atx.Match(line.Text), line.Line))
            .Where(x => x.Match.Success)
            .Select(x => new Heading(x.Match.Groups[1].Length, x.Match.Groups[2].Value, x.Line))
            .ToList();

    /// <summary>
    /// The identifier GitHub gives a heading with this text, before any suffix for a repeat.
    /// </summary>
    /// <remarks>
    /// Inline code markers, emphasis and link syntax come off first, because the identifier is made
    /// from the rendered text. Then the text is lower-cased; every character that is not a letter,
    /// a digit, a space, a hyphen or an underscore is removed; and each space becomes a hyphen.
    /// Nothing collapses, so the spaces either side of a removed em dash leave a double hyphen.
    /// </remarks>
    public static string Slug(string headingText)
    {
        var text = Regex.Replace(headingText, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = text.Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal);

        var slug = new StringBuilder(text.Length);

        foreach (var c in text.ToLowerInvariant())
        {
            if (c == ' ')
            {
                slug.Append('-');
            }
            else if (c == '-' || char.IsLetter(c) || IsKept(CharUnicodeInfo.GetUnicodeCategory(c)))
            {
                slug.Append(c);
            }
        }

        return slug.ToString();

        // GitHub's filter removes [^\p{Word}\- ], and Onigmo's \p{Word} is letters, every mark,
        // DECIMAL digits and connector punctuation. So a superscript or a Roman numeral (other or
        // letter numbers) is removed. Keeping a character GitHub removes is the dangerous direction:
        // the guard would pass a fragment GitHub does not resolve.
        static bool IsKept(UnicodeCategory category) => category is
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation;
    }

    /// <summary>
    /// The identifiers of every heading on the page, in order, with GitHub's <c>-1</c>, <c>-2</c>
    /// suffixes for a repeated identifier.
    /// </summary>
    public static IReadOnlyList<string> HeadingIds(string markdown)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var ids = new List<string>();

        foreach (var heading in Headings(markdown))
        {
            var slug = Slug(heading.Text);

            if (seen.TryGetValue(slug, out var count))
            {
                seen[slug] = count + 1;
                ids.Add($"{slug}-{count}");
            }
            else
            {
                seen[slug] = 1;
                ids.Add(slug);
            }
        }

        return ids;
    }

    /// <summary>
    /// The page's prose as paragraphs: runs of non-blank lines, broken by a blank line or by a
    /// fenced block between them, each with the 1-based line it starts on.
    /// </summary>
    private static IEnumerable<(string Text, int FirstLine)> Paragraphs(string markdown)
    {
        var lines = new List<string>();
        var first = 0;
        var last = 0;

        foreach (var (text, line) in ProseLines(markdown))
        {
            var breaks = string.IsNullOrWhiteSpace(text) || (lines.Count > 0 && line != last + 1);

            if (breaks && lines.Count > 0)
            {
                yield return (string.Join('\n', lines), first);
                lines.Clear();
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                if (lines.Count == 0)
                {
                    first = line;
                }

                lines.Add(text);
            }

            last = line;
        }

        if (lines.Count > 0)
        {
            yield return (string.Join('\n', lines), first);
        }
    }

    /// <summary>Every inline link and image target on the page, outside code.</summary>
    /// <remarks>
    /// Code spans are removed a paragraph at a time, not a line at a time, because a span can wrap:
    /// read line by line, the wrapped span's backticks pair with the wrong partners and can swallow a
    /// link or expose one. A span is replaced by the line breaks it contained, so every link keeps
    /// its line number.
    /// </remarks>
    public static IReadOnlyList<Link> Links(string markdown)
        => Paragraphs(markdown)
            .SelectMany(paragraph =>
            {
                var prose = InlineCode.Replace(paragraph.Text, span => new string('\n', span.Value.Count(c => c == '\n')));

                return InlineLink.Matches(prose)
                    .Select(m => new Link(m.Groups[1].Value, paragraph.FirstLine + prose[..m.Index].Count(c => c == '\n')));
            })
            .ToList();

    /// <summary>
    /// Lines using syntax this reader does not understand and that would change what a heading
    /// or a link is: setext heading underlines, reference-style link definitions, and raw HTML
    /// links and images.
    /// </summary>
    public static IReadOnlyList<string> UnsupportedSyntax(string markdown)
    {
        var found = new List<string>();
        string? previous = null;

        foreach (var (text, line) in ProseLines(markdown))
        {
            // An underline directly beneath a line of text makes that text a heading. A thematic
            // break (`---` after a blank line) does not, and is fine.
            if (Setext.IsMatch(text) && !string.IsNullOrWhiteSpace(previous))
            {
                found.Add($"line {line}: a setext heading underline");
            }

            if (ReferenceDefinition.IsMatch(text))
            {
                found.Add($"line {line}: a reference-style link definition");
            }

            // Raw HTML links and images are invisible to Links, so a page using them would have
            // links nothing checks.
            if (RawHtmlLink.IsMatch(InlineCode.Replace(text, string.Empty)))
            {
                found.Add($"line {line}: a raw HTML link or image");
            }

            previous = text;
        }

        return found;
    }
}
