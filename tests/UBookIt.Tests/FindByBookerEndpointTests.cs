using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Tests.Support;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core.Stores;

namespace UBookIt.Tests;

/// <summary>
/// The by-address search: what it asks the store for, what it refuses, and how it is gated.
/// </summary>
/// <remarks>
/// The gate is asserted against the attribute rather than by calling the action with an
/// unprivileged caller: it is a policy, so the framework refuses before the action runs and an
/// in-process call never meets it. A test that invoked the method and found it succeeded would
/// be measuring the absence of the mechanism under test. The policy's own composition, and the
/// handler that decides it, are covered in <see cref="EraseBookerEndpointTests"/> and
/// <see cref="UBookItSensitiveDataAccessTests"/>.
/// </remarks>
public class FindByBookerEndpointTests
{
    [Fact]
    public async Task The_search_asks_the_store_for_the_address_it_was_given()
    {
        // Asserted against the QUERY the endpoint built, not only the rows it returned. A
        // handler that ignored the address and returned whatever the store had would produce a
        // perfectly plausible page — and on this endpoint that page is somebody else's booking,
        // shown to an operator who asked about a different person.
        var (controller, store) = Endpoint();

        await controller.FindBookingsByBooker(new FindBookingsByBookerModel { Email = "ada@example.com" });

        Assert.NotNull(store.LastEmailQuery);
        Assert.Equal("ada@example.com", store.LastEmailQuery.Email);
    }

    [Fact]
    public async Task An_address_is_trimmed_rather_than_missed()
    {
        // A copied-and-pasted address arrives with a trailing space more often than not, and
        // reporting "no bookings" to somebody exercising a right because of whitespace is the
        // wrong failure. Trimmed by the domain, so it matches how the address was stored.
        var (controller, store) = Endpoint();

        await controller.FindBookingsByBooker(new FindBookingsByBookerModel { Email = "  ada@example.com  " });

        Assert.Equal("ada@example.com", store.LastEmailQuery!.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-address")]
    [InlineData("@example.com")]
    public async Task A_value_that_is_not_an_address_is_refused_rather_than_searched_for(string email)
    {
        // Refused, not matched. Searching for a value no booking can hold would return an empty
        // page, and "no bookings" is a different answer from "that was not a question" — the
        // first tells a data subject they are not in the records.
        var (controller, store) = Endpoint();

        var result = await controller.FindBookingsByBooker(new FindBookingsByBookerModel { Email = email });

        Assert.IsNotType<OkObjectResult>(result);
        Assert.Null(store.LastEmailQuery);
    }

    [Fact]
    public async Task The_rows_carry_contact_details_when_the_caller_is_in_the_group()
    {
        // Answering a question about a booker while refusing to say who they are is the one
        // shape this feature must not have — the operator would find the booking and still not
        // know whose it is.
        var (controller, _) = Endpoint(Summary(), sensitiveData: true);

        var model = Payload<PagedBookingsModel>(
            await controller.FindBookingsByBooker(new FindBookingsByBookerModel { Email = "ada@example.com" }));

        var row = Assert.Single(model.Items);

        Assert.Equal(BookerConditions.Shown, row.Booker.Condition);
        Assert.NotNull(row.Booker.Contact);
        Assert.Equal("ada@example.com", row.Booker.Contact.Email);
    }

    [Fact]
    public async Task A_caller_outside_the_group_is_withheld_from_rather_than_trusted()
    {
        // DEFENCE IN DEPTH, and the reason this endpoint asks Umbraco rather than inferring
        // "shown" from having been reached.
        //
        // The policy should make this unreachable: it composes the section requirement and the
        // sensitive-data one, so a caller who got here holds both. But "should" is doing all
        // the work in that sentence — it is true because of a registration in a composer one
        // file away, and if that registration is ever mis-composed this endpoint is the one
        // place in the package that discloses on the strength of an attribute while the list
        // beside it correctly withholds.
        //
        // The rows still come back. Withholding removes the people, never the bookings.
        var (controller, _) = Endpoint(Summary(), sensitiveData: false);

        var model = Payload<PagedBookingsModel>(
            await controller.FindBookingsByBooker(new FindBookingsByBookerModel { Email = "ada@example.com" }));

        var row = Assert.Single(model.Items);

        Assert.Equal(BookerConditions.Withheld, row.Booker.Condition);
        Assert.Null(row.Booker.Contact);
        Assert.False(string.IsNullOrWhiteSpace(row.Reference));
    }

    [Fact]
    public async Task Paging_is_passed_through_and_the_total_is_the_stores()
    {
        var (controller, store) = Endpoint(Summary(), total: 42);

        var model = Payload<PagedBookingsModel>(
            await controller.FindBookingsByBooker(
                new FindBookingsByBookerModel { Email = "ada@example.com", Skip = 20, Take = 10 }));

        Assert.Equal(20, store.LastEmailQuery!.Skip);
        Assert.Equal(10, store.LastEmailQuery.Take);

        // The unpaged total, so a pager can offer a second page. Returning Items.Count would
        // report that a prolific booker has exactly one booking.
        Assert.Equal(42, model.Total);
    }

    [Fact]
    public void The_search_requires_sensitive_data_access_by_policy()
    {
        // Among the action's policies, no longer alone: the permissions model added the
        // booking read verb BESIDE this gate (its spec: "Sensitive-data gates are joined
        // by verbs, never replaced"), so this asserts membership rather than singularity
        // — and asserts the companion too, so the pair is a decision, not an accident.
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.FindBookingsByBooker))!;

        var policies = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();

        Assert.Contains(UBookIt.Backoffice.Constants.SensitiveDataAccessPolicy, policies);
        Assert.Contains(UBookIt.Backoffice.Constants.VerbPolicies.BookingsRead, policies);
    }

    [Fact]
    public void The_search_takes_its_address_in_a_body_rather_than_a_query_string()
    {
        // A POST for a read, deliberately. An address in a URL is written to the web server's
        // log, to every proxy in between and to the operator's browser history — none of which
        // this endpoint's authorization controls. A gate that holds for the response while the
        // request scatters the value through the infrastructure is not much of a gate.
        var action = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.FindBookingsByBooker))!;

        Assert.NotEmpty(action.GetCustomAttributes<HttpPostAttribute>());
        Assert.Empty(action.GetCustomAttributes<HttpGetAttribute>());

        var request = action.GetParameters()
            .Single(parameter => parameter.ParameterType == typeof(FindBookingsByBookerModel));

        Assert.NotEmpty(request.GetCustomAttributes<FromBodyAttribute>());
    }

    [Fact]
    public void The_request_offers_no_partial_match_and_no_ordering_by_a_contact_detail()
    {
        // The narrowing that keeps this a lookup rather than an enumeration tool, pinned as a
        // membership snapshot rather than as "there is no Contains parameter". A guard naming
        // the fields that exist today passes unchanged when a fifth arrives, which is the only
        // case worth catching — and the field somebody adds will be called `match` or `mode` or
        // `partial`, none of which a list of forbidden names anticipates.
        var members = typeof(FindBookingsByBookerModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(["Email", "Skip", "Take"], members);
    }

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
            SummaryBooker.Of(new SummaryContact("Ada Lovelace", "ada@example.com")),
            [new BookedResource(Guid.NewGuid(), "Meeting Room A")],
            Service: null);

    private static (BookingsController Controller, SearchingStore Store) Endpoint(
        BookingSummary? summary = null, int total = 1, bool sensitiveData = true)
    {
        var page = summary is null
            ? new BookingPage([], 0)
            : new BookingPage([summary], total);

        var store = new SearchingStore(page);

        return (
            new BookingsController(
                store,
                new UnusedBookingService(),
                new UnusedServiceBookingService(),
                new InMemoryResourceStore(),
                new InMemoryServiceStore(),
                new UBookIt.Core.SiteBookingSettings { TimeZoneId = "UTC" },
                Security(sensitiveData)),
            store);
    }

    private static T Payload<T>(IActionResult result)
        => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    /// <summary>Returns a fixed page and records the search it was handed.</summary>
    private sealed class SearchingStore(BookingPage page) : IBookingManagementStore
    {
        public BookerEmailQuery? LastEmailQuery { get; private set; }

        public Task<BookingPage> FindByBookerEmailAsync(
            BookerEmailQuery query, CancellationToken cancellationToken = default)
        {
            LastEmailQuery = query;
            return Task.FromResult(page);
        }

        public Task<BookingSummary?> FindByReferenceAsync(
            BookingReference reference, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The search endpoint does not look up by reference.");

        public Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The search endpoint does not list bookings.");
    }

    private sealed class UnusedBookingService : UBookIt.Core.Bookings.IBookingService
    {
        private static InvalidOperationException Unexpected()
            => new("The search endpoint reached the booking service; it should not.");

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> PlaceAsync(
            UBookIt.Core.Bookings.BookingRequest request, CancellationToken cancellationToken = default)
            => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> PlaceAsync(
            UBookIt.Core.Bookings.MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> PlaceForServiceAsync(
            UBookIt.Core.Bookings.ServiceAttribution service,
            UBookIt.Core.Bookings.MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> PlaceOnBehalfAsync(
            UBookIt.Core.Bookings.BookingRequest request,
            CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> PlaceForServiceOnBehalfAsync(
            UBookIt.Core.Bookings.ServiceAttribution service,
            UBookIt.Core.Bookings.MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default) => throw Unexpected();

        public DomainResult CheckPlacementRules(
            UBookIt.Core.Resources.Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw Unexpected();

        public Task<DomainResult<Booking>> MoveAsync(
            Guid bookingId, DateTimeOffset newStart, TimeSpan newLength, CancellationToken cancellationToken = default)
            => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> CancelAsVisitorAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> ConfirmAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> DeclineAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<UBookIt.Core.Bookings.Booking>> EraseBookerAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();
    }

    /// <summary>
    /// A backoffice user who is, or is not, in Umbraco's Sensitive data group.
    /// </summary>
    /// <remarks>
    /// Umbraco's real <c>User</c>, <c>ReadOnlyUserGroup</c> and real group key rather than a
    /// stubbed <c>IUser</c> — a stub would be a second opinion about what membership means, and
    /// membership is what the endpoint asks about.
    /// </remarks>
    private static Umbraco.Cms.Core.Security.IBackOfficeSecurityAccessor Security(bool sensitiveData)
    {
        var user = new Umbraco.Cms.Core.Models.Membership.User(
            new Umbraco.Cms.Core.Configuration.Models.GlobalSettings());

        user.AddGroup(new Umbraco.Cms.Core.Models.Membership.ReadOnlyUserGroup(
            id: 1,
            key: sensitiveData ? Umbraco.Cms.Core.Constants.Security.SensitiveDataGroupKey : Guid.NewGuid(),
            name: "Test group",
            description: null,
            icon: null,
            startContentId: null,
            startMediaId: null,
            alias: "testGroup",
            allowedLanguages: [],
            allowedSections: [UBookIt.Backoffice.Constants.SectionAlias],
            permissions: new HashSet<string>(),
            granularPermissions: new HashSet<Umbraco.Cms.Core.Models.Membership.Permissions.IGranularPermission>(),
            hasAccessToAllLanguages: true));

        return new StubAccessor(new StubSecurity(user));
    }

    private sealed class StubAccessor(Umbraco.Cms.Core.Security.IBackOfficeSecurity? security)
        : Umbraco.Cms.Core.Security.IBackOfficeSecurityAccessor
    {
        public Umbraco.Cms.Core.Security.IBackOfficeSecurity? BackOfficeSecurity { get; } = security;
    }

    private sealed class StubSecurity(Umbraco.Cms.Core.Models.Membership.IUser? currentUser)
        : Umbraco.Cms.Core.Security.IBackOfficeSecurity
    {
        public Umbraco.Cms.Core.Models.Membership.IUser? CurrentUser { get; } = currentUser;

        public bool UserHasSectionAccess(string section, Umbraco.Cms.Core.Models.Membership.IUser user)
            => throw new InvalidOperationException(
                "The endpoint decides visibility from group membership, not from section access.");

        public bool IsAuthenticated()
            => throw new InvalidOperationException(
                "The endpoint does not authenticate; the authorization policy has already run.");
    }
}
