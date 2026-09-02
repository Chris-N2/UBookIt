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

    /// <summary>An active change's delta: which capability, and which requirements it modifies.</summary>
    private sealed record Delta(string Change, string Capability, IReadOnlyList<string> Modified);

    private static IReadOnlyList<Delta> ActiveDeltas()
    {
        var root = Path.Combine(RepoFiles.Root, ChangesRoot);
        var deltas = new List<Delta>();

        foreach (var changeDir in Directory.GetDirectories(root))
        {
            // Archived changes are history and are never edited — CLAUDE.md is explicit.
            if (Path.GetFileName(changeDir) == "archive")
            {
                continue;
            }

            var specsDir = Path.Combine(changeDir, "specs");

            if (!Directory.Exists(specsDir))
            {
                continue;
            }

            foreach (var capabilityDir in Directory.GetDirectories(specsDir))
            {
                var file = Path.Combine(capabilityDir, "spec.md");

                if (!File.Exists(file))
                {
                    continue;
                }

                deltas.Add(new Delta(
                    Path.GetFileName(changeDir),
                    Path.GetFileName(capabilityDir),
                    ModifiedIn(File.ReadAllText(file))));
            }
        }

        return deltas;
    }

    /// <summary>Requirement headings that fall under a <c>## MODIFIED Requirements</c> section.</summary>
    private static IReadOnlyList<string> ModifiedIn(string delta)
    {
        var names = new List<string>();
        var inModified = false;

        foreach (var line in delta.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inModified = line.Contains("MODIFIED", StringComparison.Ordinal);
                continue;
            }

            var match = RequirementHeading.Match(line.TrimEnd('\r'));

            if (inModified && match.Success)
            {
                names.Add(match.Groups["name"].Value);
            }
        }

        return names;
    }

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

            var headings = ModifiedHeadingsOf(File.ReadAllText(upstream));

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
            var tasksPath = Path.Combine(RepoFiles.Root, ChangesRoot, delta.Change, "tasks.md");

            if (!File.Exists(tasksPath))
            {
                continue;
            }

            var tasks = File.ReadAllText(tasksPath);

            foreach (var name in delta.Modified)
            {
                Assert.True(
                    tasks.Contains(name, StringComparison.Ordinal),
                    $"{delta.Change} modifies \"{name}\" ({delta.Capability}) but never names it in tasks.md. "
                    + "A wholesale replacement nobody lists is a wholesale replacement nobody diffs.");
            }
        }
    }

    private static HashSet<string> ModifiedHeadingsOf(string spec)
        => RequirementHeading.Matches(spec)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
}
