using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UBookIt.Core.Stores;
using UBookIt.Persistence;
using UBookIt.Persistence.Composing;
using UBookIt.Persistence.Stores;
using UBookIt.Tests.Integration.Support;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Exceptions;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace UBookIt.Tests.Integration;

/// <summary>
/// <see cref="IUBookItStorageReadyHandler"/>s are called by the migration runner itself, after it
/// has migrated (service-email-templates, task 7.2) — driven through
/// <see cref="RunUBookItMigrations.HandleAsync"/> against a real database.
/// </summary>
/// <remarks>
/// <b>The case that failed live</b>: the first start after an upgrade, with a migration pending
/// that adds a column the handler's read selects. As a handler for Umbraco's started notification
/// the template-set report ran before the migrations and its read failed on the missing column.
/// Here the database is stopped one migration short and the handler reads services; it can only
/// succeed if it is called after the migration.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class StorageReadyHandlerTests(SqlServerFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class CapturingLogger : ILogger<RunUBookItMigrations>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class RunningRuntimeState : IRuntimeState
    {
        public RuntimeLevel Level => RuntimeLevel.Run;

        public string? CurrentMigrationState => null;

        public string? FinalMigrationState => null;

        public RuntimeLevelReason Reason => RuntimeLevelReason.Run;

        public Umbraco.Cms.Core.Semver.SemVersion SemanticVersion => null!;

        public Version Version => new(17, 0, 0);

        public string VersionComment => string.Empty;

        public BootFailedException? BootFailedException => null;

        public IReadOnlyDictionary<string, object> StartupState => new Dictionary<string, object>();

        public void Configure(RuntimeLevel level, RuntimeLevelReason reason, Exception? bootFailedException = null)
            => throw new NotSupportedException("The handler only reads the level.");

        public void DetermineRuntimeLevel()
            => throw new NotSupportedException("The handler only reads the level.");
    }

    /// <summary>Reads every service — selecting the column the pending migration adds — and records the order of events.</summary>
    private sealed class ReadingHandler(UBookItDbContext context, List<string> events, string name) : IUBookItStorageReadyHandler
    {
        public async Task OnStorageReadyAsync(CancellationToken cancellationToken)
        {
            var page = await new SqlServiceStore(context).ListAsync(0, 50, cancellationToken);
            events.Add($"{name} read {page.Total} service(s)");
        }
    }

    private sealed class ThrowingHandler(List<string> events) : IUBookItStorageReadyHandler
    {
        public Task OnStorageReadyAsync(CancellationToken cancellationToken)
        {
            events.Add("throwing handler called");
            throw new InvalidOperationException("A handler that fails.");
        }
    }

    [Fact]
    public async Task Handlers_run_after_the_pending_migration_and_one_failing_does_not_stop_the_rest()
    {
        fixture.EnsureAvailable();

        const string previous = "20261019195550_AddServiceBookerInstructions";

        await using var probe = fixture.CreateContext();
        var database = $"uBookItReady_{Guid.NewGuid():N}";
        var baseConnection = probe.Database.GetConnectionString()!;
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
            }

            var serviceId = Guid.NewGuid();
            await ExecuteAsync(
                builder.ConnectionString,
                $"""
                 INSERT INTO uBookItService (Id, Name, DurationKind, MinDurationMinutes, MaxDurationMinutes)
                 VALUES ('{serviceId}', 'Online consultation', 'Fixed', 60, 60);

                 INSERT INTO uBookItServiceRole (ServiceId, ResourceType, Count, VisitorSelectable)
                 VALUES ('{serviceId}', 'consultant', 1, 0);
                 """);

            await using var context = new UBookItDbContext(options);
            Assert.NotEmpty(await context.Database.GetPendingMigrationsAsync(Ct));

            var events = new List<string>();
            var logger = new CapturingLogger();

            await new RunUBookItMigrations(
                    context,
                    new RunningRuntimeState(),
                    new ConfigurationBuilder().Build(),
                    new SqlSettingsStore(context, TimeProvider.System),
                    logger,
                    [new ReadingHandler(context, events, "first"), new ThrowingHandler(events), new ReadingHandler(context, events, "last")])
                .HandleAsync(new UmbracoApplicationStartedNotification(false), Ct);

            // Every handler ran, in order, and both reads met the migrated schema.
            Assert.Equal(["first read 1 service(s)", "throwing handler called", "last read 1 service(s)"], events);
            Assert.Empty(await context.Database.GetPendingMigrationsAsync(Ct));

            // The migration is logged BEFORE any handler's failure, and the failure names the handler.
            var applied = logger.Entries.FindIndex(e => e.Message.Contains("applied 1 database migration", StringComparison.Ordinal));
            var failed = logger.Entries.FindIndex(e => e.Level == LogLevel.Error && e.Message.Contains(nameof(ThrowingHandler), StringComparison.Ordinal));
            Assert.True(applied >= 0 && failed > applied, string.Join(" | ", logger.Entries.Select(e => e.Message)));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await ExecuteAsync(
                master,
                $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
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
