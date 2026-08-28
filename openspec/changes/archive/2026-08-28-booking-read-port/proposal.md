## Why

There is no way to list bookings. `IBookingStore` reads claims for a resource over a
time range (for availability), fetches one booking by id, places one, and updates one.
Nothing enumerates them.

That single gap blocks the whole of backoffice booking management, and nothing else
does. A list screen, a day view, a "what's on today" panel, an export — every one of
them waits on the same port. The rest of the section is ordinary work once it exists.

The pattern to follow is already in the repository twice. `IResourceStore` /
`IResourceManagementStore` and `IServiceStore` / `IServiceManagementStore` split the
delivery read from the backoffice read, and `resource-management` is its own capability
rather than an annex of `resources`. Bookings have no management store and no management
capability; this change adds both, back-end only.

**What "booking management" turns out to mean in v1 is worth stating**, because it is
smaller than the phrase suggests and the difference is a scope trap. Placement
auto-confirms: the `bookings` spec says no v1 pathway produces `Requested` or `Declined`,
and `IBookingService` exposes only `PlaceAsync` and `CancelAsync`. So an editor's honest
verbs are **see** and **cancel**. Approve, decline, amend and edit-the-booker are not
thin UI layers over an existing domain — each is a domain change. This change ships the
first half of "see" and deliberately touches none of the rest.

## What Changes

- **A management read port.** `IBookingManagementStore` in Core, with a single windowed,
  paged, filtered query returning summaries rich enough to render a list row without a
  second round trip per booking.
- **A query type that cannot hold an unusable window.** `BookingQuery.Create` returns
  `DomainResult<BookingQuery>`, so a window that runs backwards or exceeds the site's
  guardrail never becomes a value at all and the store has nothing to validate. Stronger
  than checking in front of the store, which a caller can route around — and it keeps Core
  free of a service depending on a management store, which `bookings` forbids so the read
  ports stay the only pathway anonymous delivery traffic reaches storage through.
- **Its SQL implementation.** `SqlBookingManagementStore` in Persistence, joining
  `BookingRow` → `ClaimRow` → `ResourceRow` so a row carries its resources' **names**,
  not just their ids. `ClaimRow` holds only `ResourceId`, so without the join every list
  row costs a lookup per claim.
- **A required date window, bounded by the existing guardrail.** The booking table grows
  without limit, which is exactly the cost hole `MaxQueryRangeDays` already exists to
  close for availability queries. The same setting bounds this query, and the same
  `date-range-too-large` failure code is returned.
- **Filters: status and resource.** Both are answerable from rows the query already
  touches — the resource join exists for the names regardless. The status default excludes
  `Cancelled` and `Declined`: visible on request, hidden by default, because a cancellation
  is noise on the question "what is booked". The resource filter takes a **set** of ids and
  matches a booking claiming any of them, because widening a single id to a set afterwards
  would be a breaking change to published API and starting with a set costs nothing.
- **A new `booking-management` capability.** Not an annex of `bookings`, because the actor
  is different: `bookings` is a visitor placing one, `booking-management` is an operator
  looking at what a site has taken — and, before long, recording one on someone's behalf.

No API, no UI, no schema change, no migration, no change to how bookings are placed or
cancelled.

## Non-goals

- **Filtering or grouping by service.** *This is not a decision, it is a limitation, and
  it was measured:* a booking does not record the service that produced it. `BookingRow`
  has no `ServiceId` (the only `ServiceId` in the schema belongs to `ServiceRoleRow`), and
  the `Booking` domain object has no service either — `ServiceBookingService.PlaceAsync`
  uses the service to *choose* resources and then discards it. Offering a service filter
  would require an additive column, a migration, and a decision about the bookings already
  placed without one. That is its own change; naming it here stops it being discovered
  halfway through a UI.
- **Search by booker name, email or reference.** "A customer rang about their booking" is
  a lookup, not a windowed list, and a required date window makes it impossible. It wants
  its own query and probably its own index. Deliberately out.
- **Sorting by anything but start time.** One total order, chosen so paging is stable
  (see design D5). A sortable column set is a UI decision and a set of index questions,
  and neither is answerable yet.
- **Any mutation.** No cancel, no confirm, no decline, no amend. `IBookingService.CancelAsync`
  already exists and is untouched.
- **Approval workflow.** Making `Requested` reachable is a domain change to placement, and
  the `bookings` spec deliberately keeps the status and its transitions available for
  exactly that future change. Not this one.
- **Management API endpoints or backoffice UI.** Both are the obvious next changes. Keeping
  them out is what makes this one small enough to be worth reviewing on its own.
- **Personal data in logs or fixtures.** The port necessarily returns booker name and
  email, because a list row shows who booked. Nothing logs them, and test fixtures use
  invented people.

## Capabilities

### New Capabilities
- `booking-management`: how the backoffice reads bookings — the windowed, paged, filtered
  query, what a summary carries, which statuses are returned by default, and the limits
  the query refuses to exceed.

## Impact

- **`UBookIt.Core`**: `IBookingManagementStore` and the query, summary and page types it
  uses, the query carrying its own validation. No change to `IBookingStore`,
  `IBookingService` or `Booking`, and **no new Core service** — see design D2.
- **`UBookIt.Persistence`**: `SqlBookingManagementStore` and its DI registration. Reads
  only; no entity, schema or migration change.
- **Public API surface** (additive): the port and its query, summary and page types. A
  store is a Core port, so a site could implement it against another database, exactly as
  with the existing management stores.
- **`UBookIt.Tests` / `UBookIt.Tests.Integration`**: coverage for windowing, the range
  guardrail, paging, status defaults and filters, and the resource-name join.
- **No dependencies, no schema change, no migration.** Nothing installs into a site.
