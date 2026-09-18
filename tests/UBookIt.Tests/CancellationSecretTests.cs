using UBookIt.Core.Bookings;

namespace UBookIt.Tests;

/// <summary>
/// The package's first credential (`self-service-cancellation`, "The credential is the mailbox,
/// never the reference").
/// </summary>
public class CancellationSecretTests
{
    [Fact]
    public void Two_issued_secrets_differ()
    {
        // Not a test of randomness — one collision in 2^256 is not something a test can wait for.
        // It catches the failure that actually happens: a generator seeded once, or a constant
        // returned by a stub that was never replaced.
        var secrets = Enumerable.Range(0, 64).Select(_ => CancellationSecret.Issue().Value).ToArray();

        Assert.Equal(secrets.Length, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void An_issued_secret_is_url_safe_and_long()
    {
        var secret = CancellationSecret.Issue();

        Assert.DoesNotContain(secret.Value, character => character is '+' or '/' or '=');
        Assert.True(secret.Value.Length >= 40, $"An issued secret was {secret.Value.Length} characters.");
    }

    [Fact]
    public void The_hash_is_stable_for_a_value_and_differs_between_values()
    {
        var one = CancellationSecret.Issue();
        Assert.True(CancellationSecret.TryParse(one.Value, out var same));

        Assert.Equal(one.Hash, same!.Hash);
        Assert.NotEqual(one.Hash, CancellationSecret.Issue().Hash);
    }

    [Fact]
    public void The_hash_does_not_contain_the_secret()
    {
        // The point of hashing: what is stored cannot be presented back as the credential.
        var secret = CancellationSecret.Issue();

        Assert.DoesNotContain(secret.Value, secret.Hash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, secret.Hash.Length);
        Assert.Equal(secret.Hash.ToLowerInvariant(), secret.Hash);
    }

    [Fact]
    public void A_secret_never_renders_itself()
    {
        // A secret reaching a log or an exception message stops being a secret, and the commonest
        // route is somebody interpolating the object rather than the member.
        var secret = CancellationSecret.Issue();

        Assert.DoesNotContain(secret.Value, secret.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret.Value, $"{secret}", StringComparison.Ordinal);
    }

    [Fact]
    public void An_issued_secret_round_trips()
    {
        var issued = CancellationSecret.Issue();

        Assert.True(CancellationSecret.TryParse(issued.Value, out var parsed));
        Assert.Equal(issued.Value, parsed!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("has a space in it that is long enough otherwise")]
    [InlineData("punctuation!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData("slash/and+plus/and+plus/and+plus/and+plus/")]
    public void A_value_that_is_not_a_secret_is_refused(string? text)
    {
        Assert.False(CancellationSecret.TryParse(text, out var secret));
        Assert.Null(secret);
    }

    [Fact]
    public void Parsing_is_intolerant_where_a_reference_is_tolerant()
    {
        // THE CONTRAST IS THE POINT, and it is the opposite of BookingReference.TryParse, which
        // forgives case, dashes and whitespace because a person types it from memory. Nothing types
        // this: it is followed from a link. Every tolerance would only widen what counts as a match
        // for a credential.
        var issued = CancellationSecret.Issue();

        Assert.False(CancellationSecret.TryParse(" " + issued.Value, out _));
        Assert.False(CancellationSecret.TryParse(issued.Value + " ", out _));

        // Case-folding would be the tolerance with teeth: the alphabet is case-sensitive, so
        // accepting a folded value would collapse the keyspace.
        var swapped = new string(issued.Value
            .Select(character => char.IsUpper(character) ? char.ToLowerInvariant(character) : char.ToUpperInvariant(character))
            .ToArray());

        if (!string.Equals(swapped, issued.Value, StringComparison.Ordinal))
        {
            Assert.True(CancellationSecret.TryParse(swapped, out var folded));
            Assert.NotEqual(issued.Hash, folded!.Hash);
        }
    }
}
