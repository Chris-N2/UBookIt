using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
/// added later is a decision to make in the spec, not one to slip past a list of known names. The
/// free text inside <c>AuthorDetails</c> is checked for naming an Umbraco major, in the vocabulary
/// <c>Umbraco</c>, whitespace, digits, case-sensitive: the same vocabulary, and the same limit, as
/// the package-description guard. "Umbraco v18" would pass unseen and rests on review.
/// </para>
/// <para>
/// <b>A logo URL into this repository has exactly one accepted form</b>: the raw URL of the default
/// branch as the packed project URL spells it. Any other form that reaches this repository - the
/// <c>refs/heads/main</c> form GitHub's own Raw button produces, <c>github.com/…/raw/…</c>, another
/// branch, a different letter case, dot segments - is refused rather than waved through, because a
/// check that recognised only the canonical form passed every other form without looking. The
/// canonical form's file must be <b>tracked</b> (<c>git ls-files</c>, compared ordinally): GitHub
/// serves only committed files and is case-sensitive, and a file-system check on Windows is neither.
/// A logo hosted anywhere else is checked for scheme and extension only.
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

    /// <summary>Hosts through which a URL can reach a GitHub repository's files.</summary>
    private static readonly string[] GitHubHosts = ["github.com", "www.github.com"];

    private static readonly Regex UmbracoMajor = new(@"Umbraco\s+\d+", RegexOptions.CultureInvariant);

    private static readonly Lazy<HashSet<string>> Tracked = new(TrackedFiles);

    [Fact]
    public void The_listing_file_names_the_declared_publisher_and_states_nothing_line_specific()
    {
        var path = Path.Combine(RepoFiles.Root, ListingFile);
        var document = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;

        var problems = Problems(document, DeclaredAuthors(), OwnRepository(), Tracked.Value.Contains);

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
    [InlineData("wrong-case-image", "docs/images/NDD-logo.png")]
    [InlineData("refs-heads-form", "not the accepted form")]
    [InlineData("github-raw-form", "not the accepted form")]
    [InlineData("lower-case-repository", "not the accepted form")]
    [InlineData("other-branch", "not the accepted form")]
    [InlineData("dot-segments", "not the accepted form")]
    [InlineData("title", "'Title'")]
    [InlineData("empty-description", "AuthorDetails.Description")]
    [InlineData("umbraco-major", "Umbraco 17")]
    [InlineData("absent", ListingFile)]
    public void Every_mutation_of_the_listing_file_is_refused(string mutation, string named)
    {
        var path = Path.Combine(RepoFiles.Root, ListingFile);
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var author = document["AuthorDetails"]!;
        var (owner, repository) = OwnRepository();
        var canonical = CanonicalPrefix(owner, repository);

        switch (mutation)
        {
            case "name":
                author["Name"] = "Somebody Else Ltd.";
                break;
            case "http-url":
                author["Url"] = "http://www.norwood-development.co.uk";
                break;
            case "gif-image":
                author["ImageUrl"] = canonical + "docs/images/ndd-logo.gif";
                break;
            case "missing-image":
                author["ImageUrl"] = canonical + "docs/images/no-such-logo.png";
                break;
            case "wrong-case-image":
                author["ImageUrl"] = canonical + "docs/images/NDD-logo.png";
                break;
            case "refs-heads-form":
                author["ImageUrl"] = $"https://raw.githubusercontent.com/{owner}/{repository}/refs/heads/{DefaultBranch}/docs/images/no-such-logo.png";
                break;
            case "github-raw-form":
                author["ImageUrl"] = $"https://github.com/{owner}/{repository}/raw/{DefaultBranch}/docs/images/no-such-logo.png";
                break;
            case "lower-case-repository":
                author["ImageUrl"] = $"https://raw.githubusercontent.com/{owner.ToLowerInvariant()}/{repository.ToLowerInvariant()}/{DefaultBranch}/docs/images/no-such-logo.png";
                break;
            case "other-branch":
                author["ImageUrl"] = $"https://raw.githubusercontent.com/{owner}/{repository}/dev/v18/docs/images/ndd-logo.png";
                break;
            case "dot-segments":
                author["ImageUrl"] = canonical + "docs/../docs/images/ndd-logo.png";
                break;
            case "title":
                document["Title"] = "uBookIt";
                break;
            case "empty-description":
                author["Description"] = " ";
                break;
            case "umbraco-major":
                author["Description"] = "Builds booking packages for Umbraco 17.";
                break;
            case "absent":
                document = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        // Anti-vacuity: the unmutated file passes, so a refusal below is the mutation's doing.
        var original = JsonNode.Parse(File.ReadAllText(path));
        Assert.Empty(Problems(original, DeclaredAuthors(), (owner, repository), Tracked.Value.Contains));

        var problems = Problems(document, DeclaredAuthors(), (owner, repository), Tracked.Value.Contains);

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
        JsonNode? document,
        string declaredAuthors,
        (string Owner, string Repository) own,
        Func<string, bool> isTracked)
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

        foreach (var (field, value) in author)
        {
            if (value is JsonValue text && text.TryGetValue<string>(out var content)
                && UmbracoMajor.Match(content) is { Success: true } major)
            {
                problems.Add(
                    $"AuthorDetails.{field} names '{major.Value}'. One listing serves every published "
                    + "line, so the file may not name an Umbraco major.");
            }
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

        if (!IsAbsoluteHttps(url, out _))
        {
            problems.Add($"AuthorDetails.Url '{url}' is not an absolute https URL.");
        }

        var imageUrl = Text(author, "ImageUrl");

        if (!IsAbsoluteHttps(imageUrl, out var image)
            || !(imageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || imageUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add(
                $"AuthorDetails.ImageUrl '{imageUrl}' is not an absolute https URL to a .png or .jpg, "
                + "which is what the Marketplace asks for.");
        }
        else if (PointsIntoRepository(image!, own))
        {
            var canonical = CanonicalPrefix(own.Owner, own.Repository);

            if (!imageUrl.StartsWith(canonical, StringComparison.Ordinal)
                || imageUrl != image!.AbsoluteUri
                || image.Query.Length > 0
                || image.Fragment.Length > 0)
            {
                problems.Add(
                    $"AuthorDetails.ImageUrl '{imageUrl}' points into this repository but is not the "
                    + $"accepted form. Use {canonical}<path>, so the file it names can be checked.");
            }
            else
            {
                var relative = imageUrl[canonical.Length..];

                if (!isTracked(relative))
                {
                    problems.Add(
                        $"AuthorDetails.ImageUrl points at {relative} on {DefaultBranch}, which this "
                        + "repository does not track. The listing would show a broken image.");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Whether a URL reaches this repository's files through any GitHub host, compared without
    /// regard to case, because GitHub resolves owner and repository names that way.
    /// </summary>
    private static bool PointsIntoRepository(Uri url, (string Owner, string Repository) own)
    {
        var host = url.Host;

        if (!GitHubHosts.Contains(host, StringComparer.OrdinalIgnoreCase)
            && !host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length >= 2
            && segments[0].Equals(own.Owner, StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals(own.Repository, StringComparison.OrdinalIgnoreCase);
    }

    private static string CanonicalPrefix(string owner, string repository) =>
        $"https://raw.githubusercontent.com/{owner}/{repository}/{DefaultBranch}/";

    private static string Text(JsonObject node, string field) =>
        node[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;

    private static bool IsAbsoluteHttps(string value, out Uri? uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>The files git tracks, as GitHub would serve them: forward slashes, case-sensitive.</summary>
    private static HashSet<string> TrackedFiles()
    {
        var start = new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = RepoFiles.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started to list tracked files.");

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git ls-files failed ({process.ExitCode}): {process.StandardError.ReadToEnd()}");
        }

        var files = output.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        // Anti-vacuity: an empty listing would refuse every logo for the wrong reason.
        if (!files.Contains(ListingFile))
        {
            throw new InvalidOperationException(
                $"git ls-files does not list {ListingFile}, so the tracked-file check has nothing to stand on.");
        }

        return files;
    }

    private string DeclaredAuthors()
    {
        var authors = fixture.Packed[Aggregate].Value("authors");

        Assert.True(authors.Length > 0, $"The packed {Aggregate} declares no authors, so there is nothing to compare with.");

        return authors;
    }

    /// <summary>
    /// This repository's owner and name, derived from the packed project URL, which is the URL the
    /// Marketplace follows to find the listing file. Derived, never restated, so a repository move
    /// cannot leave the image pointing at the old account with this test green.
    /// </summary>
    private (string Owner, string Repository) OwnRepository()
    {
        var projectUrl = fixture.Packed[Aggregate].Value("projectUrl").TrimEnd('/');

        const string github = "https://github.com/";

        var parts = projectUrl.StartsWith(github, StringComparison.OrdinalIgnoreCase)
            ? projectUrl[github.Length..].Split('/')
            : [];

        Assert.True(
            parts.Length == 2,
            $"The packed {Aggregate} project URL is '{projectUrl}', not a GitHub repository. The Marketplace "
            + $"would look for {ListingFile} there instead of in this repository.");

        return (parts[0], parts[1]);
    }
}
