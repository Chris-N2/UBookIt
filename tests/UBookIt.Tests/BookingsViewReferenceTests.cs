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
}
