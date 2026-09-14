using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The version a reader is told is the version the package carries (packaging spec, "The
/// version a reader is told is the version the package carries").
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived, never restated.</b> The expected version is parsed out of
/// <c>Directory.Build.props</c> — the single source every package and the stamped backoffice
/// manifest already take it from. A test asserting the literal <c>17.0.0</c> would not close
/// the drift, it would move it one file along: the next bump would leave both the prose and
/// the test agreeing with each other and disagreeing with the build.
/// </para>
/// <para>
/// <b>The population is found by PHRASE across every live document, not by a list of files.</b>
/// The first version of this guard named two files, and QA proved it blind: the roadmap
/// states the version in the identical sentence shape and could be set to <c>16.4.2</c> with
/// the whole suite green. A file list is a sample of the documents that make the claim, and
/// this project's most-repeated lesson is that a sample is not the population.
/// </para>
/// <para>
/// <b>The residual limit, stated rather than hidden.</b> Matching by phrase means a document
/// that invents a NEW way to say it is not covered. That is why
/// <see cref="ClaimsAreFoundWhereTheyAreKnownToLive"/> exists — it pins the documents known
/// to carry claims, so the scan cannot silently find nothing — and why
/// <c>Directory.Build.props</c> tells whoever bumps the version that a new phrasing must be
/// added here. Scanning for version-shaped text instead was rejected: these documents
/// legitimately name Umbraco 17.x and .NET 10.0, so it would need a denylist of versions to
/// ignore, and a denylist fences only what somebody thought of.
/// </para>
/// <para>
/// What this proves and what it does not: it proves the NUMBER matches wherever a known
/// phrasing states it. Whether the sentences around it are true is a different question, held
/// by <see cref="TheVersioningPolicyIsStated"/> via the house <see cref="DocumentationAssert"/>
/// idiom.
/// </para>
/// </remarks>
public class VersionTruthTests
{
    /// <summary>
    /// The ways this repository's documents state uBookIt's own version. Each captures the
    /// version in a group named <c>version</c>, and each tolerates the markdown emphasis and
    /// wrapping these files are actually written with.
    /// </summary>
    private static readonly string[] VersionClaimPatterns =
    [
        @"uBookIt is at\s+\*{0,2}`(?<version>[^`]+)`",
        @"[Tt]he first release is\s+\*{0,2}`(?<version>[^`]+)`",
        @"declared stable from\s+\*{0,2}`(?<version>[^`]+)`",
    ];

    /// <summary>
    /// The documents that must still be found making a claim. Anti-vacuity: without this, a
    /// pattern that stopped matching — or a document that dropped its version claim — would
    /// leave the scan passing over nothing at all.
    /// </summary>
    private static readonly string[] DocumentsKnownToClaim =
    [
        "README.md",
        "CLAUDE.md",
        "docs/mvp.md",
        "roadmap/version_roadmap.md",
    ];

    /// <summary>
    /// Every live document a reader could meet. <c>openspec/changes/archive/**</c> is
    /// excluded deliberately — it is history, it records versions that were true when
    /// written, and it must never be edited to satisfy a guard.
    /// </summary>
    private static IEnumerable<string> LiveDocuments()
    {
        var root = RepoFiles.Root;

        foreach (var file in new[] { "README.md", "CLAUDE.md" })
        {
            yield return file;
        }

        foreach (var directory in new[] { "docs", "roadmap", "openspec/specs" })
        {
            var full = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                yield return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            }
        }
    }

    /// <summary>The version every package is built with.</summary>
    private static string DeclaredVersion()
    {
        var props = RepoFiles.Read("Directory.Build.props");
        var matches = Regex.Matches(props, @"<Version>(?<version>[^<]+)</Version>");

        // Exactly one, not merely at least one: a second declaration — conditional, or left
        // commented above the real one — would otherwise be read silently, and whichever came
        // first would quietly become the number every document is checked against.
        Assert.True(
            matches.Count == 1,
            $"Directory.Build.props declares {matches.Count} <Version> elements; every package's "
            + "version, the stamped backoffice manifest and every documented version claim "
            + "derive from exactly one.");

        return matches[0].Groups["version"].Value.Trim();
    }

    [Fact]
    public void Every_documented_version_is_the_declared_version()
    {
        var declared = DeclaredVersion();
        var offenders = new List<string>();

        foreach (var document in LiveDocuments())
        {
            var text = RepoFiles.Read(document);

            foreach (var pattern in VersionClaimPatterns)
            {
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    var stated = match.Groups["version"].Value;

                    if (stated != declared)
                    {
                        offenders.Add($"{document}: states {stated}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Directory.Build.props declares {declared}, but these documents state another "
            + "version — each describes a release nobody can install:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void ClaimsAreFoundWhereTheyAreKnownToLive()
    {
        // The anti-vacuity half. Every document here must still state the version in a
        // phrasing this guard knows; if one is reworded, this fails loudly rather than
        // letting the scan above pass by finding nothing.
        Assert.NotEmpty(VersionClaimPatterns);

        foreach (var document in DocumentsKnownToClaim)
        {
            var text = RepoFiles.Read(document);

            Assert.True(
                VersionClaimPatterns.Any(pattern => Regex.IsMatch(text, pattern)),
                $"{document} no longer states uBookIt's version in any phrasing this guard "
                + "reads. Either the claim was removed — in which case a reader is no longer "
                + "told which version they have — or it was reworded, in which case add the "
                + "new phrasing to VersionClaimPatterns deliberately.");
        }
    }

    /// <summary>
    /// The policy a consumer plans an upgrade against. Each of these changes what somebody
    /// does, which is the bar for a documentation guard in this repository.
    /// </summary>
    [Fact]
    public void TheVersioningPolicyIsStated()
    {
        var readme = RepoFiles.Read("README.md");

        // What the major means — and, because it does not mean what SemVer trains a reader to
        // expect, that the departure is named rather than left to be inferred.
        DocumentationAssert.Says(readme, "The major tracks the Umbraco major");
        DocumentationAssert.Says(
            readme,
            "the major is not a breaking-change signal, and this is where uBookIt departs from "
            + "Semantic Versioning");

        // Where a breaking change may appear, and what comes with it.
        DocumentationAssert.Says(readme, "it lands in a minor release");
        DocumentationAssert.Says(
            readme,
            "ships with sensible defaults or a documented upgrade path so a site that already "
            + "works keeps working");
        DocumentationAssert.Says(readme, "A patch release never carries a breaking change");
    }

    /// <summary>
    /// The pre-release framing must be gone, not merely reworded around it.
    /// </summary>
    [Fact]
    public void The_pre_release_framing_is_gone()
    {
        foreach (var path in new[] { "README.md", "docs/mvp.md" })
        {
            var document = RepoFiles.Read(path);

            DocumentationAssert.DoesNotSay(document, "the public API may still move");
            DocumentationAssert.DoesNotSay(document, "treat contracts as settled from 1.0.0");
        }
    }

    /// <summary>
    /// No live document claims the package has been published, because it has not been.
    /// </summary>
    /// <remarks>
    /// <b>QA round 1 found this change asserting exactly that</b>, in three documents at once
    /// including this repository's own governing instructions — the defect class the change
    /// exists to close, reintroduced by the change. The repair was not only to reword them but
    /// to prefer sentences that stay true ACROSS the publication event ("the surface is
    /// declared stable from 17.0.0", "the release is prepared and versioned") over sentences
    /// that need a second edit at it. This guard holds the line until a push actually happens
    /// — at which point it is the thing to delete, deliberately, in the change that publishes.
    /// </remarks>
    [Fact]
    public void No_document_claims_the_package_is_already_published()
    {
        foreach (var document in LiveDocuments())
        {
            var text = RepoFiles.Read(document);

            DocumentationAssert.DoesNotSay(text, "and it is published, from 17.0.0");
            DocumentationAssert.DoesNotSay(text, "v1 shipped as 17.0.0");
            DocumentationAssert.DoesNotSay(text, "uBookIt is published on NuGet");
        }
    }
}
