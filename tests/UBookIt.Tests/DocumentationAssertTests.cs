using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The documentation guard, guarded.
/// <para>
/// Twelve assertions across two suites rest on this one helper, and it has now produced a
/// defect in each direction: a copy of it went unfixed beside its corrected twin, and the
/// correction then introduced word-boundary anchors that reported a sentence missing while
/// it was present verbatim. A helper that decides whether other tests pass is itself a thing
/// that can be wrong, and nothing was checking it.
/// </para>
/// <para>
/// Both directions matter equally. Too strict and it cries wolf, which is how a guard gets
/// weakened rather than fixed; too loose and it certifies documentation that no longer says
/// what it claims — which is the failure mode with no symptom.
/// </para>
/// </summary>
public class DocumentationAssertTests
{
    private static bool Finds(string document, string sentence)
    {
        try
        {
            DocumentationAssert.Says(document, sentence);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [Theory]
    // Plain.
    [InlineData("The data is personal.", "The data is personal")]
    // Ending on punctuation — the case `\b` anchoring got wrong, because a full stop is the
    // natural way to write a sentence and `\b` cannot follow one.
    [InlineData("The data is personal.", "The data is personal.")]
    [InlineData("A section (like any other) is granted.", "(like any other) is granted.")]
    // Wrapped mid-sentence, which is why the helper exists at all.
    [InlineData("uBookIt makes no\nclaim about a theme.", "makes no claim about a theme")]
    // Wrapped inside a blockquote, and emphasised.
    [InlineData("> the section is\n> **required** here", "the section is required here")]
    [InlineData("it is *never* implied", "it is never implied")]
    public void A_sentence_the_document_says_is_found(string document, string sentence)
        => Assert.True(Finds(document, sentence), $"Reported missing: \"{sentence}\"");

    [Theory]
    // Absent outright.
    [InlineData("The data is personal.", "The data is not personal")]
    // Present as words, but across a paragraph break — two statements, not one.
    [InlineData("uBookIt makes no\n\nclaim about a theme.", "makes no claim about a theme")]
    // Present as words, but on adjacent list items. Same fault as the paragraph break, and
    // the one the first attempt at tightening this missed: `*` was in the separator, so a
    // tight bulleted list read as a continuous sentence.
    [InlineData("* makes no\n* claim about a theme", "makes no claim about a theme")]
    [InlineData("- makes no\n- claim about a theme", "makes no claim about a theme")]
    [InlineData("1. makes no\n2. claim about a theme", "makes no claim about a theme")]
    // Present only as a fragment inside a longer word.
    [InlineData("The subsection is granted", "section is granted")]
    public void A_sentence_the_document_does_not_say_is_reported_missing(
        string document, string sentence)
        => Assert.False(Finds(document, sentence), $"Wrongly found: \"{sentence}\"");
}
