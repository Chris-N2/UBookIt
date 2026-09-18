using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The changelog exists, and it has something to say about the version being shipped
/// (`packaging` — "A release names the contract changes a consumer must act on").
/// </summary>
/// <remarks>
/// <para>
/// <b>What this guard proves: presence and shape. What it does not prove: honesty.</b> No test
/// here knows whether an entry names every contract change the release actually made, so none
/// asserts that one is complete or true. The boundary is named rather than left to be assumed,
/// because a guard whose name promises more than it reads is this project's most repeated
/// defect — and the reader who assumes coverage is the one who stops checking.
/// <para>
/// <b>That boundary is not hypothetical.</b> `17.1.0`'s entry shipped naming four affected
/// interfaces when there were five, and every test in this class was green: the missing port was
/// `IBookingManagementStore.FindByReferenceAsync`. QA found it by diffing the compiled interface
/// surface, which is what the release change's design D7 now makes the standing method. If you
/// are reading this while adding a release entry: derive the list, do not recall it.
/// </para>
/// </para>
/// <para>
/// What it does close is the failure mode a hand-written release note actually has: not being
/// wrong, but silently not being written. A bump that forgets the changelog now fails exactly
/// as a bump that forgets the README already does.
/// </para>
/// </remarks>
public class ChangelogTests
{
    private const string Changelog = "CHANGELOG.md";

    /// <summary>
    /// The body of one version's entry, or <c>null</c> when there is no such entry.
    /// </summary>
    /// <remarks>
    /// Headings are matched by <b>identity, not <c>Contains</c></b>: the version is anchored to a
    /// heading and terminated by a word boundary, so an entry for <c>17.1.10</c> does not satisfy
    /// a check for <c>17.1.1</c>. Three loose-substring instruments in this repository have
    /// reported a result that was not true, one of them a pass.
    /// </remarks>
    private static string? EntryFor(string version) => HeadingAndBodyFor(version)?.Body;

    /// <summary>
    /// The remainder of a version's heading line and the entry beneath it, or <c>null</c> when
    /// there is no such heading.
    /// </summary>
    /// <remarks>
    /// The heading remainder is returned rather than discarded because it carries the release
    /// date, which <see cref="Every_released_version_is_dated"/> requires. It was previously
    /// captured and never read — a capture nothing consumes is a guard nobody wrote.
    /// </remarks>
    private static (string Suffix, string Body)? HeadingAndBodyFor(string version)
    {
        var text = RepoFiles.Read(Changelog);
        var heading = new Regex(
            @"^##\s+" + Regex.Escape(version) + @"(?![\w.])(?<rest>.*)$",
            RegexOptions.Multiline);

        var match = heading.Match(text);

        if (!match.Success)
        {
            return null;
        }

        var body = text[(match.Index + match.Length)..];
        var next = Regex.Match(body, @"^##\s", RegexOptions.Multiline);

        return (match.Groups["rest"].Value, next.Success ? body[..next.Index] : body);
    }

    /// <summary>
    /// Every version this repository's archive records as released, as they are written in a
    /// heading.
    /// </summary>
    /// <remarks>
    /// Derived from the archived release changes, which CLAUDE.md makes immutable — so a missing
    /// entry cannot be made to pass by editing the record it is checked against.
    /// <see cref="VersionTruthTests"/> pins the version anchors to the same place for the same
    /// reason; the naming convention (<c>release-&lt;major&gt;-&lt;minor&gt;-&lt;patch&gt;</c>) is
    /// read identically here.
    /// <para>
    /// A release currently in flight is deliberately absent: its change is not archived yet, so its
    /// entry is not yet history and is covered by the declared-version rules instead. It joins this
    /// set at archive time, which is the moment it stops being editable.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<string> ReleasedVersions()
    {
        var archive = Path.Combine(RepoFiles.Root, "openspec", "changes", "archive");

        Assert.True(Directory.Exists(archive), $"No archive at {archive} to read releases from.");

        return
        [
            .. Directory
                .EnumerateDirectories(archive)
                .Select(directory => Regex.Match(
                    Path.GetFileName(directory) ?? string.Empty,
                    @"release-(?<major>\d+)-(?<minor>\d+)-(?<patch>\d+)$"))
                .Where(match => match.Success)
                .Select(match =>
                    $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}.{match.Groups["patch"].Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    [Fact]
    public void The_declared_version_has_an_entry()
    {
        var declared = VersionTruthTests.DeclaredVersion();

        Assert.True(
            EntryFor(declared) is not null,
            $"Directory.Build.props declares {declared}, but {Changelog} has no entry for it. A "
            + "release with no note is one a consumer meets by hitting whatever it changed — which "
            + "is what the versioning policy in README.md says will not happen.");
    }

    [Fact]
    public void The_entry_is_not_merely_a_heading()
    {
        var declared = VersionTruthTests.DeclaredVersion();
        var entry = EntryFor(declared);

        Assert.NotNull(entry);

        // A heading is not a callout. Asserted separately from existence because the two fail for
        // different reasons and want different fixes: one is "you forgot the release", the other
        // is "you started it and stopped".
        Assert.False(
            string.IsNullOrWhiteSpace(entry),
            $"{Changelog} has a heading for {declared} and nothing under it.");
    }

    [Fact]
    public void The_entry_says_what_upgrading_asks_of_the_reader()
    {
        // The ordering decision from design D2, made checkable. The natural way to write a release
        // note is to list what was added, which buries the only sentence a reader cannot afford to
        // miss. This does not judge the wording — it asserts the obligation section is present and
        // comes FIRST, which is the part that decays when someone is in a hurry.
        var declared = VersionTruthTests.DeclaredVersion();
        var entry = EntryFor(declared);

        Assert.NotNull(entry);

        var obligation = entry.IndexOf("### What you have to do", StringComparison.Ordinal);

        Assert.True(
            obligation >= 0,
            $"{Changelog}'s entry for {declared} has no \"What you have to do\" section. Every "
            + "entry has one, including a release that asks for nothing — saying so is the point.");

        // Compared against the FIRST section of any kind, not against a named later one. The
        // first version of this compared `IndexOf("### What you have to do")` with
        // `IndexOf("### What you")` — one string a prefix of the other, so both resolved to the
        // same index and the assertion could not fail whatever order the sections were in.
        var first = Regex.Match(entry, @"^###\s+(?<title>.+)$", RegexOptions.Multiline);

        Assert.True(first.Success, $"{Changelog}'s entry for {declared} has no sections.");

        Assert.True(
            first.Index == obligation,
            $"{Changelog}'s entry for {declared} opens with \"{first.Groups["title"].Value.Trim()}\" "
            + "rather than what upgrading asks of the reader. A reader who stops after the first "
            + "section must still have the part they cannot afford to miss.");
    }

    [Fact]
    public void Released_versions_keep_their_entries()
    {
        // The history rule, on the same terms as the version anchors in README.md and
        // docs/publishing.md: these versions are on nuget.org and cannot be changed, so an entry
        // that can be rewritten backwards records nothing.
        //
        // DERIVED, not listed. The first version of this hardcoded {17.0.0, 17.0.1}, which QA
        // showed was a sample rather than the population: nothing would have added 17.1.0 when
        // 17.1.0 became history, so at 17.2.0 the newest released entry could be deleted with the
        // suite green — and every release after it, permanently.
        //
        // The population is every version the archive records as released. That record is
        // immutable by CLAUDE.md's rule, so it cannot be edited to make a missing entry pass —
        // the same pin `VersionTruthTests` uses for the history anchors, and for the same reason.
        var released = ReleasedVersions();

        Assert.NotEmpty(released);

        foreach (var version in released)
        {
            Assert.True(
                EntryFor(version) is not null,
                $"{Changelog} has no entry for {version}, which this repository's archive records "
                + "as released. Published versions are immutable, so their entries are history and "
                + "are not removed.");
        }
    }

    [Fact]
    public void Every_released_version_is_dated()
    {
        // THE COUNTERPART TO SHIPPING A VERSION UNDATED. The declared version's heading carries no
        // date, deliberately: a date written before the push is a claim nuget.org can contradict if
        // publication slips. But "stamp it at publication" was a handover task and nothing else —
        // an instruction that fails silently when somebody is busy, which is the same shape as
        // every unguarded documentation SHALL this project has been caught by.
        //
        // Tying the stamp to ARCHIVE time rather than to the push is what makes it checkable: a
        // release change is archived once its release has happened, so a version appearing in
        // ReleasedVersions() without a date means the stamp was skipped. It also bounds the
        // exception the changelog grants to its own never-edit rule — a released entry may still
        // receive its date, and nothing else.
        foreach (var version in ReleasedVersions())
        {
            var heading = HeadingAndBodyFor(version);

            Assert.True(heading is not null, $"{Changelog} has no entry for {version}.");

            Assert.True(
                Regex.IsMatch(heading!.Value.Suffix, @"\d{4}-\d{2}-\d{2}"),
                $"{Changelog}'s heading for {version} carries no release date. {version} is "
                + "released, so the date is known — stamp it as `## " + version
                + " — YYYY-MM-DD`. A version ships undated only while it is unpublished.");
        }
    }

    [Fact]
    public void The_changelog_states_that_released_entries_are_not_edited()
    {
        // The rule belongs where an editor meets it, not only in a test they may never open. The
        // same reasoning put the find-and-replace warning inside Directory.Build.props rather than
        // only in the guard that enforces it.
        DocumentationAssert.Says(
            RepoFiles.Read(Changelog),
            "An entry for a release that has happened is never edited");
    }
}
