using System.Text.RegularExpressions;

namespace UBookIt.Tests;

/// <summary>
/// The persistence spec's "Store implementations honour Core semantics", for the move write:
/// the three writes touch disjoint columns, and the move's status condition is inside its
/// statement rather than a read before it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A source-level guard, and named as one.</b> The integration suite asserts the shape of
/// what is SENT to SQL Server; this asserts the shape of what is WRITTEN in the store, so the
/// rule is visible without a database and fails on the refactor rather than on the next CI
/// run. Both exist because each catches what the other cannot: the source guard cannot see a
/// query EF composes, and the wire guard cannot run where there is no server.
/// </para>
/// <para>
/// The source is unwrapped before matching, because this repository's lines wrap at ~100
/// columns and every string guard here has at one time been defeated by a line break.
/// </para>
/// </remarks>
public class MoveWriteShapeTests
{
    private static string Source() => Support.RepoFiles.Read("src/UBookIt.Persistence/Stores/SqlBookingStore.cs");

    private static string Unwrapped(string source) => Regex.Replace(source, @"\s+", " ");

    /// <summary>The body of one public method of the store, from its signature to the next one.</summary>
    private static string MethodBody(string source, string signatureStart)
    {
        var start = source.IndexOf(signatureStart, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{signatureStart}' in SqlBookingStore.cs.");

        var next = Regex.Match(source[(start + signatureStart.Length)..], @"\n    public ");
        var end = next.Success ? start + signatureStart.Length + next.Index : source.Length;
        return source[start..end];
    }

    [Fact]
    public void The_move_write_is_a_single_conditional_update_and_never_loads_the_row()
    {
        var move = Unwrapped(MethodBody(Source(), "public async Task<DomainResult> MoveAsync("));

        // The write is an ExecuteUpdate — one statement — and its predicate carries the status.
        Assert.Contains("ExecuteUpdateAsync", move, StringComparison.Ordinal);
        Assert.Matches(@"Where\(b => b\.Id == bookingId && permitted\.Contains\(b\.Status\)\)", move);

        // A load-then-save would be any of these. None may appear on the move path.
        Assert.DoesNotContain("SaveChangesAsync", move, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstAsync(", move, StringComparison.Ordinal);
        Assert.DoesNotContain("FirstOrDefaultAsync(", move, StringComparison.Ordinal);
        Assert.DoesNotContain("SingleAsync(", move, StringComparison.Ordinal);
    }

    [Fact]
    public void The_three_writes_touch_disjoint_columns()
    {
        var source = Source();
        var move = Unwrapped(MethodBody(source, "public async Task<DomainResult> MoveAsync("));
        var status = Unwrapped(MethodBody(source, "public async Task UpdateAsync("));
        var erase = Unwrapped(MethodBody(source, "public async Task<bool> EraseBookerAsync("));

        // The move sets the interval columns and only those.
        Assert.Contains("SetProperty(b => b.StartUtc", move, StringComparison.Ordinal);
        Assert.Contains("SetProperty(b => b.EndUtc", move, StringComparison.Ordinal);
        Assert.Contains("SetProperty(b => b.TimeZoneId", move, StringComparison.Ordinal);
        Assert.DoesNotContain("SetProperty(b => b.Status", move, StringComparison.Ordinal);
        Assert.DoesNotContain("SetProperty(b => b.Booker", move, StringComparison.Ordinal);
        Assert.DoesNotContain("SetProperty(b => b.MemberKey", move, StringComparison.Ordinal);

        // The status write assigns the status and no interval column.
        Assert.Contains("row.Status =", status, StringComparison.Ordinal);
        Assert.DoesNotContain("row.StartUtc", status, StringComparison.Ordinal);
        Assert.DoesNotContain("row.EndUtc", status, StringComparison.Ordinal);
        Assert.DoesNotContain("row.TimeZoneId", status, StringComparison.Ordinal);

        // The erasure write touches no interval column and no status.
        Assert.DoesNotContain("StartUtc", erase, StringComparison.Ordinal);
        Assert.DoesNotContain("EndUtc", erase, StringComparison.Ordinal);
        Assert.DoesNotContain("SetProperty(b => b.Status", erase, StringComparison.Ordinal);
    }

    [Fact]
    public void The_conflict_check_excludes_the_booking_being_moved()
    {
        var move = Unwrapped(MethodBody(Source(), "public async Task<DomainResult> MoveAsync("));

        Assert.Contains("existing.Id != bookingId", move, StringComparison.Ordinal);
    }
}
