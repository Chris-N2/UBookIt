using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UBookIt.Persistence.Composing;

namespace UBookIt.Tests;

/// <summary>
/// Covers the persistence spec scenario "Missing time zone setting defaults
/// safely": UTC default when UBookIt:TimeZoneId is absent (or blank), and the
/// startup warning.
/// </summary>
public class SiteSettingsTests
{
    private static IConfiguration Config(string? timeZoneId = null)
    {
        var values = new Dictionary<string, string?>();
        if (timeZoneId is not null)
        {
            values[UBookItPersistenceComposer.TimeZoneSettingKey] = timeZoneId;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Missing_setting_defaults_to_utc()
        => Assert.Equal("UTC", UBookItPersistenceComposer.ResolveSettings(Config()).TimeZoneId);

    [Fact]
    public void Blank_setting_defaults_to_utc()
        => Assert.Equal("UTC", UBookItPersistenceComposer.ResolveSettings(Config("  ")).TimeZoneId);

    [Fact]
    public void Configured_setting_is_used()
        => Assert.Equal("Europe/London", UBookItPersistenceComposer.ResolveSettings(Config("Europe/London")).TimeZoneId);

    [Fact]
    public void Missing_setting_logs_a_warning_at_startup()
    {
        var logger = new CapturingLogger();

        RunUBookItMigrations.WarnIfTimeZoneNotConfigured(Config(), logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(UBookItPersistenceComposer.TimeZoneSettingKey, entry.Message);
    }

    [Fact]
    public void Configured_setting_logs_no_warning()
    {
        var logger = new CapturingLogger();

        RunUBookItMigrations.WarnIfTimeZoneNotConfigured(Config("Europe/London"), logger);

        Assert.Empty(logger.Entries);
    }

    private static IConfiguration MaxRangeConfig(string? value)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null)
        {
            values[UBookItPersistenceComposer.MaxQueryRangeDaysSettingKey] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Theory]
    [InlineData(null)]     // missing
    [InlineData("")]       // blank
    [InlineData("lots")]   // malformed
    [InlineData("0")]      // non-positive
    [InlineData("-5")]     // negative
    public void Max_query_range_falls_back_to_default(string? configured)
        => Assert.Equal(
            UBookItPersistenceComposer.DefaultMaxQueryRangeDays,
            UBookItPersistenceComposer.ResolveSettings(MaxRangeConfig(configured)).MaxQueryRangeDays);

    [Fact]
    public void Configured_max_query_range_is_used()
        => Assert.Equal(7, UBookItPersistenceComposer.ResolveSettings(MaxRangeConfig("7")).MaxQueryRangeDays);

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
