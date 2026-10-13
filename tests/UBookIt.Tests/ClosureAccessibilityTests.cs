using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The accessibility properties of the two closure surfaces that are decided in source:
/// every control carries an accessible name, each name identifies WHICH closure it acts on,
/// and the superseded statement is referenced from the exception it describes rather than
/// merely placed inside it.
/// </summary>
/// <remarks>
/// <b>What this can and cannot prove.</b> These are source-level assertions, so they catch a
/// control shipped without a name and a statement shipped without an association — the two
/// failures this package has actually had. They do NOT establish that the rendered backoffice
/// is operable: `uui-*` components put their internals in a shadow root, and only a real
/// browser can show that focus lands where it should. That check is live, and is recorded as
/// such rather than implied by this file passing.
/// </remarks>
public class ClosureAccessibilityTests
{
    private const string ClosuresView = "src/UBookIt.Backoffice/Client/src/section/closures-view.element.ts";

    private const string ResourceEditor = "src/UBookIt.Backoffice/Client/src/section/resource-editor.element.ts";

    [Fact]
    public void Every_closure_row_control_names_the_closure_it_acts_on()
    {
        // A list of identical "Edit" and "Delete" buttons is unusable without sight of the
        // row: the accessible name has to carry the dates and the label, not just the verb.
        // The dates are the closure's RANGE since closure-date-ranges — a start date alone would
        // name two different closures that begin on neighbouring days identically.
        var source = RepoFiles.Read(ClosuresView);

        // No colon: the house form is "Edit <thing>", as the Resources list already does.
        Assert.Contains(
            "label=\"${this.#term(\"edit\")} ${rangeText(closure.date, closure.endDate)} ${closure.label}\"",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "label=\"${this.#term(\"delete\")} ${rangeText(closure.date, closure.endDate)} ${closure.label}\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_opt_out_control_names_the_closure_it_exempts_from()
    {
        // Same failure mode in the resource editor: a column of "Open anyway" toggles with
        // nothing in the accessible name to say which date each one opens.
        var source = RepoFiles.Read(ResourceEditor);

        Assert.Contains(
            "label=\"${this.#term(\"closureOpenAnyway\")}: ${rangeText(closure.date, closure.endDate)} ${closure.label}\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_inputs_in_the_closures_view_are_labelled()
    {
        // `label[for]` inside the same shadow root, which is what works here — a uui-label is
        // not a <label> and cannot pierce a component's shadow root.
        var source = RepoFiles.Read(ClosuresView);

        foreach (var id in new[] { "closure-date", "closure-end-date", "closure-label" })
        {
            Assert.Contains($"<label for=\"{id}\">", source, StringComparison.Ordinal);
            Assert.Contains($"id=\"{id}\"", source, StringComparison.Ordinal);
        }

        // To's hint ("leave it empty for one day") is referenced from To, not merely rendered.
        Assert.Contains(
            "aria-describedby=${closureDescribedBy(\"closure-end-date\", this._errorControl, \"closure-end-date-hint\")}",
            source,
            StringComparison.Ordinal);
        Assert.Contains("id=\"closure-end-date-hint\"", source, StringComparison.Ordinal);

        // The hint is referenced from the input, not merely rendered under it — through the shared
        // helper, which also adds the error's id when a failure concerns this input.
        Assert.Contains(
            "aria-describedby=${closureDescribedBy(\"closure-label\", this._errorControl, \"closure-label-hint\")}",
            source,
            StringComparison.Ordinal);
        Assert.Contains("id=\"closure-label-hint\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_closure_save_is_announced_and_focused()
    {
        var source = RepoFiles.Read(ClosuresView);

        Assert.Contains("role=\"alert\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"closure-error\"", source, StringComparison.Ordinal);

        // Focusable, and focused: an alert nobody is sent to is read only if the screen
        // reader happens to be listening at that moment.
        Assert.Contains("tabindex=\"-1\"", source, StringComparison.Ordinal);
        Assert.Contains("querySelector<HTMLElement>(\"#closure-error\")?.focus()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_superseded_statement_is_referenced_from_its_exception()
    {
        // THE claim that matters here. Text placed inside a fieldset is visually present and
        // programmatically unrelated to it until something references it — and this statement
        // exists precisely to be read by somebody deciding whether their exception is in
        // force. The ids come from the shared module so the reference and the rendered id
        // cannot drift apart.
        var source = RepoFiles.Read(ResourceEditor);

        Assert.Contains("exceptionDescribedByIds(index, {", source, StringComparison.Ordinal);
        Assert.Contains("id=${supersededId(index)}", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_holiday_checkbox_names_the_date_it_would_close()
    {
        // A column of identical "Close on" checkboxes is unusable without sight of the row. The
        // accessible name has to carry the date and the holiday's name, exactly as the closure
        // row controls do.
        var source = RepoFiles.Read(ClosuresView);

        Assert.Contains(
            "label=\"${this.#term(\"importChoose\")}: ${row.date} ${row.name}\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_import_window_inputs_are_labelled()
    {
        var source = RepoFiles.Read(ClosuresView);

        foreach (var id in new[] { "holiday-from", "holiday-to" })
        {
            Assert.Contains($"<label for=\"{id}\">", source, StringComparison.Ordinal);
            Assert.Contains($"id=\"{id}\"", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_failed_holiday_source_is_announced_and_an_import_result_is_not()
    {
        // TWO different live regions, deliberately. A source that failed is an ALERT — it
        // interrupts, because the operator's next move depends on it. A summary of what was
        // created is a STATUS: worth announcing, not worth interrupting for.
        var source = RepoFiles.Read(ClosuresView);

        Assert.Contains("id=\"holiday-error\" class=\"error\" role=\"alert\"", source, StringComparison.Ordinal);
        Assert.Contains("id=\"holiday-summary\" role=\"status\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_selectable_holiday_row_renders_a_checkbox()
    {
        // The rendering asks the shared decision rather than testing the state inline, so the
        // control offered and the row sent can never disagree about what is selectable.
        var source = RepoFiles.Read(ClosuresView);

        Assert.Contains("${isSelectable(row)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("row.state === \"new\"", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// The view asks the shared decisions whether the import belongs on screen, rather than
    /// re-deciding inline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This pins the seam, which testing both sides of it does not.</b> `showsImport` and
    /// `shouldAskForSource` are covered by eight cases in `holiday-fields.test.ts`, and the view
    /// is what must keep calling them — inline the condition again and every client test stays
    /// green while "no source, no control" quietly stops being guarded. That guarantee is the one
    /// the whole absence design rests on, and it reached QA with no test at all.
    /// </para>
    /// <para>
    /// Asserted in both directions, as the selectable-row guard above is: the call present, and
    /// the hand-written form absent. A positive assertion alone survives a second, inline copy
    /// sitting beside the call and disagreeing with it.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_import_asks_the_shared_decisions_rather_than_re_deciding_inline()
    {
        var source = RepoFiles.Read(ClosuresView);

        Assert.Contains("showsImport(", source, StringComparison.Ordinal);
        Assert.Contains("shouldAskForSource(", source, StringComparison.Ordinal);

        // The two conditions these replaced. Either one reappearing means the seam has been
        // bypassed, whether or not the shared function is still called somewhere.
        Assert.DoesNotContain("_hasHolidaySource !== true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_hasHolidaySource === undefined", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both holiday endpoints report an absent source the same way.
    /// </summary>
    /// <remarks>
    /// The server answers 404 from the preview AND the import for one reason — the site has no
    /// source — so a client that applies the rule to one of them conflates "this feature is gone"
    /// with "your closures could not be created" on the other. The rule was applied to the
    /// preview first and the import was found by QA; this asserts the class rather than the
    /// instance.
    /// </remarks>
    [Fact]
    public void Both_holiday_calls_treat_a_404_as_an_absent_source()
    {
        var source = RepoFiles.Read(ClosuresView);

        Assert.Equal(2, Occurrences(source, "previewFailure(response?.status)"));
        // `this.` prefixed, so the method's own declaration is not counted as a third call.
        Assert.Equal(2, Occurrences(source, "this.#withdrawImport()"));
    }

    /// <summary>
    /// Both holiday calls word a refusal as a refusal (public-holidays, "A refused request is
    /// reported as a refusal").
    /// </summary>
    /// <remarks>
    /// <c>previewFailure</c> and <c>previewOutcome</c> are tested in <c>holiday-fields.test.ts</c>;
    /// this pins the view's use of them, which no client test renders — the seam the 404 rule
    /// above is held at too. A 401/403 must reach <c>importRefused</c> from the preview AND from
    /// the import, and a refused preview must render in the same announced alert as a failed one,
    /// since the operator's next move depends on it just as much.
    /// </remarks>
    [Fact]
    public void Both_holiday_calls_word_a_refusal_as_a_refusal()
    {
        var source = RepoFiles.Read(ClosuresView);

        // The preview: a refusal keeps the panel and records why, rather than collapsing to the
        // source-failure flag it used to set.
        Assert.Contains("this._holidayFailure = failure;", source, StringComparison.Ordinal);
        Assert.Contains("previewOutcome(this._holidayRows, this._holidayFailure)", source, StringComparison.Ordinal);
        Assert.Contains("if (outcome === \"failed\" || outcome === \"refused\") {", source, StringComparison.Ordinal);
        Assert.Contains(
            "${this.#term(outcome === \"refused\" ? \"importRefused\" : \"importSourceFailed\")}",
            source,
            StringComparison.Ordinal);

        // The import.
        Assert.Contains(
            "this._error = this.#term(failure === \"refused\" ? \"importRefused\" : \"importFailed\");",
            source,
            StringComparison.Ordinal);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    [Fact]
    public void Closed_period_inputs_are_labelled_and_their_hints_referenced()
    {
        // Indexed ids, one set per period: label[for] must name the same index as the input.
        var source = RepoFiles.Read(ResourceEditor);

        foreach (var part in new[] { "from", "to", "note" })
        {
            Assert.Contains($"<label for=\"cp-${{index}}-{part}\">", source, StringComparison.Ordinal);
            Assert.Contains($"id=\"cp-${{index}}-{part}\"", source, StringComparison.Ordinal);
        }

        // The note's hint is the one that says it never reaches a visitor and should not hold
        // personal details — it must be read with the field, not only seen beneath it.
        //
        // ONE HINT PER PERIOD, inside that period's fieldset (tidy-2026-10). The hints used to be
        // rendered once, after every period, to keep their ids unique: each input still named
        // them, but on screen they floated under the whole section as stray text, apart from
        // every field they described. So the guard now asserts WHERE they are rendered, not
        // merely that some element carries the id.
        var fieldset = ClosedPeriodFieldset(source);

        foreach (var part in new[] { "to", "note" })
        {
            Assert.Contains($"aria-describedby=\"cp-${{index}}-{part}-hint\"", fieldset, StringComparison.Ordinal);
            Assert.Contains($"id=\"cp-${{index}}-{part}-hint\"", fieldset, StringComparison.Ordinal);

            // And the shared, unindexed hint is gone from the whole file, so a second copy of it
            // cannot return beside the per-period ones.
            Assert.DoesNotContain($"cp-{part}-hint", source, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The template one closed period renders: from its <c>&lt;fieldset</c> to its
    /// <c>&lt;/fieldset&gt;</c>, inside <c>#renderClosedPeriods</c>.
    /// </summary>
    private static string ClosedPeriodFieldset(string source)
    {
        var method = source.IndexOf("#renderClosedPeriods() {", StringComparison.Ordinal);
        Assert.True(method >= 0, "resource-editor.element.ts no longer has #renderClosedPeriods.");

        var open = source.IndexOf("<fieldset", method, StringComparison.Ordinal);
        var close = source.IndexOf("</fieldset>", open, StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open, "#renderClosedPeriods no longer renders a fieldset per period.");

        return source[open..close];
    }

    [Fact]
    public void A_closure_save_failure_is_associated_with_the_input_it_concerns()
    {
        // Announcing the failure is not enough on the project's bar: the input it concerns must
        // say it is invalid and point at the message. Each of the three inputs is wired, so a
        // failure about any of them reaches the right one.
        var source = RepoFiles.Read(ClosuresView);

        foreach (var id in new[] { "closure-date", "closure-end-date", "closure-label" })
        {
            Assert.Contains(
                $"aria-invalid=${{this._errorControl === \"{id}\" ? \"true\" : nothing}}",
                source,
                StringComparison.Ordinal);
        }

        Assert.Contains("this._errorControl = closureErrorControl(failure?.field);", source, StringComparison.Ordinal);
    }
}
