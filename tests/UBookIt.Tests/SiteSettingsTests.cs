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

    private static IConfiguration RetentionConfig(string? value)
    {
        var values = new Dictionary<string, string?>();
        if (value is not null)
        {
            values[UBookItPersistenceComposer.RetentionDaysSettingKey] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Retention_is_off_by_default()
        => Assert.Null(UBookItPersistenceComposer.ResolveSettings(RetentionConfig(null)).RetentionDays);

    [Theory]
    [InlineData("")]       // blank
    [InlineData("  ")]     // whitespace
    [InlineData("lots")]   // malformed
    [InlineData("90.5")]   // not whole
    [InlineData("0")]      // the "off" somebody meant to write
    [InlineData("-5")]     // negative
    public void An_unreadable_retention_period_resolves_to_off_and_never_to_a_default(string configured)
    {
        // The whole point of this test, and the reason it does not mirror the
        // Max_query_range_falls_back_to_default theory above: that setting SUBSTITUTES a working
        // default, and this one must not. A future refactor that unified the two "for
        // consistency" would make every case here erase data on a period nobody wrote.
        Assert.Null(UBookItPersistenceComposer.ResolveSettings(RetentionConfig(configured)).RetentionDays);
    }

    [Fact]
    public void A_configured_retention_period_is_used()
        => Assert.Equal(90, UBookItPersistenceComposer.ResolveSettings(RetentionConfig("90")).RetentionDays);

    [Fact]
    public void The_shortest_usable_period_is_one_day()
        => Assert.Equal(1, UBookItPersistenceComposer.ResolveSettings(RetentionConfig("1")).RetentionDays);

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("lots")]
    [InlineData("0")]
    [InlineData("-5")]
    public void A_retention_period_that_was_written_and_cannot_be_read_logs_an_error(string configured)
    {
        var logger = new CapturingLogger();

        RunUBookItMigrations.ErrorIfRetentionUnreadable(RetentionConfig(configured), logger);

        var entry = Assert.Single(logger.Entries);

        // An ERROR, not a warning. Louder than the missing-time-zone case deliberately: a missing
        // time zone defaults to UTC and the site keeps working, while this leaves retention
        // silently off on a site that believes it is on — and a later change publishes the
        // configured period to visitors in a privacy notice.
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(UBookItPersistenceComposer.RetentionDaysSettingKey, entry.Message);
    }

    [Fact]
    public void An_absent_retention_period_logs_nothing()
    {
        // Not configuring retention is the package's default and an ordinary choice. Complaining
        // about it on every startup of every site would bury the message above among messages
        // that do not matter, which is how a real fault gets ignored.
        var logger = new CapturingLogger();

        RunUBookItMigrations.ErrorIfRetentionUnreadable(RetentionConfig(null), logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void A_readable_retention_period_logs_nothing()
    {
        var logger = new CapturingLogger();

        RunUBookItMigrations.ErrorIfRetentionUnreadable(RetentionConfig("90"), logger);

        Assert.Empty(logger.Entries);
    }

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
