using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using UBookIt.Persistence.Migrations;

namespace UBookIt.Tests;

/// <summary>
/// The closure-date-ranges migration adds and fills, and alters or drops nothing — asserted
/// from the operations it emits, on the terms <see cref="SiteClosureMigrationShapeTests"/>
/// set for the migration before it.
/// </summary>
/// <remarks>
/// <b>The fill is part of the shape.</b> The new column takes a placeholder default, and an
/// existing closure left with it would end before it starts. So the test does not only check
/// that nothing is altered; it checks that the one statement turning every existing row into a
/// range of its own date is there, and is the statement it should be.
/// </remarks>
public class ClosureDateRangesMigrationShapeTests
{
    private static IReadOnlyList<MigrationOperation> UpOperations()
        => new ClosureDateRanges().UpOperations;

    [Fact]
    public void The_migration_only_adds_and_fills()
    {
        var kinds = UpOperations().Select(o => o.GetType().Name).Distinct().Order(StringComparer.Ordinal);

        Assert.Equal(
            new[] { "AddColumnOperation", "CreateIndexOperation", "CreateTableOperation", "SqlOperation" },
            kinds);
    }

    [Fact]
    public void The_one_added_column_is_the_closure_end_date()
    {
        var added = Assert.Single(UpOperations().OfType<AddColumnOperation>());

        Assert.Equal("uBookItSiteClosure", added.Table);
        Assert.Equal("EndDate", added.Name);
        Assert.False(added.IsNullable);
    }

    [Fact]
    public void Every_existing_closure_is_filled_as_a_range_of_its_own_date()
    {
        // Ordered after the column it fills, or the statement would name a column that does
        // not exist yet.
        var operations = UpOperations().ToList();
        var fill = Assert.Single(operations.OfType<SqlOperation>());

        Assert.Equal("UPDATE [uBookItSiteClosure] SET [EndDate] = [Date];", fill.Sql);
        Assert.True(
            operations.IndexOf(fill) > operations.FindIndex(o => o is AddColumnOperation),
            "The fill must run after the column is added.");
    }

    [Fact]
    public void It_creates_exactly_the_closed_period_table_and_its_index()
    {
        var created = Assert.Single(UpOperations().OfType<CreateTableOperation>());
        var index = Assert.Single(UpOperations().OfType<CreateIndexOperation>());

        Assert.Equal("uBookItResourceClosedPeriod", created.Name);

        // On the new table only: an index added to an EXISTING table would be an alteration.
        Assert.Equal("uBookItResourceClosedPeriod", index.Table);
        Assert.Equal(new[] { "ResourceId" }, index.Columns);
    }
}
