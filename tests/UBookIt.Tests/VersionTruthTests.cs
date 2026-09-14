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
    /// <remarks>
    /// The pin is per DOCUMENT, not per claim: <c>docs/mvp.md</c> states the version twice,
    /// and reworking one of the two away leaves this satisfied by the other. Deliberate —
    /// the surviving claim still keeps the file honest at the next bump, which is what this
    /// is protecting, and pinning every individual sentence would make ordinary rewording
    /// fail for no gain.
    /// </remarks>
    private static readonly string[] DocumentsKnownToClaim =
    [
        "README.md",
        "CLAUDE.md",
        "docs/mvp.md",
        "roadmap/version_roadmap.md",
    ];

    /// <summary>
    /// Every live document a reader could meet — the markdown a consumer reads AND the
    /// package sources, whose XML documentation is what a site author meets first, in
    /// IntelliSense.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source files are here because <see cref="DocumentationAssert"/> was built for
    /// them: its own remarks say it strips <c>///</c> "because these guards are asked about
    /// source files as well as markdown", and that a claim in a public type's
    /// <c>&lt;remarks&gt;</c> is the one a site author meets first. A scan that walked only
    /// markdown would be blind exactly where the helper was designed to see (QA round 2).
    /// </para>
    /// <para>
    /// <c>openspec/changes/archive/**</c> is excluded deliberately — it is history, it
    /// records versions that were true when written, and it must never be edited to satisfy
    /// a guard. <c>ref/</c> is Umbraco's source, not ours.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> LiveDocuments()
    {
        var root = RepoFiles.Root;

        foreach (var file in new[] { "README.md", "CLAUDE.md" })
        {
            yield return file;
        }

        foreach (var (directory, pattern) in new[]
        {
            ("docs", "*.md"),
            ("roadmap", "*.md"),
            ("openspec/specs", "*.md"),
            ("src", "*.md"),
            ("src", "*.cs"),
        })
        {
            var full = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var file in EnumeratePruned(full, pattern))
            {
                yield return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            }
        }
    }

    /// <summary>
    /// Walks a directory, skipping build output and dependency trees as it goes.
    /// </summary>
    /// <remarks>
    /// Pruned DURING the walk rather than filtered after it. Filtering afterwards still
    /// enumerates <c>Client/node_modules</c> — tens of thousands of files — and took the
    /// suite from instant to 34 seconds. The difference is invisible in the results and
    /// obvious in the clock.
    /// </remarks>
    private static IEnumerable<string> EnumeratePruned(string directory, string pattern)
    {
        var pending = new Stack<string>();
        pending.Push(directory);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if (Path.GetFileName(child) is not ("node_modules" or "bin" or "obj" or "dist"))
                {
                    pending.Push(child);
                }
            }

            foreach (var file in Directory.EnumerateFiles(current, pattern))
            {
                yield return file;
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
    /// The vocabulary a document uses when it claims THE PACKAGE has reached a feed. Every
    /// occurrence must be classified in <see cref="AcceptedPublicationMentions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scoped to the package as the SUBJECT. A bare "is published" is pervasive in this
    /// repository for a value being published on a read model — "a role's count is
    /// published", "the contract is published" — and matching it unscoped would bury a real
    /// claim under twenty legitimate ones.
    /// </para>
    /// <para>
    /// <b>Matched against text with markdown decoration stripped</b>, and that is not a
    /// nicety. The first version of this scan ran against raw text, so
    /// <c>`UBookIt.Core` is published</c> — backticked, as this repository quotes every
    /// identifier — did not match at all: the single entry in
    /// <see cref="AcceptedPublicationMentions"/> was dead code, and the guard was green for
    /// the wrong reason while the very claim it was written for sat in a file it walked.
    /// Found by mutating the allow-list and watching the test PASS when it had to fail —
    /// a guard is only proven by a mutant that makes it fail for the right reason. Same trap
    /// as <see cref="DocumentationAssert"/>'s wrapped sentence, third costume this change.
    /// </para>
    /// </remarks>
    private static readonly string[] PublicationVocabulary =
    [
        @"nuget\.org",
        @"on NuGet",
        @"(?:uBookIt|UBookIt\.[A-Za-z.]+|[Tt]he package|[Ii]t) (?:is|has been) published",
        @"published to (?:a |the )?feed",
        @"is now (?:live|available) on",
    ];

    /// <summary>
    /// Every accepted occurrence of <see cref="PublicationVocabulary"/>, with the reason it
    /// is accepted. Anything not here fails.
    /// </summary>
    private static readonly (string Document, string Accepted, string Why)[] AcceptedPublicationMentions =
    [
        ("openspec/specs/bookings/spec.md", "UBookIt.Core is published",
            "Pre-existing rationale for stating a break in that requirement, written in "
            + "anticipation of publication. Becomes true the moment the package ships, and "
            + "correcting it would mean replacing a requirement about booker identity "
            + "wholesale for two words — the disproportionate edit CLAUDE.md warns against. "
            + "Recorded here and in the deferred-obligations memory for whichever change "
            + "next legitimately modifies `bookings`."),
    ];

    /// <summary>
    /// No live document claims the package has reached a feed, because it has not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>QA round 1 found this change asserting exactly that</b> in three documents at once,
    /// including this repository's own governing instructions — the defect class the change
    /// exists to close, reintroduced by the change. The repair was to prefer sentences that
    /// stay true ACROSS the publication event over sentences needing a second edit at it.
    /// </para>
    /// <para>
    /// <b>This guard is an ALLOW-LIST, and the first version of it was not.</b> That version
    /// forbade three named sentences — the two it had just deleted, plus one no document ever
    /// contained — under a name that quantified over every document and every phrasing. QA
    /// round 2 demonstrated it green while <c>docs/mvp.md</c> claimed "live on nuget.org
    /// today", and green while <c>bookings/spec.md</c> said "UBookIt.Core is published" in a
    /// directory it walked. The reasoning against it was already written eighty lines above,
    /// in <see cref="VersionClaimPatterns"/>' own doc: a denylist fences only what somebody
    /// thought of. <b>An allow-list fails closed — a new claim, in a phrasing nobody
    /// anticipated, fails until a human classifies it.</b>
    /// </para>
    /// <para>
    /// When the package is published, this does not get deleted: the accepted list absorbs
    /// the claims that become true, one at a time, each with its reason.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_document_claims_the_package_has_reached_a_feed()
    {
        var unclassified = new List<string>();

        foreach (var document in LiveDocuments())
        {
            // Decoration stripped so a backticked or emphasised identifier still reads as
            // the sentence it is. The version scan above deliberately does NOT do this — it
            // needs the backticks to delimit the number it captures.
            var text = Regex.Replace(RepoFiles.Read(document), @"[`*_]", string.Empty);

            foreach (var vocabulary in PublicationVocabulary)
            {
                foreach (Match match in Regex.Matches(text, vocabulary))
                {
                    var accepted = AcceptedPublicationMentions.Any(entry =>
                        entry.Document == document
                        && match.Value.Contains(entry.Accepted, StringComparison.Ordinal));

                    if (!accepted)
                    {
                        unclassified.Add($"{document}: \"{match.Value}\"");
                    }
                }
            }
        }

        Assert.True(
            unclassified.Count == 0,
            "These read as claims that uBookIt has reached a package feed. It has not — "
            + "nothing is pushed. Correct the claim, or, if it is legitimate, add it to "
            + "AcceptedPublicationMentions with the reason:\n  "
            + string.Join("\n  ", unclassified));
    }

    /// <summary>
    /// The publication blocker in <c>Directory.Build.props</c> is now the only thing standing
    /// between a push and a package pointing consumers at a private repository, so the two
    /// halves are tied together: while the URLs are the Azure DevOps ones, the warning must
    /// still be there.
    /// </summary>
    /// <remarks>
    /// A biconditional rather than an assertion about the URLs themselves (QA round 2's
    /// suggestion, taken). Asserting they are NOT the Azure URLs would fail today, when they
    /// correctly are; asserting only that the warning exists would not notice the URLs being
    /// fixed and the stale warning left behind. This fails on the one rot that can actually
    /// happen — somebody tidying the comment away while the URLs remain.
    /// </remarks>
    [Fact]
    public void The_publication_blocker_stands_while_the_private_urls_do()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        var namesPrivateUrls = props.Contains("dev.azure.com", StringComparison.Ordinal);
        var carriesWarning = props.Contains(
            "CORRECT THESE BEFORE THE FIRST PUSH TO A PUBLIC FEED", StringComparison.Ordinal);

        Assert.True(
            namesPrivateUrls == carriesWarning,
            namesPrivateUrls
                ? "Directory.Build.props still points PackageProjectUrl/RepositoryUrl at a "
                  + "PRIVATE Azure DevOps organisation, but the warning that says to correct "
                  + "them before the first push is gone. nuget.org does not allow a pushed "
                  + "version's metadata to be edited: publishing like this costs a version "
                  + "number and leaves the bad listing permanently visible."
                : "The private Azure DevOps URLs are gone from Directory.Build.props — good — "
                  + "but the warning about them is still there, telling a reader to fix "
                  + "something already fixed. Remove it.");
    }
}
