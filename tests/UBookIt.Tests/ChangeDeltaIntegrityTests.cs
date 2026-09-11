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
    /// The section headings OpenSpec understands, matched <b>exactly</b> after trimming.
    /// </summary>
    /// <remarks>
    /// Exact, not <c>Contains</c>, and in both directions. <c>Contains("MODIFIED")</c> accepted
    /// <c>## Notes about MODIFIED Requirements</c> as a real section, and matching only
    /// <c>"## "</c>-prefixed lines meant <c>##MODIFIED Requirements</c> and an indented
    /// <c>  ## MODIFIED Requirements</c> were not seen as sections at all — so every
    /// requirement beneath them silently inherited the section above, which in a mixed delta is
    /// <c>ADDED Requirements</c>. Both were measured passing every test in this file while
    /// OpenSpec itself reclassified three wholesale replacements as ADDED.
    /// </remarks>
    private static readonly string[] SectionHeadings =
        ["ADDED Requirements", "MODIFIED Requirements", "REMOVED Requirements", "RENAMED Requirements"];

    /// <summary>An active change's delta: which capability, what it modifies, and what is orphaned.</summary>
    private sealed record Delta(
        string Change,
        string Capability,
        IReadOnlyList<string> Modified,
        IReadOnlyList<string> Added,
        IReadOnlyList<string> Retired,
        IReadOnlyList<string> Sections,
        IReadOnlyList<string> Unattributed,
        IReadOnlyList<(string From, string To)> Renames,
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
            // A delta that modifies nothing has no upstream requirement to name, and a change
            // introducing a NEW capability has no upstream spec at all — that file does not
            // exist until the change is synced. Asserting its existence unconditionally made
            // every new capability a failure, which this guard never intended: its whole
            // subject, stated above, is a MODIFIED entry silently syncing as an ADDED one.
            //
            // It went unnoticed because this walks active changes only, and by the time a
            // change is archived its capability is upstream. The first new capability proposed
            // after the guard was written is what found it.
            if (delta.Modified.Count == 0)
            {
                continue;
            }

            var upstream = Path.Combine(RepoFiles.Root, "openspec", "specs", delta.Capability, "spec.md");

            Assert.True(
                File.Exists(upstream),
                $"{delta.Change} modifies capability '{delta.Capability}', which has no spec at {upstream}.");

            var headings = HeadingsOf(File.ReadAllText(upstream));

            foreach (var name in delta.Modified)
            {
                // A MODIFIED entry under a name being introduced by this delta's own RENAMED
                // section is the shape the OpenSpec CLI prescribes — the rename applies first,
                // so the replacement must carry the NEW header. It resolves through the pair:
                // the FROM must exist upstream, or the rename itself is renaming nothing and
                // the modification would still land beside rather than on its target.
                var renamedFrom = delta.Renames
                    .Where(pair => pair.To == name)
                    .Select(pair => pair.From)
                    .FirstOrDefault();

                Assert.True(
                    headings.Contains(name) || (renamedFrom is not null && headings.Contains(renamedFrom)),
                    $"{delta.Change}/{delta.Capability} modifies \"{name}\", which is not a requirement in "
                    + $"openspec/specs/{delta.Capability}/spec.md and is not the TO of a RENAMED entry whose "
                    + "FROM is. It would sync as a new requirement beside the one it meant to replace.");
            }
        }
    }

    [Fact]
    public void No_added_requirement_already_exists_upstream()
    {
        // This guards the CONSEQUENCE rather than the spelling, which is why it survives the
        // input shapes that defeated three previous versions of the guard below.
        //
        // A malformed section heading — "##MODIFIED" without a space, an indented one — does
        // not make OpenSpec fail. It makes OpenSpec read every requirement beneath it as
        // ADDED. Measured: with one such heading, three wholesale replacements came back from
        // `openspec show --json` as "operation": "ADDED", and every gate in this repository
        // stayed green. At sync they would have landed BESIDE the requirements they meant to
        // replace, leaving two requirements per subject making overlapping claims.
        //
        // An ADDED requirement whose heading already exists upstream is that mistake, whatever
        // caused it — a copied section, or an author who meant MODIFIED and wrote ADDED.
        //
        // EXCEPT when the same delta retires it first. `## REMOVED Requirements` for X followed
        // by `## ADDED Requirements` re-adding X is the split-and-replace idiom, it is used in
        // this repository's own archive, and OpenSpec accepts it — so flagging it would block
        // valid work. The first version of this guard did exactly that, which is the mirror of
        // the fault this file keeps making: a guard that is wrong about what is legitimate is
        // as costly as one that is blind to what is not.
        //
        // NOTE ON ATTRIBUTION, because the comment here was wrong once: a MALFORMED heading is
        // not caught by this test. The parser orphans those requirements, so they never reach
        // `Added`, and `This_guard_is_not_watching_nothing` is what fires. This test catches
        // the case where the sections parse correctly and the author chose the wrong one.
        foreach (var delta in ActiveDeltas())
        {
            var upstream = Path.Combine(RepoFiles.Root, "openspec", "specs", delta.Capability, "spec.md");

            if (!File.Exists(upstream))
            {
                continue;
            }

            var upstreamBodies = BodiesOf(File.ReadAllText(upstream));
            var deltaBodies = BodiesOf(File.ReadAllText(
                Path.Combine(RepoFiles.Root, ChangesRoot, delta.Change, "specs", delta.Capability, "spec.md")));

            foreach (var name in delta.Added.Where(n => !delta.Retired.Contains(n, StringComparer.Ordinal)))
            {
                // Already synced is not a duplicate. Between `openspec archive`'s sync step and
                // the archive itself, every ADDED requirement exists upstream BY DEFINITION —
                // so comparing names alone made this guard fail on correct work for the whole
                // of that window. Found the moment it was first exercised on a real sync.
                //
                // The failure worth catching is a requirement that would land BESIDE a
                // DIFFERENT one of the same name. If the upstream body is the delta's body,
                // this delta is what put it there.
                var alreadySynced =
                    upstreamBodies.TryGetValue(name, out var upstreamBody)
                    && deltaBodies.TryGetValue(name, out var deltaBody)
                    && Normalise(upstreamBody) == Normalise(deltaBody);

                Assert.False(
                    upstreamBodies.ContainsKey(name) && !alreadySynced,
                    $"{delta.Change}/{delta.Capability} ADDS \"{name}\", which already exists in "
                    + $"openspec/specs/{delta.Capability}/spec.md. Syncing would leave two requirements of "
                    + "that name making overlapping claims. If it was meant to be modified, check the "
                    + "section heading is exactly \"## MODIFIED Requirements\" — a malformed one is read as "
                    + "ADDED rather than rejected.");
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

            // Retire-and-re-add is a wholesale replacement too — it deletes whatever it forgets
            // to restate, exactly as MODIFIED does — so it must be listed for the same reason.
            // Before this it escaped: the name landed in Added and Retired, never in Modified,
            // so the one guard whose purpose is to force a re-diff never saw it. Round 8's
            // exemption made that silent where it had at least failed loudly before.
            var replacements = delta.Modified
                .Concat(delta.Added.Where(n => delta.Retired.Contains(n, StringComparer.Ordinal)))
                .Distinct(StringComparer.Ordinal);

            foreach (var name in replacements)
            {
                Assert.True(
                    tasks.Contains(name, StringComparison.Ordinal),
                    $"{delta.Change} replaces \"{name}\" ({delta.Capability}) but never names it in tasks.md. "
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
                    RequirementsUnder(text, "MODIFIED Requirements"),
                    RequirementsUnder(text, "ADDED Requirements"),
                    [.. RequirementsUnder(text, "REMOVED Requirements"),
                     .. RequirementsUnder(text, "RENAMED Requirements")],
                    SectionsIn(text),
                    UnattributedIn(text),
                    RenamesIn(text),
                    hasTasks));
            }
        }

        return deltas;
    }

    /// <summary>
    /// Walks a delta, yielding each requirement heading with the section it actually sits
    /// under. <c>null</c> means "a section this guard does not recognise", including none.
    /// </summary>
    /// <remarks>
    /// <b>Any line that attempts a <c>##</c> heading is a boundary</b> — but only one at column
    /// zero can BE a section, because that is what OpenSpec accepts. A near-miss must END the
    /// previous section rather than be invisible and let its requirements inherit it. Failing
    /// closed turns a malformed heading into a named failure; earlier versions turned it into a
    /// silent reattribution, twice.
    /// <para>
    /// This remark said "whatever its spacing or indentation" until round 9, describing round
    /// 7's rule after round 8 had replaced it — a stale claim about a guard, in the file whose
    /// subject is guards claiming the wrong thing.
    /// </para>
    /// <para>
    /// Fenced code blocks are skipped, so a <c>##</c> or <c>### Requirement:</c> inside an
    /// example cannot move a section boundary or invent a requirement.
    /// </para>
    /// </remarks>
    private static IEnumerable<(string? Section, string Requirement)> RequirementsIn(string delta)
    {
        string? section = null;
        var fenced = false;

        foreach (var line in Lines(delta))
        {
            var trimmed = line.Trim();
            var indent = line.Length - line.TrimStart(' ', '	').Length;

            // Four or more leading spaces is an indented code block to CommonMark, and OpenSpec
            // ignores it. Skipped before anything else so an example cannot move a boundary.
            //
            // Tabs count as indentation. Measuring only spaces made `	## MODIFIED Requirements`
            // read as column zero here while OpenSpec returned the requirement below it as
            // ADDED — the same divergence class, surviving one more round because nobody had
            // tried a tab.
            if (indent >= 4)
            {
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal)
                || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced)
            {
                continue;
            }

            // Matched against what OpenSpec ACTUALLY does, measured with `openspec show --json`
            // against every shape either of us could think of:
            //
            //   ## MODIFIED Requirements     MODIFIED   ##MODIFIED Requirements   all ADDED
            //   ##  MODIFIED Requirements    MODIFIED      ## MODIFIED (3 spaces) all ADDED
            //   ##	MODIFIED Requirements    MODIFIED       ## MODIFIED (4 spaces) all ADDED
            //   ## Modified Requirements     MODIFIED
            //
            // So: a tab separates, and the kind is case-insensitive — CommonMark would also
            // allow up to three spaces of indent and OpenSpec does NOT, which is why this
            // requires column zero. Guessing CommonMark here would have left the 3-space shape
            // silently classified as MODIFIED by this guard and ADDED by the tool — the exact
            // divergence that has produced a finding in five consecutive rounds.
            //
            // Every one of those was a DIVERGENCE, not a hole: this guard was stricter than the
            // tool in four measured ways and would have failed the build on a perfectly valid
            // delta. A guard wrong about what is legitimate costs as much as one blind to what
            // is not — and my round-6 "catch" of `## Modified Requirements` was in fact a false
            // positive I recorded as a success.
            if (indent == 0 && trimmed.StartsWith("##", StringComparison.Ordinal)
                && !trimmed.StartsWith("###", StringComparison.Ordinal))
            {
                var text = trimmed.TrimStart('#');

                // `##Modified` with nothing between hashes and text is not a heading at all —
                // OpenSpec reads what follows as ADDED — so it must clear the section.
                section = text.Length > 0 && char.IsWhiteSpace(text[0])
                          && SectionHeadings.Contains(text.Trim(), StringComparer.OrdinalIgnoreCase)
                    ? SectionHeadings.First(h => h.Equals(text.Trim(), StringComparison.OrdinalIgnoreCase))
                    : null;

                continue;
            }

            var match = RequirementHeading.Match(trimmed);

            if (match.Success)
            {
                yield return (section, match.Groups["name"].Value);
            }
        }
    }

    /// <summary>Every <c>## </c> section heading, so a failure can name what was actually there.</summary>
    private static IReadOnlyList<string> SectionsIn(string delta)
        => Lines(delta)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("##", StringComparison.Ordinal)
                        && !l.StartsWith("###", StringComparison.Ordinal))
            .Select(l => l.TrimStart('#').Trim())
            .ToList();

    private static IReadOnlyList<string> RequirementsUnder(string delta, string heading)
        => RequirementsIn(delta).Where(r => r.Section == heading).Select(r => r.Requirement).ToList();

    /// <summary>
    /// The FROM/TO pairs a delta's RENAMED section declares, in document order.
    /// </summary>
    /// <remarks>
    /// Parsed as adjacent bullet pairs, which is the shape OpenSpec prescribes and the archive
    /// contains. A FROM with no TO, or the reverse, yields no pair — and that is the safe
    /// direction here: an unpaired bullet gives the exists-check nothing to resolve through, so
    /// a malformed rename fails as an unknown name rather than silently licensing one.
    /// </remarks>
    private static IReadOnlyList<(string From, string To)> RenamesIn(string delta)
    {
        var pairs = new List<(string From, string To)>();
        string? from = null;

        foreach (var line in Lines(delta))
        {
            var fromMatch = Regex.Match(line, @"^-\s*FROM:\s*`###\s+Requirement:\s*(?<name>.+?)\s*`\s*$");
            if (fromMatch.Success)
            {
                from = fromMatch.Groups["name"].Value;
                continue;
            }

            var toMatch = Regex.Match(line, @"^-\s*TO:\s*`###\s+Requirement:\s*(?<name>.+?)\s*`\s*$");
            if (toMatch.Success && from is not null)
            {
                pairs.Add((from, toMatch.Groups["name"].Value));
                from = null;
            }
        }

        return pairs;
    }

    /// <summary>Requirement headings sitting under no section this guard recognises.</summary>
    private static IReadOnlyList<string> UnattributedIn(string delta)
        => RequirementsIn(delta)
            .Where(r => r.Section is null)
            .Select(r => $"(no recognised section) / {r.Requirement}")
            .ToList();

    /// <summary>Lines without their line endings, so CRLF and LF read the same.</summary>
    private static IEnumerable<string> Lines(string text)
        => text.Split('\n').Select(l => l.TrimEnd('\r'));

    /// <summary>Requirement name to the text beneath it, up to the next heading.</summary>
    private static Dictionary<string, string> BodiesOf(string spec)
    {
        var bodies = new Dictionary<string, string>(StringComparer.Ordinal);
        string? current = null;
        var body = new List<string>();

        void Flush()
        {
            if (current is not null)
            {
                bodies[current] = string.Join(" ", body);
            }
        }

        foreach (var line in Lines(spec))
        {
            var match = RequirementHeading.Match(line);

            if (match.Success)
            {
                Flush();
                current = match.Groups["name"].Value;
                body.Clear();
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                current = null;
                body.Clear();
                continue;
            }

            body.Add(line);
        }

        Flush();

        return bodies;
    }

    /// <summary>Collapses whitespace so re-wrapping is not mistaken for a different requirement.</summary>
    private static string Normalise(string text)
        => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static HashSet<string> HeadingsOf(string spec)
        => RequirementHeading.Matches(spec)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
}
