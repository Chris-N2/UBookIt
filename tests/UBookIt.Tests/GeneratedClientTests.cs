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
/// <para>
/// <b>This is the client half of that guard, and only the half.</b> The file is a committed
/// artifact, so it does not disagree with the C# until somebody regenerates it — a
/// `[BindRequired]` removed today would pass here until the next regeneration.
/// `BookingsControllerContractTests.The_window_cannot_be_omitted_by_a_caller` asks MVC's own
/// binding metadata the same question, and that is the half that catches it immediately.
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
