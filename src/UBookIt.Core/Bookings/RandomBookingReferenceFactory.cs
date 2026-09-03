using System.Security.Cryptography;

namespace UBookIt.Core.Bookings;

/// <summary>
/// The default <see cref="IBookingReferenceFactory"/>: symbols drawn uniformly from
/// <see cref="BookingReference.Alphabet"/>.
/// <para>
/// It lives in Core, which is consistent rather than a compromise —
/// <see cref="BookingService"/> has always called <c>Guid.NewGuid()</c> inline, so random
/// identity is not new here. <see cref="RandomNumberGenerator"/> is a <c>System</c> type, so
/// Core's zero-package-reference invariant is untouched.
/// </para>
/// <para>
/// <b>Cryptographic rather than <c>Random.Shared</c>.</b> Not because a reference is a secret
/// — it is printed on confirmations and read down telephones, and nothing should ever be built
/// as though it were one. It is because a reference is the obvious thing to put in a "manage
/// my booking" link later, and predictable references would make that link guessable. The cost
/// of closing that door now is nil.
/// </para>
/// </summary>
public sealed class RandomBookingReferenceFactory : IBookingReferenceFactory
{
    public BookingReference Next()
    {
        var symbols = new char[BookingReference.Length];

        for (var i = 0; i < symbols.Length; i++)
        {
            // GetInt32 rejection-samples internally, so the distribution over the alphabet is
            // uniform. Taking `random % 27` would quietly favour the first few symbols.
            symbols[i] = BookingReference.Alphabet[
                RandomNumberGenerator.GetInt32(BookingReference.Alphabet.Length)];
        }

        return BookingReference.FromCanonical(new string(symbols));
    }
}
