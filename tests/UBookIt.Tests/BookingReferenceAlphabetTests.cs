using System.Text.RegularExpressions;
using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The client's reference-shape rule agrees with the domain's parser (booking-management spec,
/// "The bookings view can find a booking" — "The shape rule agrees with the parser").
/// </summary>
/// <remarks>
/// <para>
/// <b>Two copies of an alphabet drift the day somebody edits one.</b> `find-fields.ts` ports
/// <see cref="BookingReference.TryParse"/> so the view can tell a reference from an address before
/// a round trip; the rule is tested in the client suite against the same vectors the C# suite
/// uses, but the ALPHABET is a literal in both languages and nothing but this holds them equal.
/// </para>
/// <para>
/// This is the fallback the design named — quoting the constant and guarding the equality —
/// rather than a shared fixture file, which was disproportionate plumbing for one string. Stated
/// in the change's handover.
/// </para>
/// </remarks>
public class BookingReferenceAlphabetTests
{
    [Fact]
    public void The_clients_alphabet_is_the_domains_alphabet()
    {
        var source = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/section/find-fields.ts");

        var match = Regex.Match(source, @"export const REFERENCE_ALPHABET = ""([^""]+)"";");
        Assert.True(match.Success, "find-fields.ts no longer declares REFERENCE_ALPHABET where this guard looks.");

        Assert.Equal(BookingReference.Alphabet, match.Groups[1].Value);

        var length = Regex.Match(source, @"export const REFERENCE_LENGTH = (\d+);");
        Assert.True(length.Success, "find-fields.ts no longer declares REFERENCE_LENGTH where this guard looks.");
        Assert.Equal(BookingReference.Length, int.Parse(length.Groups[1].Value));
    }
}
