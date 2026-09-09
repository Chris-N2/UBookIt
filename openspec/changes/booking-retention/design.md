## Context

`booker-erasure` built the verb and made it absorbing at the point of storage:
`SqlBookingStore.EraseBookerAsync(id, instant)` is one `ExecuteUpdate` whose already-erased test
is *inside* the statement, so it is idempotent, race-free and irreversible by construction. It
took six QA rounds and three fix-induced defects to get there. **This change adds no erasure
mechanics.** It adds a clock, a way to find what the clock has caught, and something to turn the
handle.

That is the shape to protect. Every decision below is chosen to keep retention a *caller* of
existing machinery rather than a second implementation of it.

Two facts from the codebase constrain the design and were verified rather than assumed:

- `IBookingService`, `IBookingStore` and the `DbContext` are **scoped**. Anything Umbraco
  registers as a background job is a **singleton**, resolved from the root provider.
- `SiteBookingSettings` is a singleton snapshot resolved once from `IConfiguration` at startup.
  Configuration changes already require a restart everywhere else in the package.

## Goals / Non-Goals

**Goals:**

- Retention erases through the one erasure verb, so anything erasure grows later (an audit
  record, a notification) retention gets without being changed.
- A sweep that is safe to interrupt, safe to repeat, and safe to run while an operator is
  erasing by hand or a visitor is booking.
- A sweep that cannot silently do half its job.
- Off unless configured, and unreadable configuration means off — never a guessed period.
- The unattended path never touches a contact detail, so `booker-erasure`'s access requirement
  survives the existence of a caller-less erasure.

**Non-Goals:**

- Bounding a single run's total work. See D6.
- A second erase-many verb at the store. See D3.
- Making the sweep observable beyond logs. Out of scope per the proposal.

## Decisions

### D1. `IDistributedBackgroundJob`, not `IRecurringBackgroundJob`

Umbraco 17 ships both, and **my earlier note named the wrong one.**

| | `IRecurringBackgroundJob` | `IDistributedBackgroundJob` |
|:--|:--|:--|
| Scheduling | One hosted service per job, per server | One shared scheduler, DB-backed lease |
| Load balancing | Gated on `ServerRole` ∈ {Single, SchedulingPublisher} + `IMainDom` | Any server; the lease makes exactly one run it |
| Stale run | Nothing | `MaximumExecutionTime` releases it for another server |
| Umbraco's own cleanup jobs | — | `ContentVersionCleanupJob`, `LogScrubberJob`, `TemporaryFileCleanupJob`, `LongRunningOperationsCleanupJob` |

Every cleanup job Umbraco ships uses the distributed one, and retention is exactly that shape:
periodic, idempotent, must run *somewhere* rather than on a particular server. Registration is a
plain `AddSingleton<IDistributedBackgroundJob, T>()` — no extension method.

**Verified against the pinned 17.6.2 assembly's XML documentation, not against the 17.8 source in
`ref/`.** `IDistributedBackgroundJob`, `ExecuteAsync(CancellationToken)` and the scheduler are all
present in 17.6.2. Checking this mattered: the 17.8 source carries members marked as added since,
and the package floats at `>= 17.6.2`.

*Alternative considered — our own `AppLock`.* The persistence layer already has one
(`AppLock.ForResourcePlacement`). Rejected: it would be a second scheduling mechanism inside a
CMS that has one, and it solves only mutual exclusion, not "who runs it and how often".

**`Name` is the lease key** and is stored in Umbraco's database. It must be stable for the life
of the package: renaming it later orphans a row and restarts the schedule. Fixed as
`"UBookItBookerRetention"`.

### D2. The due-read returns identifiers only — and that is a security decision

`booker-erasure` requires that only a caller who may *read* contact details may erase them. A
background job has no caller, so the requirement cannot be applied as written; it is reopened in
the spec delta. What makes the reopening honest rather than a hole is that the unattended path is
built so that there is nothing to leak:

```
IBookingStore.GetBookingIdsDueForErasureAsync(cutoffUtc, take, ct)
    → IReadOnlyList<Guid>
```

It selects on **time alone** (`EndUtc < cutoff AND BookerErasedUtc IS NULL`), takes no contact
detail as input, and returns no personal data — not a name, not an address, not a
`BookingListItem`. The job then holds a list of GUIDs and erases each one.

The alternative — reusing `IBookingManagementStore.ListAsync` and filtering — was rejected on
three counts. It cannot express an unwindowed query (`BookingQuery` forbids one by construction).
It returns rows carrying booker names and emails the job has no use for, which would put contact
details in the hands of the one code path with no user to be accountable for them. And it would
make a *management read* the mechanism of an *unattended write*.

### D3. The job calls `IBookingService.EraseBookerAsync` per booking, not a set-based erase

A single `UPDATE … WHERE EndUtc < @cutoff` would erase every due booking in one statement, and it
is tempting: it is faster, it is atomic, and the absorbing `CASE` construction would carry over.

Rejected, because it is a **second erase verb**. `booker-erasure`'s whole design is that erasure
happens in exactly one place, so a future audit record or notification attaches once. A set-based
sibling would be a place for erasure to happen that the next change forgets to update — and the
failure would be silent, because both would still erase.

Going through `IBookingService` rather than straight to the store costs one read-back per booking
(the service re-reads to report the *stored* erasure instant, which an HTTP caller needs and the
job does not). On a nightly batch with no latency budget that is an acceptable price for one
erasure path. If it ever isn't, that is a measured problem with a measured fix.

**Per-booking failure is caught, logged and skipped.** The service throws
`InvalidOperationException` if a row is erased but cannot be read back. Letting that abort the run
would let one permanently bad row block retention for every booking after it, forever. The run
continues and reports a failure count.

### D4. Page by always taking the *first* page — never `skip`

This is the defect this design most exists to prevent.

The due-set is defined by `BookerErasedUtc IS NULL`, so **erasing a booking removes it from the
result set.** Paging with `skip = 0, 100, 200, …` would therefore step over un-erased bookings:
after erasing rows 0–99 the remaining set shifts down by 100, and `skip = 100` lands past a
hundred bookings that were never touched. The sweep would report success having erased half the
data, and every test with fewer than one page of fixtures would pass.

```
take 100 ── erase all 100 ── set shrinks by 100 ── take 100 again ── … ── take 100 → 0 rows → done
   ▲                                                                   │
   └───────────────────────────  never skip  ───────────────────────────┘
```

**Anti-spin guard.** If an erase fails, that booking still matches the predicate and comes back in
the next batch — forever. So: **a batch that yields zero successful erasures ends the run**, with
an error logged naming the count it could not erase. Without this, one bad row is an infinite
loop holding a database connection.

### D5. `RetentionDays` is `int?`, and an unreadable value means OFF

`MaxQueryRangeDays` falls back to a working default when the configured value is malformed. **The
retention setting must not**, and the asymmetry is the point:

- Falling back to *off* keeps data longer than the operator intended. Recoverable — they fix the
  typo and the next run catches up.
- Falling back to *a number* erases data irreversibly on a value nobody wrote.

So: absent, blank, unparseable, zero or negative all resolve to `null` — off. **Zero is rejected
rather than read as "erase as soon as the booking ends"**, because `RetentionDays: 0` is far more
likely to be somebody writing "off" than somebody wanting immediate erasure, and the cost of
misreading it that way is unrecoverable.

**An unreadable value is logged as an error at startup**, not passed over. Silence here would be
worse than the time-zone case (which logs a warning), because change ④ publishes this period in a
privacy notice: a site whose retention silently failed to configure would be telling visitors
their data is erased after 90 days while nothing erases it.

`null` rather than a sentinel `0` because change ④ must distinguish *"retention is off"* from
*"retention is N days"* to avoid writing a notice that lies — the same reasoning that stopped
erasure reusing `sensitive-data`'s null.

### D6. Loop until the set is empty; bound the run by cancellation, not by a cap

The job loops until a batch comes back empty, honouring the host's `CancellationToken`.

*Alternative considered — cap the run at N bookings.* Rejected. The first run on an existing site
is the large one (potentially the whole table); a cap would spread it over days for no benefit,
during which the site is neither in its old state nor its new one. Interruption is already safe:
erasure is absorbing, so a run cut off by a deployment resumes next time with no double work and
no gap.

Batch size is **100**, and each batch gets **its own DI scope** — so the job (a singleton) can
reach the scoped `IBookingService`, and no `DbContext` lives for the whole sweep.

Umbraco's `MaximumExecutionTime` (5 minutes by default) may hand the lease to another server
mid-run. Harmless: the second runner erases what the first has not, and any overlap is absorbed.
Worth stating because it looks alarming and is not.

### D7. A filtered index, added additively

The predicate is `EndUtc < @cutoff AND BookerErasedUtc IS NULL`. The existing index is
`(StartUtc, EndUtc) INCLUDE (Status)` — `EndUtc` is not the leading column, so it cannot seek.

```sql
CREATE INDEX IX_uBookItBooking_EndUtc_Unerased
    ON uBookItBooking (EndUtc) WHERE BookerErasedUtc IS NULL
```

Filtered, which is what makes it cheap in the steady state: it holds only un-erased bookings, so
on a site with retention on it shrinks to roughly one period's worth and stays there. Adding an
index is additive; the migration touches no data.

### D8. Status-blind

The sweep filters on time and erasure state, and on nothing else. A status filter would be a way
for the sweep to under-erase — the same reasoning that kept a status filter off `BookerEmailQuery`
in `find-by-booker`. A booking still `Requested` with an end two years in the past holds a real
person's details just as firmly as a `Confirmed` one.

### D9. Where the code lives

The job goes in `UBookIt.Persistence`, beside `Notifications/UmbracoBookingObserver`. That project
is nominally "persistence" but is in practice the package's Umbraco integration layer — it owns
the composer and every Umbraco-facing registration, and `IDistributedBackgroundJob` is an Umbraco
type that `UBookIt.Core` must not see. Noted because the name reads oddly; moving the boundary is
not this change's job.

## Risks / Trade-offs

**A site turns retention on and loses three years of contact details in one run.** → Correct
behaviour, and the reason the docs must call it a one-way door rather than a setting. Mitigated by
being off by default and by requiring a deliberate configuration edit. Not mitigated by a
confirmation prompt, because there is nowhere to prompt — which is itself an argument the docs
have to carry.

**A guard is written that cannot see the paging defect.** → The single sharpest risk, given this
project's history. Any test for the sweep must use **more than one batch** of fixtures; a fixture
set smaller than the batch size makes the `skip`-versus-first-page distinction unobservable, and a
test that cannot fail is worse than no test. Stated here so QA can check the fixture count, not
just the assertion.

**The retention setting is read once at startup, so changing it needs a restart.** → Consistent
with every other `UBookIt:*` setting; documented rather than fixed. Reading it per-run via
`IOptionsMonitor` was considered and rejected as an inconsistency with the rest of the package for
a setting nobody changes hourly.

**Erasure grows a notification later and the sweep sends thousands.** → Not a problem this change
creates, but it is one this change makes reachable, because retention is the first bulk caller of
the verb. Recorded so the change that adds a notification considers the batch case rather than
discovering it.

**The lease name is a durable key in somebody else's table.** → Fixed constant, stated in the
spec, and never derived from a version or an assembly name.

## Migration Plan

One EF Core migration adding the filtered index. Additive, no data movement, no rollback concern
beyond dropping the index. Existing sites acquire the index on the package's normal run-once
migration path and behave identically until somebody sets `RetentionDays`.

## Open Questions

None blocking. Two recorded for later:

- Whether the first run on a large existing table wants a one-off throttle. Deferred until
  somebody has a table big enough to measure; D6 explains why guessing now is worse.
- Whether a future settings screen (roadmap 0.9.0) should refuse to *lower* the retention period
  without a confirmation, since lowering it erases on the next run.
