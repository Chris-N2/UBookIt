using UBookIt.Persistence.Notifications;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the notification documentation promises, tied to what the package raises.
/// </summary>
/// <remarks>
/// <para>
/// A hook nobody is told about is not an extension point, and this documentation is the only
/// way a site learns these exist. The type names are asserted against the real types so the
/// page cannot send someone to subscribe to something that has been renamed.
/// </para>
/// <para>
/// Two sentences are load-bearing rather than informative, and both are pinned: that the
/// package sends nothing itself, and that a handler which throws is a notification nobody
/// receives. Each is the kind of thing discovered the expensive way — the first by a customer
/// arriving for a booking that was cancelled, the second during an incident.
/// </para>
/// </remarks>
public class NotificationDocumentationTests
{
    private static string Docs() => RepoFiles.Read("docs/notifications.md");

    [Fact]
    public void The_documented_notification_types_are_the_ones_the_package_raises()
    {
        var docs = Docs();

        Assert.Contains(nameof(BookingPlacedNotification), docs, StringComparison.Ordinal);
        Assert.Contains(nameof(BookingCancelledNotification), docs, StringComparison.Ordinal);

        // And the namespace, because a subscriber needs the using directive as much as the
        // name.
        Assert.Contains(typeof(BookingPlacedNotification).Namespace!, docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_package_sending_nothing_is_stated_rather_than_implied()
    {
        // The sentence that stops "uBookIt notified them" being assumed. An operator who
        // believes the package emails the customer finds out when somebody arrives for a
        // booking that no longer exists.
        var docs = Docs();

        DocumentationAssert.Says(docs, "uBookIt sends nothing itself");
        DocumentationAssert.Says(docs, "the customer will turn up");
    }

    [Fact]
    public void The_delivery_limit_is_stated()
    {
        // "You will be told, unless your handler throws" is the kind of half-promise that
        // gets relied on and then discovered during an incident.
        var docs = Docs();

        DocumentationAssert.Says(docs, "A handler that throws is a notification nobody receives");
        DocumentationAssert.Says(docs, "There is no retry and no queue");
    }

    [Fact]
    public void The_guarantee_that_a_handler_cannot_break_a_booking_is_stated()
    {
        DocumentationAssert.Says(Docs(), "Your handler cannot break a booking");
    }

    [Fact]
    public void The_documentation_shows_how_to_subscribe()
    {
        // A name and a description are not enough to act on; the composer registration is the
        // part a site author actually has to get right.
        var docs = Docs();

        Assert.Contains("AddNotificationAsyncHandler", docs, StringComparison.Ordinal);
        Assert.Contains("INotificationAsyncHandler", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void The_backoffice_documentation_says_cancelling_tells_nobody()
    {
        // Said where the consequence lands, not only on the notifications page. An operator
        // reading about cancelling is the person who needs to know.
        var docs = RepoFiles.Read("docs/backoffice.md");

        DocumentationAssert.Says(docs, "Cancelling tells nobody");
        Assert.Contains("notifications.md", docs, StringComparison.Ordinal);
    }
}
