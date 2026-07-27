using Microsoft.EntityFrameworkCore;
using UBookIt.Persistence;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public class MigrationTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Fresh_database_has_all_tables_and_the_private_history_table()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var tables = await context.Database
            .SqlQuery<string>($"SELECT TABLE_NAME AS [Value] FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync(Ct);

        Assert.Contains("uBookItResource", tables);
        Assert.Contains("uBookItResourceOpenHours", tables);
        Assert.Contains("uBookItResourceException", tables);
        Assert.Contains("uBookItBooking", tables);
        Assert.Contains("uBookItResourceClaim", tables);
        Assert.Contains(UBookItDbContext.MigrationsHistoryTableName, tables);
        Assert.DoesNotContain("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Rerunning_migrations_is_a_no_op()
    {
        fixture.EnsureAvailable();

        await using var context = fixture.CreateContext();
        var pending = await context.Database.GetPendingMigrationsAsync(Ct);

        Assert.Empty(pending);
        await context.Database.MigrateAsync(Ct); // must not throw
    }
}
