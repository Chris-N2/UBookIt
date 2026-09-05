using UBookIt.Tests.Support;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Resources;
using UBookIt.Core.Stores;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Models.Membership.Permissions;
using Umbraco.Cms.Core.Security;

namespace UBookIt.Tests;

/// <summary>
/// The booking endpoint, exercised through the controller.
///
/// <para>
/// <b>These exist because their absence hid three defects.</b> The change previously had
/// only reflection-level assertions over the controller's shape — nothing invoked the
/// action, and the mapper had no test at all. A comma-joined status list collapsing to the
/// wrong single status, a restated paging default, and an untested failure code all sat
/// behind that gap and all are caught here.
/// </para>
/// <para>
/// The store double records what it was asked for, because half of what matters is not the
/// response but the query the endpoint built: which statuses, which paging, which window.
/// </para>
/// </summary>
public class BookingsEndpointTests
{
    private static readonly DateOnly From = new(2026, 6, 1);
    private static readonly DateOnly To = new(2026, 6, 7);

    private static SiteBookingSettings Settings(string zone = "UTC")
        => new() { TimeZoneId = zone, MaxQueryRangeDays = 31 };

    /// <summary>
    /// Returns a fixed page and keeps the query it was handed.
    /// </summary>
    private sealed class RecordingStore(BookingPage page) : IBookingManagementStore
    {
        public BookingQuery? LastQuery { get; private set; }

        public Task<BookingPage> ListAsync(BookingQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(page);
        }

        /// <summary>The by-address search this double records, for the search endpoint's tests.</summary>
        public BookerEmailQuery? LastEmailQuery { get; private set; }

        public Task<BookingPage> FindByBookerEmailAsync(
            BookerEmailQuery query, CancellationToken cancellationToken = default)
        {
            LastEmailQuery = query;
            return Task.FromResult(page);
        }
    }

    [Fact]
    public async Task The_row_carries_the_bookings_own_reference()
    {
        // Spec: "An operator can match what a caller reads out". Asserted against the summary
        // the port supplied, not against a shape — a mapper hard-wired to a constant satisfies
        // "there is a reference on the row" and cannot match anything, and that mutation
        // passed all 1786 tests before this existed.
        var summary = Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed);
        var (controller, _) = Endpoint(new BookingPage([summary], Total: 1));

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));

        var row = Assert.Single(model.Items);

        Assert.Equal(summary.Reference.Value, row.Reference);

        // Canonical on the wire; the client groups it for display.
        Assert.DoesNotContain('-', row.Reference);
    }

    private static BookingSummary Summary(
        DateTimeOffset startUtc,
        BookingStatus status,
        ServiceAttribution? service = null,
        params (Guid Id, string Name)[] resources)
        => new(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(startUtc, startUtc.AddHours(1), "Europe/London").Value,
            status,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            SummaryBooker.Of(new SummaryContact("Ada Lovelace", "ada@example.com")),
            [.. resources.Select(r => new BookedResource(r.Id, r.Name))],
            service);

    private static (BookingsController Controller, RecordingStore Store) Endpoint(
        BookingPage? page = null,
        string zone = "UTC",
        IBookingService? bookingService = null,
        IBackOfficeSecurityAccessor? security = null)
    {
        var store = new RecordingStore(page ?? new BookingPage([], 0));

        return (
            new BookingsController(
                store,
                bookingService ?? new UnusedBookingService(),
                Settings(zone),
                // Defaults to a user who may see contact details, so that every test written
                // before withholding existed still asserts what it was written to assert. The
                // withholding tests pass their own.
                security ?? Security(sensitiveData: true)),
            store);
    }

    /// <summary>
    /// A backoffice user who is, or is not, in Umbraco's Sensitive data group.
    /// </summary>
    /// <remarks>
    /// Built on Umbraco's real <c>User</c> and <c>ReadOnlyUserGroup</c> and its real group key,
    /// rather than a stubbed <c>IUser</c> — the same reasoning the section-access tests record.
    /// A stub would be a second opinion about what membership means, and this is precisely the
    /// question under test.
    /// </remarks>
    private static IBackOfficeSecurityAccessor Security(bool sensitiveData)
    {
        var user = new User(new GlobalSettings());

        user.AddGroup(new ReadOnlyUserGroup(
            id: 1,
            // The real built-in key when the user is meant to have access, and a group that is
            // emphatically NOT it otherwise — rather than no group at all, so the negative case
            // is "a user in some other group" and not "a user in none", which is the state an
            // ordinary editor is actually in.
            key: sensitiveData ? Constants.Security.SensitiveDataGroupKey : Guid.NewGuid(),
            name: "Test group",
            description: null,
            icon: null,
            startContentId: null,
            startMediaId: null,
            alias: "testGroup",
            allowedLanguages: [],
            allowedSections: [UBookIt.Backoffice.Constants.SectionAlias],
            permissions: new HashSet<string>(),
            granularPermissions: new HashSet<IGranularPermission>(),
            hasAccessToAllLanguages: true));

        return new StubBackOfficeSecurityAccessor(new StubBackOfficeSecurity(user));
    }

    private sealed class StubBackOfficeSecurityAccessor(IBackOfficeSecurity? security)
        : IBackOfficeSecurityAccessor
    {
        public IBackOfficeSecurity? BackOfficeSecurity { get; } = security;
    }

    private sealed class StubBackOfficeSecurity(IUser? currentUser) : IBackOfficeSecurity
    {
        public IUser? CurrentUser { get; } = currentUser;

        public bool UserHasSectionAccess(string section, IUser user)
            => throw new InvalidOperationException(
                "The endpoint decides visibility from group membership, not from section access.");

        public bool IsAuthenticated()
            => throw new InvalidOperationException(
                "The endpoint does not authenticate; the authorization policy has already run.");
    }

    /// <summary>
    /// Stands in for the booking service on the read tests, and refuses to be used.
    /// </summary>
    /// <remarks>
    /// Every member throws rather than returning a plausible empty answer. A listing test
    /// that started depending on placement or cancellation would say so here, instead of
    /// quietly passing against a stub that invented one.
    /// </remarks>
    private sealed class UnusedBookingService : IBookingService
    {
        private static InvalidOperationException Unexpected([CallerMemberName] string member = "")
            => new($"The listing endpoint reached {member} on the booking service; it should not.");

        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> PlaceForServiceAsync(
            ServiceAttribution service,
            MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default) => throw Unexpected();

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw Unexpected();

        public Task<DomainResult<Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();

        public Task<DomainResult<Booking>> EraseBookerAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw Unexpected();
    }

    private static T Payload<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value);
    }

    /// <summary>Answers cancellation with whatever the test needs, and records the id asked for.</summary>
    private sealed class CancellingBookingService(DomainResult<Booking> answer) : IBookingService
    {
        public Guid? CancelledId { get; private set; }

        public Task<DomainResult<Booking>> CancelAsync(
            Guid bookingId, CancellationToken cancellationToken = default)
        {
            CancelledId = bookingId;
            return Task.FromResult(answer);
        }

        public Task<DomainResult<Booking>> EraseBookerAsync(
            Guid bookingId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceAsync(
            BookingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceAsync(
            MultiClaimBookingRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DomainResult<Booking>> PlaceForServiceAsync(
            ServiceAttribution service,
            MultiClaimBookingRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public DomainResult CheckPlacementRules(Resource resource, DateTimeOffset start, TimeSpan duration)
            => throw new NotSupportedException();
    }

    private static Booking Cancelled()
    {
        var booking = Booking.Rehydrate(
            Guid.NewGuid(),
            References.Any(),
            BookingInterval.Create(
                new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 2, 10, 0, 0, TimeSpan.Zero),
                "Europe/London").Value,
            Booker.Create(null, "Ada Lovelace", "ada@example.com", null).Value,
            [new ResourceClaim(Guid.NewGuid())],
            BookingStatus.Cancelled,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)).Value;

        return booking;
    }

    [Fact]
    public async Task Cancelling_returns_the_bookings_new_status()
    {
        var booking = Cancelled();
        var service = new CancellingBookingService(DomainResult<Booking>.Success(booking));
        var (controller, _) = Endpoint(bookingService: service);

        var model = Payload<CancelledBookingModel>(await controller.CancelBooking(booking.Id));

        Assert.Equal(booking.Id, service.CancelledId);
        Assert.Equal(booking.Id, model.BookingId);

        // A NAME on the wire, as everywhere else in this contract — never the enum's ordinal.
        Assert.Equal("Cancelled", model.Status);
    }

    [Fact]
    public void The_cancellation_response_does_not_imitate_a_list_row()
    {
        // A list row carries each resource's NAME, which the management port joins for. This
        // path has the domain's booking, which knows ids only — so a response shaped like a
        // row would have to leave those names blank and look quietly less true than the thing
        // it resembles. It carries what it knows instead.
        var properties = typeof(CancelledBookingModel).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["BookingId", "Status"], properties);
    }

    [Fact]
    public async Task Cancelling_an_uncancellable_booking_is_a_400_carrying_the_domains_code()
    {
        // Refused, not quietly reported as done. A caller told "cancelled" when nothing
        // changed cannot tell a completed action from a rejected one.
        var service = new CancellingBookingService(DomainResult<Booking>.Failure(
            FailureCodes.InvalidStatusTransition, "A booking cannot move from Cancelled to Cancelled."));
        var (controller, _) = Endpoint(bookingService: service);

        var result = Assert.IsType<ObjectResult>(await controller.CancelBooking(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains(FailureCodes.InvalidStatusTransition, Codes(result));
    }

    [Fact]
    public async Task Cancelling_an_unknown_booking_is_a_404()
    {
        // Distinct from the 400 above, and that distinction is the point: a stale list and a
        // booking somebody already dealt with call for different actions from the operator.
        var service = new CancellingBookingService(DomainResult<Booking>.Failure(
            FailureCodes.BookingNotFound, "No booking exists with that id."));
        var (controller, _) = Endpoint(bookingService: service);

        var result = Assert.IsType<ObjectResult>(await controller.CancelBooking(Guid.NewGuid()));

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Contains(FailureCodes.BookingNotFound, Codes(result));
    }

    private static IEnumerable<string?> Codes(ObjectResult result)
    {
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        var errors = Assert.IsType<ApiErrorModel[]>(problem.Extensions["errors"]);

        return errors.Select(e => e.Code);
    }

    [Fact]
    public async Task A_page_is_returned_with_its_unpaged_total()
    {
        var roomId = Guid.NewGuid();
        var therapistId = Guid.NewGuid();

        var serviceId = Guid.NewGuid();

        var page = new BookingPage(
            [
                Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed,
                    new ServiceAttribution(serviceId, "Initial Consultation"),
                    (roomId, "Meeting Room A"), (therapistId, "MRC Therapist")),
            ],
            Total: 75);

        var (controller, _) = Endpoint(page);

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));

        // The total is the port's, not the page size — returning Items.Count would give 1.
        Assert.Equal(75, model.Total);

        var item = Assert.Single(model.Items);

        // Status is a NAME on the wire. Serialising the enum would put an ordinal here and
        // pin the contract to a Core declaration's order.
        Assert.Equal("Confirmed", item.Status);
        Assert.Equal(BookerConditions.Shown, item.Booker.Condition);
        Assert.NotNull(item.Booker.Contact);
        Assert.Equal("Ada Lovelace", item.Booker.Contact.Name);
        Assert.Equal("ada@example.com", item.Booker.Contact.Email);
        Assert.Null(item.Booker.ErasedUtc);
        Assert.Equal("Europe/London", item.TimeZoneId);

        // Both resources, each with its own name — the mapper had no test before this.
        Assert.Equal(2, item.Resources.Count);
        Assert.Contains(item.Resources, r => r.ResourceId == roomId && r.DisplayName == "Meeting Room A");
        Assert.Contains(item.Resources, r => r.ResourceId == therapistId && r.DisplayName == "MRC Therapist");

        // The service, as one object rather than two fields that must agree about being
        // null together.
        Assert.NotNull(item.Service);
        Assert.Equal(serviceId, item.Service.ServiceId);
        Assert.Equal("Initial Consultation", item.Service.DisplayName);
    }

    [Fact]
    public async Task A_directly_placed_booking_carries_a_null_service()
    {
        // Not an object with empty values. A client cannot tell "placed directly" from
        // "placed for a service we failed to record" if the absence is spelled as a
        // populated object carrying blanks — and the first is a fact while the second is
        // a defect, so they must not look alike.
        var page = new BookingPage(
            [
                Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed,
                    service: null,
                    (Guid.NewGuid(), "Meeting Room A")),
            ],
            Total: 1);

        var (controller, _) = Endpoint(page);

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));

        Assert.Null(Assert.Single(model.Items).Service);
    }

    [Fact]
    public async Task Cancelled_bookings_are_reachable_over_http()
    {
        var (controller, store) = Endpoint();

        await controller.ListBookings(From, To, statuses: ["Cancelled"]);

        Assert.Equal([BookingStatus.Cancelled], store.LastQuery!.Statuses);
    }

    [Fact]
    public async Task A_comma_joined_status_list_is_refused_rather_than_silently_collapsing()
    {
        // The defect this test exists for. Enum.TryParse accepts comma-separated lists and
        // combines them bitwise even on a non-flags enum: Confirmed(1) | Cancelled(3) is
        // 3, which IS defined, so a caller asking for both received CANCELLED ONLY with a
        // 200 — a page filtered by something other than what was asked for.
        var (controller, store) = Endpoint();

        var result = await controller.ListBookings(From, To, statuses: ["Confirmed,Cancelled"]);

        Assert.IsNotType<OkObjectResult>(result);
        Assert.Null(store.LastQuery);
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" 1")]
    [InlineData("  2  ")]
    [InlineData("Sideways")]
    [InlineData("")]
    public async Task An_unrecognised_status_is_refused_with_its_stable_code(string status)
    {
        // Numerics bind by ordinal rather than by the name the response uses, and
        // Enum.TryParse trims whitespace before parsing — so a digit guard that does not
        // trim identically is simply bypassed by a leading space.
        var (controller, store) = Endpoint();

        var result = await controller.ListBookings(From, To, statuses: [status]);
        var problem = Assert.IsType<ObjectResult>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains(
            FailureCodes.BookingStatusInvalid,
            System.Text.Json.JsonSerializer.Serialize(problem.Value),
            StringComparison.Ordinal);

        // Refused before the store is asked for anything.
        Assert.Null(store.LastQuery);
    }

    [Fact]
    public async Task Omitted_paging_uses_the_ports_default_rather_than_a_restated_one()
    {
        // The endpoint must not carry its own copy of the paging default. Asserted against
        // the port's constants rather than against literals, so changing the port's
        // default cannot leave the endpoint silently pinned to the old one.
        var (controller, store) = Endpoint();

        await controller.ListBookings(From, To);

        Assert.Equal(BookingQuery.DefaultSkip, store.LastQuery!.Skip);
        Assert.Equal(BookingQuery.DefaultTake, store.LastQuery.Take);
    }

    [Fact]
    public async Task Supplied_paging_is_passed_through()
    {
        var (controller, store) = Endpoint();

        await controller.ListBookings(From, To, skip: 10, take: 5);

        Assert.Equal(10, store.LastQuery!.Skip);
        Assert.Equal(5, store.LastQuery.Take);
    }

    [Fact]
    public async Task Omitted_filters_reach_the_port_as_omitted()
    {
        // No status set and no resource set, so the port applies its own defaults. The
        // endpoint inventing a set here would be the same defect as restating paging.
        var (controller, store) = Endpoint();

        await controller.ListBookings(From, To);

        Assert.Equal(
            BookingQuery.Create(
                store.LastQuery!.FromUtc, store.LastQuery.ToUtc, Settings()).Value.Statuses.Order(),
            store.LastQuery.Statuses.Order());
        Assert.Empty(store.LastQuery.ResourceIds);
    }

    [Fact]
    public async Task The_window_reaches_the_port_as_site_local_instants()
    {
        var (controller, store) = Endpoint(zone: "Europe/London");

        await controller.ListBookings(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

        // London is UTC+1 in June, so the local day starts at 23:00 the previous UTC day
        // and the inclusive end is the start of the next local day.
        Assert.Equal(new DateTimeOffset(2026, 5, 31, 23, 0, 0, TimeSpan.Zero), store.LastQuery!.FromUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 23, 0, 0, TimeSpan.Zero), store.LastQuery.ToUtc);
    }

    [Fact]
    public async Task An_over_wide_window_is_refused_before_the_store_is_asked()
    {
        var (controller, store) = Endpoint();

        var result = await controller.ListBookings(From, From.AddDays(31));
        var problem = Assert.IsType<ObjectResult>(result);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Null(store.LastQuery);
    }

    // ---------------------------------------------------------------- withholding

    [Fact]
    public async Task A_caller_with_sensitive_data_access_is_given_the_booker()
    {
        var page = new BookingPage(
            [Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed)],
            Total: 1);

        var (controller, _) = Endpoint(page, security: Security(sensitiveData: true));

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));
        var item = Assert.Single(model.Items);

        Assert.Equal(BookerConditions.Shown, item.Booker.Condition);
        Assert.NotNull(item.Booker.Contact);
        Assert.Equal("Ada Lovelace", item.Booker.Contact.Name);
        Assert.Equal("ada@example.com", item.Booker.Contact.Email);
        Assert.Null(item.Booker.ErasedUtc);
    }

    [Fact]
    public async Task A_caller_without_sensitive_data_access_is_given_the_same_rows_without_the_booker()
    {
        // Two bookings and a total larger than the page, so this also asserts what withholding
        // does NOT do: it removes details from rows, never rows from the result. A filter that
        // dropped what it could not show would silently answer a different question, and the
        // operator would have no way to see that anything was missing.
        var page = new BookingPage(
            [
                Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed),
                Summary(new DateTimeOffset(2026, 6, 3, 9, 0, 0, TimeSpan.Zero), BookingStatus.Cancelled),
            ],
            Total: 75);

        var (controller, _) = Endpoint(page, security: Security(sensitiveData: false));

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));

        Assert.Equal(75, model.Total);
        Assert.Equal(2, model.Items.Count);
        Assert.All(model.Items, item => Assert.Equal(BookerConditions.Withheld, item.Booker.Condition));
        Assert.All(model.Items, item => Assert.Null(item.Booker.Contact));

        // Withheld is NOT erased. The row says the details exist and this caller may not see
        // them, which sends the operator to a colleague — where "erased" would tell them there
        // is nobody to ask. Reporting either as the other is the failure this condition exists
        // to prevent, so the negative is asserted as well as the positive.
        Assert.All(model.Items, item => Assert.Null(item.Booker.ErasedUtc));

        // And everything that is not personal data survives — a row withheld down to nothing
        // would be unusable, and the reference is what an operator identifies it by.
        Assert.All(model.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Reference)));
        Assert.All(model.Items, item => Assert.Equal("Europe/London", item.TimeZoneId));
    }

    [Fact]
    public async Task A_withheld_booker_leaves_nothing_behind_in_the_serialized_payload()
    {
        // Asserting `Booker is null` proves the member is empty; it does not prove the values
        // are gone. A future shape that kept a copy elsewhere — a display string, a search
        // key, an audit field — would satisfy that assertion and still disclose the address.
        // The guarantee is about the payload, so the payload is what is searched.
        var page = new BookingPage(
            [Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed)],
            Total: 1);

        var (controller, _) = Endpoint(page, security: Security(sensitiveData: false));

        var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));
        var json = System.Text.Json.JsonSerializer.Serialize(model);

        Assert.DoesNotContain("Ada", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lovelace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ada@example.com", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unresolvable_current_user_is_withheld_from_rather_than_trusted()
    {
        // Both ways the accessor can fail to name a user. The endpoint is already authorized so
        // neither should occur — which is exactly why they are tested: "cannot happen" is how a
        // defaulted `true` ships, and the failure mode of guessing wrong here is disclosure.
        var page = new BookingPage(
            [Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed)],
            Total: 1);

        foreach (var security in new IBackOfficeSecurityAccessor[]
        {
            new StubBackOfficeSecurityAccessor(null),
            new StubBackOfficeSecurityAccessor(new StubBackOfficeSecurity(null)),
        })
        {
            var (controller, _) = Endpoint(page, security: security);

            var model = Payload<PagedBookingsModel>(await controller.ListBookings(From, To));

            var booker = Assert.Single(model.Items).Booker;

            Assert.Equal(BookerConditions.Withheld, booker.Condition);
            Assert.Null(booker.Contact);
        }
    }
}
