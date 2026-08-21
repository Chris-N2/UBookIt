using UBookIt.Tests.Rendering.Support;
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
        };

    public static TheoryData<string> InScopeViews()
    {
        var data = new TheoryData<string>();

        foreach (var view in ViewInventory.InScope)
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
    [MemberData(nameof(InScopeViews))]
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

    [Fact]
    public void The_derivation_finds_what_it_should_on_a_known_view()
    {
        // Guard B. `_DateAndLength.cshtml` is the branch-densest view in the
        // package — 11 conditionals — and the one ⑩-1's defect was in. If the
        // extraction silently stops finding references, this fails; without it, a
        // pattern that matched nothing would make the whole rule vacuous.
        Assert.Equal(
            [
                "ChosenResourceId",
                "DurationMinutes",
                "DurationOptions",
                "ErrorFor",
                "FlowToken",
                "LengthIsFixed",
                "LengthIsTheProblem",
                "MaxDate",
                "MinDate",
                "OffersResourceChoice",
                "ResourceChoiceCount",
                "ResourceChoiceWasReset",
                "ResourceChoices",
                "SelectedDate",
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
        // The exemptions all claim one shape: a flag silenced by an error against
        // the control it speaks about. Claimed in prose until now. This checks it —
        // so an exemption cannot be added for a different reason under cover of the
        // shared wording, which is exactly what a shared reason makes easy.
        foreach (var ((view, member, state), reason) in Suppressed)
        {
            Assert.Equal(SupersededByAnError, reason);

            var field = Assert.Contains(member, FlagSpeaksAbout);
            var model = (IBookingFormView)Assert.Single(
                ViewFixtures.For(view), c => c.State == state).Model;

            Assert.True(
                model.ErrorFor(field) is not null,
                $"{view}: the suppression for '{member}' in state '{state}' claims an error "
                + $"against '{field}' takes precedence, but that state carries no such error. "
                + "Either the reason is wrong or the entry is.");
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
    [MemberData(nameof(InScopeViews))]
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
            await _renderer.RenderAsync(view, before),
            await _renderer.RenderAsync(view, after),
            StringComparison.Ordinal);

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
                await _renderer.RenderAsync(view, state.Model)));
        }

        return rendered.Any(a => rendered.Any(b =>
            !string.Equals(a.Value, b.Value, StringComparison.Ordinal)
            && !string.Equals(a.Html, b.Html, StringComparison.Ordinal)));
    }
}
