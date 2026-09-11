using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The claims the responsibility feature's documentation must keep making. Wrap-safe on
/// purpose (<see cref="DocumentationAssert"/>): a rewrapped paragraph must not silently
/// retire a promise.
/// </summary>
public class ResponsibilityDocumentationTests
{
    private static string Notifications() => RepoFiles.Read("docs/notifications.md");

    private static string Backoffice() => RepoFiles.Read("docs/backoffice.md");

    /// <summary>
    /// The boundary both documents must state: responsibility decides who is emailed and
    /// nothing else. An operator reading either page in isolation must meet it, because
    /// the mistaken reading — "I assigned her, why can't she see the section?" — arrives
    /// from either direction.
    /// </summary>
    [Fact]
    public void Both_documents_state_that_responsibility_is_not_permissions()
    {
        DocumentationAssert.Says(
            Notifications(),
            "assigning a user grants them no access to the backoffice or to anything in it");
        DocumentationAssert.Says(Backoffice(), "It is not permissions");
        DocumentationAssert.Says(
            Backoffice(),
            "Assigning a user grants them nothing: no section access, no extra visibility");
    }

    /// <summary>
    /// The PII guarantee does not narrow with the audience: a responsible person's message
    /// is an internal message like any other. Stated where recipients are described, so
    /// the reader cannot conclude that "targeted" means "more detailed".
    /// </summary>
    [Fact]
    public void The_no_booker_details_rule_is_stated_for_responsible_recipients()
        => DocumentationAssert.Says(
            Notifications(),
            "they carry no booker contact details, whoever receives them");

    /// <summary>
    /// The Workflow-documented surprise, disclosed on purpose: membership is read at send
    /// time, so joining a group starts the mail with no uBookIt action.
    /// </summary>
    [Fact]
    public void Send_time_group_membership_is_disclosed()
        => DocumentationAssert.Says(
            Notifications(),
            "Group membership is read at the moment of sending");

    /// <summary>
    /// The union's non-surprise property — the reason the tiers are a union at all: the
    /// first assignment must not silently stop the site-wide list hearing about that
    /// subject. If this sentence goes, the design's whole point is undocumented.
    /// </summary>
    [Fact]
    public void The_first_assignment_not_silencing_the_list_is_stated()
        => DocumentationAssert.Says(
            Notifications(),
            "does not stop the InternalRecipients list hearing about that resource's bookings");

    /// <summary>
    /// The boot check's boundary: it reads configuration, so an assignments-only site
    /// with broken mail gets no startup warning. Disclosed rather than discovered.
    /// </summary>
    [Fact]
    public void The_startup_check_boundary_is_disclosed()
        => DocumentationAssert.Says(
            Notifications(),
            "a site relying on assignments alone gets no startup line");
}
