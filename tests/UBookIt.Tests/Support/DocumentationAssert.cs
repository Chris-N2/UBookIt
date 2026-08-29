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
/// </remarks>
public static class DocumentationAssert
{
    /// <summary>
    /// Asserts <paramref name="document"/> contains <paramref name="sentence"/>, however it
    /// is wrapped, emphasised or quoted.
    /// </summary>
    public static void Says(string document, string sentence)
    {
        var pattern = string.Join(
            @"[\s>*_]+",
            sentence
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => Regex.Escape(word.Trim('*', '_'))));

        Assert.True(
            Regex.IsMatch(document, pattern),
            $"The documentation no longer says: \"{sentence}\"");
    }
}
