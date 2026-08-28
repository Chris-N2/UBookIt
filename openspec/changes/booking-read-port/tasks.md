## 1. The Core contract

- [ ] 1.1 Add `BookingQuery` (window, statuses, **resource ids as a set**, skip, take), `BookingSummary` (id, interval, `TimeZoneId`, status, created, booker name and email, claimed resources as id + name) and `BookingPage(Items, Total)`. Follow the existing `ResourcePage`/`ServicePage` shape rather than inventing a third.
- [ ] 1.2 Add `IBookingManagementStore` with one list method taking a `BookingQuery` and returning `BookingPage` — **not** `DomainResult`. Management store reads return the page directly; only mutations carry failures. Getting this backwards makes it the only validating store in the codebase.
- [ ] 1.3 Add `IBookingQueryService` and its implementation: enforce the window against `SiteBookingSettings.MaxQueryRangeDays`, fail with `FailureCodes.DateRangeTooLarge`, otherwise delegate. Mirror how `AvailabilityService` does the same guard — read it first rather than reimplementing the arithmetic.
- [ ] 1.4 Make the window non-optional in the type, so "list every booking" cannot be expressed. A guard the caller may skip is not a guard.
- [ ] 1.5 Register both in the Core composer alongside the existing services. Scoped, matching the stores they compose — a singleton over scoped stores captures a disposed scope's DbContext on the second request.

## 2. The SQL implementation

- [ ] 2.1 Add `SqlBookingManagementStore` joining `BookingRow` → `ClaimRow` → `ResourceRow` so a summary carries resource **names**. `ClaimRow` holds only `ResourceId`; without the join every row costs a lookup per claim, which is the cost this port exists to remove.
- [ ] 2.2 Match the window by **overlap**, half-open `[fromUtc, toUtc)`, the same as `IBookingStore.GetClaimsAsync`. Assert a booking that starts before the window and ends inside it is returned — the case a naive `StartUtc >= from` predicate silently drops.
- [ ] 2.3 Order by `StartUtc` then `Id`, both ascending. A total order, or paging repeats and drops rows.
- [ ] 2.4 Apply the status and resource filters in the query, not in memory after paging — filtering after `Take` returns short pages and a wrong total. The resource predicate is `any claim matches any named id`; an empty set means no filter, not `match nothing`.
- [ ] 2.5 Compute `Total` over the window and filters, not over the page.
- [ ] 2.6 Register the store in the persistence composer beside the other Sql stores.

## 3. Guards that would catch the real mistakes

- [ ] 3.1 **Paging over a tie.** Fixture several bookings sharing one start time, read across a page boundary, assert nothing repeats or vanishes. A fixture of distinct start times passes against an ordering with no tiebreak at all — this is the ordering landmine already recorded against this project.
- [ ] 3.2 **Overlap, not containment.** One booking starting before the window, one ending after it, one wholly inside, one wholly outside. Assert exactly the first three come back.
- [ ] 3.3 **The range guard**, at the boundary and one day past it, asserting the failure code and that no results come with it.
- [ ] 3.4 **The default status set** returns blocking bookings only, and asking explicitly for `Cancelled` returns them. Vary the fixture so a query returning everything and a query returning the right thing are distinguishable.
- [ ] 3.5 **A booking claiming two resources** comes back **once**, with both resources named — and once in `Total` — both with and without a resource filter that matches both. This is the join fanning out, and it is the likeliest defect in the whole change: a one-claim fixture cannot see it, and neither can a filter naming only one of the two.
- [ ] 3.6 **`Total` versus page size** — assert with a `take` smaller than the match count, so returning `Items.Count` fails.
- [ ] 3.7 **The join returns the right names**, asserted against resources whose names differ from each other and from anything derivable from an id — so a join that pairs a booking with the wrong resource is visible rather than passing on a single-resource fixture.
- [ ] 3.8 Mutation-check the ordering and the overlap predicate: break each deliberately, from a clean build, and confirm a test fails. **Restore with an edit, never a timestamp-preserving copy** — a restore that looks older than its own build output is skipped silently while the build reports success.

## 4. Verify and close

- [ ] 4.1 Full solution build at **zero** warnings — the baseline is zero, so read every build against zero and not against "no new ones".
- [ ] 4.2 Full test suite green, with the count compared against the 1612 baseline.
- [ ] 4.3 `openspec validate --all --strict`.
- [ ] 4.4 **Re-read the delta spec against the code before syncing.** Deltas go stale after every QA round; on the last two changes the deltas were corrected at sync, and syncing them unread would have written a weaker requirement into the main spec while looking like a clean sync.
- [ ] 4.5 Sweep the sibling specs this change could falsify — in particular whether anything in `bookings` or `resource-management` claims something about how bookings are read. Use **multiline** matching when absence-checking a clause: a wrapped sentence defeats a single-line grep and reads exactly like a dropped guarantee.
- [ ] 4.6 Hand the change to `qa-review` in a **fresh context or subagent** — the context that implemented it never reviews it.
