namespace UBookIt.Core.Bookings;

/// <summary>
/// The identifier a person uses: short, unambiguous, and quotable down a telephone.
/// <para>
/// A booking has two identifiers and that is deliberate. Its <see cref="Booking.Id"/> is
/// opaque, is the primary key, and is what routes and payloads carry — machines read it. This
/// is what a human reads: on a confirmation, into a search box, or aloud to somebody at a
/// desk. Collapsing them would mean either putting a parseable value in the primary key or
/// asking a person to read a <c>Guid</c>, which is where this came from — the shipped
/// confirmation view printed one under a <c>&lt;dt&gt;Reference&lt;/dt&gt;</c>.
/// </para>
/// <para>
/// <b>The alphabet is the design.</b> It contains no vowels, so a reference cannot spell a
/// word — a booking system that emails somebody a reference which happens to read as an
/// obscenity has a problem it cannot apologise its way out of, and removing the letters
/// removes the entire class rather than filtering for it afterwards. It also omits
/// <c>0 1 L O I U</c>: the first four are the transcription confusions, and <c>U</c> goes so
/// that nothing in it has a spoken homophone. What remains is unambiguous read aloud, written
/// down, or typed back.
/// </para>
/// <para>
/// Stored and compared in canonical form — upper case, no separators — while
/// <see cref="Display"/> groups it for the eye. Keeping those apart means the display format
/// can change without a migration, and it is why <see cref="TryParse"/> accepts whatever a
/// person actually types.
/// </para>
/// </summary>
public sealed record BookingReference
{
    /// <summary>
    /// Consonants and digits only. See the type remarks: no vowels means no accidental words,
    /// and the omitted digits and letters are the ones confused when transcribed.
    /// </summary>
    public const string Alphabet = "BCDFGHJKMNPQRSTVWXYZ23456789";

    /// <summary>
    /// Long enough that collisions are not a practical concern for a site's bookings
    /// (<see cref="Alphabet"/> has 28 symbols, so 28^8 ≈ 3.8 × 10^11), short enough to read in
    /// one breath. Uniqueness is still enforced by the store rather than assumed from this.
    /// </summary>
    public const int Length = 8;

    /// <summary>Where <see cref="Display"/> puts its separator.</summary>
    private const int GroupSize = 4;

    private BookingReference(string value) => Value = value;

    /// <summary>The canonical form: upper case, exactly <see cref="Length"/> symbols, no separator.</summary>
    public string Value { get; }

    /// <summary>The form shown to a person, grouped so the eye can hold it.</summary>
    public string Display => $"{Value[..GroupSize]}-{Value[GroupSize..]}";

    /// <summary>
    /// Parses anything a person might reasonably type or paste: any case, with or without the
    /// display separator, with or without surrounding or interior whitespace.
    /// </summary>
    /// <remarks>
    /// Lenient on input and strict on output, because the alternative is telling somebody that
    /// the reference they are reading off their own confirmation email is invalid. Only
    /// characters that are pure formatting are discarded — anything else is a real difference
    /// and makes the parse fail rather than being silently repaired into a different booking.
    /// </remarks>
    public static bool TryParse(string? input, out BookingReference reference)
    {
        reference = null!;

        if (input is null)
        {
            return false;
        }

        Span<char> buffer = stackalloc char[Length];
        var written = 0;

        foreach (var candidate in input)
        {
            if (candidate is '-' || char.IsWhiteSpace(candidate))
            {
                continue;
            }

            // Written before the length check so that an over-long input fails rather than
            // being truncated into a valid reference for a booking nobody asked about.
            if (written == Length)
            {
                return false;
            }

            var upper = char.ToUpperInvariant(candidate);

            if (!Alphabet.Contains(upper, StringComparison.Ordinal))
            {
                return false;
            }

            buffer[written++] = upper;
        }

        if (written != Length)
        {
            return false;
        }

        reference = new BookingReference(new string(buffer));

        return true;
    }

    /// <summary>
    /// Builds a reference from a value already known to be canonical — the persistence
    /// boundary, where the value came out of a column this type put it in.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The value is not canonical. Deliberately a throw rather than a failure result: a
    /// non-canonical value in storage is corruption, not a validation case a caller can
    /// sensibly handle, and quietly accepting it would let a reference exist that no search
    /// could ever match.
    /// </exception>
    public static BookingReference FromCanonical(string value)
    {
        if (!TryParse(value, out var reference) || reference.Value != value)
        {
            throw new ArgumentException(
                $"'{value}' is not a canonical booking reference.", nameof(value));
        }

        return reference;
    }

    public override string ToString() => Value;
}
