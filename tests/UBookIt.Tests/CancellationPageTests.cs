using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;
using UBookIt.Web.Controllers;
using UBookIt.Web.Rendering;

namespace UBookIt.Tests;

/// <summary>
/// The cancellation page's behaviour (`self-service-cancellation`: the safe-GET/acting-POST split,
/// the one answer every unusable secret gets, and what the page may show).
/// </summary>
public class CancellationPageTests
{
    private static readonly UBookIt.Core.Resources.Resource Room = TestData.Room();

    private static readonly DateTimeOffset Now = TestData.Utc(TestData.BaseDate, "08:00");

    private static readonly DateTimeOffset Start = TestData.Utc(TestData.BaseDate, "09:00");

    private sealed class Secrets : ICancellationSecretStore
    {
        public CancellationSecretRecord? Record { get; set; }

        public Guid? RedeemsTo { get; set; }

        public int Redemptions { get; private set; }

        public Task IssueAsync(
            Guid bookingId, string hash, DateTimeOffset expiresUtc, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CancellationSecretRecord?> FindAsync(string hash, CancellationToken cancellationToken = default)
            => Task.FromResult(Record);

        public Task<Guid?> TryRedeemAsync(
            string hash, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            Redemptions++;
            return Task.FromResult(RedeemsTo);
        }
    }

    private sealed class CancellingService(bool succeeds) : IBookingService
    {
        public int Cancellations { get; private set; }

        public Task<DomainResult<Booking>> CancelAsVisitorAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
        {
            Cancellations++;
            return Task.FromResult(succeeds
                ? DomainResult<Booking>.Success(Sample())
                : DomainResult<Booking>.Failure(FailureCodes.BookingAlreadyStarted, "Already started."));
        }

        public Task<DomainResult<Booking>> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceForServiceAsync(
            ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceOnBehalfAsync(
            BookingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceOnBehalfAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceForServiceOnBehalfAsync(
            ServiceAttribution service, MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> ConfirmAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> DeclineAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> EraseBookerAsync(Guid bookingId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> MoveAsync(
            Guid bookingId, DateTimeOffset start, TimeSpan duration, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public DomainResult CheckPlacementRules(
            UBookIt.Core.Resources.Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Booking Sample(BookingStatus status = BookingStatus.Confirmed)
        => Booking.Rehydrate(
            Guid.NewGuid(),
            new RandomBookingReferenceFactory().Next(),
            BookingInterval.Create(Start, Start.AddHours(1), TestData.LondonZoneId).Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "07700 900123").Value,
            [new ResourceClaim(Room.Id)],
            status,
            Now).Value;

    private static CancellationController Controller(
        Secrets secrets, Booking? booking, IBookingService service, DateTimeOffset? now = null)
    {
        // The real in-memory store rather than a hand-rolled double: IBookingStore has members
        // this test has no opinion about, and a double that threw on them would be asserting
        // something about the controller's internals rather than its behaviour.
        var store = new InMemoryBookingStore();

        if (booking is not null)
        {
            store.PlaceAsync(booking).GetAwaiter().GetResult();
        }

        var controller = new CancellationController(
            secrets,
            store,
            new InMemoryResourceStore().Add(Room),
            service,
            new SiteBookingSettings { TimeZoneId = TestData.LondonZoneId },
            new FixedClock(now ?? Now));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor(),
        };

        return controller;
    }

    private static string ViewNameOf(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return view.ViewName ?? "Index";
    }

    [Fact]
    public async Task A_valid_secret_shows_the_booking_and_redeems_nothing()
    {
        // GET IS SAFE. Mail scanners fetch every link in a message unattended; a retrieval that
        // redeemed would spend the booker's one use before they read the message.
        var booking = Sample();
        var secret = CancellationSecret.Issue();
        var secrets = new Secrets { Record = new CancellationSecretRecord(booking.Id, Start, Redeemed: false) };
        var service = new CancellingService(true);

        var result = await Controller(secrets, booking, service).Index(secret.Value);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CancellationPageModel>(view.Model);

        Assert.Equal(booking.Reference.Display, model.Reference);
        Assert.Equal(0, secrets.Redemptions);
        Assert.Equal(0, service.Cancellations);
    }

    [Fact]
    public void The_page_model_cannot_express_a_booker()
    {
        // STRUCTURAL, not a matter of what the view is written to omit: the page is reached with a
        // secret and nothing else, so a model that could name the booker would be one forwarded
        // email away from disclosing them.
        var members = typeof(CancellationPageModel)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.NotEmpty(members);

        // PRECISE, because a loose substring reports a violation that is not one — "Name" matches
        // ServiceName and ResourceNames, both of which this page is supposed to show. (The second
        // time in this change that a substring match flagged an innocent member; the first was
        // CancellationSecretRecord matching CancellationSecret.)
        foreach (var forbidden in new[] { "Booker", "Email", "Phone", "Secret", "Telephone", "Address" })
        {
            Assert.DoesNotContain(members, member => member.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }

        // And the members it DOES have are the non-PII description the package already trusts to a
        // reader who may not see contact details: which booking, when, and what was booked.
        Assert.Equal(
            ["LocalEnd", "LocalStart", "Reference", "ResourceNames", "ServiceName", "TimeZoneId"],
            members.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("never issued")]
    [InlineData("already redeemed")]
    [InlineData("expired")]
    [InlineData("unparseable")]
    [InlineData("booking already cancelled")]
    public async Task Every_unusable_secret_gets_the_same_page(string cause)
    {
        // ONE ANSWER FOR FIVE CAUSES. Telling them apart is exactly what an attacker wants:
        // "already used" says a booking exists and somebody cancelled it, "expired" says one
        // exists and roughly when, "never issued" says the guess was wrong.
        var booking = Sample();
        var valid = CancellationSecret.Issue();

        var (secrets, presented, stored) = cause switch
        {
            "never issued" => (new Secrets { Record = null }, valid.Value, booking),
            "already redeemed" => (
                new Secrets { Record = new CancellationSecretRecord(booking.Id, Start, Redeemed: true) },
                valid.Value, booking),
            "expired" => (
                new Secrets { Record = new CancellationSecretRecord(booking.Id, Now.AddMinutes(-1), false) },
                valid.Value, booking),
            "unparseable" => (new Secrets(), "not a secret", booking),
            _ => (
                new Secrets { Record = new CancellationSecretRecord(booking.Id, Start, false) },
                valid.Value, Sample(BookingStatus.Cancelled)),
        };

        var result = await Controller(secrets, stored, new CancellingService(true)).Index(presented);

        Assert.Equal("Unusable", ViewNameOf(result));
        Assert.Equal(0, secrets.Redemptions);
    }

    [Fact]
    public async Task Posting_redeems_then_cancels()
    {
        var booking = Sample();
        var secret = CancellationSecret.Issue();
        var secrets = new Secrets
        {
            Record = new CancellationSecretRecord(booking.Id, Start, false),
            RedeemsTo = booking.Id,
        };
        var service = new CancellingService(true);

        var result = await Controller(secrets, booking, service).Cancel(secret.Value);

        Assert.Equal("Cancelled", ViewNameOf(result));
        Assert.Equal(1, secrets.Redemptions);
        Assert.Equal(1, service.Cancellations);
    }

    [Fact]
    public async Task A_secret_the_store_refuses_cancels_nothing()
    {
        var booking = Sample();
        var secrets = new Secrets { RedeemsTo = null };
        var service = new CancellingService(true);

        var result = await Controller(secrets, booking, service).Cancel(CancellationSecret.Issue().Value);

        Assert.Equal("Unusable", ViewNameOf(result));
        Assert.Equal(0, service.Cancellations);
    }

    [Fact]
    public async Task A_refused_cancellation_joins_the_same_sentence()
    {
        // "Already started" is a fact about the booking that a holder of a valid secret could
        // otherwise learn. The capability's answer to every unusable state is one response.
        var booking = Sample();
        var secrets = new Secrets { RedeemsTo = booking.Id };

        var result = await Controller(secrets, booking, new CancellingService(false))
            .Cancel(CancellationSecret.Issue().Value);

        Assert.Equal("Unusable", ViewNameOf(result));
    }

    [Fact]
    public async Task Both_routes_keep_the_secret_out_of_the_referer()
    {
        var booking = Sample();
        var secrets = new Secrets
        {
            Record = new CancellationSecretRecord(booking.Id, Start, false),
            RedeemsTo = booking.Id,
        };

        var get = Controller(secrets, booking, new CancellingService(true));
        await get.Index(CancellationSecret.Issue().Value);
        Assert.Equal("no-referrer", get.Response.Headers["Referrer-Policy"]);

        var post = Controller(secrets, booking, new CancellingService(true));
        await post.Cancel(CancellationSecret.Issue().Value);
        Assert.Equal("no-referrer", post.Response.Headers["Referrer-Policy"]);
    }

    [Fact]
    public async Task A_booking_that_has_started_is_not_offered_a_button_that_cannot_work()
    {
        // THE TWO CLOCKS CAN DISAGREE. The expiry is frozen when the secret is issued, so an
        // operator who moves a booking EARLIER leaves a secret whose stored expiry is later than
        // the booking itself. Without checking the booking's own start, the page would render the
        // confirmation form, the submission would burn the secret, and the visitor would be told
        // the link no longer works for a booking they were just shown.
        var booking = Sample();
        var secrets = new Secrets
        {
            Record = new CancellationSecretRecord(booking.Id, Start.AddDays(7), Redeemed: false),
        };

        var result = await Controller(secrets, booking, new CancellingService(true), now: Start.AddMinutes(1))
            .Index(CancellationSecret.Issue().Value);

        Assert.Equal("Unusable", ViewNameOf(result));
        Assert.Equal(0, secrets.Redemptions);
    }

    [Fact]
    public async Task Neither_response_may_be_stored()
    {
        // A 200 carrying a booking's details, at a URL carrying a credential, must not be left to
        // whatever a CDN, a proxy or a shared browser decides.
        var booking = Sample();
        var secrets = new Secrets
        {
            Record = new CancellationSecretRecord(booking.Id, Start, false),
            RedeemsTo = booking.Id,
        };

        var get = Controller(secrets, booking, new CancellingService(true));
        await get.Index(CancellationSecret.Issue().Value);
        Assert.Contains("no-store", get.Response.Headers["Cache-Control"].ToString(), StringComparison.Ordinal);

        var post = Controller(secrets, booking, new CancellingService(true));
        await post.Cancel(CancellationSecret.Issue().Value);
        Assert.Contains("no-store", post.Response.Headers["Cache-Control"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_route_the_link_is_built_for_is_the_route_that_is_served()
    {
        // A link built one way and routed another is broken only for the person who needs it,
        // days later, with nothing to tell them why. The two constants are held equal here.
        Assert.Equal(
            UBookIt.Persistence.Notifications.CancellationLink.PathBase,
            UBookIt.Web.Constants.CancellationPath);
    }
}
