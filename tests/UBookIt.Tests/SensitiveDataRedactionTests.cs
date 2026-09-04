using System.Reflection;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Mapping;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Bookings;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// That booker contact details reach only a caller permitted to see them.
/// <para>
/// The endpoint's two callers are exercised in <see cref="BookingsEndpointTests"/>, where the
/// controller and its security accessor already live. What is here is the part that is not
/// about one request: the mapping decision itself, and the two guards whose job is to fail
/// when somebody later adds something nobody decided about.
/// </para>
/// </summary>
public class SensitiveDataRedactionTests
{
    private static BookingSummary Summary()
        => new(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                "Europe/London").Value,
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            "Ada Lovelace",
            "ada@example.com",
            [],
            null);

    [Fact]
    public void The_mapper_carries_the_booker_when_it_is_shown()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Shown);

        Assert.NotNull(model.Booker);
        Assert.Equal("Ada Lovelace", model.Booker.Name);
        Assert.Equal("ada@example.com", model.Booker.Email);
    }

    [Fact]
    public void The_mapper_withholds_the_booker_when_it_is_not()
    {
        var model = BookingModelMapper.ToModel(Summary(), BookerVisibility.Withheld);

        Assert.Null(model.Booker);
    }

    [Fact]
    public void Everything_that_is_not_personal_data_survives_withholding()
    {
        // Withholding must subtract the booker and nothing else. A mapper that returned a
        // sparser row for an unprivileged caller would give them a screen that is not merely
        // missing names but missing the reference, the time and the status — and the failure
        // would look like withholding working.
        var summary = Summary();

        var shown = BookingModelMapper.ToModel(summary, BookerVisibility.Shown);
        var withheld = BookingModelMapper.ToModel(summary, BookerVisibility.Withheld);

        Assert.Equal(shown.BookingId, withheld.BookingId);
        Assert.Equal(shown.Reference, withheld.Reference);
        Assert.Equal(shown.StartUtc, withheld.StartUtc);
        Assert.Equal(shown.EndUtc, withheld.EndUtc);
        Assert.Equal(shown.TimeZoneId, withheld.TimeZoneId);
        Assert.Equal(shown.Status, withheld.Status);
        Assert.Equal(shown.CreatedUtc, withheld.CreatedUtc);
        Assert.Equal(shown.Resources.Count, withheld.Resources.Count);
    }

    [Fact]
    public void The_default_visibility_withholds()
    {
        // `default(BookerVisibility)` is reachable — a field, an array element, a struct member
        // — and the value it lands on decides whether personal data is disclosed. It is pinned
        // to the safe answer here so that reordering the enum for tidiness cannot quietly make
        // disclosure the default.
        Assert.Equal(BookerVisibility.Withheld, default(BookerVisibility));
    }

    /// <summary>
    /// The members of the models that carry a booking to the backoffice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this detects is CHANGE, not personal data.</b> Nothing in a property's name or
    /// type makes it personal, and a guard claiming to recognise that would be checking a
    /// mechanism while appearing to promise a guarantee. What it buys is that a member cannot
    /// join these models silently: the recorded set must be edited, and whoever edits it is
    /// asked the question in the failure message.
    /// </para>
    /// <para>
    /// <b>The set, not a list of known fields.</b> Asserting that <c>Booker</c> is withheld
    /// passes unchanged when a fourth member arrives carrying a phone number — which is the
    /// only case worth catching, since the members that exist today are already covered by
    /// tests that exercise them.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(typeof(BookingModel),
        "BookingId,Reference,StartUtc,EndUtc,TimeZoneId,Status,CreatedUtc,Booker,Resources,Service")]
    [InlineData(typeof(BookerModel), "Name,Email")]
    public void The_booking_response_models_have_not_gained_a_member_nobody_decided_about(
        Type model, string expected)
    {
        var actual = string.Join(
            ',',
            model.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal));

        var recorded = string.Join(
            ',',
            expected.Split(',').OrderBy(name => name, StringComparer.Ordinal));

        Assert.True(
            actual == recorded,
            $"{model.Name}'s members have changed.\n"
            + $"  recorded: {recorded}\n"
            + $"  actual:   {actual}\n"
            + "Before updating the recorded set, answer this: is the difference personal data, "
            + "and who may see it? If it is, it belongs inside the withheld object rather than "
            + "beside it, and BookingModelMapper decides it — a member added outside that "
            + "object reaches every caller, including one with no sensitive-data access.");
    }

    [Fact]
    public void No_booking_endpoint_can_be_asked_about_a_booker()
    {
        // A caller who may not read an email but may filter by one can confirm an address by
        // watching whether a row comes back, and enumerate candidates the same way. Withholding
        // a value while answering questions about it is not withholding it.
        //
        // Written over the whole controller rather than over ListBookings' signature: the point
        // is that no endpoint acquires such a parameter, and naming the one that exists today
        // would pass unchanged when a search endpoint is added tomorrow.
        var parameters = typeof(BookingsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.Name ?? string.Empty)
            .ToArray();

        Assert.NotEmpty(parameters);

        foreach (var forbidden in new[] { "name", "email", "booker", "search", "query", "term" })
        {
            Assert.DoesNotContain(
                parameters,
                parameter => parameter.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }
}
