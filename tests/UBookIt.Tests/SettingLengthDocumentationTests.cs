using System.Globalization;
using System.Text.RegularExpressions;
using UBookIt.Persistence.Entities;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// <c>docs/configuration.md</c> states the settings store's length limit as a number, and that
/// number is <see cref="SettingRow.MaxValueLength"/>, not a copy of it.
/// </summary>
/// <remarks>
/// The limit is a figure a site author needs ("a longer value can still be set in
/// configuration"), so the document cannot simply say "the store's limit". Before this guard the
/// figure was a literal nothing tied to the constant: changing the constant — with its migration —
/// would have left the documentation stating the old capacity with the suite green. The settings
/// screen's message cannot drift the same way, because it interpolates the constant.
/// </remarks>
public class SettingLengthDocumentationTests
{
    private static readonly string Limit =
        SettingRow.MaxValueLength.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public void The_documented_limit_is_the_stores_capacity()
    {
        var document = RepoFiles.Read("docs/configuration.md");

        DocumentationAssert.SaysOnce(document, $"longer than the {Limit} characters the store holds");
    }

    [Fact]
    public void No_other_figure_is_given_for_that_limit()
    {
        // The pin above holds the sentence in place; this holds every OTHER "N characters" in the
        // document to the same figure, so a second, stale statement of the limit cannot sit
        // beside the pinned one.
        var document = RepoFiles.Read("docs/configuration.md");

        var stated = Regex.Matches(document, @"\b(?<n>\d{3,})\s+characters\b")
            .Select(match => match.Groups["n"].Value)
            .ToList();

        Assert.NotEmpty(stated);
        Assert.All(stated, figure => Assert.Equal(Limit, figure));
    }
}
