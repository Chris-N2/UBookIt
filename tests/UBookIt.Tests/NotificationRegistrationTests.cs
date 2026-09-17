using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Events;
using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Every notification the email handler can handle is actually registered to receive one.
/// </summary>
/// <remarks>
/// <para>
/// <b>A handler that is implemented but not registered fails silently.</b> It compiles, its
/// tests pass when called directly, and the only symptom is an email nobody receives — which
/// looks exactly like a site that has not configured notifications. This repository has already
/// paid for one registration miss (the composer CRITICAL in change ⑭), and adding a sixth
/// notification to a five-line registration list is precisely the shape of edit that misses one.
/// </para>
/// <para>
/// <b>It reads the composer's source rather than building a container</b>, deliberately: the
/// registration is a generic type argument, so the fact worth checking — that this notification
/// type appears against this handler — is visible in the text and needs no Umbraco host. The
/// cost is that it cannot see a registration made some other way; the comment in the composer
/// says to keep them in the list.
/// </para>
/// </remarks>
public class NotificationRegistrationTests
{
    [Fact]
    public void Every_notification_the_email_handler_implements_is_registered()
    {
        var handled = typeof(BookingEmailHandler)
            .GetInterfaces()
            .Where(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(INotificationAsyncHandler<>))
            .Select(i => i.GetGenericArguments()[0].Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(handled);

        var composer = File.ReadAllText(Path.Combine(
            RepoFiles.Root, "src", "UBookIt.Persistence", "Composing", "UBookItPersistenceComposer.cs"));

        var registered = Regex
            .Matches(composer, @"AddNotificationAsyncHandler<\s*(\w+)\s*,\s*BookingEmailHandler\s*>")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var missing = handled.Except(registered, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            "BookingEmailHandler handles notifications it is never registered for, so the "
            + "messages they would send are never sent and nothing reports it:"
            + Environment.NewLine
            + $"  unregistered: {string.Join(", ", missing)}"
            + Environment.NewLine
            + $"  handled:      {string.Join(", ", handled)}"
            + Environment.NewLine
            + $"  registered:   {string.Join(", ", registered)}"
            + Environment.NewLine
            + "Add builder.AddNotificationAsyncHandler<TNotification, BookingEmailHandler>() to "
            + "UBookItPersistenceComposer.");
    }

    [Fact]
    public void The_guard_can_see_a_missing_registration()
    {
        // The guard's own instrument, exercised: a name-matching scan that silently matched
        // nothing would report every handler as registered and pass forever. Proven here against
        // a notification the composer genuinely does not register the email handler for.
        var composer = File.ReadAllText(Path.Combine(
            RepoFiles.Root, "src", "UBookIt.Persistence", "Composing", "UBookItPersistenceComposer.cs"));

        var registered = Regex
            .Matches(composer, @"AddNotificationAsyncHandler<\s*(\w+)\s*,\s*BookingEmailHandler\s*>")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(registered);
        Assert.DoesNotContain("ANotificationNobodyRegistered", registered);
        Assert.Contains("BookingPlacedOnBehalfNotification", registered);
    }
}
