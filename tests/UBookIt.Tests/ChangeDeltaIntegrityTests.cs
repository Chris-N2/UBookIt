using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// Mechanical checks over an in-flight change's delta specs.
/// <para>
/// These exist because a hand-maintained list of what a change modifies failed three QA rounds
/// running, and the third failure was not clerical: a requirement was restated by hand,
/// omitted from the change's own list of modified requirements, and therefore never
/// re-diffed — and it had silently dropped four guarantees. The list that should have
/// prompted the re-diff was the thing that was wrong.
/// </para>
/// <para>
/// CLAUDE.md's rule is that a <c>## MODIFIED Requirements</c> entry <b>replaces its
/// requirement wholesale</b>, deleting anything it forgets to restate, with nothing in the
/// diff that looks like a deletion. Nothing can mechanically check that the prose survived —
/// that is a reading job. What a machine can check is that every modification is <i>declared
/// where the reader will look</i>, and that it names a requirement that actually exists.
/// </para>
/// </summary>
public class ChangeDeltaIntegrityTests
{
    private const string ChangesRoot = "openspec/changes";

    private static readonly Regex RequirementHeading =
        new(@"^###\s+Requirement:\s*(?<name>.+?)\s*$", RegexOptions.Multiline);

    /// <summary>
    /// The section kinds OpenSpec understands. A requirement under anything else is orphaned:
    /// it will not sync as its author intended, and no guard here can see it.
    /// </summary>
    private static readonly string[] SectionKinds = ["ADDED", "MODIFIED", "REMOVED", "RENAMED"];

    /// <summary>An active change's delta: which capability, what it modifies, and what is orphaned.</summary>
    private sealed record Delta(
        string Change,
        string Capability,
        IReadOnlyList<string> Modified,
        IReadOnlyList<string> Sections,
        IReadOnlyList<string> Unattributed,
        bool HasTasks);

    [Fact]
    public void Every_modified_requirement_names_one_that_exists()
    {
        // A delta that modifies a requirement whose heading does not appear upstream is either
        // a typo or a rename, and either way it will sync as an ADDED requirement sitting
        // beside the one it meant to replace — leaving two requirements making overlapping
        // claims and no sign that anything went wrong.
        foreach (var delta in ActiveDeltas())
        {
            var upstream = Path.Combine(RepoFiles.Root, "openspec", "specs", delta.Capability, "spec.md");

            Assert.True(
                File.Exists(upstream),
                $"{delta.Change} modifies capability '{delta.Capability}', which has no spec at {upstream}.");

            var headings = HeadingsOf(File.ReadAllText(upstream));

            foreach (var name in delta.Modified)
            {
                Assert.True(
                    headings.Contains(name),
                    $"{delta.Change}/{delta.Capability} modifies \"{name}\", which is not a requirement in "
                    + $"openspec/specs/{delta.Capability}/spec.md. It would sync as a new requirement beside "
                    + "the one it meant to replace.");
            }
        }
    }

    [Fact]
    public void Every_modified_requirement_is_named_in_its_change_tasks()
    {
        // THE ONE THAT WOULD HAVE CAUGHT IT. A wholesale replacement has to be read against the
        // original, guarantee by guarantee — a machine cannot do that. What it can do is refuse
        // to let a modification go unlisted where the reader doing that reading will look.
        //
        // Round 3's MAJOR was exactly this: a requirement restated by hand, absent from the
        // change's task list, so nothing prompted anyone to diff it, and four guarantees went
        // missing. The count in that list was wrong in three consecutive rounds.
        foreach (var delta in ActiveDeltas())
        {
            var tasks = File.ReadAllText(Path.Combine(RepoFiles.Root, ChangesRoot, delta.Change, "tasks.md"));

            foreach (var name in delta.Modified)
            {
                Assert.True(
                    tasks.Contains(name, StringComparison.Ordinal),
                    $"{delta.Change} modifies \"{name}\" ({delta.Capability}) but never names it in tasks.md. "
                    + "A wholesale replacement nobody lists is a wholesale replacement nobody diffs.");
            }
        }
    }

    [Fact]
    public void This_guard_is_not_watching_nothing()
    {
        // Every path above iterates a collection. A collection that is empty satisfies all of
        // them, and this project has now shipped that fault twice: RepoFiles.Paths carries
        // "a scan over nothing passes every assertion made about it", and default-frontend
        // makes an anti-vacuity guard a SHALL, noting the fault has shipped before. The first
        // version of THIS file — written to end a four-round failure — had it too.
        //
        // Measured rather than argued: renaming one heading from "## MODIFIED Requirements" to
        // "## Modified Requirements" made three wholesale replacements invisible to both tests
        // above, and both passed. `openspec validate --strict` passed as well.
        // The parser is proved against the ARCHIVE, which is never empty and never changes.
        // That separates the two ways this guard could see nothing: "there is no active change"
        // — legitimate, and true for most of a repository's life — from "the parser stopped
        // working", which is the one that must never pass quietly. Only the second is a defect,
        // and only the second is detectable without an active change to look at.
        var archived = ArchivedDeltas();

        Assert.True(
            archived.Any(d => d.Modified.Count > 0),
            "The parser found no MODIFIED requirement anywhere in openspec/changes/archive, which is "
            + "not credible — archived changes contain many. The section or heading parsing has broken, "
            + "and every assertion in this file would now pass by finding nothing.");

        foreach (var delta in ActiveDeltas())
        {
            Assert.True(
                delta.HasTasks,
                $"{delta.Change} has delta specs but no tasks.md. This guard's whole premise is that a "
                + "modification is declared where the reader will look; without that file there is nowhere "
                + "to look, and both assertions above would pass by finding nothing.");

            Assert.NotEmpty(delta.Sections);

            // EVERY requirement must sit under a section this guard understands.
            //
            // The previous version asked whether a recognised section was *present*, which any
            // delta carrying an ADDED section satisfied — so a reworded MODIFIED heading in a
            // mixed file was exempt, and two of this change's five deltas were in exactly that
            // shape. That is the fourth iteration of one fault: testing a guard against the
            // single mutation that motivated it instead of against the shapes of input it will
            // actually meet. Delta files come in three (ADDED-only, MODIFIED-only, both), and
            // only one had been tried.
            //
            // Asking instead whether anything is left UNATTRIBUTED cannot be dodged by shape.
            // A reworded heading orphans every requirement beneath it, whatever else the file
            // contains, and an orphan is reported by name rather than inferred from a count.
            Assert.True(
                delta.Unattributed.Count == 0,
                $"{delta.Change}/{delta.Capability}: {delta.Unattributed.Count} requirement(s) sit under no "
                + $"section this guard understands — {string.Join("; ", delta.Unattributed)}. Sections present: "
                + $"{string.Join(", ", delta.Sections)}. A section heading whose wording this guard does not "
                + "match makes every requirement beneath it invisible to the two assertions above.");
        }
    }

    /// <summary>
    /// Archived changes, read only to prove the parser still works. Never modified — CLAUDE.md
    /// is explicit that the archive is history.
    /// </summary>
    private static IReadOnlyList<Delta> ArchivedDeltas()
        => DeltasUnder(Path.Combine(RepoFiles.Root, ChangesRoot, "archive"));

    private static IReadOnlyList<Delta> ActiveDeltas()
        => DeltasUnder(Path.Combine(RepoFiles.Root, ChangesRoot));

    private static IReadOnlyList<Delta> DeltasUnder(string root)
    {
        var deltas = new List<Delta>();

        if (!Directory.Exists(root))
        {
            return deltas;
        }

        foreach (var changeDir in Directory.GetDirectories(root))
        {
            // The archive is walked only by ArchivedDeltas, which is handed it directly.
            if (Path.GetFileName(changeDir) == "archive")
            {
                continue;
            }

            var specsDir = Path.Combine(changeDir, "specs");

            // A change with no delta specs is legitimate: not every change touches a capability.
            // Named here so a reader knows it was considered rather than overlooked.
            if (!Directory.Exists(specsDir))
            {
                continue;
            }

            var hasTasks = File.Exists(Path.Combine(changeDir, "tasks.md"));

            foreach (var capabilityDir in Directory.GetDirectories(specsDir))
            {
                var file = Path.Combine(capabilityDir, "spec.md");

                if (!File.Exists(file))
                {
                    continue;
                }

                var text = File.ReadAllText(file);

                deltas.Add(new Delta(
                    Path.GetFileName(changeDir),
                    Path.GetFileName(capabilityDir),
                    ModifiedIn(text),
                    SectionsIn(text),
                    UnattributedIn(text),
                    hasTasks));
            }
        }

        return deltas;
    }

    /// <summary>Every <c>## </c> section heading, so an unrecognised one can be named in a failure.</summary>
    private static IReadOnlyList<string> SectionsIn(string delta)
        => Lines(delta)
            .Where(l => l.StartsWith("## ", StringComparison.Ordinal))
            .Select(l => l[3..].Trim())
            .ToList();

    /// <summary>
    /// Requirement headings sitting under a section whose kind OpenSpec does not recognise —
    /// including the case of no section at all. Each is reported as
    /// <c>"&lt;section&gt;" / &lt;requirement&gt;</c> so a failure names both.
    /// </summary>
    private static IReadOnlyList<string> UnattributedIn(string delta)
    {
        var orphans = new List<string>();
        var section = "(no section)";

        foreach (var line in Lines(delta))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                section = line[3..].Trim();
                continue;
            }

            var match = RequirementHeading.Match(line);

            if (match.Success && !SectionKinds.Any(k => section.Contains(k, StringComparison.Ordinal)))
            {
                orphans.Add($"\"{section}\" / {match.Groups["name"].Value}");
            }
        }

        return orphans;
    }

    /// <summary>Requirement headings that fall under a <c>## MODIFIED Requirements</c> section.</summary>
    private static IReadOnlyList<string> ModifiedIn(string delta)
    {
        var names = new List<string>();
        var inModified = false;

        foreach (var line in Lines(delta))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inModified = line.Contains("MODIFIED", StringComparison.Ordinal);
                continue;
            }

            var match = RequirementHeading.Match(line);

            if (inModified && match.Success)
            {
                names.Add(match.Groups["name"].Value);
            }
        }

        return names;
    }

    /// <summary>Lines without their line endings, so CRLF and LF read the same.</summary>
    private static IEnumerable<string> Lines(string text)
        => text.Split('\n').Select(l => l.TrimEnd('\r'));

    private static HashSet<string> HeadingsOf(string spec)
        => RequirementHeading.Matches(spec)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
}
