using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UBookIt.Core;
using UBookIt.Core.Availability;
using UBookIt.Core.Bookings;
using UBookIt.Persistence;
using UBookIt.Persistence.Stores;

namespace UBookIt.Tests.Integration.Support;

/// <summary>
/// One uniquely named SQL Server database per test run: created and migrated
/// on initialize, dropped on dispose. Connection comes from the
/// UBOOKIT_TEST_DB environment variable, defaulting to LocalDB. When no
/// server is reachable, tests skip with an explicit diagnostic (never
/// silently pass) — see <see cref="EnsureAvailable"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unless a database is required.</b> With <c>UBOOKIT_TEST_DB_REQUIRED</c> set to
/// exactly <c>true</c> — lower case, compared ordinally, because it is set by a machine
/// and a near-miss spelling should behave visibly like "not set" rather than be guessed
/// at — an unreachable server fails every test in the fixture with the same diagnostic
/// instead of skipping them.
/// </para>
/// <para>
/// CI sets it. A skip does not fail a run, and the CI runner has no LocalDB, so without
/// this every integration test would skip there and the run would report green having
/// tested nothing against SQL Server. The CI results check also fails on any skip; this
/// flag is what makes the failure name its cause. Locally, unset, the skip stays — a
/// maintainer without SQL Server can still run the rest.
/// </para>
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ConnectionEnvVar = "UBOOKIT_TEST_DB";
    public const string RequiredEnvVar = "UBOOKIT_TEST_DB_REQUIRED";
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true";

    private readonly string _databaseName = $"uBookItTest_{Guid.NewGuid():N}";
    private readonly List<UBookItDbContext> _serviceContexts = [];
    private string? _masterConnectionString;
    private bool _databaseCreated;

    public string? SkipReason { get; private set; }

    public DbContextOptions<UBookItDbContext> Options { get; private set; } = null!;

    public ValueTask InitializeAsync() =>
        InitializeAsync(
            Environment.GetEnvironmentVariable(ConnectionEnvVar),
            Environment.GetEnvironmentVariable(RequiredEnvVar));

    /// <summary>
    /// Everything after the environment is read, with its two inputs made explicit, so the
    /// required-mode cases can be tested without writing process-wide environment variables that
    /// every other fixture initialising in parallel would also read.
    /// </summary>
    /// <remarks>
    /// This is NOT the path xunit takes: xunit calls the public overload, which reads the
    /// environment. An earlier version of this comment said it was, and QA showed the gap —
    /// replacing the public overload's read of <see cref="RequiredEnvVar"/> with <c>null</c> left
    /// every test here green. The public overload is covered separately, by
    /// <c>RequiredDatabaseEnvironmentTests</c>, which writes the real variables in a collection that
    /// runs alone.
    /// </remarks>
    internal async ValueTask InitializeAsync(string? configuredConnection, string? required)
    {
        var configured = configuredConnection ?? DefaultServer;
        var builder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "master",
            ConnectTimeout = 10,
        };
        _masterConnectionString = builder.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException or PlatformNotSupportedException)
        {
            SkipReason =
                $"No SQL Server reachable for integration tests ({ex.GetType().Name}: {ex.Message}). " +
                $"Set the {ConnectionEnvVar} environment variable to a SQL Server connection string, " +
                "or install LocalDB.";

            if (string.Equals(required, "true", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{RequiredEnvVar} is 'true', so an unreachable database fails rather than skips. {SkipReason}",
                    ex);
            }

            return;
        }

        builder.InitialCatalog = _databaseName;
        var optionsBuilder = new DbContextOptionsBuilder<UBookItDbContext>();
        UBookItDbContext.ConfigureSqlServer(optionsBuilder, builder.ConnectionString);
        Options = optionsBuilder.Options;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _serviceContexts)
        {
            await context.DisposeAsync();
        }

        if (!_databaseCreated)
        {
            return;
        }

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Call first in every test: skips loudly when no server is reachable.</summary>
    public void EnsureAvailable()
    {
        if (SkipReason is not null)
        {
            Assert.Skip(SkipReason);
        }
    }

    public UBookItDbContext CreateContext() => new(Options);

    /// <summary>
    /// A context that reports every command it sends, for tests about the SHAPE of a write
    /// rather than its result.
    /// </summary>
    /// <remarks>
    /// Some guarantees are invisible in the data. Whether a conditional update is one
    /// statement or a read followed by a write produces identical rows in a single-threaded
    /// test and different rows only under an interleaving a test cannot stage — so the thing
    /// to assert is what was sent.
    /// </remarks>
    public UBookItDbContext CreateContext(IInterceptor interceptor)
    {
        using var probe = new UBookItDbContext(Options);

        var builder = new DbContextOptionsBuilder<UBookItDbContext>();
        UBookItDbContext.ConfigureSqlServer(builder, probe.Database.GetConnectionString()!);
        builder.AddInterceptors(interceptor);

        return new UBookItDbContext(builder.Options);
    }

    /// <summary>
    /// Core services wired to real SQL stores, fixed clock, UTC site zone.
    /// The two backing contexts are tracked and disposed with the fixture.
    /// </summary>
    public (BookingService Bookings, AvailabilityService Availability) CreateServices(DateTimeOffset nowUtc)
    {
        var settings = new SiteBookingSettings { TimeZoneId = "UTC" };
        var time = new FixedTimeProvider(nowUtc);
        var closureContext = TrackContext();
        var resourceStore = new SqlResourceStore(closureContext, new SqlSiteClosureStore(closureContext));
        var bookingStore = new SqlBookingStore(TrackContext());

        return (
            new BookingService(resourceStore, bookingStore, time, settings),
            new AvailabilityService(resourceStore, bookingStore, time, settings));
    }

    private UBookItDbContext TrackContext()
    {
        var context = CreateContext();
        _serviceContexts.Add(context);
        return context;
    }
}

public sealed class FixedTimeProvider(DateTimeOffset nowUtc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => nowUtc;
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SqlServer";
}

/// <summary>Records the text of every command a context sends.</summary>
internal sealed class CommandRecordingInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];

    public IReadOnlyList<string> Commands => _commands;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        _commands.Add(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        _commands.Add(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        _commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}
