using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UBookIt.Persistence.Composing;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace UBookIt.Tests;

/// <summary>
/// The notification settings resolve on one rule: <b>absent, blank and unusable all mean off,
/// and nothing falls back to a value that enables anything</b>. A typo must never start writing
/// to a site's customers.
/// </summary>
public class BookingNotificationSettingsTests
{
    private static IConfiguration Config(string? send = null, params string?[] recipients)
    {
        var values = new Dictionary<string, string?>();

        if (send is not null)
        {
            values[UBookItPersistenceComposer.SendBookerEmailsSettingKey] = send;
        }

        for (var i = 0; i < recipients.Length; i++)
        {
            values[$"{UBookItPersistenceComposer.InternalRecipientsSettingKey}:{i}"] = recipients[i];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Nothing_configured_sends_nothing()
    {
        var settings = UBookItPersistenceComposer.ResolveSettings(Config()).Notifications;

        Assert.False(settings.SendBookerEmails);
        Assert.False(settings.HasInternalRecipients);
        Assert.Empty(settings.InternalRecipients);
    }

    /// <summary>
    /// The values a site might reasonably expect to work, which deliberately do not. Guessing at
    /// what "yes" meant would mean starting to write to a site's customers on the strength of the
    /// guess — so the failure direction is silence, and the boot check says so out loud.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("TRUE!")]
    [InlineData("false")]
    [InlineData("False")]
    public void Only_a_real_true_enables_booker_email(string? configured)
        => Assert.False(
            UBookItPersistenceComposer.ResolveSettings(Config(configured)).Notifications.SendBookerEmails);

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void A_real_true_enables_booker_email(string configured)
        => Assert.True(
            UBookItPersistenceComposer.ResolveSettings(Config(configured)).Notifications.SendBookerEmails);

    [Fact]
    public void One_unusable_address_does_not_silence_the_list()
    {
        var resolution = UBookItPersistenceComposer.ResolveNotifications(
            Config(null, "bookings@example.com", "not an address", "desk@example.com"));

        Assert.Equal(
            ["bookings@example.com", "desk@example.com"],
            resolution.Settings.InternalRecipients);
        Assert.Equal(["not an address"], resolution.RejectedRecipients);
    }

    [Fact]
    public void A_list_of_only_unusable_addresses_configures_nothing()
    {
        var resolution = UBookItPersistenceComposer.ResolveNotifications(
            Config(null, "nope", "also nope"));

        Assert.False(resolution.Settings.HasInternalRecipients);
        Assert.Equal(["nope", "also nope"], resolution.RejectedRecipients);
    }

    /// <summary>
    /// A blank slot is a hole in the list — an array entry whose environment variable resolved to
    /// nothing — not a mistyped address. There is nobody to report it about, so reporting it would
    /// be noise an operator learns to ignore, which is how the real refusals get missed.
    /// </summary>
    [Fact]
    public void A_blank_entry_is_skipped_and_not_reported()
    {
        var resolution = UBookItPersistenceComposer.ResolveNotifications(
            Config(null, "bookings@example.com", "", "   "));

        Assert.Equal(["bookings@example.com"], resolution.Settings.InternalRecipients);
        Assert.Empty(resolution.RejectedRecipients);
    }

    [Fact]
    public void Addresses_are_trimmed()
        => Assert.Equal(
            ["bookings@example.com"],
            UBookItPersistenceComposer.ResolveNotifications(
                Config(null, "  bookings@example.com  ")).Settings.InternalRecipients);

    /// <summary>
    /// The two directions are independent in both orders, which is the whole point of gating the
    /// internal one on the list's presence rather than on a second flag.
    /// </summary>
    [Fact]
    public void Internal_recipients_do_not_enable_booker_email()
    {
        var settings = UBookItPersistenceComposer
            .ResolveSettings(Config(null, "bookings@example.com")).Notifications;

        Assert.True(settings.HasInternalRecipients);
        Assert.False(settings.SendBookerEmails);
    }

    [Fact]
    public void Booker_email_does_not_enable_internal_recipients()
    {
        var settings = UBookItPersistenceComposer.ResolveSettings(Config("true")).Notifications;

        Assert.True(settings.SendBookerEmails);
        Assert.False(settings.HasInternalRecipients);
    }

    // ---- the boot check ----------------------------------------------------------------------

    [Fact]
    public void A_refused_recipient_is_reported()
    {
        var (problems, logger) = Check(Config(null, "bookings@example.com", "not an address"));

        Assert.Contains(problems, p => p.Contains("not an address", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public void Sending_configured_on_a_host_that_cannot_send_is_reported()
    {
        var (problems, _) = Check(Config("true"), canSend: false);

        Assert.Contains(problems, p => p.Contains("no usable", StringComparison.Ordinal));
    }

    [Fact]
    public void Sending_configured_on_a_host_that_can_send_reports_nothing()
        => Assert.Empty(Check(Config("true"), canSend: true).Problems);

    /// <summary>
    /// A site that has not asked for anything is not nagged about mail it never wanted to send.
    /// </summary>
    [Fact]
    public void Nothing_configured_is_not_reported_even_without_mail()
        => Assert.Empty(Check(Config(), canSend: false).Problems);

    /// <summary>
    /// A host that cannot ANSWER the question has told us nothing about the answer, so it must not
    /// be recorded as a configured decision not to send.
    /// </summary>
    [Fact]
    public void A_host_that_cannot_be_asked_is_reported_as_such()
    {
        var (problems, logger) = Check(Config("true"), throws: true);

        Assert.Contains(problems, p => p.Contains("could not be asked", StringComparison.Ordinal));
        Assert.DoesNotContain(problems, p => p.Contains("no usable", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
    }

    /// <summary>
    /// REGRESSION. The first version of the check collected problems and logged them in a sweep at
    /// the end, and the throwing path returned before reaching it — so on the one configuration
    /// that has two things wrong at once, the refused recipient was silently dropped. The check
    /// exists to break exactly that silence.
    /// </summary>
    [Fact]
    public void A_refused_recipient_is_still_reported_when_the_host_cannot_be_asked()
    {
        var (problems, logger) = Check(Config("true", "not an address"), throws: true);

        Assert.Contains(problems, p => p.Contains("not an address", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("not an address", StringComparison.Ordinal));
    }

    private static (IReadOnlyList<string> Problems, CapturingLogger Logger) Check(
        IConfiguration configuration, bool canSend = true, bool throws = false)
    {
        var logger = new CapturingLogger();
        var check = new UBookItNotificationBootCheck(
            configuration,
            new StubEmailSender { CanSend = canSend, Throws = throws },
            logger);

        return (check.Run(), logger);
    }

    private sealed class StubEmailSender : IEmailSender
    {
        public bool CanSend { get; init; }

        public bool Throws { get; init; }

        public bool CanSendRequiredEmail()
            => Throws
                ? throw new NotImplementedException("To send an Email ensure IEmailSender is implemented")
                : CanSend;

        public Task SendAsync(EmailMessage message, string emailType) => Task.CompletedTask;

        public Task SendAsync(EmailMessage message, string emailType, bool enableNotification)
            => Task.CompletedTask;

        // Umbraco 18 added the scheduling overload to IEmailSender. The stub records nothing
        // and sends nothing, exactly as the others do — this test is about whether the check
        // reports the sender as usable, not about what reaches a mail server.
        public Task SendAsync(
            EmailMessage message,
            string emailType,
            bool enableNotification,
            TimeSpan? delay)
            => Task.CompletedTask;
    }

    private sealed class CapturingLogger : ILogger<UBookItNotificationBootCheck>
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
