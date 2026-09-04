using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// That the backoffice bookings list actually renders the reference.
/// <para>
/// <b>A source-level guard, and it is second choice.</b> The client suite is deliberately
/// pure-module — there is no DOM environment in this repository, which is a recorded
/// obligation of its own — so nothing can render the Lit element and assert over the result.
/// The unit tests cover <c>bookingReference()</c>, but a formatter nothing calls is a
/// formatter that formats nothing: deleting the cell from the template left seven header cells
/// over six body cells, a visibly broken table, and passed all 116 client tests.
/// </para>
/// <para>
/// What this can prove is that the template still calls the formatter, and that the table is
/// internally coherent. What it cannot prove is that the browser draws it — for that there is
/// the record of a person looking at the screen, which is how the version defect and the
/// section-access gap were both found. When a DOM environment arrives this should be replaced
/// by a real render.
/// </para>
/// </summary>
public class BookingsViewReferenceTests
{
    private const string Element = "src/UBookIt.Backoffice/Client/src/section/bookings-list.element.ts";

    [Fact]
    public void The_row_template_renders_the_reference()
    {
        var source = RepoFiles.Read(Element);

        Assert.Contains("bookingReference(booking)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_table_has_one_body_cell_for_every_header()
    {
        // This is the assertion that catches the cell being deleted, and it catches a column
        // being added without a header just as well. A count is a blunt instrument, but the
        // failure it guards — a table whose columns and headings have silently drifted apart —
        // is exactly the kind that looks fine in code review and wrong on screen.
        var source = RepoFiles.Read(Element);

        var headers = Occurrences(source, "<uui-table-head-cell");
        var cells = Occurrences(source, "<uui-table-cell");

        Assert.True(headers > 0, "No header cells found — the template has moved and this guard is measuring nothing.");
        Assert.Equal(headers, cells);

        // Known limit, stated rather than discovered later: an empty-state or loading row
        // carrying a spanning cell would rebalance this arithmetic and let a deleted column
        // through. The guard is a stand-in until a DOM environment exists and can render the
        // element; if a spanning row is added, this needs to count per-row rather than
        // per-file, and the assertion above is what should force that conversation.
    }

    [Fact]
    public void The_reference_is_the_first_column()
    {
        // `booking-management` makes this a bolded SHALL with a reason: it is the column an
        // operator scans while somebody reads a reference out. Nothing asserted it — moving
        // the column to the end passed all 1798 tests and all 116 client tests, because the
        // guards above check presence and header/cell parity, and both are position-blind.
        var source = RepoFiles.Read(Element);

        var firstHeader = source.IndexOf("<uui-table-head-cell", StringComparison.Ordinal);
        var firstCell = source.IndexOf("<uui-table-cell", StringComparison.Ordinal);

        Assert.True(firstHeader > 0 && firstCell > 0, "The table markup has moved; this guard is measuring nothing.");

        // Guarded, because a self-closing `<uui-table-head-cell … />` has no closing tag and the
        // slice would throw ArgumentOutOfRange — fail-closed, but with a diagnostic pointing at
        // the wrong thing.
        var headerEnd = source.IndexOf("</uui-table-head-cell>", firstHeader, StringComparison.Ordinal);
        var cellEnd = source.IndexOf("</uui-table-cell>", firstCell, StringComparison.Ordinal);

        Assert.True(
            headerEnd > firstHeader && cellEnd > firstCell,
            "The first header or body cell has no closing tag — the markup shape has changed and this "
            + "guard can no longer tell which column is first.");

        var headerText = source[firstHeader..headerEnd];
        var cellText = source[firstCell..cellEnd];

        Assert.Contains("\"reference\"", headerText, StringComparison.Ordinal);
        Assert.Contains("bookingReference(booking)", cellText, StringComparison.Ordinal);
    }

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = 0;

        while ((at = haystack.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += needle.Length;
        }

        return count;
    }

    // ------------------------------------------------- withheld booker details

    [Fact]
    public void The_booker_cell_states_the_absence_rather_than_leaving_it_blank()
    {
        // A blank cell reads as data that failed to load. Here it would read as a defect in the
        // package — which is precisely the support ticket this feature is trying not to
        // generate — so the cell says something, on the same terms as "Booked directly".
        //
        // WIRING ONLY, and deliberately so. This used to grep for `bookerWithheld(booking)` and
        // was the only thing watching the cell — which meant swapping the two arms of the
        // template's ternary changed no token and passed every guard, rendering an empty cell
        // for a withheld row and "Contact details hidden" over a name. The derivation now lives
        // in `booking-rows.bookerCell`, where the client suite asserts the MAPPING, and the
        // template's arms cannot be swapped without a type error. What is left for a source
        // grep is that the element still calls it.
        var source = RepoFiles.Read(Element);

        Assert.Contains("bookerCell(booking", source, StringComparison.Ordinal);
        Assert.Contains("bookerHidden", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_page_explains_why_details_are_hidden()
    {
        // The note is derived from the rows rather than from a page-level flag or a second
        // question to Umbraco, so it cannot appear over a table showing every name, nor be
        // missing from one that hides them.
        //
        // WIRING ONLY — see the note above. `bookerNote` returns a list of zero or one and the
        // template renders it with `.map()`, so there is no condition here to invert; the
        // polarity is asserted in the client suite, against the function. It was invertible
        // while this grepped for `anyBookerWithheld(this._items)`, and inverting it showed the
        // explanation exactly when nothing was withheld, with every guard green.
        var source = RepoFiles.Read(Element);

        Assert.Contains("bookerNote(this._items", source, StringComparison.Ordinal);
        Assert.Contains("bookerHiddenNote", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_explanation_is_announced_rather_than_only_rendered()
    {
        // role=status, not role=alert: this is a standing explanation of what the reader is
        // looking at, where the alert above it reports something that just went wrong. An
        // assertive role would talk over the operator on every page.
        var source = RepoFiles.Read(Element);

        // Anchored on the note's own class rather than on the term key. The term is now read
        // before the element is opened — `bookerNote(this._items, this.#term("bookerHiddenNote"))
        // .map(note => html`<p role="status" …>`)` — so looking backwards from the key finds the
        // call, not the tag. The class sits on the element that carries the note and nowhere
        // else, which is the anchor that survives the render being restructured.
        var note = source.IndexOf("class=\"withheld-note\"", StringComparison.Ordinal);

        Assert.True(note > 0, "The note's element has moved and this guard is measuring nothing.");

        var open = source.LastIndexOf('<', note);

        Assert.True(open > 0, "The note is no longer inside an element.");

        Assert.Contains("role=\"status\"", source[open..note], StringComparison.Ordinal);
    }

    [Fact]
    public void The_view_does_not_ask_umbraco_whether_details_may_be_shown()
    {
        // The response already says what happened. A second source could answer "yes, you may"
        // over a row that was withheld anyway, leaving a blank cell and no explanation — the
        // exact failure the note exists to prevent, arriving by the route meant to prevent it.
        var source = RepoFiles.Read(Element);

        Assert.DoesNotContain("hasAccessToSensitiveData", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentUser", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_booking_is_identified_to_the_operator_by_its_reference()
    {
        // Both places the view names a particular booking: the per-row control's accessible
        // name, and the confirmation. Unconditionally, for every operator — a branch that named
        // the booker where it could would be two behaviours to test for no gain, and would
        // render "booking for undefined" for anyone without sensitive-data access.
        var source = RepoFiles.Read(Element);

        Assert.Contains(
            "label=\"${this.#term(\"cancel\")} ${bookingReference(booking)}\"",
            source,
            StringComparison.Ordinal);

        Assert.Contains("confirmCancelContent", source, StringComparison.Ordinal);

        var confirm = source.IndexOf("confirmCancelContent", StringComparison.Ordinal);
        var afterConfirm = source[confirm..Math.Min(source.Length, confirm + 200)];

        Assert.Contains("bookingReference(booking)", afterConfirm, StringComparison.Ordinal);

        // And the booker is not what identifies it anywhere.
        Assert.DoesNotContain("booking.bookerName", source, StringComparison.Ordinal);
    }
}
