using System.Text.RegularExpressions;
using UBookIt.Core.Notifications;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;
using UBookIt.Web.Emails;

namespace UBookIt.Tests;

/// <summary>
/// What the documentation says about email template sets (service-email-templates, tasks 9.1 and
/// 9.2), tied to the code it describes.
/// </summary>
/// <remarks>
/// The load-bearing sentences are the ones a site acts on: the lookup order, that only "nothing
/// supplied" moves down it, the lower-case rule, branching on the set rather than the id, the
/// startup report and when it is taken, and that a custom renderer must read the set itself. The
/// folder, the limit, the member names and the field label are asserted against the real values.
/// </remarks>
public class ServiceTemplateSetDocumentationTests
{
    private static string Notifications() => RepoFiles.Read("docs/notifications.md");

    private static string Backoffice() => RepoFiles.Read("docs/backoffice.md");

    [Fact]
    public void The_notifications_page_states_the_order_and_the_failure_rules()
    {
        var docs = Notifications();

        Assert.Contains(RazorBookingTemplateRenderer.TemplateFolder.TrimStart('~', '/'), docs, StringComparison.Ordinal);
        DocumentationAssert.Says(docs, "For each message, uBookIt looks in this order:");
        DocumentationAssert.Says(docs, "the view for that message in the service's set");
        DocumentationAssert.Says(docs, "where the set has no view for it, the view for every service");
        DocumentationAssert.Says(docs, "Two failures send uBookIt's own wording, never the view for every service:");
        DocumentationAssert.Says(docs, "A view in the set that fails to render.");
        DocumentationAssert.Says(docs, "A service that cannot be read");
        DocumentationAssert.Says(docs, "A set does not have to supply every message.");
    }

    [Fact]
    public void The_notifications_page_states_the_name_rule_against_the_real_limit()
    {
        var docs = Notifications();

        DocumentationAssert.Says(docs, "Set names are lower case.");
        DocumentationAssert.Says(docs, $"up to {Service.TemplateSetMaxLength} characters");
        DocumentationAssert.Says(docs, "read from the service when each message is composed");
    }

    [Fact]
    public void The_notifications_page_recommends_the_set_over_the_id_for_branching()
    {
        var docs = Notifications();

        Assert.Contains($"Model.{nameof(BookingMessageModel.TemplateSet)}", docs, StringComparison.Ordinal);
        Assert.Contains($"Model.{nameof(BookingMessageModel.ServiceId)}", docs, StringComparison.Ordinal);
        DocumentationAssert.Says(docs, "To tell services apart in a view, use `Model.TemplateSet`, not `Model.ServiceId`.");
        DocumentationAssert.Says(docs, "A view written before either existed renders unchanged.");
    }

    [Fact]
    public void The_notifications_page_describes_the_startup_report_and_when_it_is_taken()
    {
        var docs = Notifications();

        DocumentationAssert.Says(docs, "A set that supplies nothing is logged as a warning");
        DocumentationAssert.Says(docs, "as they stand at startup");
        DocumentationAssert.Says(docs, "the site starts regardless");
    }

    [Fact]
    public void The_notifications_page_tells_a_custom_renderer_to_read_the_set()
    {
        DocumentationAssert.Says(Notifications(), $"must read it itself to support sets");
        Assert.Contains(nameof(IBookingTemplateRenderer), Notifications(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_backoffice_page_names_the_field_as_the_editor_labels_it_and_states_the_rule()
    {
        var docs = Backoffice();

        var localization = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/localization/en-us.ts");
        var label = Regex.Match(localization, @"^\s*templateSet:\s*""([^""]+)""", RegexOptions.Multiline);
        Assert.True(label.Success, "The editor's label for the template set was not found in en-us.ts.");

        DocumentationAssert.Says(docs, $"optional **{label.Groups[1].Value}** field");
        DocumentationAssert.Says(docs, "saved in lower case");
        DocumentationAssert.Says(docs, $"up to {Service.TemplateSetMaxLength} characters");
        DocumentationAssert.Says(docs, "The set is not shown on the public site.");
    }
}
