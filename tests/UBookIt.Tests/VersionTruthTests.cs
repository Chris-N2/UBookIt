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
/// <b>Each claim is matched by its sentence shape, not by hunting version-shaped tokens.</b>
/// These documents legitimately name other versions — Umbraco 17.x, .NET 10.0 — so a scan
/// for "something that looks like a version" would need a denylist of the ones to ignore,
/// and a denylist fences only what somebody thought of. Anchoring on the phrase that makes
/// the claim ("uBookIt is at …") says precisely which number is uBookIt's own. Rewording the
/// sentence fails the guard rather than slipping past it — a false failure, which is the
/// safe direction.
/// </para>
/// <para>
/// What this proves and what it does not: it proves the NUMBER matches. Whether the
/// sentences around it are true is a different question, held by
/// <see cref="TheVersioningPolicyIsStated"/> below via the house
/// <see cref="DocumentationAssert"/> idiom.
/// </para>
/// </remarks>
public class VersionTruthTests
{
    /// <summary>
    /// Where a document states uBookIt's own version, and the phrase that identifies the
    /// claim. The capture group is the version.
    /// </summary>
    private static readonly (string Path, string Pattern)[] VersionClaims =
    [
        ("README.md", @"uBookIt is at `(?<version>[^`]+)`"),
        ("docs/mvp.md", @"the first release is\s+`(?<version>[^`]+)`"),
    ];

    /// <summary>The version every package is built with.</summary>
    private static string DeclaredVersion()
    {
        var props = RepoFiles.Read("Directory.Build.props");
        var match = Regex.Match(props, @"<Version>(?<version>[^<]+)</Version>");

        Assert.True(
            match.Success,
            "Directory.Build.props declares no <Version>. Every package's version, the stamped "
            + "backoffice manifest and every documented version claim derive from it.");

        return match.Groups["version"].Value.Trim();
    }

    [Fact]
    public void Every_documented_version_is_the_declared_version()
    {
        var declared = DeclaredVersion();

        // Anti-vacuity: a table that fell empty, or a pattern that matched nothing, would
        // otherwise make this pass by checking nothing at all.
        Assert.NotEmpty(VersionClaims);

        foreach (var (path, pattern) in VersionClaims)
        {
            var match = Regex.Match(RepoFiles.Read(path), pattern);

            Assert.True(
                match.Success,
                $"{path} no longer states uBookIt's version in the form this guard reads "
                + $"(/{pattern}/). Either the claim was removed — in which case a reader is no "
                + "longer told which version they have — or it was reworded, in which case "
                + "update the pattern deliberately.");

            var stated = match.Groups["version"].Value;

            Assert.True(
                stated == declared,
                $"{path} says uBookIt is at {stated}, but Directory.Build.props declares "
                + $"{declared}. The documentation describes a release nobody can install.");
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
    /// The pre-release framing must be gone, not merely reworded around it. Both sentences
    /// were live until this release: the README told a reader to treat contracts as settled
    /// from a version that will never exist, and both documents named the pre-release number.
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
}
