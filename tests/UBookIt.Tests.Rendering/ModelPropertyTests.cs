using UBookIt.Tests.Rendering.Support;

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
    public void The_strong_rule_is_actually_applied_to_something()
    {
        // Non-vacuity for design D8, and the guard that matters most now that the
        // strong rule is restricted twice — to settable flags, and to the states
        // where they are set. If either restriction ever excluded everything, every
        // member would quietly fall back to the weak rule, ⑩-1's defect would pass
        // again, and nothing would say so.
        //
        // Asserted on the member the defect was in.
        var flippable = ViewFixtures
            .For(ViewInventory.DateAndLength)
            .Where(state => state.Model.GetType()
                    .GetProperty(nameof(UBookIt.Web.Rendering.ServiceFormModel.ResourceChoiceWasReset))
                is { CanWrite: true })
            .Where(state => ModelVariation.Evaluate(
                state.Model,
                nameof(UBookIt.Web.Rendering.ServiceFormModel.ResourceChoiceWasReset)) == bool.TrueString)
            .ToList();

        Assert.NotEmpty(flippable);

        // And more than one, or "every state where it is set" would be the same
        // claim as "some state" and the strengthening would be words only. The
        // defect was invisible precisely because one such state rendered it and
        // another did not.
        Assert.True(
            flippable.Count > 1,
            "The reset flag is set in only one exercised state, so the strong rule "
            + "cannot distinguish 'live somewhere' from 'live wherever set' — which "
            + "is the distinction it exists to make.");
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
                $"{view}: '{member}' is referenced by the view but changes nothing it renders, "
                + $"across {states.Count} model states. Either the reference is dead, or it sits "
                + "behind a branch that cannot be reached — which is a message placed where it "
                + "can never appear.");
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
        // Judged over the states where the flag is settable AND currently TRUE.
        //
        // Two restrictions, each earned by a false positive:
        //
        // - settable, because the shared partials render BOTH form models and a
        //   member settable on one is computed on the other (`LengthIsFixed` is
        //   init-only on the service model and `=> false` on the resource model);
        //
        // - true, because the claim is "if the model says show this, the page shows
        //   something it otherwise would not" — not "turning this on from any state
        //   whatsoever changes something". Turning the reset flag on in the
        //   refused-choice state changes nothing, correctly: the error against that
        //   control takes precedence and says the same thing in stronger terms.
        //
        // Narrowed to true-states rather than exempted, because an exemption would
        // have switched the rule off for the very member ⑩-1's defect was in.
        var flippable = states
            .Where(state => IsSettableBoolean(state.Model, member)
                && ModelVariation.Evaluate(state.Model, member) == bool.TrueString)
            .ToList();

        if (flippable.Count > 0)
        {
            foreach (var state in flippable)
            {
                if (ModelVariation.Vary(state.Model, member) is not { } flipped
                    || await RendersTheSameAsync(view, state.Model, flipped))
                {
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
