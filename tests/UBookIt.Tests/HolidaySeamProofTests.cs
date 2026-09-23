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

    private static IReadOnlyList<(string Path, string Text)> Sources(string directory)
        => RepoFiles.Paths(directory, "*.cs")
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
        var offenders = Sources("tests")
            .Where(file => file.Text.Contains(GovUkHost, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetFileName(file.Path))
            .Where(name => name != "HolidaySeamProofTests.cs")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These test files name the live bank-holiday feed, which would make the suite depend "
            + "on somebody else's uptime: " + string.Join(", ", offenders));
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
        // PHRASES, not stems. "substitut" alone flagged a retention message reading "No default
        // period has been substituted" — ordinary English about something else entirely. What a
        // real leak would say is the feed's own vocabulary: a substitute DAY, or a BANK holiday.
        var jurisdictionWords = new Regex(
            "\"[^\"\\r\\n]*(substitute day|bank holiday)[^\"\\r\\n]*\"",
            RegexOptions.IgnoreCase);

        var offenders = Sources("src")
            .Where(file => !file.Path.Contains(
                $"{Path.DirectorySeparatorChar}UBookIt.TestSite{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Where(file => jurisdictionWords.IsMatch(StripComments(file.Text)))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These SHIPPED files carry one jurisdiction's vocabulary in a string literal, which "
            + "is a country's rules leaking into a port that must stay neutral: "
            + string.Join(", ", offenders));
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
    [Fact]
    public void The_test_site_implementation_carries_that_knowledge()
    {
        var implementation = RepoFiles.Read("src/UBookIt.TestSite/GovUkBankHolidaySource.cs");

        Assert.Contains("IPublicHolidaySource", implementation, StringComparison.Ordinal);
        Assert.Contains("Notes", implementation, StringComparison.Ordinal);
        Assert.Contains("substitut", implementation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The implementation is in the dev site, which is not packed — so no site installing uBookIt
    /// receives a source it did not write.
    /// </summary>
    [Fact]
    public void The_dev_site_is_not_a_packable_project()
    {
        var project = RepoFiles.Read("src/UBookIt.TestSite/UBookIt.TestSite.csproj");

        Assert.DoesNotContain("<IsPackable>true", project, StringComparison.OrdinalIgnoreCase);
    }
}
