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

    [Fact]
    public void The_clients_whitespace_is_the_domains_whitespace()
    {
        // THE ALPHABET WAS NOT THE ONLY LITERAL IN TWO LANGUAGES. `TryParse` skips anything
        // `char.IsWhiteSpace` accepts; the client used JavaScript's `\s`, and the two sets are
        // NOT the same — `\s` omits U+0085 (NEL) and includes U+FEFF. Measured consequence: a
        // reference pasted with a NEL was refused in place by the client though the server would
        // have found the booking, and one carrying a BOM was accepted by the client and refused
        // by the server. The spec scenario claims the two "accept and reject the same ones", so
        // either the claim or the code had to move; the code did.
        //
        // Enumerated rather than spot-checked: a guard over a handful of characters would have
        // missed U+0085 exactly as the original did.
        var source = RepoFiles.Read("src/UBookIt.Backoffice/Client/src/section/find-fields.ts");

        var match = Regex.Match(source, @"export const REFERENCE_WHITESPACE =\s*""([^""]+)"";");
        Assert.True(match.Success, "find-fields.ts no longer declares REFERENCE_WHITESPACE where this guard looks.");

        var declared = Unescape(match.Groups[1].Value);

        var actual = new string(Enumerable.Range(0, 0x10000)
            .Select(code => (char)code)
            .Where(char.IsWhiteSpace)
            .ToArray());

        // POSITIVE CONTROL: the unescaping must produce something, or a broken parse would
        // compare two empty strings and pass.
        Assert.NotEmpty(declared);

        Assert.Equal(actual, declared);
    }

    /// <summary>Turns the TypeScript literal's <c>\uXXXX</c> escapes into the characters they name.</summary>
    private static string Unescape(string literal)
        => Regex.Replace(
            literal,
            @"\\u([0-9A-Fa-f]{4})",
            match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
}
