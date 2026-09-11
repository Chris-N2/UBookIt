using UBookIt.Core.Bookings;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace UBookIt.Tests.Support;

/// <summary>
/// Shared doubles for the email send path, extracted from <c>BookingEmailTests</c> when
/// the responsibility gate tests needed the same recording sender and hosting stub.
/// </summary>
internal sealed class StubHostingEnvironment(string applicationUrl = "https://site.example/")
    : IHostingEnvironment
{
    public Uri ApplicationMainUrl { get; } = new(applicationUrl);

    public string SiteName => "Test";

    public string ApplicationId => "test";

    public string ApplicationPhysicalPath => ".";

    public string ApplicationVirtualPath => "/";

    public bool IsHosted => true;

    public bool IsDebugMode => false;

    public string LocalTempPath => ".";

    public string MapPathContentRoot(string path) => path;

    public string MapPathWebRoot(string path) => path;

    public string ToAbsolute(string virtualPath) => virtualPath;

    public void EnsureApplicationMainUrl(Uri? currentApplicationUrl)
    {
    }
}

internal sealed class RecordingEmailSender : IEmailSender
{
    public bool CanSend { get; init; } = true;

    public bool ThrowOnProbe { get; init; }

    public bool ThrowOnSend { get; init; }

    public bool Probed { get; private set; }

    public List<EmailMessage> Sent { get; } = [];

    public List<string> Types { get; } = [];

    public List<bool> Notified { get; } = [];

    /// <summary>Every call, including the ones that threw.</summary>
    public int Attempts { get; private set; }

    public bool CanSendRequiredEmail()
    {
        Probed = true;

        return ThrowOnProbe
            ? throw new NotImplementedException("To send an Email ensure IEmailSender is implemented")
            : CanSend;
    }

    public Task SendAsync(EmailMessage message, string emailType)
        => SendAsync(message, emailType, false, null);

    public Task SendAsync(EmailMessage message, string emailType, bool enableNotification)
        => SendAsync(message, emailType, enableNotification, null);

    public Task SendAsync(
        EmailMessage message, string emailType, bool enableNotification = false, TimeSpan? expires = null)
    {
        Attempts++;

        if (ThrowOnSend)
        {
            throw new InvalidOperationException("The mail server refused the message.");
        }

        Sent.Add(message);
        Types.Add(emailType);
        Notified.Add(enableNotification);

        return Task.CompletedTask;
    }
}
