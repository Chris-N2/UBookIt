using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UBookIt.Backoffice.Controllers;
using UBookIt.Backoffice.Models;
using UBookIt.Core;
using UBookIt.Core.Bookings;
using UBookIt.Core.Common;
using UBookIt.Core.Stores;

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
    }

    private static BookingSummary Summary(
        DateTimeOffset startUtc, BookingStatus status, params (Guid Id, string Name)[] resources)
        => new(
            Guid.NewGuid(),
            BookingInterval.Create(startUtc, startUtc.AddHours(1), "Europe/London").Value,
            status,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            "Ada Lovelace",
            "ada@example.com",
            [.. resources.Select(r => new BookedResource(r.Id, r.Name))]);

    private static (BookingsController Controller, RecordingStore Store) Endpoint(
        BookingPage? page = null, string zone = "UTC")
    {
        var store = new RecordingStore(page ?? new BookingPage([], 0));
        return (new BookingsController(store, Settings(zone)), store);
    }

    private static T Payload<T>(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<T>(ok.Value);
    }

    [Fact]
    public async Task A_page_is_returned_with_its_unpaged_total()
    {
        var roomId = Guid.NewGuid();
        var therapistId = Guid.NewGuid();

        var page = new BookingPage(
            [
                Summary(new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), BookingStatus.Confirmed,
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
        Assert.Equal("Ada Lovelace", item.BookerName);
        Assert.Equal("ada@example.com", item.BookerEmail);
        Assert.Equal("Europe/London", item.TimeZoneId);

        // Both resources, each with its own name — the mapper had no test before this.
        Assert.Equal(2, item.Resources.Count);
        Assert.Contains(item.Resources, r => r.ResourceId == roomId && r.DisplayName == "Meeting Room A");
        Assert.Contains(item.Resources, r => r.ResourceId == therapistId && r.DisplayName == "MRC Therapist");
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
}
