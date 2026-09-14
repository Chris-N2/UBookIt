using System.Text.RegularExpressions;
using UBookIt.Tests.Rendering.Support;
using UBookIt.Tests.Support;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests.Rendering;

/// <summary>
/// Rule 2 — a view renders every state its model can express (default-frontend
/// spec).
/// <para>
/// For each model member a view's source refers to, varying that member must
/// change what the view renders. A member a view names but cannot make any
/// difference to is either dead, or sits behind a branch that cannot be reached —
/// and the second is a message placed where it can never appear, which is ⑩-1's
/// defect exactly.
/// </para>
/// <para>
/// The member set is derived from the view's own source, so this catches that
/// defect <b>generically</b>: nobody has to have thought about the branch.
/// </para>
/// </summary>
public class ModelPropertyTests
{
    private readonly ViewRenderer _renderer = new();

    /// <summary>
    /// The state a flag was found dead in, carried out of the check so the failure
    /// message can name it rather than only the count of states tried.
    /// </summary>
    private string? DeadIn { get; set; }

    /// <summary>
    /// Why a flag is legitimately silent in a state: an error against the very
    /// control the flag speaks about is already saying the same thing, more
    /// strongly.
    /// </summary>
    private const string SupersededByAnError =
        "An error against the control this flag speaks about takes precedence and says the same "
        + "thing more strongly, so setting the flag from this state correctly changes nothing. "
        + "The visitor is told either way.";

    /// <summary>
    /// Why <c>SelectedDateIsListed</c> is legitimately silent when no dates are listed: the
    /// statement it controls refers to "the list above", and with no list there is nothing for
    /// it to refer to.
    /// </summary>
    /// <remarks>
    /// This is a suppression added because the view was CORRECTED, not because the rule was
    /// inconvenient. The statement used to render unconditionally, so on an empty window the
    /// page read "…which is not in the list above" directly beneath a paragraph saying there is
    /// no list — found by looking at the rendered page, not by any assertion. Conditioning it on
    /// there being a list is the fix, and this entry is the honest consequence: in those states
    /// the flag genuinely changes nothing, because the empty-window message already accounts for
    /// the whole step.
    /// </remarks>
    private const string NoListToBeAbsentFrom =
        "the statement this flag controls names 'the list above', and these states render no list";

    /// <summary>
    /// Which control each flag speaks about, so a suppression can be checked to be
    /// of the shape it claims rather than taken on trust.
    /// </summary>
    private static readonly Dictionary<string, string> FlagSpeaksAbout = new(StringComparer.Ordinal)
    {
        ["ResourceChoiceWasReset"] = BookingFieldIds.Resource,
    };

    /// <summary>
    /// The (view, flag, state) combinations where a flag legitimately changes
    /// nothing, each with its reason.
    /// <para>
    /// Enumerated rather than expressed as a blanket rule, because the spec requires
    /// it: a property may be exempt "only by an explicit, reasoned entry — never by
    /// being absent from a list". A rule that skipped every state carrying such an
    /// error would swallow a genuine defect that happened to coexist with one.
    /// </para>
    /// <para>
    /// There are now three of one shape, which is the point at which a shared reason
    /// beats three restatements — so the reason is a constant, and
    /// <see cref="Every_suppression_is_of_the_shape_it_claims"/> proves each entry
    /// really is that shape. The list stays visible; only the prose is shared.
    /// </para>
    /// </summary>
    private static readonly Dictionary<(string View, string Member, string State), string> Suppressed =
        new()
        {
            [(ViewInventory.DateAndLength, "ResourceChoiceWasReset", "service: refused choice")] =
                SupersededByAnError,

            [(ViewInventory.DateAndLength, "ResourceChoiceWasReset", "service: with errors")] =
                SupersededByAnError,

            [(ViewInventory.DateAndLength, "ResourceChoiceWasReset", "service: refused choice and none left")] =
                SupersededByAnError,

            [(ViewInventory.AvailableDates, "SelectedDateIsListed", "service: no dates at this length")] =
                NoListToBeAbsentFrom,

            [(ViewInventory.AvailableDates, "SelectedDateIsListed", "service: no dates at any length")] =
                NoListToBeAbsentFrom,

            [(ViewInventory.AvailableDates, "SelectedDateIsListed", "resource: no dates at this length")] =
                NoListToBeAbsentFrom,
        };

    public static TheoryData<string> ShippedViews()
    {
        var data = new TheoryData<string>();

        foreach (var view in ViewInventory.All)
        {
            data.Add(view);
        }

        return data;
    }

    // -----------------------------------------------------------------------
    // Non-vacuity. These two come first because without them the rule below
    // passes trivially on a derivation that has quietly stopped finding things —
    // green, and worthless.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ShippedViews))]
    public void Every_view_refers_to_at_least_one_model_member(string view)
    {
        // Guard A. "This view refers to nothing" is precisely what a broken
        // extraction looks like, so a view that genuinely refers to nothing must be
        // an explicit, reasoned exemption rather than a silent pass.
        if (ModelReferences.DelegatingViews.TryGetValue(view, out var reason))
        {
            Assert.Empty(ModelReferences.Of(view));
            Assert.NotEmpty(reason);
            return;
        }

        Assert.NotEmpty(ModelReferences.Of(view));
    }

    [Theory]
    [MemberData(nameof(ShippedViews))]
    public async Task Two_renders_of_one_model_compare_equal(string view)
    {
        // Rule 2's own precondition, and it had none.
        //
        // Both routes through IsLiveAsync ask "did changing the model change the
        // page" by comparing two renders. That question is meaningless on a view
        // that never renders twice alike — the strong route's equality is never
        // true, the weak route's inequality always is, and EVERY member of the view
        // is reported live while nothing is checked. Rule 2 was vacuous on the three
        // views carrying an Umbraco form, which are the ones hardest to check by
        // reading.
        //
        // Asserted for every shipped view rather than the three, so a fourth source
        // of per-render variance is caught the day it appears rather than the day
        // someone notices a rule has stopped working.
        //
        // Across EVERY state, not the first. Asking only state [0] would leave
        // variance that appears in any later state unseen for every view — and it
        // was worse than that: `_ErrorSummary`'s first state has no errors, so the
        // partial renders nothing and the check compared "" against "". A guard
        // against vacuity, itself vacuous, on the view where it mattered least to
        // notice and most to be right.
        var rendered = 0;

        foreach (var state in ViewFixtures.For(view))
        {
            var once = await RenderForComparisonAsync(view, state.Model);
            var twice = await RenderForComparisonAsync(view, state.Model);

            Assert.Equal(once, twice);

            if (once.Trim().Length > 0)
            {
                rendered++;
            }
        }

        // Non-vacuity: a view all of whose states render nothing would satisfy the
        // equality above without comparing anything. A view legitimately renders
        // nothing in SOME states — an error summary with no errors — so the bar is
        // that at least one state produced a document to compare.
        Assert.True(
            rendered > 0,
            $"{view} rendered nothing in every state, so this guard compared nothing.");
    }

    [Fact]
    public async Task The_stripping_fires_where_there_is_something_to_strip()
    {
        // Non-vacuity for the rule above, which passes trivially on any view that
        // was already deterministic. This asserts the flow view is NOT one of those:
        // its raw output really does vary between renders, and it is the stripping
        // that makes the comparison sound rather than the view being well-behaved.
        //
        // Without this, an Umbraco version that changed the form id's shape or
        // renamed a token field would make the patterns match nothing; the strip
        // would become a no-op and rule 2 would go quietly back to vacuous. Here it
        // fails instead, naming the count it expected.
        var flow = await _renderer.RenderAsync(
            ViewInventory.ServiceFlow,
            ViewFixtures.For(ViewInventory.ServiceFlow)[0].Model);

        Assert.Equal(3, RenderNondeterminism.CountIn(flow));

        // And that the raw output really does vary, so the strip is not decorative.
        var again = await _renderer.RenderAsync(
            ViewInventory.ServiceFlow,
            ViewFixtures.For(ViewInventory.ServiceFlow)[0].Model);

        Assert.NotEqual(flow, again);
        Assert.Equal(RenderNondeterminism.Strip(flow), RenderNondeterminism.Strip(again));

        // A deterministic view has nothing to strip: the patterns are specific to
        // the Umbraco form rather than matching whatever they happen to see.
        var partial = await _renderer.RenderAsync(
            ViewInventory.DateAndLength,
            ViewFixtures.For(ViewInventory.DateAndLength)[0].Model);

        Assert.Equal(0, RenderNondeterminism.CountIn(partial));
    }

    [Fact]
    public void The_members_a_partial_keeps_alive_are_enumerated()
    {
        // Rule 2's blind spot on a composite page, made visible instead of silent —
        // the same treatment rule 3's shared-literal case already gets.
        //
        // A flow page renders partials that refer to the same model. If the PAGE's
        // own reference to a shared member dies, the partial keeps that member
        // changing the output, and rule 2 reports it live. Demonstrated: moving
        // `ChosenResourceId` in Service.cshtml into a dead branch leaves the suite
        // green, because `_DateAndLength` still refers to it — while the same
        // mutation on page-unique `ServiceId` fails.
        //
        // The blind spot is inherent to a per-view rule asked of a composed page and
        // cannot be removed without attributing rendered output to the view that
        // emitted it. It can be BOUNDED: every masked member is listed here, so the
        // set is reviewed rather than discovered, and a NEW one fails this test and
        // has to be justified.
        //
        // This matters most right now, because rule 2 has never run on these views
        // before, and "rule 2 is live on the flow views" is otherwise about to be
        // read as stronger than it is.
        var masked = ViewInventory.All
            .Where(view => !ModelReferences.DelegatingViews.ContainsKey(view))
            .SelectMany(view => ModelReferences
                .MaskedByIncludesIn(view)
                .Select(member => $"{view[(view.LastIndexOf('/') + 1)..]}:{member}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                // Both flow pages hand their whole model to `_DateAndLength`,
                // `_Times`, `_ErrorSummary` and `_YourDetails`, so any member the
                // page and a partial both name is one rule 2 cannot judge FOR THE
                // PAGE. Each is judged for the partial itself, where it is not
                // masked — the loss is the page's own reference, not the member.
                //
                // Booking/Default.cshtml — the resource flow.
                "Default.cshtml:DurationMinutes",
                "Default.cshtml:FlowToken",
                "Default.cshtml:HasTimes",
                "Default.cshtml:SelectedDate",

                // BookingFlow/Service.cshtml — the service flow, the same four
                // plus the visitor's choice of who, which `_DateAndLength` renders
                // the control for.
                "Service.cshtml:ChosenResourceId",
                "Service.cshtml:DurationMinutes",
                "Service.cshtml:FlowToken",
                "Service.cshtml:HasTimes",
                "Service.cshtml:SelectedDate",

                // `_DateAndLength` names DurationMinutes in the settled-length branch, and now
                // also hands its whole model to `_AvailableDates`, which names it in the
                // legend — so within that partial the member is masked exactly as the flow
                // pages' members are. Judged where it is not masked: in `_AvailableDates`.
                "_DateAndLength.cshtml:DurationMinutes",
                // `_YourDetails` now reads `PrivacyNotice` to decide what the email field's hint
                // says. Its flow page hands over the whole model and renders the notice itself
                // through `_PrivacyNotice`, so the member is kept alive by that sibling whatever
                // this partial does with it — which is exactly the masking this list bounds.
                //
                // What it masks is only the READ, not the behaviour: the hint's two wordings are
                // asserted directly in PrivacyNoticeTests, against a view model built for each.
                "_YourDetails.cshtml:PrivacyNotice",
            ],
            masked);

        // Worth knowing, and the reason this bound is narrower than it reads:
        // masked by rule 2 does not mean unchecked. `HasTimes` is the guard on
        // `_YourDetails`, and neutralising it fails a rule 1 check
        // (`Every_problem_whose_control_is_on_the_page_is_linked_to_it`) because the
        // booker fields then appear on a page whose errors point at them. A member
        // here is one RULE 2 cannot speak for, not one nothing can.

        // Non-vacuity: the enumeration is only meaningful while the views it
        // describes still compose partials. A scan that found nothing would satisfy
        // the equality by matching an empty list against an empty list.
        Assert.NotEmpty(masked);
    }

    [Fact]
    public void An_exempted_view_really_is_a_delegate()
    {
        // The exemption registry is the one hand-kept list in this suite, and it
        // switches off two rules for whatever it names. Nothing checked that a named
        // view is actually the harmless shape the exemption is written for, so a
        // view with real markup could be silenced by adding a line here — the rule
        // turned off by the same mechanism meant to document turning it off.
        //
        // This half checks the source shape: the view hands its whole model to
        // exactly one other shipped view. The other half of the stated reason — that
        // it renders nothing else — is not visible in source and is asserted against
        // rendered output by
        // ViewInventoryTests.An_exempted_delegate_adds_no_markup_to_the_view_it_delegates_to.
        // Neither half alone is the claim.
        Assert.NotEmpty(ModelReferences.DelegatingViews);

        foreach (var (view, reason) in ModelReferences.DelegatingViews)
        {
            Assert.Contains(view, ViewInventory.All);
            Assert.NotEmpty(reason);

            var target = ModelReferences.DelegationTargetOf(view);

            Assert.True(
                target is not null,
                $"{view} is exempted as a delegate but does not hand its model to "
                + "exactly one view; the exemption's reason does not hold.");

            Assert.Contains(target!, ViewInventory.All);
        }
    }

    [Fact]
    public void The_derivation_finds_what_it_should_on_a_known_view()
    {
        // Guard B. `_DateAndLength.cshtml` is the branch-densest view in the
        // package — 11 conditionals — and the one ⑩-1's defect was in. If the
        // extraction silently stops finding references, this fails; without it, a
        // pattern that matched nothing would make the whole rule vacuous.
        //
        // MinDate and MaxDate left this list when the date control moved into
        // `_AvailableDates` — they bound the "another date" field, which went with it. That
        // this guard noticed is the point of it: a snapshot that had quietly kept naming two
        // members the view no longer touches would still have passed while measuring less.
        // SelectedDate went the same way: it was the date input's `value`.
        Assert.Equal(
            [
                "ChosenResourceId",
                "DurationMinutes",
                "DurationOptions",
                "ErrorFor",
                "FlowToken",
                "LengthIsFixed",
                "LengthIsTheProblem",
                "OffersResourceChoice",
                "PreservedQuery",
                "ResourceChoiceCount",
                "ResourceChoiceWasReset",
                "ResourceChoices",
            ],
            ModelReferences.Of(ViewInventory.DateAndLength));
    }

    [Fact]
    public void The_derivation_sees_the_null_conditional_form()
    {
        // `ServiceUnavailable.cshtml` refers to its model only as `Model?.Reason`.
        // A pattern matching `Model.` alone reports it as referring to nothing —
        // and a view referring to nothing passes the rule trivially. This is the
        // reference form that would have slipped through.
        Assert.Contains("Reason", ModelReferences.Of(ViewInventory.ServiceUnavailable));
    }

    [Fact]
    public async Task Every_suppression_is_still_earning_its_place()
    {
        // A stale exemption is a hole nobody rechecks. Each entry must still be a
        // state where the flag genuinely changes nothing — if the view is fixed so
        // that it does, the entry must go rather than sit there excusing a check
        // that would now pass.
        foreach (var ((view, member, state), reason) in Suppressed)
        {
            Assert.NotEmpty(reason);

            var model = Assert.Single(ViewFixtures.For(view), c => c.State == state).Model;

            Assert.True(
                ModelVariation.Vary(model, member) is { } flipped
                    && await RendersTheSameAsync(view, model, flipped),
                $"{view}: the suppression for '{member}' in state '{state}' is no longer needed — "
                + "varying it now changes the output. Remove the entry.");
        }
    }

    [Fact]
    public void Every_suppression_is_of_the_shape_it_claims()
    {
        // Every exemption's own claim is checked, and each SHAPE is checked its own way.
        //
        // This asserted a single shape — a flag silenced by an error against the control it
        // speaks about — because until now there was only one. A second shape arrived with the
        // available-dates list, and the choice at that moment mattered: relaxing this to "any
        // non-empty reason" would have turned a guarded exemption list into an unguarded one,
        // which is the defect this project has now produced three times in other guards.
        //
        // So the shapes are enumerated instead. An exemption whose reason is neither is not
        // merely unverified — it fails, and says so.
        foreach (var ((view, member, state), reason) in Suppressed)
        {
            var model = (IBookingFormView)Assert.Single(
                ViewFixtures.For(view), c => c.State == state).Model;

            if (reason == SupersededByAnError)
            {
                var field = Assert.Contains(member, FlagSpeaksAbout);

                Assert.True(
                    model.ErrorFor(field) is not null,
                    $"{view}: the suppression for '{member}' in state '{state}' claims an error "
                    + $"against '{field}' takes precedence, but that state carries no such error. "
                    + "Either the reason is wrong or the entry is.");
            }
            else if (reason == NoListToBeAbsentFrom)
            {
                Assert.True(
                    model.AvailableDates.Count == 0,
                    $"{view}: the suppression for '{member}' in state '{state}' claims there is "
                    + "no list of dates for the statement to refer to, but that state lists "
                    + $"{model.AvailableDates.Count}. Either the reason is wrong or the entry is.");
            }
            else
            {
                Assert.Fail(
                    $"{view}: the suppression for '{member}' in state '{state}' gives a reason "
                    + "this guard does not know how to check. Every exemption must claim a shape "
                    + "that is verified here — an unverifiable reason is an unguarded hole.");
            }
        }
    }

    [Fact]
    public void The_strong_rule_is_actually_applied_to_something()
    {
        // Non-vacuity for the strong rule. If the settable restriction ever excluded
        // everything, every member would quietly fall back to the weak rule, ⑩-1's
        // defect would pass again, and nothing would say so.
        //
        // Asserted over EVERY settable flag on the service form model rather than
        // over one member: QA's point was that guarding a single member leaves a
        // flag added later unguarded.
        var flags = typeof(UBookIt.Web.Rendering.ServiceFormModel)
            .GetProperties()
            .Where(p => p.PropertyType == typeof(bool) && p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        Assert.NotEmpty(flags);

        foreach (var flag in flags)
        {
            var settable = ViewFixtures
                .For(ViewInventory.DateAndLength)
                .Count(state => IsSettableBoolean(state.Model, flag));

            // More than one, or "every settable state" would be the same claim as
            // "some state" and the strengthening would be words only.
            Assert.True(
                settable > 1,
                $"'{flag}' is settable in {settable} exercised state(s) of _DateAndLength, so the "
                + "strong rule cannot distinguish 'live somewhere' from 'live wherever settable'.");
        }
    }

    // -----------------------------------------------------------------------
    // The rule.
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ShippedViews))]
    public async Task Every_referenced_member_changes_what_the_view_renders(string view)
    {
        var states = ViewFixtures.For(view);

        Assert.NotEmpty(states);

        foreach (var member in ModelReferences.Of(view))
        {
            Assert.True(
                await IsLiveAsync(view, member, states),
                $"{view}: '{member}' is referenced by the view but changes nothing it renders"
                + (DeadIn is { } state
                    ? $" in state '{state}'."
                    : $", across {states.Count} model states.")
                + " Either the reference is dead, or it sits behind a branch that cannot be "
                + "reached — which is a message placed where it can never appear.");
        }
    }

    /// <summary>
    /// Whether varying a member changes what the view renders.
    /// <para>
    /// Two routes, because not every member can be set. A settable property is
    /// varied <b>directly</b>, so the difference is attributable to it and nothing
    /// else. A computed property or a method — <c>HasTimes</c>,
    /// <c>LengthIsTheProblem</c>, <c>ErrorFor</c> — has no value to set, so it is
    /// judged by co-variation across the view's own states: some pair on which it
    /// evaluates differently must render differently.
    /// </para>
    /// <para>
    /// The second route is honestly weaker — a pair can differ in more than one
    /// member — and it is the best available for a derived value. It is also
    /// sufficient for the defect this rule exists to catch, since a computed flag is
    /// derived from a settable member which the view almost always names too, and
    /// that one takes the strong route.
    /// </para>
    /// </summary>
    private async Task<bool> IsLiveAsync(string view, string member, IReadOnlyList<ViewCase> states)
    {
        // A settable BOOLEAN is held to the strong rule: it must change the output
        // in EVERY state, not merely in one.
        //
        // "In one" is not enough, and this is the correction the mutation check
        // forced. ⑩-1's defect was a notice reachable for two of its three causes:
        // the flag was live wherever a control existed, and dead in the state where
        // none did. An exists-a-state rule sees the live states, passes, and lets
        // exactly that defect through — verified by reintroducing it, watching the
        // rule stay green, and rewriting the rule rather than the claim.
        //
        // A boolean on these models is a claim that something is SHOWN. If setting
        // it changes nothing from some state a visitor can be in, the thing it
        // promises to show cannot appear from there.
        // Judged over EVERY state where the flag is settable — both values, not only
        // the states where it is currently set.
        //
        // One restriction only, and it is genuinely necessary: the shared partials
        // render both form models, and a member settable on one is computed on the
        // other (`LengthIsFixed` is init-only on the service model and `=> false` on
        // the resource model). Judging a state that cannot express the flag reports
        // a fault that is not one.
        //
        // An earlier version ALSO skipped every state where the flag was false, to
        // accommodate one state where it is legitimately silent. QA showed what that
        // cost: `@if (Model.LengthIsFixed && !Model.LengthIsTheProblem)` passed all
        // 212 tests — a fixed-length service would render a length dropdown on a
        // date with no times, offering a choice the service does not permit. It also
        // contradicted this change's own spec, which says a property may be exempt
        // "only by an explicit, reasoned entry — never by being absent from a list",
        // and a blanket skip is exemption by absence.
        //
        // So the legitimately-silent case is now an explicit entry, and everything
        // else is checked.
        var flippable = states
            .Where(state => IsSettableBoolean(state.Model, member))
            .ToList();

        if (flippable.Count > 0)
        {
            foreach (var state in flippable)
            {
                if (Suppressed.ContainsKey((view, member, state.State)))
                {
                    continue;
                }

                if (ModelVariation.Vary(state.Model, member) is not { } flipped
                    || await RendersTheSameAsync(view, state.Model, flipped))
                {
                    // The state is the piece needed to act on the failure, now that
                    // the rule checks per state and suppressions are keyed by one.
                    DeadIn = state.State;
                    return false;
                }
            }

            return true;
        }

        // Everything else — values, collections, computed properties, the ErrorFor
        // method — is held to the weaker rule: live in at least one state. A value
        // legitimately says nothing in states where the view does not show it
        // (`LongestAvailableMinutes` while there are times), so "in every state"
        // would demand falsehoods of it.
        foreach (var state in states)
        {
            if (ModelVariation.Vary(state.Model, member) is not { } varied)
            {
                continue;
            }

            if (!await RendersTheSameAsync(view, state.Model, varied))
            {
                return true;
            }
        }

        return await CoVariesAsync(view, member, states);
    }

    private static bool IsSettableBoolean(object model, string member)
        => model.GetType().GetProperty(member) is { CanWrite: true } property
            && property.PropertyType == typeof(bool);

    private async Task<bool> RendersTheSameAsync(string view, object before, object after)
        => string.Equals(
            await RenderForComparisonAsync(view, before),
            await RenderForComparisonAsync(view, after),
            StringComparison.Ordinal);

    /// <summary>
    /// A render with the values that vary on their own removed — see
    /// <see cref="RenderNondeterminism"/>.
    /// <para>
    /// Both routes through <see cref="IsLiveAsync"/> compare rendered output, and
    /// both are corrupted the same way by a document that never renders twice
    /// alike: the strong route's equality is never true, and the weak route's
    /// inequality is always true. So both go through here, and nothing in this file
    /// compares a raw render.
    /// </para>
    /// </summary>
    private async Task<string> RenderForComparisonAsync(string view, object model)
        => RenderNondeterminism.Strip(await _renderer.RenderAsync(view, model));

    /// <summary>
    /// The fallback route: some pair of states on which the member evaluates
    /// differently also renders differently.
    /// </summary>
    private async Task<bool> CoVariesAsync(string view, string member, IReadOnlyList<ViewCase> states)
    {
        var rendered = new List<(string? Value, string Html)>(states.Count);

        foreach (var state in states)
        {
            rendered.Add((
                ModelVariation.Evaluate(state.Model, member),
                await RenderForComparisonAsync(view, state.Model)));
        }

        return rendered.Any(a => rendered.Any(b =>
            !string.Equals(a.Value, b.Value, StringComparison.Ordinal)
            && !string.Equals(a.Html, b.Html, StringComparison.Ordinal)));
    }
}
