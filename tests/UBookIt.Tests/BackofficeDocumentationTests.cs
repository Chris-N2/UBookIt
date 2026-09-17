using System.Text.RegularExpressions;
using UBookIt.Backoffice;
using UBookIt.Persistence.Composing;
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
        // "name and email address" alone matches this document in more than one place — two, at
        // the last count — so on its own it no longer pins the "Grant it deliberately" callout
        // it was written for. The number is deliberately not restated precisely: a comment
        // carrying a count nobody re-measures goes stale on the next edit, and it is the
        // "more than one" that the argument below rests on. The
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

        // Three times now this assertion has had to move with the behaviour, which is the
        // point of it. First it said bookings "do not yet have a screen"; the screen arrived.
        // Then it said the view was "read-only"; cancelling arrived. Then it said "see and
        // cancel" were the two things the view does; approval arrived, and with it confirm
        // and decline on a requested row. Each sentence was true when written and each was
        // made false by the very change that had to update it.
        // THROUGH THE SAME MATCHER AS THE ASSERTION ABOVE. These were raw substring checks
        // sitting directly beneath a wrap-safe `Says` — one test, two matchers, which is precisely
        // the asymmetry that let an over-claim walk back into `docs/notifications.md` unnoticed.
        // Re-adding "Bookings do not yet\nhave a screen of their own." here left the whole suite
        // green. Since this guard has already had to move with the behaviour twice, that is a live
        // regression guard that could not see the regression.
        DocumentationAssert.Says(
            docs,
            "you can see bookings, cancel them, and — where a booking awaits approval — confirm or decline it");
        DocumentationAssert.DoesNotSay(docs, "do not yet have a screen");
        DocumentationAssert.DoesNotSay(docs, "view is read-only");
        DocumentationAssert.DoesNotSay(docs, "see bookings and cancel them");

        // And the status default is disclosed, because the endpoint hides cancelled
        // bookings by default and an operator who cannot find one must be able to learn
        // why from the package rather than by experiment.
        // "tick", not "toggle": the status filter became four native checkboxes when the
        // hint had to be associated with the controls, and this sentence went on describing
        // a switch that is no longer on the screen — while the paragraph four lines below it
        // already said "ticking". An operator reading a page that contradicts itself hunts
        // for a control that does not exist.
        DocumentationAssert.Says(
            docs, "cancelled or declined booking is one tick away rather than missing");
        DocumentationAssert.DoesNotSay(docs, "one toggle away");

        // And that ticking REPLACES rather than adds. The screen's own hint said
        // "include others" until operating it showed that ticking Cancelled makes the
        // confirmed bookings disappear — correct behaviour, wrongly described. An
        // operator who reads "include" and watches today's bookings vanish will conclude
        // the filter is broken.
        DocumentationAssert.Says(docs, "Ticking statuses shows only those");

        // And the verbs the section genuinely lacks are named, because "management
        // section" invites the assumption that it manages everything. Approve/decline left
        // this list with the approval-decline change, and amend left it with move-booking —
        // the section does both now — and the falsified-claims sweep in
        // NotificationDocumentationTests holds the retired sentences out. What a MOVE does
        // not do is what the list now names in their place, because "you can move a booking"
        // invites exactly the assumptions those two sentences refuse.
        DocumentationAssert.Says(docs, "It does not place bookings");
        DocumentationAssert.Says(docs, "It does not change which resources a booking claims");
        DocumentationAssert.Says(docs, "It does not keep a history of where a booking has been");
        DocumentationAssert.DoesNotSay(docs, "It does not amend a booking's time");
    }

    [Fact]
    public void The_move_is_documented_with_its_terms_and_its_limits()
    {
        // Each of these is a fact an operator would otherwise discover by being refused, or —
        // worse — by NOT being refused: the notice and horizon exceptions are the two rules
        // that bind a visitor and not them, and an operator who does not know that will
        // apologise to a customer for a move they could have made.
        var docs = Docs();

        DocumentationAssert.Says(docs, "move a booking to a new date, time or length");
        DocumentationAssert.Says(docs, "A move changes when, and nothing else");
        DocumentationAssert.Says(docs, "minimum notice does not bind you");
        DocumentationAssert.Says(docs, "a booking cannot be moved into the past");
        DocumentationAssert.Says(docs, "There is no picker showing where a booking could go");
        DocumentationAssert.Says(docs, "A service booking moves with the resources it was given");
        DocumentationAssert.Says(docs, "Nothing records where a booking used to be");

        // The truthful conditional, on cancel's and decline's terms: never an unconditional
        // claim in either direction.
        DocumentationAssert.Says(docs, "Moving tells the person who booked only if booking emails are configured");
        DocumentationAssert.Says(docs, "Your own recipients are not told");
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

        // LEFT AS RAW SUBSTRING CHECKS, deliberately, unlike the prose guards above.
        //
        // This document is TypeScript, and the pair is symmetric — both directions are raw, so
        // there is no matcher asymmetry to correct. More to the point, a long string here wraps by
        // concatenation or a template literal, and `DocumentationAssert`'s separator bridges
        // neither; converting would buy the appearance of wrap-safety without the substance, which
        // is worse than a raw check that is honest about what it does.
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

    /// <summary>
    /// The third boundary of erasure, stated where an operator performing one will meet it.
    /// </summary>
    /// <remarks>
    /// <b>Here rather than only in the notifications guide, because that is where the requirement
    /// puts it.</b> Somebody honouring a right-to-be-forgotten request is reading about erasure,
    /// not about email — and being told there that erasure is complete, when a message is already
    /// in a mailbox and a mail server may have quoted the address into the site's log, is an
    /// answer given in good faith that turns out to be untrue to a data subject.
    /// </remarks>
    [Fact]
    public void The_documentation_says_what_erasure_cannot_reach_on_a_sending_site()
    {
        var docs = Docs();

        DocumentationAssert.Says(
            docs, "erasure does not reach what has already left");
        DocumentationAssert.Says(docs, "is in somebody's mailbox");
        Assert.Contains("notifications.md", docs, StringComparison.Ordinal);
    }

    /// <summary>
    /// `booker-erasure`'s Purpose paragraph must not claim more than the requirements beneath it.
    /// </summary>
    /// <remarks>
    /// <b>A CONSISTENCY guard, not a "must say three" guard, and the distinction is the whole
    /// design.</b> A Purpose is prose and OpenSpec deltas carry requirements, so it is edited by
    /// hand at sync time — the sibling test below exists because recording that edit in `tasks.md`
    /// was demonstrably not enough once already.
    /// <para>
    /// Asserting the post-sync wording directly would fail from the moment the delta is written
    /// until the moment it is synced, which is most of a change's life and exactly when the suite
    /// has to be green for QA. So this compares the summary to the requirements **as the file
    /// currently stands**: self-consistent before the sync, self-consistent after it, and red only
    /// in the state that actually matters — requirements updated, summary left behind.
    /// </para>
    /// <para>
    /// `booking-emails` narrows both of this paragraph's claims: erasing the one durable home
    /// erases the data, and there are <b>two</b> boundaries the documentation must state.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_erasure_capabilitys_summary_does_not_outrun_its_requirements()
    {
        var spec = Support.RepoFiles.Read("openspec/specs/booker-erasure/spec.md");

        var purposeStart = spec.IndexOf("## Purpose", StringComparison.Ordinal);
        Assert.True(purposeStart >= 0, "The capability spec has no ## Purpose section to check.");

        var nextSection = spec.IndexOf("\n## ", purposeStart + 1, StringComparison.Ordinal);
        Assert.True(nextSection > purposeStart, "The Purpose section has no following section.");

        // WHITESPACE COLLAPSED BEFORE MATCHING, and this is not tidiness. Markdown wraps at column
        // 100 in this repository, so "among the stores it owns" is written across a line break in
        // the very delta that introduces it — and a Contains() over the raw text would therefore be
        // false after the sync, silently skipping the durable-home check in exactly the state it
        // exists to catch. Found because a mutation that ought to have failed did not: the mutant
        // and the guard disagreed about a newline, not about the claim.
        var purpose = Collapse(spec[purposeStart..nextSection]);
        var requirements = Collapse(spec[nextSection..]);

        // The boundaries as the REQUIREMENTS enumerate them, counted rather than assumed.
        // Counted from the RAW text, because this one is anchored to line starts.
        var boundaries = Regex.Matches(spec[nextSection..], @"^- \*\*It ", RegexOptions.Multiline).Count;
        Assert.True(boundaries >= 2, $"Expected the erasure boundaries to be enumerated; found {boundaries}.");

        // If the summary commits to a COUNT, it has to be that count. A summary that says "the
        // boundaries" commits to nothing and is always safe — which is the wording to prefer.
        var claimed = Regex.Match(purpose, @"the (two|three|four) boundaries", RegexOptions.IgnoreCase);

        if (claimed.Success)
        {
            var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["two"] = 2,
                ["three"] = 3,
                ["four"] = 4,
            };

            Assert.True(
                words[claimed.Groups[1].Value] == boundaries,
                $"The Purpose claims '{claimed.Value}' while the requirements enumerate {boundaries}. "
                + "A capability's summary is what a reader derives the capability from after archiving.");
        }

        // And the durable-home claim: if the requirement has been narrowed to the package's own
        // stores, the summary may not go on asserting the absolute.
        //
        // THE TRIGGER IS STRUCTURAL, NOT A VERBATIM PHRASE, and that is the second attempt. The
        // first gated on `Contains("among the stores it owns")` — a literal lifted from the delta,
        // guarding a HAND EDIT AT SYNC, which is the operation most likely to reword it. Rewording
        // the narrowing to "among the stores the package owns" turned the guard off silently while
        // the Purpose went on promising the absolute. Both phrasings are already live in this
        // change: the delta uses one and `tasks.md` 6.7 prescribes the other.
        //
        // So it asks whether the requirement's durable-location sentence carries ANY ownership
        // qualifier, and then requires the summary to carry one too.
        var narrowed = Regex.IsMatch(
            requirements, @"durable location[^.]*\bowns\b", RegexOptions.IgnoreCase);

        if (narrowed && purpose.Contains("durable home", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(
                purpose.Contains("owns", StringComparison.OrdinalIgnoreCase),
                "The requirement scopes the durable home to stores the package owns, but the "
                + "capability's Purpose still claims it without that qualifier. A summary is what "
                + "a reader derives the capability from after archiving.");
        }
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
                @"\[Http(Get|Post|Put|Delete|Patch)\(""([^""]+)""\)\]")
            .Select(match => $"{match.Groups[1].Value.ToUpperInvariant()} {match.Groups[2].Value}")
            .ToList();

        // The fixture is load-bearing: a regex that stopped matching would make the loop below
        // iterate over nothing and pass. A LOWER bound rather than an exact count, so that a
        // fourth endpoint reaches the loop and fails with the message written for it, instead
        // of tripping "expected 3, got 4" here and telling its author nothing useful.
        Assert.True(routes.Count >= 4, $"Expected at least 4 routes, found {routes.Count}.");

        // Each verb the capability offers is named in the paragraph that summarises it. Keyed
        // off the route, so a FOURTH endpoint fails here until somebody says what it is.
        // KEYED ON THE METHOD AS WELL AS THE ROUTE, and that is a fix rather than a flourish.
        // The exclusion below was written as `route != "bookings"` when the only thing at that
        // route was the list — so when `booking-on-behalf` added POST at the same route, its
        // endpoint was silently exempt from this guard and the whole suite stayed green. A
        // route alone stopped identifying an endpoint the moment two verbs shared one.
        var described = new Dictionary<string, string>
        {
            ["POST bookings"] = "record a booking on a booker's behalf",
            ["POST bookings/find-by-booker"] = "find a subject's bookings by their email address",
            ["POST bookings/{id:guid}/confirm"] = "confirm",
            ["POST bookings/{id:guid}/decline"] = "decline",
            ["POST bookings/{id:guid}/cancel"] = "cancel",
            ["POST bookings/{id:guid}/move"] = "move",
            ["POST bookings/{id:guid}/erase-booker"] = "erase a booker's contact details",
        };

        // Only the LIST is exempt, named precisely: it is the read the whole capability is
        // about, described by the Purpose paragraph in its own words rather than by a verb.
        foreach (var route in routes.Where(route => route != "GET bookings"))
        {
            Assert.True(
                described.TryGetValue(route, out var phrase),
                $"BookingsController exposes '{route}', which this test does not know about. "
                + "Add it here AND to the capability's Purpose paragraph, which summarises what "
                + "the capability does and is read by anyone deriving its surface from the spec.");

            DocumentationAssert.Says(purpose, phrase);
        }

        DocumentationAssert.DoesNotSay(purpose, "about those two verbs");
    }

    [Fact]
    public void The_retention_setting_is_documented_with_its_unit_and_its_default()
    {
        // A site owner cannot configure what is not named, and cannot reason about a period
        // whose starting point is not stated. The key itself is asserted against the constant
        // the code reads, so a rename cannot leave the documentation pointing at nothing.
        var docs = Docs();

        Assert.Contains(
            UBookItPersistenceComposer.RetentionDaysSettingKey.Split(':')[^1],
            docs,
            StringComparison.Ordinal);

        DocumentationAssert.Says(docs, "It is off unless you set it");
        DocumentationAssert.Says(docs, "whose slot **ended** more than that many days ago");
        DocumentationAssert.Says(docs, "The setting is read at startup");
    }

    [Fact]
    public void The_one_way_door_is_documented()
    {
        // THE MOST LOAD-BEARING SENTENCES IN THE DOCUMENT, and the reason this test exists.
        //
        // Enabling retention erases a site's history on the first run, irreversibly, and there
        // is no confirmation step — the spec says outright that this paragraph IS the
        // confirmation dialog, because a config file has nowhere to put one. Undocumented, the
        // feature's first use is a disaster; and until this test existed the callout could be
        // edited away with a green suite.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Turning this on erases your history immediately");
        DocumentationAssert.Says(docs, "Not gradually — on the first run, within minutes");
        DocumentationAssert.Says(docs, "no permission, group or support call brings any of them back");
        DocumentationAssert.Says(docs, "There is no \"are you sure?\" step");
    }

    [Fact]
    public void The_boundaries_of_what_retention_reaches_are_documented()
    {
        // Each of these is a decision that looks like a defect to somebody who has not been
        // told: that a cancelled booking is erased on the same clock as one that went ahead,
        // that a future-dated booking is never reached however old, and that an unreadable
        // value disables the feature rather than defaulting it.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Every booking counts, whatever its status");

        // The SUBJECT, not just the predicate. "is never erased by retention" alone was satisfied
        // by any sentence ending that way — including "A cancelled booking is never erased by
        // retention", which is flatly wrong and is the most likely miswording, since it is the
        // rule a reader is most tempted to assume. What the spec requires is that the doc say a
        // booking whose interval HAS NOT ENDED is never erased, so that is what is pinned.
        DocumentationAssert.Says(docs, "A booking whose slot has not ended is never erased by retention");

        DocumentationAssert.Says(docs, "A value it cannot read means off, not a default");

        // The cap, tied to the constant the code reads — the same treatment the setting key gets
        // above, and for the same reason: a literal in prose can drift from the value that
        // enforces it with nothing to notice.
        Assert.Contains(
            $"{UBookItPersistenceComposer.MaxRetentionDays}",
            docs,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Erasure_that_nobody_performed_is_documented()
    {
        // `booker-erasure` requires the reader be told erasure is not exclusively something a
        // person does. Without it, the first automatically erased booking is a bug report about
        // vanishing data — and the operator has no way to reach the explanation, since there is
        // no actor to look up.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Details can also disappear with nobody having erased them");
        DocumentationAssert.Says(docs, "almost certainly that, and not a fault");

        // "and points to where that is described" is half the scenario, and it was the unguarded
        // half: deleting the link left this test green. The anchor is asserted as a link rather
        // than as prose, because a reader following a broken pointer is no better served than one
        // given no pointer at all.
        Assert.Contains(
            "(#erasing-old-bookings-automatically)",
            docs,
            StringComparison.Ordinal);

        // And the heading it points at, so the link cannot survive its own target being renamed.
        Assert.Contains(
            "## Erasing old bookings automatically",
            docs,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_privacy_notice_and_its_setting_are_documented()
    {
        var docs = Docs();

        Assert.Contains(
            UBookItPersistenceComposer.PrivacyPolicyUrlSettingKey.Split(':')[^1],
            docs,
            StringComparison.Ordinal);

        DocumentationAssert.Says(docs, "above the button that submits it");
        DocumentationAssert.Says(docs, "You cannot mistype the retention period into it");
    }

    [Fact]
    public void The_notice_is_documented_as_not_being_a_privacy_policy()
    {
        // The boundary a site owner most needs and is least likely to assume. Left unstated,
        // somebody reads a compliant-looking paragraph on their booking form and concludes they
        // have a privacy policy.
        var docs = Docs();

        DocumentationAssert.Says(docs, "This notice is not a privacy policy");
        DocumentationAssert.Says(docs, "If you need a privacy policy, you still need one");
    }

    [Fact]
    public void The_refusal_of_an_unusable_policy_link_is_documented()
    {
        // Why this setting is stricter than the others, in the terms that make it obvious: it is
        // the only value that reaches a link on a public page.
        var docs = Docs();

        DocumentationAssert.Says(docs, "Anything else is refused and logged as an error");
        DocumentationAssert.Says(docs, "not by blocking the dangerous ones, but by allowing only these");
    }

    [Fact]
    public void The_default_rendering_with_no_retention_period_is_documented()
    {
        // The DEFAULT install. A site owner reading "it says so plainly rather than going quiet"
        // knows the awkward-sounding sentence on their form is deliberate rather than a bug.
        DocumentationAssert.Says(Docs(), "If you have not, it says so plainly rather than going quiet");
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

    /// <summary>
    /// One line, single-spaced — so a claim that wraps in the source still matches the phrase a
    /// test looks for.
    /// </summary>
    private static string Collapse(string text)
        => Regex.Replace(text, @"\s+", " ");
}
