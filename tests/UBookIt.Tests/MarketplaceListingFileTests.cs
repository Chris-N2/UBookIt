using System.Text.Json.Nodes;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The root <c>umbraco-marketplace.json</c>: who the Umbraco Marketplace says publishes uBookIt.
/// </summary>
/// <remarks>
/// <para>
/// The Marketplace reads this file from the folder the package's project URL points to, which for
/// a GitHub repository is the root of the default branch. It reads it at its own sync, not from a
/// package, so a change here needs no release. The file therefore lives on <c>main</c> only: a
/// copy on <c>dev/v18</c> would be read by nothing and free to drift. This class is deliberately
/// not cherry-picked to <c>dev/v18</c> either.
/// </para>
/// <para>
/// <b>One listing serves every published line</b>, so the file may state nothing that differs
/// between them. Its top-level fields are an allow-list rather than a deny-list of overrides:
/// <c>Title</c> and <c>Description</c> are the obvious ones, but the schema has others, and a field
/// added later is a decision to make in the spec, not one to slip past a list of known names.
/// </para>
/// <para>
/// The author name is compared with the <b>packed</b> <c>&lt;authors&gt;</c>, the value the
/// Marketplace reads, not with the text of <c>Directory.Build.props</c>, where it is XML-escaped
/// and one step removed from what is published.
/// </para>
/// </remarks>
[Collection(PackedSolutionCollection.Name)]
public class MarketplaceListingFileTests(PackedSolutionFixture fixture)
{
    private const string ListingFile = "umbraco-marketplace.json";

    /// <summary>The package a site installs, and the only one listed on the Marketplace.</summary>
    private const string Aggregate = "UBookIt";

    /// <summary>The branch the Marketplace reads. If the default branch changes, the file follows it.</summary>
    private const string DefaultBranch = "main";

    private static readonly string[] AllowedTopLevelFields = ["$schema", "AuthorDetails"];

    [Fact]
    public void The_listing_file_names_the_declared_publisher_and_states_nothing_line_specific()
    {
        var path = Path.Combine(RepoFiles.Root, ListingFile);
        var document = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;

        var problems = Problems(document, DeclaredAuthors(), OwnRawPrefix(), RepoFileExists);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Each mutation of the real file must be refused, and must be refused for the reason it was
    /// made. A guard that passes the real file has only been shown not to fire.
    /// </summary>
    [Theory]
    [InlineData("name", "AuthorDetails.Name")]
    [InlineData("http-url", "AuthorDetails.Url")]
    [InlineData("gif-image", "AuthorDetails.ImageUrl")]
    [InlineData("missing-image", "docs/images/no-such-logo.png")]
    [InlineData("title", "'Title'")]
    [InlineData("empty-description", "AuthorDetails.Description")]
    [InlineData("absent", ListingFile)]
    public void Every_mutation_of_the_listing_file_is_refused(string mutation, string named)
    {
        var path = Path.Combine(RepoFiles.Root, ListingFile);
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var author = document["AuthorDetails"]!;
        var prefix = OwnRawPrefix();

        switch (mutation)
        {
            case "name":
                author["Name"] = "Somebody Else Ltd.";
                break;
            case "http-url":
                author["Url"] = "http://www.norwood-development.co.uk";
                break;
            case "gif-image":
                author["ImageUrl"] = prefix + "docs/images/ndd-logo.gif";
                break;
            case "missing-image":
                author["ImageUrl"] = prefix + "docs/images/no-such-logo.png";
                break;
            case "title":
                document["Title"] = "uBookIt";
                break;
            case "empty-description":
                author["Description"] = " ";
                break;
            case "absent":
                document = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        // Anti-vacuity: the unmutated file passes, so a refusal below is the mutation's doing.
        var original = JsonNode.Parse(File.ReadAllText(path));
        Assert.Empty(Problems(original, DeclaredAuthors(), prefix, RepoFileExists));

        var problems = Problems(document, DeclaredAuthors(), prefix, RepoFileExists);

        Assert.True(
            problems.Any(p => p.Contains(named, StringComparison.Ordinal)),
            $"Mutation '{mutation}' was not refused for naming '{named}'. Problems reported: "
            + (problems.Count == 0 ? "none" : string.Join(" | ", problems)));
    }

    /// <summary>
    /// Everything wrong with the listing file, each naming the field or path at fault. Empty when
    /// the file is right. Takes the document as a parameter so a mutation can be checked without
    /// touching the real file.
    /// </summary>
    private static List<string> Problems(
        JsonNode? document, string declaredAuthors, string ownRawPrefix, Func<string, bool> repoFileExists)
    {
        var problems = new List<string>();

        if (document is not JsonObject root)
        {
            problems.Add(
                $"There is no {ListingFile} object at the repository root, so the Marketplace listing "
                + "has no author description, website or logo.");
            return problems;
        }

        foreach (var field in root.Select(p => p.Key))
        {
            if (!AllowedTopLevelFields.Contains(field, StringComparer.Ordinal))
            {
                problems.Add(
                    $"{ListingFile} has the top-level field '{field}'. One listing serves every "
                    + "published line, so the file may only carry " + string.Join(" and ", AllowedTopLevelFields)
                    + ". Adding a field is a change to the packaging spec first.");
            }
        }

        if (root["AuthorDetails"] is not JsonObject author)
        {
            problems.Add($"{ListingFile} has no AuthorDetails object.");
            return problems;
        }

        var name = Text(author, "Name");

        if (name != declaredAuthors)
        {
            problems.Add(
                $"AuthorDetails.Name is '{name}', but the packed {Aggregate} declares its authors as "
                + $"'{declaredAuthors}'. The two places that name the publisher must agree.");
        }

        if (Text(author, "Description").Trim().Length == 0)
        {
            problems.Add("AuthorDetails.Description is missing or blank.");
        }

        var url = Text(author, "Url");

        if (!IsAbsoluteHttps(url))
        {
            problems.Add($"AuthorDetails.Url '{url}' is not an absolute https URL.");
        }

        var imageUrl = Text(author, "ImageUrl");

        if (!IsAbsoluteHttps(imageUrl)
            || !(imageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || imageUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(
                $"AuthorDetails.ImageUrl '{imageUrl}' is not an absolute https URL to a .png or .jpg, "
                + "which is what the Marketplace asks for.");
        }
        else if (imageUrl.StartsWith(ownRawPrefix, StringComparison.Ordinal))
        {
            var relative = imageUrl[ownRawPrefix.Length..];

            if (!repoFileExists(relative))
            {
                problems.Add(
                    $"AuthorDetails.ImageUrl points at {relative} on {DefaultBranch}, which this "
                    + "repository does not contain. The listing would show a broken image.");
            }
        }

        return problems;
    }

    private static string Text(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

    private static bool IsAbsoluteHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    private static bool RepoFileExists(string relativePath) =>
        File.Exists(Path.Combine(RepoFiles.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private string DeclaredAuthors()
    {
        var authors = fixture.Packed[Aggregate].Value("authors");

        Assert.True(authors.Length > 0, $"The packed {Aggregate} declares no authors, so there is nothing to compare with.");

        return authors;
    }

    /// <summary>
    /// The raw-content prefix of this repository's default branch, derived from the packed
    /// project URL, which is the URL the Marketplace follows to find the listing file. Derived,
    /// never restated, so a repository move cannot leave the image pointing at the old account
    /// with this test green.
    /// </summary>
    private string OwnRawPrefix()
    {
        var projectUrl = fixture.Packed[Aggregate].Value("projectUrl").TrimEnd('/');

        const string github = "https://github.com/";

        Assert.True(
            projectUrl.StartsWith(github, StringComparison.OrdinalIgnoreCase),
            $"The packed {Aggregate} project URL is '{projectUrl}', not a GitHub repository. The Marketplace "
            + $"would look for {ListingFile} there instead of in this repository.");

        return $"https://raw.githubusercontent.com/{projectUrl[github.Length..]}/{DefaultBranch}/";
    }
}
