using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Backoffice.Security;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using UBookIt.Tests.Support;

namespace UBookIt.Tests;

/// <summary>
/// The erase endpoint: what it returns, what it refuses, and how it is gated.
/// </summary>
/// <remarks>
/// The authorization is asserted against the <b>attribute</b> rather than by calling the
/// action with an unprivileged user, and that is deliberate: the gate is a policy, so the
/// framework refuses before the action runs and an in-process call on the controller never
/// meets it. A test that invoked the method and found it succeeded would be measuring the
/// absence of the very mechanism under test.
/// </remarks>
public class EraseBookerEndpointTests
{
    private static readonly DateTimeOffset ErasedAt = new(2026, 9, 5, 9, 0, 0, TimeSpan.Zero);

    private static Booking Erased()
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                "Europe/London").Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", "01234 567890").Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)).Value;

        booking.EraseBooker(ErasedAt);
        return booking;
    }

    private static BookingsController Endpoint(IBookingService bookingService)
        => new(
            new EmptyStore(),
            bookingService,
            new SiteBookingSettings { TimeZoneId = "UTC" },
            new StubAccessor());

    [Fact]
    public async Task An_erasure_returns_the_booking_and_the_instant()
    {
        var booking = Erased();
        var service = new ErasingBookingService(DomainResult<Booking>.Success(booking));

        var result = await Endpoint(service).EraseBooker(booking.Id);

        var model = Assert.IsType<ErasedBookerModel>(Assert.IsType<OkObjectResult>(result).Value);

        Assert.Equal(booking.Id, model.BookingId);
        Assert.Equal(ErasedAt, model.ErasedUtc);
        Assert.Equal(booking.Id, service.ErasedId);
    }

    [Fact]
    public async Task The_response_echoes_nothing_that_was_erased()
    {
        // Returning the removed name or address as confirmation would hand back the data the
        // operation exists to remove — into a response body, a browser's network log and any
        // proxy in between, at the exact moment the site was told to stop holding it.
        //
        // **What this test can and cannot see.** The aggregate it is handed is already erased,
        // so it carries no name for the controller to echo even if the controller tried: this
        // observes that the composed payload is clean, NOT that a leak would be caught. The
        // guarantee is held by two other things — `SensitiveDataRedactionTests`' membership
        // snapshot over `ErasedBookerModel`, which fails if the model gains a member at all,
        // and the sibling test below, which passes a booking that DOES still carry details and
        // asserts none of them reach the response. Stated because a test whose comment claims
        // more than it checks is worse than no comment: the next reader trusts it.
        var booking = Erased();
        var result = await Endpoint(
            new ErasingBookingService(DomainResult<Booking>.Success(booking))).EraseBooker(booking.Id);

        var payload = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result).Value);

        Assert.DoesNotContain("Ada", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ada@example.com", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("01234", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Erasing_an_unknown_booking_reports_the_domain_failure()
    {
        var service = new ErasingBookingService(
            DomainResult<Booking>.Failure(FailureCodes.BookingNotFound, "No booking exists."));

        var result = await Endpoint(service).EraseBooker(Guid.NewGuid());

        Assert.IsNotType<OkObjectResult>(result);
    }

    [Fact]
    public async Task A_booking_the_service_did_not_erase_is_not_reported_as_erased()
    {
        // The domain guarantees a booking is erased once the verb has run, so this is the
        // branch that "cannot happen". It is written rather than asserted away because the
        // failure mode of guessing is the worst one available: telling a caller their data is
        // gone, with a fabricated instant, when nothing established that it is.
        var notErased = Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                "Europe/London").Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", null).Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Confirmed,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)).Value;

        var result = await Endpoint(
            new ErasingBookingService(DomainResult<Booking>.Success(notErased)))
            .EraseBooker(notErased.Id);

        Assert.IsNotType<OkObjectResult>(result);

        var payload = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<ObjectResult>(result).Value);

        Assert.DoesNotContain("Ada", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_endpoint_requires_sensitive_data_access_by_policy()
    {
        // Not by a check inside the handler. A conditional is correct only for as long as
        // somebody remembers to write it, and it leaves a route that reaches the operation
        // having established nothing; a policy is a property of the endpoint.
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.EraseBooker))!;

        var authorize = action.GetCustomAttributes<AuthorizeAttribute>().SingleOrDefault();

        Assert.NotNull(authorize);
        Assert.Equal(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy, authorize.Policy);
    }

    [Fact]
    public void The_cancel_endpoint_is_NOT_gated_on_sensitive_data()
    {
        // The negative half, so this pair says the attribute was placed deliberately rather
        // than sprayed across the controller. Cancelling is a different act: it does not
        // disclose or destroy personal data, and requiring the group for it would lock out
        // operators the site meant to let cancel bookings.
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.CancelBooking))!;

        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>());
    }

    [Fact]
    public async Task The_sensitive_data_policy_also_requires_the_section()
    {
        // Composed for real and read back, rather than restated here. An earlier draft of this
        // test built an empty AuthorizationOptions, found no policy, and fell back to asserting
        // the two type names it had just written down — a test that could not fail, which is
        // worse than no test because it reads as coverage.
        //
        // What it guards: naming this policy on an action must only ever ADD a condition. A
        // policy carrying the sensitive-data requirement alone would be satisfiable by somebody
        // without the section at all, which is a widening wearing the clothes of a tightening.
        var services = new ServiceCollection();
        services.AddLogging();

        new UBookIt.Backoffice.Composers.UBookItAuthorizationComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(services));

        await using var provider = services.BuildServiceProvider();
        var policy = await provider
            .GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy);

        Assert.NotNull(policy);

        var requirements = policy.Requirements.Select(requirement => requirement.GetType()).ToList();

        Assert.Contains(typeof(UBookItSectionRequirement), requirements);
        Assert.Contains(typeof(UBookItSensitiveDataRequirement), requirements);
    }

    [Fact]
    public async Task The_sensitive_data_policy_carries_the_backoffice_authentication_scheme()
    {
        // The sibling guard on the section policy exists because deleting this exact line
        // "passed 860/860" — its comment says so. The new policy adds the same line and had
        // no equivalent test, which is a recorded lesson not carried across to the code it
        // was about.
        //
        // Harmless today only because this policy is never named alone: the base controller's
        // section policy always applies too, and its scheme IS guarded. But the composer's own
        // comment invites naming this one by itself, and that is exactly the configuration in
        // which a missing scheme bites — an authenticated user rejected with nothing useful to
        // say about why, because the request never resolved to a backoffice principal.
        var services = new ServiceCollection();
        services.AddLogging();

        new UBookIt.Backoffice.Composers.UBookItAuthorizationComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(services));

        await using var provider = services.BuildServiceProvider();
        var policy = await provider
            .GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy);

        Assert.NotNull(policy);
        Assert.Contains(
            OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
            policy.AuthenticationSchemes);
    }

    [Fact]
    public async Task The_section_policy_does_NOT_require_sensitive_data()
    {
        // The other direction, and the one that would break the whole section if it drifted:
        // every management endpoint carries the section policy, so adding the sensitive-data
        // requirement to it would hide the bookings list, the resources and the services from
        // everybody outside the group — far more than this change intends.
        var services = new ServiceCollection();
        services.AddLogging();

        new UBookIt.Backoffice.Composers.UBookItAuthorizationComposer()
            .Compose(new ServicesOnlyUmbracoBuilder(services));

        await using var provider = services.BuildServiceProvider();
        var policy = await provider
            .GetRequiredService<IAuthorizationPolicyProvider>()
            .GetPolicyAsync(UBookIt.Backoffice.Constants.SectionAccessPolicy);

        Assert.NotNull(policy);
        Assert.DoesNotContain(
            typeof(UBookItSensitiveDataRequirement),
            policy.Requirements.Select(requirement => requirement.GetType()));
    }

    /// <summary>Answers erasure with whatever the test needs, and records the id asked for.</summary>
    private sealed class ErasingBookingService(DomainResult<Booking> answer) : IBookingService
    {
        public Guid? ErasedId { get; private set; }

        public Task<DomainResult<Booking>> EraseBookerAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
        {
            ErasedId = bookingId;
            return Task.FromResult(answer);
        }

        private static InvalidOperationException Unexpected([CallerMemberName] string member = "")
            => new($"The erase endpoint reached {member} on the booking service; it should not.");

        public Task<DomainResult<Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> ConfirmAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> DeclineAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw Unexpected();

        public Task<DomainResult<Booking>> PlaceForServiceAsync(
            ServiceAttribution service,
            MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default) => throw Unexpected();

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw Unexpected();
    }

    private sealed class EmptyStore : IBookingManagementStore
    {
        public Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The erase endpoint does not list bookings.");

        public Task<BookingPage> FindByBookerEmailAsync(
            BookerEmailQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The erase endpoint does not search for bookings.");
    }

    private sealed class StubAccessor : Umbraco.Cms.Core.Security.IBackOfficeSecurityAccessor
    {
        public Umbraco.Cms.Core.Security.IBackOfficeSecurity? BackOfficeSecurity => null;
    }
}
