using System.Text.RegularExpressions;

namespace UBookIt.Tests.Support;

/// <summary>
/// Asserting that documentation still says something, in the face of how markdown is
/// actually written.
/// </summary>
/// <remarks>
/// <para>
/// One implementation, deliberately. There were two: the second was written by copying the
/// first, the copy was fixed after a false failure, and the original was left standing with
/// the defect. A known-broken helper sitting beside its corrected twin is worse than either
/// alone, because the next person to hit it has no way to tell which one is right.
/// </para>
/// <para>
/// The separator allows whitespace, blockquote markers and emphasis between words. A
/// sentence that wraps inside a blockquote continues with <c>&gt; </c>, and a
/// whitespace-only match reports it as missing — a documentation guard that cries wolf gets
/// weakened rather than fixed, so it has to match the markup people write.
/// </para>
/// <para>
/// It does <b>not</b> cross a blank line or a line break into a new list item, and each end
/// is anchored so a word cannot match inside a longer one. Without those, the guard is
/// satisfiable by the same words scattered across separate paragraphs or adjacent bullets —
/// a sentence the document no longer says, passing a test that claims it does.
/// </para>
/// </remarks>
public static class DocumentationAssert
{
    /// <summary>
    /// Asserts <paramref name="document"/> contains <paramref name="sentence"/>, however it
    /// is wrapped, emphasised or quoted.
    /// </summary>
    public static void Says(string document, string sentence)
        => Assert.True(
            Regex.IsMatch(Normalise(document), PatternFor(sentence)),
            $"The documentation no longer says: \"{sentence}\"");

    /// <summary>
    /// Asserts <paramref name="document"/> does <b>not</b> contain <paramref name="sentence"/>,
    /// however it is wrapped, emphasised or quoted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Built on the same pattern as <see cref="Says"/>, because the alternative was measured
    /// and it fails silently.</b> A guard for a claim that must NOT be made was written with a raw
    /// <c>Assert.DoesNotContain</c> beside a wrap-safe <c>Says</c> in the same test — so the
    /// positive half survived rewrapping and the negative half did not. Re-adding the forbidden
    /// sentence **wrapped at the column this repository writes prose at** left the whole suite
    /// green.
    /// </para>
    /// <para>
    /// That asymmetry is the tell, and it is why this lives here rather than as a
    /// <c>DoesNotContain</c> at each call site: one implementation, so the two directions cannot
    /// come to disagree about what counts as a match.
    /// </para>
    /// <para>
    /// <b>Two properties are inherited from <see cref="Says"/> and they have opposite signs — know
    /// which is which before relying on this.</b> The separator's generosity (whitespace, wrapping,
    /// emphasis, blockquote markers) is <i>safe</i> here: it makes the guard stricter, catching a
    /// forbidden sentence however it is dressed. The refusal to cross a blank line or a list
    /// boundary is <i>not</i>: for <see cref="Says"/> it prevents a false pass, but here it is a
    /// blind spot, and a forbidden sentence split across two paragraphs goes undetected. Nobody
    /// writes a sentence that way, which is why this is documented rather than fixed — but it is
    /// the half of the inheritance that does not protect you.
    /// </para>
    /// </remarks>
    /// <param name="document">The text to search.</param>
    /// <param name="sentence">The sentence that must not appear.</param>
    /// <param name="source">
    /// Optional name of where <paramref name="document"/> came from, included in the failure.
    /// <b>Supply it whenever the same needle is checked against more than one document</b> — the
    /// sweep over every shipped markdown file reports a sentence that must not be there, and
    /// without this the reader is told what is wrong but not which of twenty files to open.
    /// </param>
    public static void DoesNotSay(string document, string sentence, string? source = null)
        => Assert.False(
            Regex.IsMatch(Normalise(document), PatternFor(sentence)),
            source is null
                ? $"The documentation still says, or says again: \"{sentence}\""
                : $"{source} still says, or says again: \"{sentence}\"");

    /// <summary>
    /// Asserts <paramref name="document"/> contains <paramref name="sentence"/> <b>exactly
    /// once</b> — for a pin whose job is to hold one specific sentence in place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pinned string quoted a second time stops pinning anything.</b> <see cref="Says"/> is
    /// satisfied by any occurrence, so the moment the phrase appears somewhere else in the same
    /// document — most naturally in prose <i>about</i> the pin — the sentence it was protecting is
    /// free to be rewritten, deleted, or falsified with the guard still green. The pin has not
    /// weakened; it has moved.
    /// </para>
    /// <para>
    /// <b>Measured, not theorised.</b> <c>docs/publishing.md</c> pins the phrase
    /// <c>reached nuget.org on</c> to hold its Status line. A later edit added a paragraph
    /// explaining the pin and quoted a falsified version of that very line as the example — which
    /// reproduced the substring. QA then rewrote the Status line into a phrasing the pin does not
    /// recognise, with a date years in the future, and every guard stayed green. **The document
    /// written to explain the guard was what defeated it.**
    /// </para>
    /// <para>
    /// So the rule is: a pinned string must not be quoted elsewhere in the document it pins. This
    /// enforces that rather than trusting an author to notice, which is the difference between a
    /// convention and a guard.
    /// </para>
    /// </remarks>
    public static void SaysOnce(string document, string sentence)
    {
        var occurrences = Regex.Matches(Normalise(document), PatternFor(sentence)).Count;

        Assert.True(
            occurrences == 1,
            occurrences == 0
                ? $"The documentation no longer says: \"{sentence}\""
                : $"The documentation says \"{sentence}\" {occurrences} times. A pinned string "
                  + "quoted a second time — usually in prose ABOUT the pin — stops holding the "
                  + "sentence it was protecting: any one occurrence satisfies the pin, so the "
                  + "original can then be rewritten or falsified with this guard green. "
                  + "Paraphrase the other occurrence.");
    }

    /// <summary>
    /// Strips the <c>///</c> prefix from each line, so a sentence that wraps inside an XML
    /// documentation comment reads as one sentence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Because these guards are asked about source files as well as markdown</b>, and a claim
    /// in a public type's <c>&lt;remarks&gt;</c> is the one a site author meets first, in
    /// IntelliSense. The separator below tolerates whitespace and markdown decoration between
    /// words but not a comment prefix, so an over-claim re-added across two <c>///</c> lines went
    /// undetected with the whole suite green.
    /// </para>
    /// <para>
    /// Done to the DOCUMENT rather than by widening the pattern, deliberately: adding <c>/</c> to
    /// the separator would let a sentence match across a URL or a path. Stripping the prefix is
    /// also what makes a bare <c>///</c> line — an XML paragraph break — read as the blank line it
    /// is, so the no-crossing-a-blank-line rule keeps working there too. Markdown has no such
    /// prefix, so nothing about existing callers changes.
    /// </para>
    /// <para>
    /// <b>Two claims here are currently unfalsifiable, and saying so is cheaper than pretending
    /// otherwise.</b> No document these helpers are given contains a <c>///</c>-prefixed line
    /// except the five C# sources, and none of those contains a <b>bare</b> <c>///</c> line — so
    /// the paragraph-break argument above is reasoning, not something the suite can check. For
    /// markdown, specs and TypeScript this is provably an identity transform for the same reason:
    /// the prefix does not occur in them at all.
    /// </para>
    /// </remarks>
    private static string Normalise(string document)
        => Regex.Replace(document, @"^[^\S

]*///[^\S

]?", string.Empty, RegexOptions.Multiline);

    private static string PatternFor(string sentence)
    {
        // Horizontal whitespace, markdown decoration, or a single line break — but never a
        // blank line, and never a line break into a new list item. Both are places where one
        // statement stops and another begins, so crossing either satisfies a sentence
        // assertion out of words the document no longer says in that order.
        // The backtick joined the decoration class when a sentence asserting a claim about
        // `InternalRecipients` failed against a document that made the claim verbatim — the
        // summary above always promised "quoted", and code quoting is how this repository's
        // prose quotes an identifier. For DoesNotSay the addition is strictly safer: a
        // forbidden sentence cannot hide by backticking one of its words.
        const string Separator =
            @"(?:[^\S\r\n]|[>*_`]|\r?\n(?!\s*(?:\r?\n|[-*+][^\S\r\n]|\d+\.[^\S\r\n])))+";

        // Anchored so a word cannot match inside a longer one — but with lookarounds rather
        // than `\b`, which asserts a word boundary and therefore FAILS on a sentence whose
        // first or last character is not a word character. "The data is personal." is the
        // obvious thing to write and `\b` reported it missing while it was present verbatim.
        var pattern = @"(?<!\w)" + string.Join(
            Separator,
            sentence
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => Regex.Escape(word.Trim('*', '_', '`')))) + @"(?!\w)";

        return pattern;
    }
}
