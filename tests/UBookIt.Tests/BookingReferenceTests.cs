using UBookIt.Core.Bookings;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The reference a person quotes: its shape, and the properties that shape exists for.
/// <para>
/// The alphabet is asserted as <b>properties</b> rather than as a literal. A test that
/// restates the constant proves only that somebody typed it twice; what matters is that the
/// alphabet cannot spell a word and cannot produce a character that is confused when
/// transcribed. Those are the reasons it was chosen, so those are what is guarded — and they
/// would survive somebody reordering or extending it for a good reason while catching
/// somebody adding a vowel for a bad one.
/// </para>
/// </summary>
public class BookingReferenceTests
{
    [Fact]
    public void The_alphabet_cannot_spell_a_word()
    {
        // Without vowels there is no word to accidentally produce, which is the whole defence
        // against emailing a customer a reference that reads as an obscenity. A filter list
        // would be the alternative and it would be endless, language-specific, and wrong.
        foreach (var vowel in "AEIOU")
        {
            Assert.DoesNotContain(vowel, BookingReference.Alphabet);
        }
    }

    [Fact]
    public void The_alphabet_contains_nothing_confused_when_transcribed()
    {
        // 0/O, 1/I, 1/L are the classic misreads, on paper and on a phone line.
        foreach (var ambiguous in "01OIL")
        {
            Assert.DoesNotContain(ambiguous, BookingReference.Alphabet);
        }
    }

    [Fact]
    public void The_alphabet_has_no_duplicates_and_is_all_upper_case()
    {
        // A repeated symbol would silently bias the distribution towards it.
        Assert.Equal(BookingReference.Alphabet.Length, BookingReference.Alphabet.Distinct().Count());
        Assert.All(BookingReference.Alphabet, c => Assert.False(char.IsLower(c)));
    }

    [Fact]
    public void A_generated_reference_is_the_declared_length_and_drawn_from_the_alphabet()
    {
        var factory = new RandomBookingReferenceFactory();

        for (var i = 0; i < 200; i++)
        {
            var reference = factory.Next();

            Assert.Equal(BookingReference.Length, reference.Value.Length);
            Assert.All(reference.Value, c => Assert.Contains(c, BookingReference.Alphabet));
        }
    }

    [Fact]
    public void Generated_references_differ()
    {
        // Not a randomness test — it cannot be one. It catches a generator wired to return a
        // constant, which is the failure that would make every booking share a reference.
        var factory = new RandomBookingReferenceFactory();

        var produced = Enumerable.Range(0, 100).Select(_ => factory.Next().Value).ToHashSet();

        Assert.True(produced.Count > 90, $"Only {produced.Count} distinct references in 100 draws.");
    }

    [Theory]
    [InlineData("7qx4m2np")]          // lower case
    [InlineData("7QX4-M2NP")]         // display form
    [InlineData(" 7QX4 M2NP ")]       // pasted with whitespace
    [InlineData("7qx4 - m2np")]       // both, badly
    public void A_reference_is_recognised_however_a_person_types_it(string typed)
    {
        Assert.True(BookingReference.TryParse(typed, out var parsed));
        Assert.Equal("7QX4M2NP", parsed.Value);
    }

    [Fact]
    public void Case_does_not_change_which_booking_is_meant()
    {
        Assert.True(BookingReference.TryParse("7qx4m2np", out var lower));
        Assert.True(BookingReference.TryParse("7QX4M2NP", out var upper));

        Assert.Equal(upper, lower);
        Assert.Equal(upper.GetHashCode(), lower.GetHashCode());
    }

    [Theory]
    [InlineData("")]
    [InlineData("7QX4M2N")]           // too short
    [InlineData("7QX4M2NPQ")]         // too long
    [InlineData("7QX4M2N0")]          // contains an excluded digit
    [InlineData("7QX4M2NA")]          // contains a vowel
    [InlineData("7QX4M2N!")]          // punctuation that is not a separator
    public void Anything_that_is_not_a_reference_is_refused(string typed)
    {
        Assert.False(BookingReference.TryParse(typed, out _));
    }

    [Fact]
    public void An_over_long_input_is_refused_rather_than_truncated()
    {
        // Truncating would turn a mistyped reference into a VALID one for a different
        // booking — the worst possible outcome for a lookup, because nothing reports it.
        Assert.False(BookingReference.TryParse("7QX4M2NPXXXX", out _));
    }

    [Fact]
    public void Display_groups_the_reference_without_changing_it()
    {
        var reference = References.Of("7QX4M2NP");

        Assert.Equal("7QX4-M2NP", reference.Display);
        Assert.Equal("7QX4M2NP", reference.Value);

        // And the display form parses back to the same reference, so a person reading it off
        // a confirmation and typing it into a search finds their booking.
        Assert.True(BookingReference.TryParse(reference.Display, out var round));
        Assert.Equal(reference, round);
    }

    [Fact]
    public void A_non_canonical_stored_value_is_a_fault_not_a_validation_case()
    {
        // FromCanonical is the persistence boundary. A lower-case or separated value in the
        // column is corruption: it would be invisible to every lookup that normalises first.
        Assert.Throws<ArgumentException>(() => BookingReference.FromCanonical("7qx4m2np"));
        Assert.Throws<ArgumentException>(() => BookingReference.FromCanonical("7QX4-M2NP"));
    }
}
