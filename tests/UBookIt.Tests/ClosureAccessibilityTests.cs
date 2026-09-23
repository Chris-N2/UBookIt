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
        // row: the accessible name has to carry the date and the label, not just the verb.
        var source = RepoFiles.Read(ClosuresView);

        // No colon: the house form is "Edit <thing>", as the Resources list already does.
        Assert.Contains(
            "label=\"${this.#term(\"edit\")} ${closure.date} ${closure.label}\"",
            source,
            StringComparison.Ordinal);

        Assert.Contains(
            "label=\"${this.#term(\"delete\")} ${closure.date} ${closure.label}\"",
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
            "label=\"${this.#term(\"closureOpenAnyway\")}: ${closure.date} ${closure.label}\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Native_inputs_in_the_closures_view_are_labelled()
    {
        // `label[for]` inside the same shadow root, which is what works here — a uui-label is
        // not a <label> and cannot pierce a component's shadow root.
        var source = RepoFiles.Read(ClosuresView);

        foreach (var id in new[] { "closure-date", "closure-label" })
        {
            Assert.Contains($"<label for=\"{id}\">", source, StringComparison.Ordinal);
            Assert.Contains($"id=\"{id}\"", source, StringComparison.Ordinal);
        }

        // The hint is referenced from the input, not merely rendered under it.
        Assert.Contains("aria-describedby=\"closure-label-hint\"", source, StringComparison.Ordinal);
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
}
