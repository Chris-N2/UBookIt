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
        // A site owner cannot weigh a grant they have not been told the consequence of.
        //
        // The sentence this asserted said the SECTION was the control over booker names and
        // addresses, and that the section grant let anyone read them. Both stopped being true
        // when withholding shipped, and the wording changed with them — which is what this
        // guard is for. What survives unchanged is the obligation: the reader must be told the
        // records contain contact details.
        //
        // "name and email address" alone now matches four places in this document, so on its
        // own it no longer pins the "Grant it deliberately" callout it was written for. The
        // second assertion is what holds that callout: the reader deciding on the section
        // grant must be told, there, that contact details are a separate question — otherwise
        // they grant the section believing it is the only control, which is what the old
        // wording said and what stopped being true.
        var docs = Docs();

        DocumentationAssert.Says(docs, "name and email address");
        DocumentationAssert.Says(docs, "is a second question, answered by the Sensitive data group");
    }

    [Fact]
    public void The_second_gate_over_contact_details_is_documented()
    {
        // Two questions, and a reader who conflates them grants the wrong thing: the section
        // decides who sees the bookings, the Sensitive data group decides who sees the people.
        var docs = Docs();

        DocumentationAssert.Says(docs, "shown only to backoffice users in Umbraco's built-in Sensitive data group");
        DocumentationAssert.Says(docs, "add Sensitive data");
    }

    [Fact]
    public void The_administrator_surprise_is_documented()
    {
        // The most likely support ticket this feature will generate, and the one thing that
        // makes it look like a defect: a brand-new administrator sees every contact detail
        // hidden, because Umbraco's installer seeds only the original super user.
        //
        // Asserted because it is the sentence most likely to be trimmed as verbose by somebody
        // who already knows how the group works — and its whole audience is the reader who
        // does not.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Being an administrator does not grant this");
        DocumentationAssert.Says(docs, "into the Sensitive data group");
    }

    [Fact]
    public void The_redaction_is_documented_as_server_side()
    {
        // A reader assessing the package's data handling needs to know the values are not sent
        // and hidden. "The interface does not display it" and "the browser never receives it"
        // are different assurances, and only one of them is worth anything.
        DocumentationAssert.Says(Docs(), "The redaction happens on the server");
    }

    [Fact]
    public void The_documentation_does_not_promise_a_bookings_screen_that_does_not_exist()
    {
        var docs = Docs();

        // Twice now this assertion has had to move with the behaviour, which is the point of
        // it. First it said bookings "do not yet have a screen"; the screen arrived. Then it
        // said the view was "read-only"; cancelling arrived. Each sentence was true when
        // written and each was made false by the very change that had to update it.
        //
        // What it says now is the pair of verbs v1 actually has, which is stable in a way
        // "read-only" was not: see and cancel.
        DocumentationAssert.Says(docs, "you can see bookings and cancel them");
        Assert.DoesNotContain("do not yet have a screen", docs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("view is read-only", docs, StringComparison.OrdinalIgnoreCase);

        // And the status default is disclosed, because the endpoint hides cancelled
        // bookings by default and an operator who cannot find one must be able to learn
        // why from the package rather than by experiment.
        // "tick", not "toggle": the status filter became four native checkboxes when the
        // hint had to be associated with the controls, and this sentence went on describing
        // a switch that is no longer on the screen — while the paragraph four lines below it
        // already said "ticking". An operator reading a page that contradicts itself hunts
        // for a control that does not exist.
        DocumentationAssert.Says(docs, "cancelled booking is one tick away rather than missing");
        Assert.DoesNotContain("one toggle away", docs, StringComparison.OrdinalIgnoreCase);

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
    public void The_screen_and_the_documentation_agree_about_what_ticking_a_status_does()
    {
        // Defect 9.2 lived in two places — the hint an operator reads on the screen, and
        // the documentation — and the assertion above guards only one of them. Reverting
        // the hint alone would ship green, with the docs correctly describing behaviour
        // that the screen once again misdescribes: the same defect with its halves
        // swapped.
        //
        // The screen's string is the one that matters more of the two. Nobody reads the
        // documentation while standing in front of the filter.
        var strings = Support.RepoFiles.Read(
            "src/UBookIt.Backoffice/Client/src/localization/en-us.ts");

        Assert.Contains("show only those instead", strings, StringComparison.Ordinal);
        Assert.DoesNotContain("Tick a status to include others", strings, StringComparison.Ordinal);
    }

    [Fact]
    public void The_two_things_erasure_does_not_reach_are_documented()
    {
        // The `booker-erasure` capability makes these normative — "What erasure does not reach
        // is documented" — with a scenario each, and a spec requirement discharged only by
        // prose is discharged by nothing: somebody tidying the page deletes a paragraph, the
        // suite stays green, and the requirement is violated with nothing to notice it.
        //
        // Both omissions arrive as incidents rather than as questions. The first arrives as a
        // data-protection failure: an operator erases the one booking they were shown and
        // believes the person is gone from the system. The second arrives as a support call
        // about a booking nobody can ring, which is what the requirement itself says.
        var docs = Docs();

        DocumentationAssert.Says(docs, "It erases one booking, not a person");
        DocumentationAssert.Says(docs, "You can erase a booking that has not happened yet");
        DocumentationAssert.Says(docs, "unable to contact somebody who is going to turn up");
    }

    [Fact]
    public void The_documentation_says_how_to_find_the_bookings_to_erase()
    {
        // `booker-erasure` → "What erasure does not reach is documented" was MODIFIED by
        // find-by-booker so that its first boundary points at the search: the documentation
        // SHALL direct an operator to search first and erase each result, and SHALL state that
        // the search finds bookings made with THAT address.
        //
        // Those SHALLs arrived in a review round and shipped with nothing observing them —
        // deleting the whole "Finding the bookings to erase" section left 1056 unit and 749
        // rendering tests green. That is the failure this file's own comments name: a spec
        // requirement discharged only by prose is discharged by nothing.
        //
        // The four claims below are the ones an operator acts on, and each fails differently
        // if it goes missing: without the first they erase the one booking they were shown and
        // believe they are done; without the second they read a single result as proof there is
        // only one; without the third they expect a partial search to work; without the fourth
        // they treat an empty result as confirmation that an erasure succeeded.
        var docs = Docs();

        DocumentationAssert.Says(docs, "search first, then erase each result");
        DocumentationAssert.Says(docs, "It finds bookings made with *that* address");
        DocumentationAssert.Says(docs, "It matches the whole address, exactly");
        DocumentationAssert.Says(docs, "an empty result does not prove an erasure worked");
    }

    [Fact]
    public void The_documentation_says_the_search_needs_the_same_group_as_reading_and_erasing()
    {
        // The gate is the point of the feature: somebody who may not see a booker's name must
        // not be able to ask questions about one. An operator told the search exists but not
        // what it requires meets a 403 and reads it as a defect.
        //
        // **The phrase must be unique to the SEARCH section**, and the first version of this
        // test was not. It asserted "same Sensitive data group", which already matched a
        // sentence about the ERASE verb forty lines earlier — so deleting the entire search
        // section left this green while its sibling correctly failed. Mutation-checking the
        // pair together hid it: one assertion carried the other.
        //
        // The lesson is narrower than "mutation-check your tests", which was already the rule
        // and was already followed. It is that a documentation assertion has to be checked
        // AGAINST THE DELETION OF THE THING IT DESCRIBES, one assertion at a time — a phrase
        // that reads as specific can be satisfied by prose elsewhere in the same file.
        var docs = Docs();

        DocumentationAssert.Says(
            docs, "it needs the **same Sensitive data group** as reading or erasing contact details");
    }

    [Fact]
    public void The_documentation_says_erasure_cannot_be_undone()
    {
        // Separate from the pair above because it is the claim most likely to be softened by
        // somebody who finds it alarming — and softening it is how a site comes to believe
        // there is a way back. There is not, and that is the feature.
        var docs = Docs();

        DocumentationAssert.Says(docs, "It cannot be undone");
        DocumentationAssert.Says(docs, "anonymising the booking, not deleting it");
    }

    [Fact]
    public void The_capabilitys_own_summary_names_every_verb_the_capability_has()
    {
        // A capability's Purpose is prose, and OpenSpec deltas carry requirements — so this
        // paragraph is edited by hand at sync time, which is the established mechanism here
        // and also the one nothing enforces. It said the honest verbs are "see and cancel"
        // and named "editing a booker" among the things that live elsewhere, while this
        // capability grew a third endpoint. Recording the needed edit in tasks.md was not
        // enough: a checklist entry nobody reads leaves the archived spec describing a
        // surface that does not exist, to a reader deriving the capability from it.
        //
        // The PURPOSE SECTION, sliced out — not the whole file.
        //
        // This read the entire spec and matched anywhere in it, which made the sibling
        // assertion on "cancel" vacuous: that word appears 56 times across the requirements.
        // Only "erase a booker's contact details" happened to be unique to the summary, so the
        // guard worked by luck of vocabulary rather than by construction — and the phrase a
        // FOURTH endpoint's author would add is not something they choose for uniqueness.
        var spec = Support.RepoFiles.Read("openspec/specs/booking-management/spec.md");
        var purposeStart = spec.IndexOf("## Purpose", StringComparison.Ordinal);
        Assert.True(purposeStart >= 0, "The capability spec has no ## Purpose section to check.");

        var nextSection = spec.IndexOf(Environment.NewLine + "## ", purposeStart + 1, StringComparison.Ordinal);

        if (nextSection < 0)
        {
            nextSection = spec.IndexOf("\n## ", purposeStart + 1, StringComparison.Ordinal);
        }
        Assert.True(nextSection > purposeStart, "The Purpose section has no following section.");

        var purpose = spec[purposeStart..nextSection];

        // The controller's ROUTES, enumerated. The previous version of this test asserted
        // three literal strings and claimed in its comment to be "tied to the CODE… add a
        // fourth endpoint and this fails" — which was simply untrue, nothing counted
        // anything. It was the same fault it had been written to prevent, one layer up.
        var routes = System.Text.RegularExpressions.Regex
            .Matches(
                Support.RepoFiles.Read("src/UBookIt.Backoffice/Controllers/BookingsController.cs"),
                @"\[Http(?:Get|Post|Put|Delete|Patch)\(""([^""]+)""\)\]")
            .Select(match => match.Groups[1].Value)
            .ToList();

        // The fixture is load-bearing: a regex that stopped matching would make the loop below
        // iterate over nothing and pass. A LOWER bound rather than an exact count, so that a
        // fourth endpoint reaches the loop and fails with the message written for it, instead
        // of tripping "expected 3, got 4" here and telling its author nothing useful.
        Assert.True(routes.Count >= 4, $"Expected at least 4 routes, found {routes.Count}.");

        // Each verb the capability offers is named in the paragraph that summarises it. Keyed
        // off the route, so a FOURTH endpoint fails here until somebody says what it is.
        var described = new Dictionary<string, string>
        {
            ["bookings/find-by-booker"] = "find a subject's bookings by their email address",
            ["bookings/{id:guid}/cancel"] = "cancel",
            ["bookings/{id:guid}/erase-booker"] = "erase a booker's contact details",
        };

        foreach (var route in routes.Where(route => route != "bookings"))
        {
            Assert.True(
                described.TryGetValue(route, out var phrase),
                $"BookingsController exposes '{route}', which this test does not know about. "
                + "Add it here AND to the capability's Purpose paragraph, which summarises what "
                + "the capability does and is read by anyone deriving its surface from the spec.");

            DocumentationAssert.Says(purpose, phrase);
        }

        Assert.DoesNotContain("about those two verbs", purpose, StringComparison.Ordinal);
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
