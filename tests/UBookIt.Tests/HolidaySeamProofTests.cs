using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the TestSite's `gov.uk` implementation proves, and what it must keep to itself.
/// </summary>
/// <remarks>
/// The implementation is the seam's only evidence that a real jurisdiction's feed fits the port.
/// These guards assert the properties that make it evidence rather than decoration: the package's
/// own suites never depend on the live feed, and none of the jurisdiction's oddities leaked back
/// into the package.
/// </remarks>
public class HolidaySeamProofTests
{
    private const string GovUkHost = "gov.uk";

    /// <summary>
    /// The instrument the leak guard runs. <b>Defined once and tested in its own right</b> — see
    /// <see cref="The_leak_instrument_matches_a_leak_and_spares_ordinary_prose"/>.
    /// </summary>
    /// <remarks>
    /// PHRASES, not stems. "substitut" alone flagged a retention message reading "No default
    /// period has been substituted" — ordinary English about something else entirely. What a real
    /// leak would say is the feed's own vocabulary: a substitute DAY, or a BANK holiday.
    /// </remarks>
    private static readonly Regex JurisdictionWords = new(
        "\"[^\"\\r\\n]*(substitute day|bank holiday)[^\"\\r\\n]*\"",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Files whose literals could put a word on a screen or into behaviour: the C# the package
    /// ships, and the TypeScript that renders it. <b>Both halves, because a term dictionary is
    /// where a country's word would most plausibly arrive</b> — a guard over C# alone is the
    /// one-language guard this project has been caught by before.
    /// </summary>
    private static IReadOnlyList<(string Path, string Text)> ShippedSources()
        => RepoFiles.Paths("src", "*.cs")
            .Concat(RepoFiles.Paths("src", "*.ts"))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}UBookIt.TestSite{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            // Somebody else's code, and none of it ships in the package.
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".test.ts", StringComparison.Ordinal))
            .Select(path => (path, File.ReadAllText(path)))
            .ToList();

    /// <summary>
    /// <b>No test reaches the live feed.</b> A suite that called gov.uk would fail on a train, on
    /// a locked-down build agent, and on the day the service has an outage — none of which is a
    /// defect in this package. The fake source is what tests use; the live feed is exercised once,
    /// by hand, on the dev site.
    /// </summary>
    [Fact]
    public void No_test_project_names_the_live_feed()
    {
        var offenders = RepoFiles.Paths("tests", "*.cs")
            .Where(path => File.ReadAllText(path).Contains(GovUkHost, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(name => name != "HolidaySeamProofTests.cs")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These test files name the live bank-holiday feed, which would make the suite depend "
            + "on somebody else's uptime: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// <b>The instrument can fire, and does not fire at prose.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This test exists because the guard below had no control over its own instrument.</b> The
    /// regex was narrowed three times — from matching anywhere in a file, to string literals, to
    /// these two phrases — and after the third narrowing it matched <b>nothing in the repository
    /// at all</b>, including the TestSite file whose whole purpose is to carry the vocabulary
    /// being hunted. The test that was supposed to be the control asserted only that the word
    /// "substitut" appeared somewhere in that file's raw text, which it does — in a doc comment
    /// the guard strips before matching. So the control passed on evidence the instrument never
    /// sees, and a fourth narrowing into uselessness would have gone unremarked.
    /// </para>
    /// <para>
    /// A <c>DoesNotContain</c> sweep claims an absence, so it reads identically whether it is
    /// working or blind. The only thing that tells the two apart is a sample it must match and a
    /// sample it must spare, asserted here rather than reasoned about.
    /// </para>
    /// </remarks>
    [Theory]
    // Must match: a jurisdiction's own vocabulary, sitting in a literal.
    [InlineData("var name = \"Boxing Day (substitute day)\";", true)]
    [InlineData("Label = \"Spring bank holiday\",", true)]
    [InlineData("return \"SUBSTITUTE DAY\";", true)]
    // Must not match: ordinary English, and the real sentence that caused the second narrowing.
    [InlineData("\"No default period has been substituted\"", false)]
    [InlineData("\"a substitute for the missing window\"", false)]
    // Must not match: prose about a jurisdiction is documentation, not behaviour.
    //
    // The first two carry the phrase inside a QUOTED string within the comment, so they are
    // spared by the stripping and by nothing else — delete StripComments and they match. The
    // unquoted pair below would be spared either way; they are here for the shape of the comment,
    // not as evidence that stripping works.
    [InlineData("// a term like \"Boxing Day (substitute day)\" is the feed's word, not ours", false)]
    [InlineData("/// <remarks>Rendered as \"Spring bank holiday\" by the UK feed.</remarks>", false)]
    [InlineData("// the UK calls this a substitute day", false)]
    [InlineData("/// <remarks>A bank holiday, for example.</remarks>", false)]
    public void The_leak_instrument_matches_a_leak_and_spares_ordinary_prose(string line, bool isLeak)
    {
        var scanned = StripComments(line);

        Assert.Equal(isLeak, JurisdictionWords.IsMatch(scanned));
    }

    /// <summary>
    /// <b>The package learned nothing about substitutions.</b> When Christmas Day falls at a
    /// weekend the UK observes it on the next working day, and the feed expresses that in a notes
    /// field. Folding that into a name is the TestSite implementation's job — if the concept ever
    /// appears in the shipped package, the port has stopped being jurisdiction-neutral and has
    /// started encoding one country's rules.
    /// </summary>
    /// <remarks>
    /// <b>Scanned as STRING LITERALS, not as words.</b> The first version matched anywhere in a
    /// file and flagged thirteen innocent ones — "substitute" is ordinary English and appears all
    /// over this package's prose ("would substitute a generic problem"), and the port's own
    /// documentation names a bank holiday as the example it was designed against. Prose about a
    /// jurisdiction is fine; a jurisdiction's vocabulary in a literal is the leak, because a
    /// literal is what ends up in behaviour or on a screen.
    /// </remarks>
    [Fact]
    public void No_shipped_code_carries_one_jurisdictions_vocabulary_in_a_literal()
    {
        var offenders = ShippedSources()
            .Where(file => JurisdictionWords.IsMatch(StripComments(file.Text)))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These SHIPPED files carry one jurisdiction's vocabulary in a string literal, which "
            + "is a country's rules leaking into a port that must stay neutral: "
            + string.Join(", ", offenders));
    }

    /// <summary>
    /// The sweep reads both languages, and reads something in each.
    /// </summary>
    /// <remarks>
    /// Anti-vacuity for the file set rather than the pattern: a glob that silently matched nothing
    /// — a renamed directory, a changed extension — would make the sweep above pass over an empty
    /// set forever. The shipped client's term dictionary is named explicitly because it is the
    /// file a country's word would most plausibly reach.
    /// </remarks>
    [Fact]
    public void The_sweep_reads_both_languages()
    {
        var swept = ShippedSources().Select(file => file.Path).ToList();

        Assert.Contains(swept, path => path.EndsWith("HolidayImport.cs", StringComparison.Ordinal));
        Assert.Contains(swept, path => path.EndsWith("holiday-fields.ts", StringComparison.Ordinal));
        Assert.Contains(swept, path => path.EndsWith("en-us.ts", StringComparison.Ordinal));
        Assert.DoesNotContain(swept, path => path.Contains("UBookIt.TestSite", StringComparison.Ordinal));
        Assert.True(swept.Count >= 50, $"Only {swept.Count} shipped sources found; the sweep is not reading the repository.");
    }

    /// <summary>Drops comment lines, so prose is never mistaken for behaviour.</summary>
    private static string StripComments(string source)
        => string.Join(
            Environment.NewLine,
            source
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>
    /// And the implementation that DOES carry that knowledge — so the guard above is not passing
    /// merely because nothing implements the port at all.
    /// </summary>
    /// <remarks>
    /// <b>It carries it as behaviour, not as a literal</b>, which is worth stating because it is
    /// the reason the leak guard finds nothing even in the file it excludes. The implementation
    /// never writes "substitute day" down: it reads the feed's <c>notes</c> field and folds
    /// whatever that says into the name. The knowledge is the DECISION to fold it, plus the
    /// division constant — so this test asserts the seam and the mechanism, and leaves the
    /// vocabulary to the feed, where it belongs.
    /// </remarks>
    [Fact]
    public void The_test_site_implementation_carries_that_knowledge()
    {
        var implementation = RepoFiles.Read("src/UBookIt.TestSite/GovUkBankHolidaySource.cs");

        Assert.Contains("IPublicHolidaySource", implementation, StringComparison.Ordinal);
        Assert.Contains("Notes", implementation, StringComparison.Ordinal);
        Assert.Contains("england-and-wales", implementation, StringComparison.Ordinal);

        // The fold itself: the name is composed from the feed's own two fields, which is what
        // lets the package stay ignorant of what a substitution is.
        Assert.Contains("published.Title", implementation, StringComparison.Ordinal);
        Assert.Contains("published.Notes", implementation, StringComparison.Ordinal);
    }

    /// <summary>
    /// The implementation is in the dev site, which is not packed — so no site installing uBookIt
    /// receives a source it did not write.
    /// </summary>
    /// <remarks>
    /// Asserted as the POSITIVE fact. <c>DoesNotContain("&lt;IsPackable&gt;true")</c> alone stays
    /// green if the load-bearing line is simply deleted, which is the likelier accident.
    /// </remarks>
    [Fact]
    public void The_dev_site_is_not_a_packable_project()
    {
        var project = RepoFiles.Read("src/UBookIt.TestSite/UBookIt.TestSite.csproj");

        Assert.Contains("<IsPackable>false</IsPackable>", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<IsPackable>true", project, StringComparison.OrdinalIgnoreCase);
    }
}
