using System.Security.Cryptography;

namespace UBookIt.Core.Bookings;

/// <summary>
/// The credential that lets the person who made a booking cancel it themselves.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the package's first credential, and it is deliberately not the booking's
/// reference.</b> A <see cref="BookingReference"/> is designed to be quoted — printed on a
/// confirmation, read down a telephone, shown to any operator who can list bookings — and a value a
/// customer is expected to read aloud is not a secret. The reference identifies; this authenticates.
/// </para>
/// <para>
/// <b>Compare <see cref="BookingReference.TryParse"/>, which is deliberately tolerant</b> of case,
/// dashes and stray whitespace because a human types it from memory. <see cref="TryParse"/> here is
/// deliberately <i>intolerant</i>: a machine follows this value from a link, so there is no typing
/// to forgive, and every tolerance would widen what counts as a match for a secret. The two rules
/// look inconsistent side by side and are not — they serve opposite readers.
/// </para>
/// <para>
/// <b>Only <see cref="Hash"/> is ever stored.</b> The plaintext exists in the message sent to the
/// booker and nowhere the package writes, so a database copy, a backup, or a person with read
/// access to the table cannot act as a booker. That is the same reasoning that keeps booker contact
/// details out of messages to a site's own recipients: a control built deliberately must not be
/// reachable by a route around it.
/// </para>
/// </remarks>
public sealed record CancellationSecret
{
    /// <summary>
    /// Bytes of entropy behind an issued secret.
    /// </summary>
    /// <remarks>
    /// 256 bits. The threat is not a targeted guess but a sweep, and at this width a sweep is not
    /// a thing that finishes. Sized here rather than left to a caller so every secret the package
    /// issues is the same strength.
    /// </remarks>
    public const int EntropyBytes = 32;

    private CancellationSecret(string value) => Value = value;

    /// <summary>
    /// The secret itself, URL-safe. <b>Goes in the link and nowhere else the package writes.</b>
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The one-way hash of <see cref="Value"/>, lower-case hex — <b>the only form that is stored.</b>
    /// </summary>
    /// <remarks>
    /// Redemption hashes what was presented and looks the hash up, so the stored side never needs
    /// the plaintext and no comparison of secrets happens in memory.
    /// </remarks>
    public string Hash => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Value)))
        .ToLowerInvariant();

    /// <summary>Issues a new secret from a cryptographically secure source.</summary>
    public static CancellationSecret Issue()
        => new(Convert.ToBase64String(RandomNumberGenerator.GetBytes(EntropyBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('='));

    /// <summary>
    /// Reads a secret exactly as it was issued, or refuses it.
    /// </summary>
    /// <remarks>
    /// <b>No normalisation of any kind.</b> Nothing is trimmed, case-folded or stripped: a value
    /// that is not character-for-character what was issued is not the secret. A caller that arrives
    /// with whitespace around it has not typed it badly — nothing types this — so the tolerance
    /// would only ever widen the match.
    /// <para>
    /// The length bound exists so a redemption attempt with an absurd value is refused before it
    /// reaches a store lookup, and the alphabet bound so that what reaches the store is the shape
    /// the store expects.
    /// </para>
    /// </remarks>
    public static bool TryParse(string? text, out CancellationSecret? secret)
    {
        secret = null;

        if (string.IsNullOrEmpty(text) || text.Length is < 16 or > 128)
        {
            return false;
        }

        foreach (var character in text)
        {
            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-' or '_';

            if (!allowed)
            {
                return false;
            }
        }

        secret = new CancellationSecret(text);
        return true;
    }

    /// <summary>Never renders the secret — a secret in a log or an error message is not a secret.</summary>
    public override string ToString() => "CancellationSecret";
}
