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
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string ConnectionEnvVar = "UBOOKIT_TEST_DB";
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true";

    private readonly string _databaseName = $"uBookItTest_{Guid.NewGuid():N}";
    private readonly List<UBookItDbContext> _serviceContexts = [];
    private string? _masterConnectionString;
    private bool _databaseCreated;

    public string? SkipReason { get; private set; }

    public DbContextOptions<UBookItDbContext> Options { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionEnvVar) ?? DefaultServer;
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
        var builder = new DbContextOptionsBuilder<UBookItDbContext>();
        UBookItDbContext.ConfigureSqlServer(builder, new UBookItDbContext(Options).Database.GetConnectionString()!);
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
        var resourceStore = new SqlResourceStore(TrackContext());
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
