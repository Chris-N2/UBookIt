using System.Text.RegularExpressions;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// What the committed TypeScript client promises the screen that will consume it.
/// <para>
/// The client is generated rather than written, so nothing about it is checked by the C#
/// build: a regeneration that quietly weakens the contract compiles, ships, and is only
/// discovered by whoever writes the call. The window is the one part worth pinning, because
/// "the window SHALL NOT be optional" is a guarantee the endpoint cannot make on its own —
/// an optional parameter in the generated types is precisely how the unbounded call becomes
/// the easiest one to write.
/// </para>
/// </summary>
public class GeneratedClientTests
{
    [Fact]
    public void The_generated_client_requires_a_window()
    {
        var types = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/api/types.gen.ts");

        var query = Regex.Match(
            types,
            @"export type ListBookingsData = \{.*?query\??:\s*\{(?<members>.*?)\};",
            RegexOptions.Singleline);

        Assert.True(query.Success, "ListBookingsData no longer declares a query object.");

        // Not `query?:` — an optional query object makes both dates omissible however they
        // are declared inside it.
        Assert.Contains("query: {", query.Value, StringComparison.Ordinal);

        var members = query.Groups["members"].Value;

        Assert.Matches(@"\bfrom:\s*string", members);
        Assert.Matches(@"\bto:\s*string", members);
        Assert.DoesNotMatch(@"\bfrom\?:", members);
        Assert.DoesNotMatch(@"\bto\?:", members);

        // And the filters stay optional, so this cannot be satisfied by a regeneration that
        // made everything required.
        Assert.Matches(@"\bstatuses\?:", members);
        Assert.Matches(@"\bresourceIds\?:", members);
        Assert.Matches(@"\bskip\?:", members);
        Assert.Matches(@"\btake\?:", members);
    }
}
