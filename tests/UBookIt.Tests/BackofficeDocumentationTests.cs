using UBookIt.Backoffice;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the backoffice documentation promises, tied to what the code does.
/// <para>
/// The section alias is the one value here a reader might copy into a configuration, and
/// the access rule is the one a site owner makes a decision on. Both are asserted so the
/// page cannot drift from the package the way a token table would.
/// </para>
/// </summary>
public class BackofficeDocumentationTests
{
    private static string Docs() => RepoFiles.Read("docs/backoffice.md");

    [Fact]
    public void The_documented_section_alias_is_the_one_the_package_uses()
    {
        Assert.Contains($"`{Constants.SectionAlias}`", Docs(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_access_rule_is_stated_in_both_directions()
    {
        // A reader deciding who to grant this to needs both halves: that another section
        // does not confer it, and that this one alone is enough. Stating only the first
        // reads as "you also need Content", which is the opposite of true.
        var docs = Docs();

        AssertSentence("Access to another section does not grant uBookIt", docs);
        AssertSentence("a user granted only uBookIt can use it without being given Content", docs);
    }

    [Fact]
    public void The_personal_data_in_booking_records_is_disclosed()
    {
        // The section is the control over booker names and email addresses. A site owner
        // cannot weigh a grant they have not been told the consequence of.
        AssertSentence("name and email address of every person who has booked", Docs());
    }

    [Fact]
    public void The_documentation_does_not_promise_a_bookings_screen_that_does_not_exist()
    {
        var docs = Docs();

        // The endpoint exists; the screen does not. Saying so plainly is the difference
        // between documentation and a roadmap.
        AssertSentence("do not yet have a screen", docs);

        // And the three verbs the section genuinely lacks are named, because "management
        // section" invites the assumption that it manages everything.
        AssertSentence("It does not place bookings", docs);
        AssertSentence("It does not approve or decline", docs);
        AssertSentence("It does not amend a booking's time", docs);
    }

    /// <summary>
    /// Asserts a sentence is present however it is wrapped — a wrapped sentence defeats a
    /// single-line search and reads exactly like a dropped statement.
    /// </summary>
    /// <remarks>
    /// The separator allows markdown emphasis and blockquote markers between words, not
    /// only whitespace. Found the hard way: the personal-data warning is a blockquote, so
    /// its wrapped line begins <c>&gt; </c>, and a whitespace-only join reported a
    /// sentence that was plainly there as missing. A documentation guard that cries wolf
    /// gets weakened rather than fixed, so it needs to match the markup people actually
    /// write.
    /// </remarks>
    private static void AssertSentence(string sentence, string document)
    {
        var pattern = string.Join(
            @"[\s>*_]+",
            sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => System.Text.RegularExpressions.Regex.Escape(word.Trim('*', '_'))));

        Assert.True(
            System.Text.RegularExpressions.Regex.IsMatch(document, pattern),
            $"The documentation no longer says: \"{sentence}\"");
    }
}
