using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The lowest supported Umbraco version that the README and Getting started state is the lower
/// bound the packages declare.
/// </summary>
/// <remarks>
/// <para>
/// The bound is declared once, in <c>Directory.Packages.props</c>, and stated in prose in two
/// documents. Prose derives nothing, so a floor raise that misses a page leaves it telling a site
/// owner an Umbraco version uBookIt will refuse to install on. That is the failure this catches,
/// and the next floor raise is its first real use.
/// </para>
/// <para>
/// Every phrasing that states the floor is matched, not one: "17.7.1 or later", "below 17.7.1",
/// "to 17.7.1 first". A page that states the floor in a new phrasing needs a pattern here, and
/// <see cref="Each_page_states_the_floor"/> requires at least one match per page so a page can
/// never silently stop being checked.
/// </para>
/// </remarks>
public class UmbracoFloorDocumentationTests
{
    private static readonly Regex Floor = new(
        @"(\d+\.\d+\.\d+)(?:\*\*)? or later|below (\d+\.\d+\.\d+)|to (\d+\.\d+\.\d+) first",
        RegexOptions.CultureInvariant);

    public static string DeclaredFloor()
    {
        var props = RepoFiles.Read("Directory.Packages.props");
        var match = Regex.Match(props, @"Include=""Umbraco\.Cms""\s+Version=""\[(\d+\.\d+\.\d+),");

        Assert.True(match.Success, "Directory.Packages.props declares no Umbraco.Cms range with an inclusive lower bound.");

        return match.Groups[1].Value;
    }

    public static IReadOnlyList<string> StatedFloors(string document)
        => Floor.Matches(document)
            .Select(m => m.Groups.Values.Skip(1).First(g => g.Success).Value)
            .ToList();

    [Theory]
    [InlineData("README.md", 3)]
    [InlineData("docs/getting-started.md", 2)]
    public void Each_page_states_the_floor(string page, int atLeast)
    {
        var stated = StatedFloors(RepoFiles.Read(page));

        Assert.True(stated.Count >= atLeast, $"{page} states the Umbraco floor {stated.Count} times; expected at least {atLeast}.");
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/getting-started.md")]
    public void Every_floor_a_page_states_is_the_declared_floor(string page)
    {
        var declared = DeclaredFloor();
        var wrong = StatedFloors(RepoFiles.Read(page)).Where(v => v != declared).ToList();

        Assert.True(
            wrong.Count == 0,
            $"{page} states Umbraco {string.Join(", ", wrong)}, but Directory.Packages.props declares {declared} as the floor.");
    }

    [Fact]
    public void A_stale_floor_is_found_in_every_phrasing()
        => Assert.Equal(
            ["17.5.0", "17.5.0", "17.5.0", "17.5.0"],
            StatedFloors("Umbraco 17.5.0 or later; **17.5.0 or later**; below 17.5.0; upgrade it to 17.5.0 first."));
}
