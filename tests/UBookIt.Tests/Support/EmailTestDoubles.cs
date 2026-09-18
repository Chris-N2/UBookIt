using UBookIt.Core.Bookings;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Mail;
using Umbraco.Cms.Core.Models.Email;

namespace UBookIt.Tests.Support;

/// <summary>
/// Shared doubles for the email send path, extracted from <c>BookingEmailTests</c> when
/// the responsibility gate tests needed the same recording sender and hosting stub.
/// </summary>
internal sealed class StubHostingEnvironment(string? applicationUrl = "https://site.example/")
    : IHostingEnvironment
{
    /// <summary>
    /// The site's own address, or <c>null</c> where Umbraco has not resolved one.
    /// </summary>
    /// <remarks>
    /// <b>Null is a real state, which is why this stub can express it.</b> Umbraco declares
    /// <see cref="IHostingEnvironment.ApplicationMainUrl"/> non-nullable and backs it with a
    /// null-forgiving field that stays null until the application URL is resolved — from
    /// configuration, or from an observed request. A site that has configured neither and has not
    /// yet served one has no address to give out, and the package's link builders check for it.
    /// A stub that could not be null would leave that branch untestable.
    /// </remarks>
    public Uri ApplicationMainUrl { get; } = applicationUrl is null ? null! : new Uri(applicationUrl);

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
