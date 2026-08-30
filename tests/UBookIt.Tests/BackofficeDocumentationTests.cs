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

        DocumentationAssert.Says(docs, "Access to another section does not grant uBookIt");
        DocumentationAssert.Says(docs, "a user granted only uBookIt can use it without being given Content");
    }

    [Fact]
    public void The_personal_data_in_booking_records_is_disclosed()
    {
        // The section is the control over booker names and email addresses. A site owner
        // cannot weigh a grant they have not been told the consequence of.
        DocumentationAssert.Says(Docs(), "name and email address of every person who has booked");
    }

    [Fact]
    public void The_documentation_does_not_promise_a_bookings_screen_that_does_not_exist()
    {
        var docs = Docs();

        // The screen now exists, so the sentence that said it did not is gone — that
        // statement was true when written and this change is what made it false. What
        // replaces it is the narrower claim that still holds: the view is read-only.
        DocumentationAssert.Says(docs, "The Bookings view is read-only");
        Assert.DoesNotContain("do not yet have a screen", docs, StringComparison.OrdinalIgnoreCase);

        // And the status default is disclosed, because the endpoint hides cancelled
        // bookings by default and an operator who cannot find one must be able to learn
        // why from the package rather than by experiment.
        DocumentationAssert.Says(docs, "cancelled booking is one toggle away rather than missing");

        // And that ticking REPLACES rather than adds. The screen's own hint said
        // "include others" until operating it showed that ticking Cancelled makes the
        // confirmed bookings disappear — correct behaviour, wrongly described. An
        // operator who reads "include" and watches today's bookings vanish will conclude
        // the filter is broken.
        DocumentationAssert.Says(docs, "Ticking statuses shows only those");

        // And the three verbs the section genuinely lacks are named, because "management
        // section" invites the assumption that it manages everything.
        DocumentationAssert.Says(docs, "It does not place bookings");
        DocumentationAssert.Says(docs, "It does not approve or decline");
        DocumentationAssert.Says(docs, "It does not amend a booking's time");
    }

    [Fact]
    public void The_two_surprising_things_about_the_recorded_service_are_stated()
    {
        // Both of these look like defects to someone who has not been told, and both are
        // decisions. Documenting them is what stops the first report of "the name is
        // wrong" turning into a change that retitles historical bookings.
        var docs = Docs();

        DocumentationAssert.Says(docs, "the one recorded when the booking was placed");
        DocumentationAssert.Says(docs, "A booking with no service was booked directly");
        DocumentationAssert.Says(docs, "not a booking whose service failed to be recorded");
    }
}
