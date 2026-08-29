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
    {
        // Horizontal whitespace, markdown decoration, or a single line break — but never a
        // blank line, and never a line break into a new list item. Both are places where one
        // statement stops and another begins, so crossing either satisfies a sentence
        // assertion out of words the document no longer says in that order.
        const string Separator =
            @"(?:[^\S\r\n]|[>*_]|\r?\n(?!\s*(?:\r?\n|[-*+][^\S\r\n]|\d+\.[^\S\r\n])))+";

        // Anchored so a word cannot match inside a longer one — but with lookarounds rather
        // than `\b`, which asserts a word boundary and therefore FAILS on a sentence whose
        // first or last character is not a word character. "The data is personal." is the
        // obvious thing to write and `\b` reported it missing while it was present verbatim.
        var pattern = @"(?<!\w)" + string.Join(
            Separator,
            sentence
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => Regex.Escape(word.Trim('*', '_')))) + @"(?!\w)";

        Assert.True(
            Regex.IsMatch(document, pattern),
            $"The documentation no longer says: \"{sentence}\"");
    }
}
