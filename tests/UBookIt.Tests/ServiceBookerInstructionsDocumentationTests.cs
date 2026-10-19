using System.Text.RegularExpressions;
using UBookIt.Core.Notifications;
using UBookIt.Core.Services;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the documentation says about instructions for the booker (service-booker-instructions,
/// tasks 7.1 and 7.2), tied to the code it describes.
/// </summary>
/// <remarks>
/// The load-bearing sentences are the ones a site acts on: that the instructions reach only a
/// confirmed booker, that they never reach internal recipients or the public site, that they are
/// read when each message is composed, and that a supplied view which omits them sends none. Each
/// is pinned. The member name, the limit and the field label are asserted against the real values,
/// so a rename in code fails here rather than leaving a page that names something gone.
/// </remarks>
public class ServiceBookerInstructionsDocumentationTests
{
    private static string Notifications() => RepoFiles.Read("docs/notifications.md");

    private static string Backoffice() => RepoFiles.Read("docs/backoffice.md");

    [Fact]
    public void The_notifications_page_states_when_the_instructions_are_sent()
    {
        var docs = Notifications();

        DocumentationAssert.Says(docs, "only once the booking is confirmed");
        DocumentationAssert.Says(docs, "So a site that requires approval never hands a join link to somebody it has not accepted.");
        DocumentationAssert.Says(docs, "read from the service when each message is composed");
        DocumentationAssert.Says(docs, "Messages to `InternalRecipients` never carry them");
        DocumentationAssert.Says(docs, "Neither does the delivery API");
        DocumentationAssert.Says(docs, "Do not put secrets in them.");
    }

    [Fact]
    public void The_notifications_page_tells_a_template_author_about_the_member()
    {
        var docs = Notifications();

        Assert.Contains($"Model.{nameof(BookerMessageModel.BookerInstructions)}", docs, StringComparison.Ordinal);
        DocumentationAssert.Says(docs, "`null` means this message does not carry instructions");
        DocumentationAssert.Says(docs, "A view written before the member existed renders unchanged.");
        DocumentationAssert.Says(
            docs, "If you supply your own view and omit it, your bookers are not given the instructions.");
        DocumentationAssert.Says(docs, "`InternalMessageModel` has no such member.");
    }

    [Fact]
    public void The_backoffice_page_names_the_field_as_the_editor_labels_it_and_states_its_limit()
    {
        var docs = Backoffice();

        // The label, read from the client's own localization file: a page that names a field the
        // editor does not show sends an editor looking for something that is not there.
        var localization = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/localization/en-us.ts");
        var label = Regex.Match(localization, @"^\s*bookerInstructions:\s*""([^""]+)""", RegexOptions.Multiline);
        Assert.True(label.Success, "The editor's label for the instructions was not found in en-us.ts.");

        DocumentationAssert.Says(docs, $"optional **{label.Groups[1].Value}** field");
        DocumentationAssert.Says(docs, $"It takes up to {Service.BookerInstructionsMaxLength} characters");
        DocumentationAssert.Says(docs, "It is not shown on the public site.");
        DocumentationAssert.Says(docs, "sent to the booker once their booking is confirmed");
    }
}
