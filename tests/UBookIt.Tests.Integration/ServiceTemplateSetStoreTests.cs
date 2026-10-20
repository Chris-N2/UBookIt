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
/// A service's email template set against real SQL Server (service-email-templates: the
/// <c>persistence</c> delta) — the round-trip, <c>NULL</c> for none, normalisation on read, an
/// invalid stored value reading as no set, and an upgrade of a database that already holds
/// services and bookings.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class ServiceTemplateSetStoreTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Service NewService(string? templateSet = null, string? instructions = null)
        => Service.Create(
                "Online consultation",
                ServiceDuration.Fixed(TimeSpan.FromMinutes(60)).Value,
                [new ServiceRole("consultant", 1)]).Value
            .WithBookerInstructions(instructions).Value
            .WithTemplateSet(templateSet).Value;

    [Fact]
    public async Task A_template_set_round_trips_in_a_fresh_context()
    {
        fixture.EnsureAvailable();
        var service = NewService("remote", "Join on Teams");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using var read = fixture.CreateContext();
        var fetched = await new SqlServiceStore(read).GetAsync(service.Id, Ct);

        Assert.Equal("remote", fetched!.TemplateSet);
        Assert.Equal("Join on Teams", fetched.BookerInstructions);
    }

    [Fact]
    public async Task An_update_changes_and_then_removes_the_set()
    {
        fixture.EnsureAvailable();
        var service = NewService("in-person");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        await using (var context = fixture.CreateContext())
        {
            var changed = service.WithTemplateSet("remote").Value;
            Assert.True((await new SqlServiceManagementStore(context).UpdateAsync(changed, Ct)).Succeeded);
        }

        await using (var read = fixture.CreateContext())
        {
            Assert.Equal("remote", (await new SqlServiceStore(read).GetAsync(service.Id, Ct))!.TemplateSet);
        }

        await using (var context = fixture.CreateContext())
        {
            var removed = service.WithTemplateSet(null).Value;
            Assert.True((await new SqlServiceManagementStore(context).UpdateAsync(removed, Ct)).Succeeded);
        }

        await using var final = fixture.CreateContext();
        Assert.Null((await new SqlServiceStore(final).GetAsync(service.Id, Ct))!.TemplateSet);
        Assert.Null(await StoredSetAsync(final, service.Id));
    }

    [Fact]
    public async Task No_set_is_stored_as_NULL_not_an_empty_string()
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
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM uBookItService WHERE Id = {service.Id} AND TemplateSet IS NULL")
            .SingleAsync(Ct);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task A_stored_value_written_outside_the_package_is_normalised_on_read()
    {
        fixture.EnsureAvailable();
        var service = NewService();

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
            await SetStoredAsync(context, service.Id, " Remote ");
        }

        await using var read = fixture.CreateContext();

        Assert.Equal("remote", (await new SqlServiceStore(read).GetAsync(service.Id, Ct))!.TemplateSet);
    }

    /// <summary>
    /// The read must not fail, and the service must still load whole — compared member by member
    /// with the same service read before the bad value was written, so a mapper that dropped
    /// anything else on this path turns it red.
    /// </summary>
    [Fact]
    public async Task An_invalid_stored_value_reads_as_no_set_and_the_service_still_loads()
    {
        fixture.EnsureAvailable();
        var service = NewService(instructions: "Directions");

        await using (var context = fixture.CreateContext())
        {
            Assert.True((await new SqlServiceManagementStore(context).CreateAsync(service, Ct)).Succeeded);
        }

        Service before;
        await using (var read = fixture.CreateContext())
        {
            before = (await new SqlServiceStore(read).GetAsync(service.Id, Ct))!;
            await SetStoredAsync(read, service.Id, "../x");
        }

        await using var after = fixture.CreateContext();
        var fetched = await new SqlServiceStore(after).GetAsync(service.Id, Ct);

        Assert.NotNull(fetched);
        Assert.Null(fetched.TemplateSet);
        Assert.Equal(before.Name, fetched.Name);
        Assert.Equal(before.Duration, fetched.Duration);
        Assert.Equal(before.Roles, fetched.Roles);
        Assert.Equal("Directions", fetched.BookerInstructions);

        // The list read goes through the same mapper, and must not fail on the row either.
        // Paged, because the fixture's database is shared and the store caps a page.
        var store = new SqlServiceStore(after);
        Service? listed = null;
        for (var skip = 0; listed is null; skip += 500)
        {
            var page = await store.ListAsync(skip, 500, Ct);
            Assert.NotEmpty(page.Items);
            listed = page.Items.SingleOrDefault(s => s.Id == service.Id);
        }

        Assert.Null(listed.TemplateSet);
    }

    private static Task<string?> StoredSetAsync(UBookItDbContext context, Guid id)
        => context.Database
            .SqlQuery<string?>($"SELECT TemplateSet AS [Value] FROM uBookItService WHERE Id = {id}")
            .SingleAsync(Ct);

    private static Task SetStoredAsync(UBookItDbContext context, Guid id, string value)
        => context.Database.ExecuteSqlAsync($"UPDATE uBookItService SET TemplateSet = {value} WHERE Id = {id}", Ct);

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
    public async Task The_migration_keeps_every_existing_row_and_gives_each_service_no_set()
    {
        fixture.EnsureAvailable();

        const string previous = "20261019195550_AddServiceBookerInstructions";
        const string current = "20261020171452_AddServiceTemplateSet";

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
                 INSERT INTO uBookItService (Id, Name, DurationKind, MinDurationMinutes, MaxDurationMinutes, BookerInstructions)
                 VALUES ('{online}', 'Online consultation', 'Fixed', 60, 60, 'Join on Teams'),
                        ('{inPerson}', 'In-person consultation', 'Variable', 30, NULL, NULL);

                 INSERT INTO uBookItServiceRole (ServiceId, ResourceType, Count, VisitorSelectable)
                 VALUES ('{online}', 'consultant', 1, 0),
                        ('{inPerson}', 'consultant', 1, 1);

                 INSERT INTO uBookItBooking
                     (Id, Reference, Status, StartUtc, EndUtc, CreatedUtc, TimeZoneId,
                      BookerName, BookerEmail, BookerPhone, ServiceId, ServiceName)
                 VALUES ('{booking}', 'UPGRADE2', 1, '2027-01-11T10:00:00+00:00',
                         '2027-01-11T11:00:00+00:00', '2026-10-20T09:00:00+00:00', 'Europe/London',
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

            // The column exists once, nullable, sized to the domain's limit, and holds NULL throughout.
            var added = await ScalarAsync(
                builder.ConnectionString,
                """
                SELECT CONCAT(COUNT(*), ':', MAX(IS_NULLABLE), ':', MAX(CHARACTER_MAXIMUM_LENGTH))
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'uBookItService' AND COLUMN_NAME = 'TemplateSet'
                """);
            Assert.Equal("1:YES:64", added);
            Assert.Equal(
                "0",
                await ScalarAsync(builder.ConnectionString, "SELECT COUNT(*) FROM uBookItService WHERE TemplateSet IS NOT NULL"));

            // And no index names it: several services may share a set.
            Assert.Equal(
                "0",
                await ScalarAsync(
                    builder.ConnectionString,
                    """
                    SELECT COUNT(*) FROM sys.index_columns ic
                    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                    WHERE ic.object_id = OBJECT_ID('uBookItService') AND c.name = 'TemplateSet'
                    """));

            await using var after = new UBookItDbContext(options);
            var services = await new SqlServiceStore(after).ListAsync(0, 10, Ct);

            Assert.Equal(2, services.Total);
            Assert.All(services.Items, s => Assert.Null(s.TemplateSet));
            Assert.Equal("Join on Teams", Assert.Single(services.Items, s => s.Id == online).BookerInstructions);
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
