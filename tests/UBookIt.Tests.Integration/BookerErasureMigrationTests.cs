using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using UBookIt.Persistence;
using UBookIt.Tests.Integration.Support;

namespace UBookIt.Tests.Integration;

/// <summary>
/// That the erasure migration is safe to run on a database that already holds bookings.
/// </summary>
/// <remarks>
/// <para>
/// <b>This needs a database of its own.</b> The shared fixture migrates to the latest version
/// on initialize, so it can only ever answer "does a fresh schema work" — and the question
/// that matters for an upgrade is different: an existing site has rows written under the old
/// schema, where booker name and email were NOT NULL, and the migration must widen those
/// columns without touching what they hold. So this creates its own database, migrates it to
/// the migration <i>before</i> this change, writes a booking, and only then migrates up.
/// </para>
/// <para>
/// The booking is inserted with raw SQL rather than through EF, because the entity model in
/// this assembly already has the new column and would try to write it to a table that does not
/// have it yet. Raw SQL is what an existing site's rows actually look like.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class BookerErasureMigrationTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The migration immediately before the one under test.</summary>
    private const string PreviousMigration = "20260901184959_AddBookingReference";

    private const string ThisMigration = "20260904220933_AddBookerErasure";

    [Fact]
    public async Task Existing_bookings_keep_their_contact_details_across_the_migration()
    {
        fixture.EnsureAvailable();

        await using var probe = fixture.CreateContext();
        var baseConnectionString = probe.Database.GetConnectionString()!;

        var builder = new SqlConnectionStringBuilder(baseConnectionString);
        var upgradeDatabase = $"uBookItUpgrade_{Guid.NewGuid():N}";
        var masterConnectionString = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master",
        }.ConnectionString;

        await ExecuteAsync(masterConnectionString, $"CREATE DATABASE [{upgradeDatabase}]");

        try
        {
            builder.InitialCatalog = upgradeDatabase;
            var optionsBuilder = new DbContextOptionsBuilder<UBookItDbContext>();
            UBookItDbContext.ConfigureSqlServer(optionsBuilder, builder.ConnectionString);
            var options = optionsBuilder.Options;

            // 1. The schema as it stood before this change.
            await using (var old = new UBookItDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync(PreviousMigration, Ct);

                var applied = await old.Database.GetAppliedMigrationsAsync(Ct);
                Assert.Contains(PreviousMigration, applied);
                Assert.DoesNotContain(ThisMigration, applied);
            }

            // 2. A booking written under it, with NOT NULL name and email.
            var bookingId = Guid.NewGuid();
            var resourceId = Guid.NewGuid();

            await ExecuteAsync(
                builder.ConnectionString,
                $"""
                 INSERT INTO uBookItResource
                     (Id, Type, DisplayName, Description, GranularityMinutes, MinDurationMinutes,
                      MaxDurationMinutes, LeadTimeMinutes, HorizonDays, DirectlyBookable)
                 VALUES
                     ('{resourceId}', 'room', 'Upgrade Room', NULL, 30, 30, 480, 0, 365, 1);

                 INSERT INTO uBookItBooking
                     (Id, Reference, StartUtc, EndUtc, TimeZoneId, Status, CreatedUtc,
                      MemberKey, BookerName, BookerEmail, BookerPhone, ServiceId, ServiceName)
                 VALUES
                     ('{bookingId}', 'UPGRADE1',
                      '2026-09-10T10:00:00+00:00', '2026-09-10T11:00:00+00:00', 'UTC', 1,
                      '2026-09-01T00:00:00+00:00',
                      NULL, 'Existing Person', 'existing@example.com', '01234 000000', NULL, NULL);

                 INSERT INTO uBookItResourceClaim (BookingId, ResourceId)
                 VALUES ('{bookingId}', '{resourceId}');
                 """);

            // 3. The upgrade.
            await using (var upgraded = new UBookItDbContext(options))
            {
                await upgraded.Database.MigrateAsync(Ct);

                Assert.Contains(ThisMigration, await upgraded.Database.GetAppliedMigrationsAsync(Ct));
            }

            // 4. The row is untouched, and reads as not-erased.
            await using (var after = new UBookItDbContext(options))
            {
                var row = await after.Bookings.SingleAsync(b => b.Id == bookingId, Ct);

                // The migration widens; it must not blank. A back-fill writing empty strings
                // would satisfy "the column is nullable now" and destroy every existing site's
                // contact details on upgrade.
                Assert.Equal("Existing Person", row.BookerName);
                Assert.Equal("existing@example.com", row.BookerEmail);
                Assert.Equal("01234 000000", row.BookerPhone);
                Assert.Equal("UPGRADE1", row.Reference);

                // And a booking nobody erased has no erasure instant — the correct reading of
                // every row that existed before this feature did.
                Assert.Null(row.BookerErasedUtc);
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(
                masterConnectionString,
                $"ALTER DATABASE [{upgradeDatabase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
                + $"DROP DATABASE [{upgradeDatabase}]");
        }
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
