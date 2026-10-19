using Microsoft.EntityFrameworkCore.Migrations.Operations;
using UBookIt.Persistence.Migrations;

namespace UBookIt.Tests;

/// <summary>
/// The service-booker-instructions migration adds one nullable column and nothing else —
/// asserted from the operations it emits, on the terms
/// <see cref="ClosureDateRangesMigrationShapeTests"/> set, never by scanning its text.
/// </summary>
public class ServiceBookerInstructionsMigrationShapeTests
{
    [Fact]
    public void The_migration_adds_exactly_one_nullable_column_to_the_service_table()
    {
        var operation = Assert.Single(new AddServiceBookerInstructions().UpOperations);
        var added = Assert.IsType<AddColumnOperation>(operation);

        Assert.Equal("uBookItService", added.Table);
        Assert.Equal("BookerInstructions", added.Name);
        Assert.True(added.IsNullable);
        Assert.Equal(2000, added.MaxLength);
        Assert.Equal("nvarchar(2000)", added.ColumnType);
        Assert.Null(added.DefaultValue);
        Assert.Null(added.DefaultValueSql);
    }
}
