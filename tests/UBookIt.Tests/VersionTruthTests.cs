using System.Buffers.Binary;
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
    /// The ways this repository's documents state the version uBookIt is <b>currently at</b>.
    /// Each captures the version in a group named <c>version</c>, and each tolerates the
    /// markdown emphasis and wrapping these files are actually written with.
    /// </summary>
    private static readonly string[] CurrentVersionClaimPatterns =
    [
        @"uBookIt is at\s+\*{0,2}`(?<version>[^`]+)`",
    ];

    /// <summary>
    /// The ways this repository's documents state a version that is <b>not</b> the current one:
    /// the release the package first made, and the release its public API was declared stable
    /// from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>These are statements about history, and history does not move when a release does.</b>
    /// Until <c>17.0.1</c> there had only ever been one version, so "the version uBookIt is at"
    /// and "the version uBookIt was first released at" were the same string and a single list of
    /// patterns could not tell them apart. The first patch release separated them: requiring
    /// every stated version to equal the declared one would have demanded "the first release is
    /// <c>17.0.1</c>", and the quickest way to make that guard pass would have been to edit four
    /// true sentences into false ones.
    /// </para>
    /// <para>
    /// So anchors are checked differently, by
    /// <see cref="The_documented_anchors_do_not_move"/>: they are pinned to the first release
    /// recorded in <c>openspec/changes/archive/</c>, and must additionally agree with each other
    /// and name no version later than the declared one. The pin is what catches a bump that
    /// moves every anchor at once — agreement alone does not, as QA demonstrated. None of it
    /// needs a literal version written into this file, which is the property that made the
    /// original guard worth having.
    /// </para>
    /// </remarks>
    private static readonly string[] VersionAnchorPatterns =
    [
        @"[Tt]he first release is\s+\*{0,2}`(?<version>[^`]+)`",
        @"declared stable from\s+\*{0,2}`(?<version>[^`]+)`",
    ];

    /// <summary>
    /// Every phrasing that states a version of uBookIt, of either kind. Used only by the
    /// anti-vacuity pin, which cares that a document still says something about the version —
    /// not which of the two things it says.
    /// </summary>
    private static readonly string[] VersionClaimPatterns =
        [.. CurrentVersionClaimPatterns, .. VersionAnchorPatterns];

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

        // The runbook names a version, and it is the document a maintainer opens at the NEXT
        // release — the worst place for a stale number. Registered here rather than given a
        // new VersionClaimPattern: a pattern able to match a bare version inside a shell
        // command would fire across every document walked and need exclusions back.
        "docs/publishing.md",
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

        // CHANGELOG.md joined this list in release-17-1-0, and the omission was the whole point:
        // a third consumer-facing root document was added and every documentation guard in this
        // repository was blind to it, including the accounting guard below — which found three
        // unregistered nuget.org claims the moment it could see the file. "A file list is a sample
        // of the documents that make the claim, and a sample is not the population" is this class's
        // own lesson, and adding a root document without adding it here repeats it exactly.
        foreach (var file in new[] { "README.md", "CLAUDE.md", "CHANGELOG.md" })
        {
            yield return file;
        }

        // The OpenSpec context file is prose, and it is injected into EVERY future artifact —
        // so a false claim there propagates into changes not yet written. QA round 1 found a
        // falsified hosting sentence in it; round 2 found my fix had put a publication-
        // sensitive clause back into the same blind spot, green while the file could claim
        // the package was live on a feed. TOP LEVEL ONLY: the per-change `.openspec.yaml`
        // files carry no prose (a schema name and a date), and the ones under
        // `changes/archive/` are history that must never be edited to satisfy a guard.
        var openspec = Path.Combine(root, "openspec");

        if (Directory.Exists(openspec))
        {
            foreach (var file in Directory.EnumerateFiles(openspec, "*.yaml", SearchOption.TopDirectoryOnly))
            {
                yield return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            }
        }

        foreach (var (directory, pattern) in new[]
        {
            ("docs", "*.md"),
            ("roadmap", "*.md"),
            ("openspec/specs", "*.md"),
            ("src", "*.md"),
            ("src", "*.cs"),

            // The shipped views and the backoffice client: the last surfaces with any
            // consumer-facing text. Verified clean when added, so this closes the category
            // rather than leaving it declared-latent.
            //
            // The .ts sweep reads GENERATED sources too (`api/*.gen.ts`, emitted by
            // @hey-api/openapi-ts). Deliberate — generated text ships to a consumer exactly
            // like written text — but it couples this guard to codegen output, so a
            // regeneration could fail the suite over words nobody typed. Clean today; if it
            // ever fires there, fix the generator input or exclude the file explicitly, and
            // never by widening the vocabulary.
            ("src", "*.cshtml"),
            ("src", "*.ts"),
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
    /// <para>
    /// Pruned DURING the walk rather than filtered after it. Filtering afterwards still
    /// enumerates <c>Client/node_modules</c> — tens of thousands of files — and took the
    /// suite from instant to 34 seconds. The difference is invisible in the results and
    /// obvious in the clock.
    /// </para>
    /// <para>
    /// It prunes by directory NAME anywhere beneath the root, so a source directory
    /// legitimately called <c>dist</c> would be skipped with nothing to notice. None exists
    /// under <c>src/</c> today, and no git-tracked source lives under any pruned name.
    /// </para>
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

    /// <summary>
    /// The version every package is built with.
    /// <para>
    /// <b>Internal rather than private</b> so <see cref="ChangelogTests"/> reads the declared
    /// version through the same parser, including the exactly-one assertion below. A second
    /// `&lt;Version&gt;` parse elsewhere would be a second answer to "what version is this?",
    /// which is the defect this whole class exists to prevent.
    /// </para>
    /// </summary>
    internal static string DeclaredVersion()
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
        var claimsFound = 0;

        foreach (var document in LiveDocuments())
        {
            var text = RepoFiles.Read(document);

            foreach (var pattern in CurrentVersionClaimPatterns)
            {
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    claimsFound++;

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

        // Anti-vacuity, and specifically the hole that SPLITTING the patterns opened. Before the
        // split, ClaimsAreFoundWhereTheyAreKnownToLive covered this: any pinned document losing
        // its claim failed there. It no longer does — a document can satisfy that pin with an
        // ANCHOR alone, which would leave this scan running over nothing while reporting that
        // every documented version matches.
        Assert.True(
            claimsFound > 0,
            "No document states the version uBookIt is currently at, so this guard compared "
            + "nothing against Directory.Build.props and passed by finding nothing. Either the "
            + "claim was removed from every document, or it was reworded out of "
            + "CurrentVersionClaimPatterns.");
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
                + "told which version they have — or it was reworded, in which case add the new "
                + "phrasing deliberately: to CurrentVersionClaimPatterns if it states the "
                + "version uBookIt is AT, or to VersionAnchorPatterns if it records a version "
                + "in its history. (VersionClaimPatterns itself is the two concatenated and "
                + "cannot be added to.)");
        }
    }

    /// <summary>
    /// A version the documentation anchors to records history, and does not move when a release
    /// does (packaging spec, "A version the documentation anchors to does not move with the
    /// release").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pinned to an independent record, and still without a literal.</b> The obvious
    /// implementation — assert every anchor says <c>17.0.0</c> — would put the very number these
    /// documents state into the file that checks them, which is the restatement this whole class
    /// exists to avoid. The version the package was first released at is instead derived from
    /// <c>openspec/changes/archive/</c>: the earliest archived change named
    /// <c>release-&lt;major&gt;-&lt;minor&gt;-&lt;patch&gt;</c>. That directory is history, it is
    /// immutable by CLAUDE.md's rule, and nothing that edits documentation can move it.
    /// </para>
    /// <para>
    /// <b>Agreement between the anchors is NOT sufficient, and the first version of this guard
    /// shipped believing it was.</b> QA moved all four anchors from <c>17.0.0</c> to
    /// <c>17.0.1</c> together and every test in this class stayed green — which is precisely the
    /// failure the change was written to prevent, since a repo-wide find-and-replace at the next
    /// bump moves them uniformly by construction. Mutual agreement only catches the sloppy
    /// version of that edit, the one that misses a file. The archive pin catches the tidy one.
    /// </para>
    /// <para>
    /// <b>The remark this replaces asserted something false</b> — that nothing in this repository
    /// records the first release independently — and used it to justify the weaker guarantee. The
    /// archive was there the whole time; <see cref="LiveDocuments"/> excludes it precisely
    /// BECAUSE it is an immutable record, which is the property that makes it usable here.
    /// Reading the archive is not editing it.
    /// </para>
    /// <para>
    /// Agreement and the not-later-than-declared check are both kept. They are now redundant
    /// with the pin rather than load-bearing, and they fail with a more specific message when
    /// they fire first, which is worth more than the line count they cost.
    /// </para>
    /// <para>
    /// What this still does not prove: that the release named in the archive is the release the
    /// API was truly frozen at. It proves the documentation agrees with the repository's own
    /// immutable record of its first release.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_documented_anchors_do_not_move()
    {
        var declared = DeclaredVersion();
        var anchors = new List<(string Document, string Stated)>();

        foreach (var document in LiveDocuments())
        {
            var text = RepoFiles.Read(document);

            foreach (var pattern in VersionAnchorPatterns)
            {
                foreach (Match match in Regex.Matches(text, pattern))
                {
                    anchors.Add((document, match.Groups["version"].Value));
                }
            }
        }

        // A scan over nothing passes every assertion made about it.
        Assert.True(
            anchors.Count > 0,
            "No document anchors to a first release or a stability version. Either those "
            + "sentences were removed — in which case a reader is no longer told when the API "
            + "promise began — or they were reworded out of VersionAnchorPatterns.");

        var distinct = anchors.Select(a => a.Stated).Distinct(StringComparer.Ordinal).ToList();

        Assert.True(
            distinct.Count == 1,
            "The documents disagree about the version uBookIt was first released at, and at most "
            + "one of them can be right. This is what a find-and-replace across a version bump "
            + $"looks like:\n  {string.Join("\n  ", anchors.Select(a => $"{a.Document}: {a.Stated}"))}");

        var anchor = ParseRelease(distinct[0]);
        var current = ParseRelease(declared);

        Assert.True(
            anchor is not null && current is not null,
            $"An anchor states '{distinct[0]}' and Directory.Build.props declares '{declared}'; "
            + "at least one is not a version this guard can compare. Pre-release suffixes are "
            + "tolerated and ignored, but the numeric part has to parse.");

        Assert.True(
            anchor <= current,
            $"The documentation anchors to {distinct[0]}, which is later than the declared "
            + $"{declared}. A package cannot have been first released in a version it has not "
            + "reached — most likely the anchor was moved by a bump that should have left it "
            + "alone.");

        // THE CHECK THAT CATCHES A UNIFORM MOVE. Everything above is satisfied by four anchors
        // edited together to the same wrong value, which is exactly what a repo-wide
        // find-and-replace at the next bump produces.
        var firstReleased = FirstReleasedVersion();

        Assert.True(
            anchor == firstReleased,
            $"The documentation anchors to {distinct[0]}, but this repository's own archive "
            + $"records the first release as {firstReleased} "
            + "(openspec/changes/archive/*-release-<version>). These sentences record history, so "
            + "a release does not move them. If a version bump edited them — most likely a "
            + "find-and-replace that moved every one of them together — put them back.");
    }

    /// <summary>
    /// The version this package was first released at, read from the repository's own immutable
    /// record: the earliest archived change named <c>release-&lt;major&gt;-&lt;minor&gt;-&lt;patch&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Derived rather than written down, so no literal version enters this file. The archive is
    /// the right source because CLAUDE.md forbids editing it: an edit that falsifies documentation
    /// cannot also move the thing the documentation is checked against.
    /// <para>
    /// Its reach is exactly the naming convention: a release change named some other way is not
    /// seen. That is safe in the direction that matters — the earliest release is already
    /// archived and cannot be removed, so this value is stable no matter what later changes are
    /// called.
    /// </para>
    /// </remarks>
    private static Version FirstReleasedVersion()
    {
        var archive = Path.Combine(RepoFiles.Root, "openspec", "changes", "archive");

        Assert.True(Directory.Exists(archive), $"No archive at {archive} to read the first release from.");

        var releases = Directory
            .EnumerateDirectories(archive)
            .Select(directory => Regex.Match(
                Path.GetFileName(directory) ?? string.Empty,
                @"release-(?<major>\d+)-(?<minor>\d+)-(?<patch>\d+)$"))
            .Where(match => match.Success)
            .Select(match => new Version(
                int.Parse(match.Groups["major"].Value),
                int.Parse(match.Groups["minor"].Value),
                int.Parse(match.Groups["patch"].Value)))
            .ToList();

        // A scan over nothing passes every assertion made about it — and here it would silently
        // turn the pin above into no check at all.
        Assert.True(
            releases.Count > 0,
            $"No archived change under {archive} is named release-<major>-<minor>-<patch>, so the "
            + "first released version cannot be derived and the anchor pin would prove nothing. "
            + "The archive is immutable, so this means the naming convention changed.");

        return releases.Min()!;
    }

    /// <summary>
    /// Parses the numeric part of a release, ignoring any pre-release suffix, or null when it
    /// is not a version at all.
    /// </summary>
    private static Version? ParseRelease(string value)
        => Version.TryParse(value.Split('-', '+')[0], out var version) ? version : null;

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
        @"(?:uBookIt|UBookIt\.[A-Za-z.]+|[Tt]he package|[Ii]t) (?:is|was|has been|have been) published",
        @"(?:published|released) to (?:a |the )?feed",
        @"is now (?:live|available) on",
    ];

    /// <summary>
    /// Every accepted occurrence of <see cref="PublicationVocabulary"/>: the document, the
    /// text, HOW MANY times it may appear there, and why it is accepted.
    /// </summary>
    /// <remarks>
    /// <b>The count is what makes this accept an instance rather than a class.</b> Without
    /// it, one entry excuses any number of copies of that sentence in that file — accepting
    /// a class while meaning to accept an instance, which is the shape of this change's
    /// round-1 finding. It also makes a DEAD entry impossible to miss: an allowance nobody
    /// consumes fails, which is precisely the bug that survived round 2 unnoticed (the entry
    /// matched nothing because the scan read raw text, and nothing said so).
    /// </remarks>
    private static readonly (string Document, string Accepted, int Occurrences, string Why)[] AcceptedPublicationMentions =
    [
        ("openspec/specs/bookings/spec.md", "UBookIt.Core is published", 1,
            "Written in anticipation of publication, as the rationale for stating a break in "
            + "that requirement, and TRUE since 2026-09-15 — UBookIt.Core 17.0.0 is on "
            + "nuget.org. It was carried here for a year as a sentence waiting to become "
            + "correct; it no longer needs excusing, only counting. The entry stays because "
            + "this guard accounts for every mention, not only the doubtful ones."),

        ("CHANGELOG.md", "nuget.org", 3,
            "Added in `release-17-1-0`, and all three VERIFIED against the feed rather than "
            + "reasoned about: GET https://api.nuget.org/v3-flatcontainer/ubookit/index.json and "
            + "…/ubookit.core/index.json both return exactly [17.0.0, 17.0.1] — so \"those versions "
            + "are on nuget.org and cannot be changed\" is true, and 17.1.0 is correctly absent "
            + "until it is pushed. The other two describe what nuget.org DOES as a host: it "
            + "resolves a relative link against the package page (which is why 17.0.1 exists), and "
            + "it serves the latest listed version by default (which is why 17.0.0 was not "
            + "unlisted). This file is now in LiveDocuments(), which it was not when it was "
            + "written — that omission is what let three unregistered claims exist at all.\n"
            + "THE COUNT IS EXACT: three and only three passes. A fourth mention is reported "
            + "unclassified; a drop to two leaves an unconsumed allowance; all three going away "
            + "leaves three. Measured in each direction, against a build that actually contained "
            + "this entry.\n"
            + "That sentence was briefly weakened to \"bounds rather than pins\" on the strength of "
            + "a green run that measured nothing. TWO INDEPENDENT CAUSES were found, and NEITHER "
            + "REMEDY CATCHES THE OTHER — which is the reason both are written here rather than "
            + "the tidier one:\n"
            + "(1) THE MUTATION NEVER APPLIED. The replacement string spanned a line wrap in "
            + "CHANGELOG.md, str.replace matched nothing and returned the text unchanged. A "
            + "rebuild cannot see this; only asserting the file changed can.\n"
            + "(2) THE ASSEMBLY PREDATED THE GUARD. A binary built before CHANGELOG.md joined "
            + "LiveDocuments() never reads the file, so it reports green however carefully the "
            + "mutant is verified — reproduced with a mutation that provably applied. Asserting "
            + "the change cannot see this; only a rebuild can.\n"
            + "So a mutation result is evidence only when BOTH hold: the file demonstrably "
            + "changed, and the assembly was built from the source under test."),

        ("openspec/specs/packaging/spec.md", "nuget.org", 6,
            "Arrived at SYNC, not written by hand — `release-17-0-1`'s requirements moved into "
            + "the main spec and brought the feed's name with them. All five describe what "
            + "nuget.org DOES as a host: it resolves a relative link against the package page, "
            + "renders no image from a relative path, scopes an API key to an owner, shows a "
            + "placeholder where a package has no icon, and reports a rejected readme image to "
            + "the package's own owner alone. None asserts uBookIt has been published — "
            + "that distinction is the one this guard exists to draw, and it survives a sync "
            + "unchanged. The count is exact so a later requirement cannot smuggle a publication "
            + "claim into this file behind an allowance granted for behavioural facts. "
            + "4 -> 5 when `docs-truth-and-screenshots` synced: the guard fired ON THE ARCHIVE, "
            + "which is the moment it is designed for — a sync carries sentences into a spec "
            + "nobody re-read, and the failure is the handover of what to judge.\n"
            + "5 -> 6 when `release-17-1-2` synced, and the guard fired on the archive AGAIN, "
            + "for exactly the reason above: the sentence lived in the change's delta where "
            + "nothing scanned it, and became a spec sentence at sync. The sixth is `The "
            + "package declares which Umbraco majors it accepts` saying \"Versions already on "
            + "nuget.org keep the metadata they were published with\" — and unlike the other "
            + "five this one IS a publication claim, so it is allowed on evidence rather than "
            + "on the behavioural-fact rationale. VERIFIED against the feed, not reasoned "
            + "about: GET …/ubookit/index.json returns 17.0.0, 17.0.1, 17.1.0, 17.1.1, 17.1.2, "
            + "18.0.0, and the published 17.1.1 packages carry `Umbraco.Cms.Web.Website "
            + "17.6.2` with no ceiling — so versions already there do keep what they shipped "
            + "with, which is the whole reason `17.1.2` exists."),

        ("docs/publishing.md", "nuget.org", 22,
            "The publishing runbook names the feed as a DESTINATION — what nuget.org will "
            + "not let you undo, where the API key lives, which source to push to, how to "
            + "tell when indexing has finished — and, since publication, as a RECORD: its "
            + "Status section states that 17.0.0 shipped on 2026-09-15. Both are legitimate "
            + "here, and this is the one document where they should be. The count is "
            + "deliberately exact, so editing the runbook forces a fresh look at whether "
            + "each new mention is an instruction, a true record, or a claim nobody checked. "
            + "17 -> 22 at `docs-truth-and-screenshots`, and the fresh look this forced is "
            + "recorded rather than skipped. Four are BEHAVIOURAL facts about the host — "
            + "that nuget.org renders readme images only from an allow-list, that it "
            + "renders them on its own servers where no test reaches, and that it reports "
            + "a rejected image to the package owner alone. The fifth is the release "
            + "checklist naming the section heading `What nuget.org will not let you "
            + "undo`, so a releaser knows which sentence to move. None asserts anything "
            + "about what has been published. A sixth was written and then removed: a "
            + "parenthetical about the accounting guard itself, which would have spent an "
            + "allowance on prose about the allowance."),
    ];

    /// <summary>
    /// Every mention of a package feed in a live document is deliberate, registered and counted.
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
    /// in this class's own remarks: a denylist fences only what somebody
    /// thought of.
    /// </para>
    /// <para>
    /// <b>What it does and does not reach, stated so this guard and its neighbour agree.</b>
    /// It fails closed on every OCCURRENCE of the vocabulary it knows: an unclassified hit
    /// fails, naming the document and the matched text, and an allowance nobody consumes
    /// fails as dead. It does <b>not</b> see a claim phrased outside that vocabulary —
    /// <c>uBookIt was released to the public gallery</c> matches nothing and passes. That is
    /// the same residual this class's remarks state about the version phrasings, and
    /// it is worded the same way here deliberately: QA round 3 found this paragraph claiming
    /// a reach the vocabulary did not have, which is the round-2 fault — a name promising
    /// more than a body — climbed one layer into the documentation.
    /// </para>
    /// <para>
    /// <b>Its premise changed at publication, and so did its name.</b> Until 2026-09-15 this
    /// asserted an ABSENCE — <c>No_document_claims_the_package_has_reached_a_feed</c> — which was
    /// exactly right while nothing was pushed. `17.0.0` then shipped, and that premise expired:
    /// a guard whose failure message read "It has not — nothing is pushed" was, from that moment,
    /// itself the false claim it existed to prevent. It was neither deleted nor relaxed, both of
    /// which would have satisfied the suite while dropping the only thing watching these
    /// sentences. It was re-premised on what is still true and still worth enforcing: a mention
    /// of the feed must be REGISTERED, with a reason and an exact count.
    /// </para>
    /// <para>
    /// <b>That is a weaker guarantee than the one it replaces, and the weakening is the point
    /// rather than a cost silently absorbed.</b> "No document says X" is decidable here; "every
    /// document that says X is right" is not, because whether a package is on a feed is a fact
    /// about the feed. What survives is the accounting: because counts are exact, editing a
    /// document that mentions the feed forces a fresh look at whether the new text is true. What
    /// does not survive is any claim to catch a false statement about the feed on its own — only
    /// the feed can answer that.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_mention_of_the_feed_is_accounted_for()
    {
        var unclassified = new List<string>();

        // Distinctness asserted rather than left to ToDictionary, which would throw an
        // ArgumentException naming nothing useful. Irrelevant at one entry; the list is
        // DESIGNED to grow at publication, which is exactly when a duplicated pair would
        // appear and want explaining.
        var duplicates = AcceptedPublicationMentions
            .GroupBy(entry => (entry.Document, entry.Accepted))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.Document}: \"{group.Key.Accepted}\"")
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            "AcceptedPublicationMentions names the same (document, text) twice. Merge them "
            + "into one entry with the combined count, so the allowance is stated once:\n  "
            + string.Join("\n  ", duplicates));

        var allowance = AcceptedPublicationMentions.ToDictionary(
            entry => (entry.Document, entry.Accepted), entry => entry.Occurrences);

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
                    // One allowance CONSUMED per occurrence, so a second copy of an accepted
                    // sentence is not excused by the entry that covers the first.
                    var key = AcceptedPublicationMentions
                        .Where(entry => entry.Document == document
                            && match.Value.Contains(entry.Accepted, StringComparison.Ordinal))
                        .Select(entry => ((string, string)?)(entry.Document, entry.Accepted))
                        .FirstOrDefault();

                    if (key is { } accepted && allowance[accepted] > 0)
                    {
                        allowance[accepted]--;
                    }
                    else
                    {
                        unclassified.Add($"{document}: \"{match.Value}\"");
                    }
                }
            }
        }

        // An allowance nobody consumed is a dead entry — the sentence it excuses is gone, or
        // the scan cannot see it. Round 2 shipped exactly that and nothing said so.
        var unconsumed = allowance
            .Where(entry => entry.Value > 0)
            .Select(entry => $"{entry.Key.Item1}: \"{entry.Key.Item2}\" ({entry.Value} unused)")
            .ToList();

        // BOTH reported together, deliberately. Asserting the unclassified list first would
        // throw before the dead entries were even computed, so a dead allowance could stay
        // hidden behind an unrelated failure — and the remarks above call it "impossible to
        // miss". A sentence slightly stronger than its mechanism is the fault this whole
        // change kept climbing; this is it at NIT scale, closed rather than argued with.
        var failures = new List<string>();

        if (unclassified.Count > 0)
        {
            failures.Add(
                "These mention a package feed without being registered. uBookIt IS published, so "
                + "a mention is no longer wrong by definition — but it has to be deliberate. "
                + "Check the sentence is actually true (only the feed can tell you: GET "
                + "https://api.nuget.org/v3-flatcontainer/<id>/index.json), then either reword it "
                + "or add it to AcceptedPublicationMentions with its reason and count:\n  "
                + string.Join("\n  ", unclassified));
        }

        if (unconsumed.Count > 0)
        {
            failures.Add(
                "These AcceptedPublicationMentions entries excuse something no longer there — "
                + "either the claim was corrected (remove the entry) or the scan cannot see it "
                + "(the entry is dead code and proves nothing):\n  "
                + string.Join("\n  ", unconsumed));
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    /// <summary>
    /// The package points a consumer at a public home they can actually reach: both URLs
    /// are <c>https</c>, neither names a known-private host, and both name the repository
    /// the source lives in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This replaces a biconditional that existed to fail exactly once</b> — it tied the
    /// old Azure DevOps URLs to a <c>CORRECT THESE BEFORE THE FIRST PUSH</c> warning, so
    /// fixing the URLs without removing the warning failed, and vice versa. The GitHub move
    /// fired it. **The cheapest way to make that guard pass was to delete it**, which would
    /// have satisfied the suite while dropping the only thing watching the package's public
    /// identity — so it was replaced rather than removed, by a guard on the POSITIVE
    /// property the release depends on. A positive property keeps working after the move;
    /// the absence of a bad one goes vacuous the moment it passes.
    /// </para>
    /// <para>
    /// <b>It deliberately does not reach the network.</b> Fetching the URL would be slow,
    /// fail offline, and start going red for reasons that have nothing to do with this
    /// repository — a private repo, a rate limit, a DNS hiccup. Whether the URL truly
    /// resolves is a human check at publish time, and <c>docs/publishing.md</c> says so.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_package_points_at_a_public_home()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        var urls = new[] { "PackageProjectUrl", "RepositoryUrl" }
            .Select(element => (Element: element, Value: Regex.Match(
                props, $"<{element}>(?<url>[^<]+)</{element}>").Groups["url"].Value))
            .ToList();

        foreach (var (element, value) in urls)
        {
            Assert.True(
                value.Length > 0,
                $"Directory.Build.props declares no <{element}>. Without it a consumer has no"
                + " route from the package page back to the source.");

            Assert.True(
                value.StartsWith("https://", StringComparison.Ordinal),
                $"<{element}> is '{value}' — a package URL must be https.");

            // The hosts uBookIt has actually lived on that a consumer cannot open. Named
            // rather than inferred: 'is this URL public' is not decidable offline, and a
            // guard that pretended otherwise would be the over-claim this file keeps
            // teaching. What IS decidable is that we have not regressed to a home we know
            // was private.
            Assert.True(
                !value.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase),
                $"<{element}> is '{value}', a PRIVATE Azure DevOps organisation. A consumer"
                + " following it from nuget.org or the Umbraco Marketplace reaches a sign-in"
                + " wall. nuget.org will not let a pushed version's metadata be edited, so"
                + " publishing this costs a version number — see docs/publishing.md.");

            // ANCHORED, not a substring: QA's mutant pointed both URLs at
            // "github.com/Chris-N2/UBookIt-fork" and a Contains check passed it. Any
            // repository whose path merely EXTENDS ours would have shipped. The failure
            // message claims the URL names the repository the source lives in, so the
            // mechanism has to decide exactly that.
            Assert.True(
                Regex.IsMatch(value, @"^https://github\.com/Chris-N2/UBookIt(\.git)?$"),
                $"<{element}> is '{value}', which is not the repository the source lives in.");
        }
    }

    /// <summary>
    /// The publishing runbook keeps the claims a maintainer acts on at the one moment the
    /// mistakes are irreversible.
    /// </summary>
    /// <remarks>
    /// Guarded because every one of these costs a VERSION NUMBER rather than a commit if it
    /// is wrong or missing — and because the SourceLink ordering is the half nobody would
    /// rediscover until a consumer's debugger sent them somewhere they cannot go.
    /// </remarks>
    [Fact]
    public void The_publishing_runbook_states_what_cannot_be_undone()
    {
        var runbook = RepoFiles.Read("docs/publishing.md");

        // The spec requires the documentation to name the MANUAL check, because no
        // automated check may reach the network. Unenforced until QA round 1: deleting the
        // sentence left the suite green while the spec scenario said it must be there.
        DocumentationAssert.SaysOnce(
            runbook,
            "It cannot check the URL actually resolves — open it in a browser once, logged out.");

        // The spec requires the documentation to demand INSPECTION of what was produced,
        // rather than trust that the procedure worked. Both halves, because deleting either
        // left the suite green (QA rounds 1 and 2).
        DocumentationAssert.SaysOnce(
            runbook, "check the produced `.nuspec`, not the source, before you push");
        DocumentationAssert.SaysOnce(
            runbook, "Each package carries its own metadata, so check all five");

        // The one-way doors.
        DocumentationAssert.SaysOnce(runbook, "A pushed version's metadata cannot be edited");
        DocumentationAssert.SaysOnce(runbook, "A version number cannot be reused");
        DocumentationAssert.SaysOnce(runbook, "Unlisting is not deletion");

        // The ordering, which is the finding this change was built on.
        DocumentationAssert.SaysOnce(runbook, "This order is load-bearing, not tidiness");

        // The STEP, not just the narrative about it: QA round 2 deleted this line and the
        // suite stayed green while the document still claimed its order was load-bearing.
        // `dotnet pack` is incremental and `--no-incremental` does not govern it, so without
        // this step a stale .nupkg survives the whole procedure and is pushed by the wildcard.
        DocumentationAssert.SaysOnce(runbook, "DELETE the old artifacts");
        DocumentationAssert.SaysOnce(runbook, "Step 3 is not housekeeping");
        DocumentationAssert.SaysOnce(
            runbook,
            "a package built before the remote moved carries the old SourceLink URLs");

        // The release TAG, which the packed readme's screenshots are addressed to. Pinned for
        // the same reason as the manual URL check above: no automated check can see it. The
        // image guard proves the ref matches the declared version and that the file is in this
        // working tree — it cannot prove the tag was pushed, because the tag lives on a remote,
        // and it cannot prove nuget.org rendered anything. Both are human steps, and a runbook
        // is only where a human step can live.
        DocumentationAssert.SaysOnce(
            runbook, "the tag has to exist, and has to be pushed, before the package goes");
        DocumentationAssert.SaysOnce(runbook, "Nothing automated can catch a missing tag");

        // And the check that is the only thing in this project which ever sees what a consumer
        // sees. A rejected readme image is reported ONLY to the package owner, so a broken page
        // is silent to everybody else — which makes "somebody would have told us" false here.
        DocumentationAssert.SaysOnce(
            runbook, "Open the package page and look at the screenshots");

        // The commit SHA. Found by VERIFYING the SourceLink flip rather than reasoning about
        // it: a pack from an unpushed commit yields source links that 404 for every consumer,
        // permanently, and passes every other check here.
        DocumentationAssert.SaysOnce(runbook, "SourceLink embeds the commit SHA");
        DocumentationAssert.SaysOnce(
            runbook, "Pack from the merge commit on main, after it is pushed");
        // And the guard that is meant to fail, so a red test is not read as an obstacle.
        //
        // The sentence pinned here used to be "When you publish, that guard will start failing"
        // — future tense, written before there had been a publication. It came true on
        // 2026-09-15 and then read as a prediction about an event already past, which is how a
        // pinned sentence rots: the guarantee survives, the wording expires, and the guard holds
        // the repository to the expired wording. Replaced with a sentence that is true BEFORE
        // and AFTER any publication, because there will be more of them.
        DocumentationAssert.SaysOnce(
            runbook, "A guard that goes red at publication is doing its job. Do not delete it.");

        // WHAT THE FIRST PUBLISH ACTUALLY COST, pinned for the same reason as everything above:
        // these were learned at an irreversible moment, and an unpinned lesson is one sentence
        // away from being tidied out by somebody who was not there.
        //
        // The 403 is the sharpest of them. Its text names only the API key, which sends a
        // maintainer to check the one thing that is almost never wrong, and the real cause —
        // an organization owner that cannot publish — is invisible from the message.
        DocumentationAssert.SaysOnce(
            runbook, "an organization has its own email address that must be confirmed");
        DocumentationAssert.SaysOnce(runbook, "push new packages and package versions");

        // That ownership is not a reason to delay a push. Without this a maintainer blocks a
        // release on an account problem that is fixable afterwards.
        DocumentationAssert.SaysOnce(runbook, "transferred to an organization");

        // And how to tell a successful push from an installable package: a package presents as
        // unlisted for minutes afterwards, which reads as failure and invites a second push.
        DocumentationAssert.SaysOnce(runbook, "the flat-container endpoint");

        // THE FEED MUST BE NAMED IN THIS DOCUMENT EXACTLY ONCE, because that is the only reason
        // any guard can see the Status line at all.
        //
        // Precisely: this proves ONE sentence here carries the phrase, not that it is the Status
        // line. Moving the phrase into a trailing parenthetical while falsifying the status claim
        // passes — QA measured it. That residue is subsumed by the polarity exemption the runbook
        // already discloses in full, so it is no new exposure; it is stated here so the comment
        // does not claim placement the mechanism never delivered. This is not belt-and-braces over the occurrence count — the count catches the feed
        // name being REMOVED (the allowance goes unconsumed), but it cannot tell you the sentence
        // that is supposed to carry it is the one that lost it.
        //
        // Written because this exact sentence lost the property once: a rewrite phrased it as
        // "was published on <date>", naming no feed, and it became the single sentence in the
        // repository able to make any claim about a package feed with nothing watching. QA
        // falsified it to a version that does not exist and the suite stayed green.
        DocumentationAssert.SaysOnce(runbook, "reached nuget.org on");

        // And the lesson itself, so the next rewrite of that line knows why it is worded as it is.
        DocumentationAssert.SaysOnce(
            runbook, "A sentence whose entire job is to be watched has to be written so the "
            + "watcher can see it");
    }

    /// <summary>
    /// The package names ONE publisher, declared once (packaging spec, "The package names one
    /// publisher, declared once").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Derived from <c>&lt;Company&gt;</c>, never restated.</b> Writing the name literally
    /// here would reproduce the defect this guard exists to close — five copies of a company
    /// name with nothing comparing them, wrong in all five and shipped in every release build —
    /// one file further along.
    /// </para>
    /// <para>
    /// <b>The comparison is against the DECODED value.</b> The name contains an ampersand, so
    /// the props file must spell it <c>&amp;amp;</c> (a bare <c>&amp;</c> is invalid XML and
    /// fails the build) while <c>LICENSE</c> and <c>README.md</c> are plain text and carry a
    /// literal <c>&amp;</c>. Comparing raw text would report a mismatch that is not one —
    /// and, worse, could be "fixed" by putting an XML entity into a licence file.
    /// </para>
    /// <para>
    /// What it does NOT do: check the name is the correct legal name. No test can know that.
    /// It checks that the three places agree with the one declaration; correctness of the name
    /// itself came from the person who owns it.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_package_names_one_publisher()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        string Declared(string element)
        {
            var match = Regex.Match(props, $"<{element}>(?<value>[^<]+)</{element}>");

            Assert.True(
                match.Success,
                $"Directory.Build.props declares no <{element}>. The publisher a consumer sees "
                + "on the package listing comes from it.");

            // Decoded, so the XML spelling and the plain-text spelling compare equal.
            return System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
        }

        var company = Declared("Company");

        Assert.False(
            string.IsNullOrWhiteSpace(company),
            "<Company> is empty, so every check below would pass by comparing nothing.");

        // Authors and Company are DIFFERENT NuGet fields — Authors is the publisher shown on
        // the listing, Company lands in assembly metadata — and nothing else stops them
        // drifting, which is how a listing and the assemblies inside it come to disagree.
        Assert.True(
            Declared("Authors") == company,
            $"<Authors> is '{Declared("Authors")}' but <Company> is '{company}'. They name the "
            + "same publisher on two surfaces and must agree.");

        Assert.True(
            Declared("Copyright").Contains(company, StringComparison.Ordinal),
            $"<Copyright> is '{Declared("Copyright")}', which does not name '{company}'.");

        // The first word of the declared name — the token that means "this sentence is about
        // the publisher". Used below to find EVERY mention, not merely one.
        var marker = company.Split(' ')[0];

        foreach (var document in new[] { "LICENSE", "README.md" })
        {
            var text = RepoFiles.Read(document);

            Assert.True(
                text.Contains(company, StringComparison.Ordinal),
                $"{document} does not name the publisher declared in Directory.Build.props "
                + $"('{company}'). The licence names who grants the rights and the readme is "
                + "packed into every .nupkg, so a disagreement here ships.");

            // EVERY mention, not just one — QA round 1's MAJOR. The README named the publisher
            // twice and only one spelling matched: a `Contains` check was satisfied by the
            // good line while the footer said "Norwood Design & Development" without the
            // "Ltd.". That is the ORIGINAL defect's exact shape — the correct name minus a
            // component — reintroduced in the line added to fix something else.
            //
            // WHAT THIS REACHES, precisely: every mention that BEGINS WITH the declared name's
            // first word, matching case. A case variant ("NORWOOD DESIGN & DEVELOPMENT") or an
            // abbreviation ("NDD") shares no marker and passes — QA round 2 demonstrated both.
            // It contains the near-miss that actually shipped, which is the class worth
            // closing; it is not every possible way to misname the publisher, and saying so
            // keeps the next reader from assuming cover they do not have.
            //
            // PRECONDITION: the declared name's first word does not otherwise appear in these
            // files. It holds because "Norwood" is a proper noun absent from the prose. If a
            // future name began with a common word, this would fail on a CORRECT tree —
            // loudly, at rename time, with a file and an offset — and the right response
            // would be to change the marker, not the prose.
            for (var at = text.IndexOf(marker, StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf(marker, at + 1, StringComparison.Ordinal))
            {
                Assert.True(
                    string.CompareOrdinal(text, at, company, 0, company.Length) == 0,
                    $"{document} mentions '{marker}' at offset {at} without going on to say "
                    + $"'{company}'. Either it is a near-miss spelling of the publisher — how "
                    + "this was wrong in the first place — and it should become the declared "
                    + $"name; or '{marker}' now occurs in ordinary prose, in which case the "
                    + "marker is unsuitable for this name and the guard needs a different one. "
                    + "Do not reword the prose to satisfy the test.");
            }
        }
    }

    /// <summary>
    /// Every link in the packed readme resolves from a host that is not this repository
    /// (packaging spec, "Documentation a consumer follows from the package page resolves").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is a guard and not a review item.</b> The readme is packed into all five
    /// packages and rendered by nuget.org and the Visual Studio Package Manager, both of which
    /// resolve a relative link against their OWN address. A relative link therefore does not
    /// point at this repository from there; it points at a path on nuget.org that does not
    /// exist. Review missed exactly this once, and a published readme cannot be corrected —
    /// like the rest of the metadata it is frozen at push, so the fix costs a version number.
    /// </para>
    /// <para>
    /// <b>Derived, never restated.</b> Both the readme's filename and the repository it may
    /// point into come out of <c>Directory.Build.props</c>. Writing the repository here a
    /// second time is the restatement failure this project has already paid for twice, in the
    /// publisher name and in the version: a future move would leave the readme pointing at the
    /// old host with this test still green.
    /// </para>
    /// <para>
    /// <b>What it proves, and what it does not.</b> It proves link SHAPE (absolute https) and,
    /// for links into this repository, that the PATH names a file that exists here. It does not
    /// prove any URL answers over the network — that stays a human check, made once, logged
    /// out, exactly as <see cref="The_package_points_at_a_public_home"/> says of the declared
    /// URLs. An absolute URL to anywhere else is accepted unchecked, because checking it would
    /// mean a unit test that fails when a third party's site is down.
    /// </para>
    /// <para>
    /// <b>The blind spot, stated rather than implied.</b> Only the path is resolved, never the
    /// git ref. A link reading <c>blob/main/docs/theming.md</c> still passes after <c>main</c>
    /// has become a different product line, because <c>docs/theming.md</c> still exists in THIS
    /// working tree. The branch rename planned for the Umbraco 18 work owns that problem; this
    /// guard cannot see it and does not claim to.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_readme_links_resolve_from_anywhere()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        var readmeFile = Regex
            .Match(props, @"<PackageReadmeFile>(?<file>[^<]+)</PackageReadmeFile>")
            .Groups["file"].Value.Trim();

        Assert.True(
            readmeFile.Length > 0,
            "Directory.Build.props declares no <PackageReadmeFile>, so there is no packed readme "
            + "for this guard to be about. If the readme stopped being packed, that is the defect.");

        var repositoryUrl = Regex
            .Match(props, @"<RepositoryUrl>(?<url>[^<]+)</RepositoryUrl>")
            .Groups["url"].Value.Trim();

        Assert.True(
            repositoryUrl.Length > 0,
            "Directory.Build.props declares no <RepositoryUrl>, so the repository a readme link "
            + "may point into cannot be derived.");

        var blobPrefix = Regex.Replace(repositoryUrl, @"\.git$", string.Empty) + "/blob/";

        var readme = RepoFiles.Read(readmeFile);

        // Markdown inline links and images. The leading '!' distinguishes an image, which has a
        // harsher failure mode than a link: nuget.org renders NO image from a relative path and
        // none at all from a domain outside its allow-list, so the reader is shown a gap with
        // nothing indicating anything was intended.
        var links = Regex.Matches(readme, @"(?<image>!)?\[(?<text>[^\]]*)\]\((?<target>[^)\s]+)\)");

        // A scan over nothing passes every assertion made about it.
        Assert.True(
            links.Count > 0,
            $"No markdown links were found in {readmeFile}. Either the readme lost its links or "
            + "this guard's pattern no longer matches the way they are written.");

        var offenders = new List<string>();
        var resolvedIntoRepository = 0;

        foreach (Match link in links)
        {
            var target = link.Groups["target"].Value;
            var kind = link.Groups["image"].Success ? "image" : "link";
            var text = link.Groups["text"].Value;

            if (!target.StartsWith("https://", StringComparison.Ordinal))
            {
                offenders.Add(
                    $"{kind} '{text}' targets '{target}', which is not an absolute https URL. "
                    + "nuget.org resolves that against the package page, not against the "
                    + "repository, so it leads nowhere.");

                continue;
            }

            if (!target.StartsWith(blobPrefix, StringComparison.Ordinal))
            {
                // An absolute URL to somewhere else. Accepted unchecked — see the remarks.
                continue;
            }

            // blob/<ref>/<path in the repository>
            var afterPrefix = target[blobPrefix.Length..];
            var firstSlash = afterPrefix.IndexOf('/');

            if (firstSlash < 0)
            {
                offenders.Add(
                    $"{kind} '{text}' targets '{target}', which names a git ref but no file "
                    + "beneath it.");

                continue;
            }

            var relativePath = afterPrefix[(firstSlash + 1)..];

            // A '#heading' fragment is resolved by the rendering host, not by the filesystem.
            var fragment = relativePath.IndexOf('#');

            if (fragment >= 0)
            {
                relativePath = relativePath[..fragment];
            }

            var full = Path.Combine(
                RepoFiles.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(full))
            {
                resolvedIntoRepository++;
            }
            else
            {
                offenders.Add(
                    $"{kind} '{text}' targets '{target}', but this repository has no file at "
                    + $"'{relativePath}'. A renamed or deleted document is caught here rather "
                    + "than after it has been frozen into a published version.");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"{readmeFile} carries {offenders.Count} link(s) a consumer cannot follow from a "
            + "package page. nuget.org will not let a pushed readme be edited, so each of these "
            + $"costs a version number once published:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));

        // Anti-vacuity, and a different absence from the one above: every link could be
        // absolute and point somewhere entirely outside this repository, in which case the
        // existence half of this guard would have run over nothing while reporting success.
        Assert.True(
            resolvedIntoRepository > 0,
            $"No link in {readmeFile} resolved to a file in this repository, so the half of this "
            + "guard that checks link TARGETS proved nothing. Either the documentation links "
            + $"stopped pointing at '{blobPrefix}...', or the derivation of that prefix is wrong.");
    }

    /// <summary>
    /// Hosts nuget.org will render a readme image from.
    /// </summary>
    /// <remarks>
    /// <b>Read from NuGet's own documentation, not inferred</b> —
    /// <c>learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges</c>,
    /// consulted 2026-09-21. Only the entries this repository could plausibly use are listed;
    /// the published list is longer and consists of badge services. Note what is NOT here:
    /// plain <c>github.com</c>, which is where the link guard's <c>blob/</c> prefix points. An
    /// image addressed that way renders nowhere and serves an HTML page rather than image bytes.
    /// </remarks>
    private static readonly string[] HostsTheFeedRenders =
    [
        "raw.githubusercontent.com",
        "raw.github.com",
        "media.githubusercontent.com",
        "user-images.githubusercontent.com",
        "camo.githubusercontent.com",
        "avatars.githubusercontent.com",
        "img.shields.io",
        "dev.azure.com",
    ];

    /// <summary>
    /// Every image in the packed readme is one the feed will render, and shows what its release
    /// shipped (packaging spec, "An image in the packed readme is rendered, and shows what its
    /// release shipped").
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An image fails differently from a link, and worse.</b> nuget.org renders no image from
    /// a relative path and none from a host outside its allow-list — and it reports that in a
    /// warning <i>visible only to the package owner</i>. A reader is shown a gap, nobody outside
    /// the project is told, and the readme is frozen, so the repair costs a version number.
    /// </para>
    /// <para>
    /// <b>This does not duplicate <see cref="The_readme_links_resolve_from_anywhere"/>.</b> That
    /// guard resolves a target into the repository only when it begins with the declared
    /// repository's <c>blob/</c> prefix, and an image cannot use that prefix — it is not an
    /// allow-listed host and it serves a web page rather than image bytes. So every image
    /// necessarily takes the form that guard accepts <b>unchecked</b>. Two things are added here:
    /// the host, and the ref.
    /// </para>
    /// <para>
    /// <b>The ref is why this guard exists at all.</b> The readme is frozen per published
    /// version; the image it names is fetched live, every time somebody opens the package page.
    /// An address on a moving branch means the <c>17.1.1</c> page shows whatever the repository
    /// holds years later — a screenshot of a screen that has since changed, or a gap where a
    /// renamed file used to be, on a page nobody can correct. So the ref is a release TAG, and it
    /// is derived from the declared version rather than written out a second time: a bump that
    /// forgets the images fails here instead of shipping a page pointing at the previous release.
    /// </para>
    /// <para>
    /// <b>What it does not prove.</b> That the tag exists, or that the published page rendered
    /// anything. No test can see either — a tag lives on a remote and rendering happens on
    /// nuget.org. <c>docs/publishing.md</c> carries both as steps a person performs, and says
    /// plainly that nothing automated can catch a missing tag.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_readme_images_render_and_show_what_their_release_shipped()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        var readmeFile = Regex
            .Match(props, @"<PackageReadmeFile>(?<file>[^<]+)</PackageReadmeFile>")
            .Groups["file"].Value.Trim();

        var declaredVersion = Regex
            .Match(props, @"<Version>(?<version>[^<]+)</Version>")
            .Groups["version"].Value.Trim();

        var repositoryUrl = Regex
            .Match(props, @"<RepositoryUrl>(?<url>[^<]+)</RepositoryUrl>")
            .Groups["url"].Value.Trim();

        Assert.True(
            readmeFile.Length > 0 && declaredVersion.Length > 0 && repositoryUrl.Length > 0,
            "Directory.Build.props must declare <PackageReadmeFile>, <Version> and "
            + "<RepositoryUrl> for this guard to derive anything. One of them is missing.");

        // github.com/<owner>/<repo>.git -> raw.githubusercontent.com/<owner>/<repo>/
        // Derived, never restated: a repository move must not leave the images pointing at the
        // old account with this test green. Same reasoning as the blob prefix above.
        var ownerAndRepo = Regex.Replace(repositoryUrl, @"^https://github\.com/", string.Empty);
        ownerAndRepo = Regex.Replace(ownerAndRepo, @"\.git$", string.Empty);

        var ownRawPrefix = $"https://raw.githubusercontent.com/{ownerAndRepo}/";

        var readme = RepoFiles.Read(readmeFile);

        var images = Regex.Matches(readme, @"!\[(?<text>[^\]]*)\]\((?<target>[^)\s]+)\)");

        Assert.True(
            images.Count > 0,
            $"{readmeFile} carries no images. Screenshots were added deliberately in 17.1.1 "
            + "because a package page of unbroken prose tells a reader nothing about what they "
            + "are installing. If dropping them is a decision rather than an accident, remove "
            + "this guard in the same change — do not leave it passing over nothing.");

        var offenders = new List<string>();
        var pinnedToThisRelease = 0;

        foreach (Match image in images)
        {
            var target = image.Groups["target"].Value;
            var text = image.Groups["text"].Value;

            if (text.Trim().Length == 0)
            {
                offenders.Add(
                    $"image '{target}' has no alt text. This package's headline claim is "
                    + "accessibility; a readme that ships an undescribed image contradicts it "
                    + "on the first screen a reader sees.");
            }

            var host = Regex.Match(target, @"^https://(?<host>[^/]+)/").Groups["host"].Value;

            if (host.Length == 0)
            {
                offenders.Add(
                    $"image '{text}' targets '{target}', which is not an absolute https URL. "
                    + "nuget.org renders NO image from a relative path, and warns only the "
                    + "package owner.");

                continue;
            }

            if (!HostsTheFeedRenders.Contains(host))
            {
                offenders.Add(
                    $"image '{text}' is served from '{host}', which is not a host nuget.org "
                    + "renders images from. The page will show a gap and tell nobody but the "
                    + "package owner. Allowed here: "
                    + $"{string.Join(", ", HostsTheFeedRenders)}.");

                continue;
            }

            if (!target.StartsWith(ownRawPrefix, StringComparison.Ordinal))
            {
                // An allow-listed host that is not this repository — a badge, say. Accepted
                // unchecked, for the same reason the link guard accepts an outside URL: proving
                // it would mean failing when somebody else's service is down.
                continue;
            }

            // raw.githubusercontent.com/<owner>/<repo>/<ref>/<path in the repository>
            var afterPrefix = target[ownRawPrefix.Length..];
            var firstSlash = afterPrefix.IndexOf('/');

            if (firstSlash < 0)
            {
                offenders.Add(
                    $"image '{text}' targets '{target}', which names a ref but no file beneath "
                    + "it.");

                continue;
            }

            var reference = afterPrefix[..firstSlash];
            var relativePath = afterPrefix[(firstSlash + 1)..];

            if (reference != declaredVersion)
            {
                offenders.Add(
                    $"image '{text}' is pinned to '{reference}' but the declared version is "
                    + $"'{declaredVersion}'. The readme is frozen per release and the image is "
                    + "not: a ref that lags leaves this version's package page showing the "
                    + "previous release's screenshots, and a ref that moves leaves every past "
                    + "page showing today's.");
            }

            var full = Path.Combine(
                RepoFiles.Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(full))
            {
                pinnedToThisRelease++;
            }
            else
            {
                offenders.Add(
                    $"image '{text}' targets '{target}', but this repository has no file at "
                    + $"'{relativePath}'. A renamed or missing screenshot is caught here rather "
                    + "than as a gap on a package page that can never be corrected.");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"{readmeFile} carries {offenders.Count} image(s) a package page will not show "
            + $"correctly:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));

        // Anti-vacuity: every image could be an outside badge, in which case the ref and
        // existence halves above ran over nothing while reporting success.
        Assert.True(
            pinnedToThisRelease > 0,
            $"No image in {readmeFile} resolved to a file in this repository, so the halves of "
            + "this guard that check the REF and the FILE proved nothing. Either the screenshots "
            + $"stopped being served from '{ownRawPrefix}...', or that prefix is derived wrongly.");
    }

    /// <summary>
    /// The package presents an icon (packaging spec, "The package presents an icon").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared once in <c>Directory.Build.props</c>, like the version and the publisher, and
    /// for the same reason: five packages carrying separately-specified icons drift apart, and
    /// the drift is silent because nothing compares them.
    /// </para>
    /// <para>
    /// <b>This guard is deliberately narrower than NuGet.</b> NuGet accepts PNG or JPEG; this
    /// requires PNG, because reading a PNG's dimensions needs only its header and brings in no
    /// image library. Shipping a JPEG would fail here and would be a legitimate change to this
    /// guard, not a defect in it. The limits it enforces — 1MB, square — are NuGet's own, and
    /// they are checked HERE because at push time they would be checked against a version
    /// number that cannot be reused.
    /// </para>
    /// <para>
    /// That the icon is actually IN each produced package is a different claim, held against
    /// the artefact rather than against the project file, by the packaging tests.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_package_carries_an_icon()
    {
        var props = RepoFiles.Read("Directory.Build.props");

        var declared = Regex
            .Match(props, @"<PackageIcon>(?<file>[^<]+)</PackageIcon>")
            .Groups["file"].Value.Trim();

        Assert.True(
            declared.Length > 0,
            "Directory.Build.props declares no <PackageIcon>. Without one every package renders "
            + "with a generic placeholder on nuget.org and in the Package Manager.");

        // The declaration names the path INSIDE the package; the file gets there via a None
        // item. Finding that item is what connects the declaration to a real file — a
        // <PackageIcon> pointing at nothing produces a package whose icon silently does not
        // render, and the build does not object.
        var packing = Regex.Match(
            props,
            @"<None\s+Include=""\$\(MSBuildThisFileDirectory\)(?<source>[^""]*"
            + Regex.Escape(declared)
            + @")""\s+Pack=""true""");

        Assert.True(
            packing.Success,
            $"<PackageIcon> declares '{declared}', but no <None ... Pack=\"true\"> item in "
            + "Directory.Build.props puts a file of that name into the package. The declaration "
            + "would be packed pointing at nothing.");

        var source = packing.Groups["source"].Value
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        var full = Path.Combine(RepoFiles.Root, source);

        Assert.True(
            File.Exists(full),
            $"The icon item names '{source}', but no file exists there. Note the ItemGroup is "
            + "conditioned on Exists(), so a missing file does not fail the BUILD — it silently "
            + "produces packages with no icon.");

        var bytes = File.ReadAllBytes(full);

        Assert.True(
            bytes.Length <= 1024 * 1024,
            $"The icon is {bytes.Length} bytes; NuGet's limit is 1MB.");

        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        Assert.True(
            bytes.Length > 24 && bytes.AsSpan(0, 8).SequenceEqual(pngSignature),
            $"'{source}' is not a PNG. See this guard's remarks: PNG is required here, which is "
            + "narrower than NuGet allows, so that the dimensions below can be read from the "
            + "header without an image library.");

        // IHDR is the first chunk of every PNG: its type tag sits at offset 12, and width and
        // height follow as big-endian 32-bit values at offsets 16 and 20. The tag is checked
        // rather than assumed, so the numbers read below are known to be dimensions.
        Assert.True(
            System.Text.Encoding.ASCII.GetString(bytes, 12, 4) == "IHDR",
            $"'{source}' has a PNG signature but no IHDR chunk where one must be, so the bytes "
            + "at offsets 16 and 20 are not its dimensions.");

        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4));

        Assert.True(
            width == height,
            $"The icon is {width}x{height}. NuGet scales an icon into a square slot, so a "
            + "non-square image is distorted or letterboxed in nuget.org search results and in "
            + "the Package Manager.");
    }
}
