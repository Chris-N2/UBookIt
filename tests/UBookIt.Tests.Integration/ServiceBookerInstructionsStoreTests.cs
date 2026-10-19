using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UBookIt.Core.Services;
using UBookIt.Persistence;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// A service's instructions for the booker against real SQL Server (service-booker-instructions:
/// the <c>persistence</c> delta) — the round-trip, <c>NULL</c> for none, and an upgrade of a
/// database that already holds services and bookings.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ServiceBookerInstructionsStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Service NewService(string? instructions = null)
        => Service.Create(
                "Online consultation",
                ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
                [new ServiceRole("consultant", 1)]).Value
            .WithBookerInstructions(instructions).Value;

    [Fact]
    public async Task Instructions_over_two_lines_round_trip_in_a_fresh_context()
    {
        fixture.EnsureAvailable();
        const string text = "Join on Teams:\r\nhttps://example.test/meet";
        var service = NewService(text);

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.Equal(text, fetched!.BookerInstructions);
    }

    [Fact]
    public async Task An_update_changes_and_then_removes_the_instructions()
    {
        fixture.EnsureAvailable();
        var service = NewService("Old link");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var changed = service.WithBookerInstructions("New link").Value;
            Assert.True((await new SqlServiceManagementStore(context).UpdateAsync(changed, Ct)).Succeeded);
        }

        await using (var read = fixture.CreateContext())
        {
            Assert.Equal("New link", (await new SqlServiceStore(read).GetAsync(service.Id, Ct))!.BookerInstructions);
        }

        await using (var context = fixture.CreateContext())
        {
            var removed = service.WithBookerInstructions(null).Value;
            Assert.True((await new SqlServiceManagementStore(context).UpdateAsync(removed, Ct)).Succeeded);
        }

        await using var final = fixture.CreateContext();
        Assert.Null((await new SqlServiceStore(final).GetAsync(service.Id, Ct))!.BookerInstructions);
        Assert.Null(await StoredInstructionsAsync(final, service.Id));
    }

    [Fact]
    public async Task No_instructions_are_stored_as_NULL_not_an_empty_string()
    {
        fixture.EnsureAvailable();
        var service = NewService("   ");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();

        // Read from the column itself: the domain would map an empty string to none as well, so
        // only the raw value can tell NULL from ''.
        var count = await read.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM uBookItService WHERE Id = {service.Id} AND BookerInstructions IS NULL")
            .SingleAsync(Ct);

        Assert.Equal(1, count);
    }

    private static Task<string?> StoredInstructionsAsync(UBookItDbContext context, Guid id)
        => context.Database
            .SqlQuery<string?>($"SELECT BookerInstructions AS [Value] FROM uBookItService WHERE Id = {id}")
            .SingleAsync(Ct);

    // ── The migration, on a database that already holds services and bookings ──────────────

    /// <summary>
    /// On the terms <see cref="ClosureDateRangesStoreTests"/> set: a database of its own, migrated
    /// to the version before, written to with raw SQL, then upgraded.
    /// </summary>
    /// <remarks>
    /// "Unchanged" is asserted over every column each table had BEFORE the upgrade, read from the
    /// schema rather than listed here — so a migration that rewrote any of them, not only the ones
    /// this test thought to name, turns it red.
    /// </remarks>
    [Fact]
    public async Task The_migration_keeps_every_existing_row_and_gives_each_service_no_instructions()
    {
        fixture.EnsureAvailable();

        const string previous = "20261011182653_ClosureDateRanges";
        const string current = "20261019195550_AddServiceBookerInstructions";

        await using var probe = fixture.CreateContext();
        var baseConnection = probe.Database.GetConnectionString()!;
        var database = $"uBookItUpgrade_{Guid.NewGuid():N}";
        var master = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = "master" }.ConnectionString;
        var builder = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = database };

        await ExecuteAsync(master, $"CREATE DATABASE [{database}]");

        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<UBookItDbContext>();
            UBookItDbContext.ConfigureSqlServer(optionsBuilder, builder.ConnectionString);
            var options = optionsBuilder.Options;

            await using (var old = new UBookItDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync(previous, Ct);
                Assert.DoesNotContain(current, await old.Database.GetAppliedMigrationsAsync(Ct));
            }

            var online = Guid.NewGuid();
            var inPerson = Guid.NewGuid();
            var booking = Guid.NewGuid();

            await ExecuteAsync(
                builder.ConnectionString,
                $"""
                 INSERT INTO uBookItService (Id, Name, DurationKind, MinDurationMinutes, MaxDurationMinutes)
                 VALUES ('{online}', 'Online consultation', 'Fixed', 60, 60),
                        ('{inPerson}', 'In-person consultation', 'Variable', 30, NULL);

                 INSERT INTO uBookItServiceRole (ServiceId, ResourceType, Count, VisitorSelectable)
                 VALUES ('{online}', 'consultant', 1, 0),
                        ('{inPerson}', 'consultant', 1, 1);

                 INSERT INTO uBookItBooking
                     (Id, Reference, Status, StartUtc, EndUtc, CreatedUtc, TimeZoneId,
                      BookerName, BookerEmail, BookerPhone, ServiceId, ServiceName)
                 VALUES ('{booking}', 'UPGRADE1', 1, '2027-01-11T10:00:00+00:00',
                         '2027-01-11T11:00:00+00:00', '2026-10-19T09:00:00+00:00', 'Europe/London',
                         'Upgrade Booker', 'upgrade@example.test', NULL, '{online}', 'Online consultation');
                 """);

            var tables = new[] { "uBookItService", "uBookItServiceRole", "uBookItBooking" };
            var before = new Dictionary<string, (string Columns, string Rows)>();
            foreach (var table in tables)
            {
                var columns = await ColumnListAsync(builder.ConnectionString, table);
                before[table] = (columns, await RowsAsJsonAsync(builder.ConnectionString, table, columns));
            }

            await using (var upgraded = new UBookItDbContext(options))
            {
                await upgraded.Database.MigrateAsync(Ct);
                Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync(Ct));
            }

            foreach (var table in tables)
            {
                Assert.Equal(before[table].Rows, await RowsAsJsonAsync(builder.ConnectionString, table, before[table].Columns));
            }

            // The column exists once, nullable, sized to the domain's limit.
            var added = await ScalarAsync(
                builder.ConnectionString,
                """
                SELECT CONCAT(COUNT(*), ':', MAX(IS_NULLABLE), ':', MAX(CHARACTER_MAXIMUM_LENGTH))
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'uBookItService' AND COLUMN_NAME = 'BookerInstructions'
                """);
            Assert.Equal("1:YES:2000", added);

            await using var after = new UBookItDbContext(options);
            var services = await new SqlServiceStore(after).ListAsync(0, 10, Ct);

            Assert.Equal(2, services.Total);
            Assert.All(services.Items, s => Assert.Null(s.BookerInstructions));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(
                master,
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    private static Task<string> ColumnListAsync(string connectionString, string table)
        => ScalarAsync(
            connectionString,
            $"""
             SELECT STRING_AGG(QUOTENAME(COLUMN_NAME), ',') WITHIN GROUP (ORDER BY COLUMN_NAME)
             FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}'
             """);

    private static Task<string> RowsAsJsonAsync(string connectionString, string table, string columns)
        => ScalarAsync(
            connectionString,
            $"SELECT (SELECT {columns} FROM [{table}] ORDER BY {columns} FOR JSON PATH, INCLUDE_NULL_VALUES)");

    private static async Task<string> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync(Ct))!;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }
}
