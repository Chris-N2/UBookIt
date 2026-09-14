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
            "Pre-existing rationale for stating a break in that requirement, written in "
            + "anticipation of publication. Becomes true the moment the package ships, and "
            + "correcting it would mean replacing a requirement about booker identity "
            + "wholesale for two words — the disproportionate edit CLAUDE.md warns against. "
            + "Recorded here and in the deferred-obligations memory for whichever change "
            + "next legitimately modifies `bookings`."),

        ("docs/publishing.md", "nuget.org", 4,
            "The publishing runbook names the feed as a DESTINATION - what nuget.org will "
            + "not let you undo, where to get an API key, which source to push to. That is "
            + "the distinction this guard exists to draw: instructions FOR publishing are "
            + "not a claim of HAVING published. The count is deliberately exact, so editing "
            + "the runbook forces a fresh look at whether the new text still only instructs."),
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
    /// thought of.
    /// </para>
    /// <para>
    /// <b>What it does and does not reach, stated so this guard and its neighbour agree.</b>
    /// It fails closed on every OCCURRENCE of the vocabulary it knows: an unclassified hit
    /// fails, naming the document and the matched text, and an allowance nobody consumes
    /// fails as dead. It does <b>not</b> see a claim phrased outside that vocabulary —
    /// <c>uBookIt was released to the public gallery</c> matches nothing and passes. That is
    /// the same residual <see cref="VersionClaimPatterns"/> states about its own phrases, and
    /// it is worded the same way here deliberately: QA round 3 found this paragraph claiming
    /// a reach the vocabulary did not have, which is the round-2 fault — a name promising
    /// more than a body — climbed one layer into the documentation.
    /// </para>
    /// <para>
    /// When the package is published, this does not get deleted: the accepted list absorbs
    /// the claims that become true, one at a time, each with its reason and its count.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_document_claims_the_package_has_reached_a_feed()
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
                "These read as claims that uBookIt has reached a package feed. It has not — "
                + "nothing is pushed. Correct the claim, or, if it is legitimate, add it to "
                + "AcceptedPublicationMentions with its reason and count:\n  "
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
        DocumentationAssert.Says(
            runbook, "It cannot check the URL actually resolves — open it in a browser once,");

        // The one-way doors.
        DocumentationAssert.Says(runbook, "A pushed version's metadata cannot be edited");
        DocumentationAssert.Says(runbook, "A version number cannot be reused");
        DocumentationAssert.Says(runbook, "Unlisting is not deletion");

        // The ordering, which is the finding this change was built on.
        DocumentationAssert.Says(runbook, "This order is load-bearing, not tidiness");
        DocumentationAssert.Says(
            runbook,
            "a package built before the remote moved carries the old SourceLink URLs");

        // The commit SHA. Found by VERIFYING the SourceLink flip rather than reasoning about
        // it: a pack from an unpushed commit yields source links that 404 for every consumer,
        // permanently, and passes every other check here.
        DocumentationAssert.Says(runbook, "SourceLink embeds the commit SHA");
        DocumentationAssert.Says(
            runbook, "Pack from the merge commit on main, after it is pushed");
        // And the guard that is meant to fail, so a red test is not read as an obstacle.
        DocumentationAssert.Says(
            runbook, "When you publish, that guard will start failing. Do not delete it.");
    }
}