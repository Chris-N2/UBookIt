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
