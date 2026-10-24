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
            else if (c is '-' or '_' || char.IsLetterOrDigit(c)
                || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                slug.Append(c);
            }
        }

        return slug.ToString();
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

    /// <summary>Every inline link and image target on the page, outside code.</summary>
    public static IReadOnlyList<Link> Links(string markdown)
        => ProseLines(markdown)
            .SelectMany(line => InlineLink
                .Matches(InlineCode.Replace(line.Text, string.Empty))
                .Select(m => new Link(m.Groups[1].Value, line.Line)))
            .ToList();

    /// <summary>
    /// Lines using syntax this reader does not understand and that would change what a heading
    /// or a link is: setext heading underlines and reference-style link definitions.
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

            previous = text;
        }

        return found;
    }
}
