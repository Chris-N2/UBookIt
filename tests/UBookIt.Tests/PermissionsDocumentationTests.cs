using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The claims the permissions documentation must keep making — guarantees a site owner
/// acts on, wrap-safe per <see cref="DocumentationAssert"/>.
/// </summary>
public class PermissionsDocumentationTests
{
    private static string Backoffice() => RepoFiles.Read("docs/backoffice.md");

    /// <summary>
    /// The upgrade promise: the seed exists so nobody loses access, and the docs are
    /// where an upgrading site owner is told so. If this goes, the seed becomes
    /// undocumented magic and its one-shot boundary a surprise.
    /// </summary>
    [Fact]
    public void The_seed_keeps_access_and_its_boundary_is_stated()
    {
        DocumentationAssert.Says(
            Backoffice(),
            "Every group that already held the uBookIt section is granted all three automatically, once, at the first start");
        DocumentationAssert.Says(Backoffice(), "nobody loses access by upgrading");
        DocumentationAssert.Says(
            Backoffice(),
            "a group whose permissions you later empty stays emptied");
    }

    /// <summary>The new-group steady state, stated as the instruction it is.</summary>
    [Fact]
    public void The_new_group_shell_behaviour_is_stated()
    {
        DocumentationAssert.Says(Backoffice(), "tick the section, then tick what they may do");
        DocumentationAssert.Says(
            Backoffice(),
            "A group with the section and no uBookIt permissions sees the section shell and nothing in it");
    }

    /// <summary>
    /// Sensitive data is joined, never replaced: the reader deciding on permissions must
    /// not conclude the verbs now govern contact details.
    /// </summary>
    [Fact]
    public void Sensitive_data_is_stated_beside_the_read_permission_not_replaced()
    {
        DocumentationAssert.Says(
            Backoffice(),
            "Contact details still need the Sensitive data group on top");
        DocumentationAssert.Says(
            Backoffice(),
            "on top of access to the uBookIt section and the See bookings permission");
    }

    /// <summary>Manage implies Read, in the docs as in the rule — or an admin ticks both forever.</summary>
    [Fact]
    public void The_implication_is_stated()
        => DocumentationAssert.Says(
            Backoffice(),
            "Includes seeing them: acting on what you cannot see makes no sense, so this needs no second tick");

    /// <summary>Client hiding is convenience; the server is the truth on every request.</summary>
    [Fact]
    public void Server_is_the_truth_is_stated()
        => DocumentationAssert.Says(
            Backoffice(),
            "the server makes the actual decision on every request");

    /// <summary>The section stays the outer gate no verb can substitute for.</summary>
    [Fact]
    public void The_section_as_outer_gate_is_stated()
        => DocumentationAssert.Says(
            Backoffice(),
            "a group holding every permission but not the section reaches nothing");
}
