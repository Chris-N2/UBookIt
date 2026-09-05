using UBookIt.Core.Common;
using UBookIt.Core.Stores;

namespace UBookIt.Tests;

/// <summary>
/// The by-address search's query type: what it accepts, and what it bounds.
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no test for this type at all.</b> Deleting its clamp — returning the caller's
/// raw <c>skip</c> and <c>take</c> — passed all 1044 unit and 102 integration tests, because
/// every caller in the suite asks for a sensible page.
/// </para>
/// <para>
/// It matters more here than on the windowed list. There, an unbounded page is bounded by the
/// data inside a 31-day window; here there is no window, so <c>take</c> is the <b>only</b>
/// thing standing between a request and every booking a prolific booker has ever made, returned
/// in one response. That is the cost the list's window guard exists to prevent, arriving through
/// the one read that has no window.
/// </para>
/// </remarks>
public class BookerEmailQueryTests
{
    private const string Address = "ada@example.com";

    private static BookerEmailQuery Query(int skip = 0, int take = 20)
    {
        var result = BookerEmailQuery.Create(Address, skip, take);
        Assert.True(result.Succeeded);
        return result.Value;
    }

    [Fact]
    public void An_over_large_page_is_capped_rather_than_honoured()
    {
        Assert.Equal(BookerEmailQuery.MaxTake, Query(take: int.MaxValue).Take);
        Assert.Equal(BookerEmailQuery.MaxTake, Query(take: BookerEmailQuery.MaxTake + 1).Take);
    }

    [Fact]
    public void A_negative_skip_is_normalised_rather_than_passed_to_the_database()
    {
        // Reaching the store, a negative skip becomes `.Skip(-1)`, which SQL Server refuses —
        // so the caller gets a 500 for a request the type could simply have made sensible.
        Assert.Equal(0, Query(skip: -1).Skip);
        Assert.Equal(0, Query(skip: int.MinValue).Skip);
    }

    [Fact]
    public void The_bound_is_the_one_the_windowed_list_already_applies()
    {
        // Pinned to BookingQuery's rather than restated, because the requirement is that page
        // sizes do not differ per read: an operator who learns the list's limit and then meets
        // a different one here has been given two answers to one question. Comparing the
        // constants means a change to either is a change to both, or a failure here.
        Assert.Equal(BookingQuery.MaxTake, BookerEmailQuery.MaxTake);
        Assert.Equal(BookingQuery.DefaultSkip, BookerEmailQuery.DefaultSkip);
        Assert.Equal(BookingQuery.DefaultTake, BookerEmailQuery.DefaultTake);
    }

    [Fact]
    public void A_page_the_caller_asked_for_is_left_alone()
    {
        // The clamps must narrow only the unreasonable — a guard that quietly rewrote every
        // request would satisfy the three tests above while breaking paging entirely.
        var query = Query(skip: 40, take: 20);

        Assert.Equal(40, query.Skip);
        Assert.Equal(20, query.Take);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not-an-address")]
    [InlineData("ada@")]
    public void A_value_that_is_not_an_address_is_refused(string? email)
    {
        var result = BookerEmailQuery.Create(email, 0, 20);

        Assert.False(result.Succeeded);
        Assert.Equal(FailureCodes.EmailInvalid, result.Failures[0].Code);
    }

    [Fact]
    public void The_address_is_trimmed_so_a_pasted_value_still_finds_the_person()
    {
        // Matched exactly against a stored value the domain also trimmed. Without this, an
        // address copied out of an email with a trailing space reports that somebody exercising
        // their rights has no bookings.
        Assert.Equal(Address, BookerEmailQuery.Create($"  {Address}  ", 0, 20).Value.Email);
    }

    [Fact]
    public void The_failure_names_the_email_so_a_client_can_point_at_the_field()
    {
        var result = BookerEmailQuery.Create("nonsense", 0, 20);

        Assert.False(result.Succeeded);
        Assert.Equal("Email", result.Failures[0].Field);
    }
}
