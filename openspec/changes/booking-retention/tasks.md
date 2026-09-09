## 1. The setting

- [ ] 1.1 Add `RetentionDays` to `SiteBookingSettings` as `int?`. **Nullable, not `0`-as-off** — change ④ must distinguish "we erase after N days" from "we do not erase", and must not be able to publish the second as the first.
- [ ] 1.2 Add `RetentionDaysSettingKey = "UBookIt:RetentionDays"` beside the existing key constants in `UBookItPersistenceComposer`.
- [ ] 1.3 Resolve it in `ResolveSettings`. **Do NOT copy the `ResolveMaxQueryRangeDays` shape** — that one falls back to a working default and this one must not. Absent, blank, non-numeric, zero and negative all resolve to `null`.
- [ ] 1.4 Log an **error** when a value was written and could not be read; log **nothing** when it is absent. The two must be distinguishable, because absence is a choice and a mistyped value is a fault that change ④ will otherwise publish as a lie. Note the existing time-zone precedent logs a *warning* — this is deliberately louder.
- [ ] 1.5 Resolving the setting must not need the composer. Keep it an `internal static` on the composer as the others are, so a unit test can call it with an in-memory `IConfiguration`.

## 2. The due-read

- [ ] 2.1 Add to `IBookingStore`: `Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(DateTimeOffset cutoffUtc, int take, CancellationToken)`. **BREAKING — published port**, the third such change in 0.3.0; declare it in the proposal as `booker-erasure` and `find-by-booker` each did for theirs.
- [ ] 2.2 Document on the member *why* it returns ids and not rows: the unattended path must handle no contact detail, which is what lets `booker-erasure`'s access requirement survive an erasure with no caller. This is a security property, not a performance one — say so, or somebody will later "improve" it into returning `BookingListItem`.
- [ ] 2.3 Do **not** add it to `IBookingManagementStore`. It is not a management read; it answers no user's question and returns nothing a screen could show.
- [ ] 2.4 Implement in `SqlBookingStore`: `EndUtc < cutoffUtc && BookerErasedUtc == null`, ordered deterministically, `Take(take)`, `AsNoTracking()`, projecting to `b.Id`. **No status filter** — status-blind is a requirement, not an omission.
- [ ] 2.5 Order by `EndUtc` then `Id`. Not for the caller's benefit — an unordered `TOP (n)` is free to return the same rows or different rows on each call, and the sweep's termination argument assumes progress.

## 3. Persistence

- [ ] 3.1 Add the filtered index in `UBookItDbContext`: `HasIndex(b => b.EndUtc).HasFilter("[BookerErasedUtc] IS NULL")`.
- [ ] 3.2 One additive migration. Check the generated SQL is `CREATE INDEX … WHERE` only — no column change, no data statement.
- [ ] 3.3 Regenerate the model snapshot; confirm the diff is the index and nothing else.
- [ ] 3.4 **Re-check the availability path.** A new index changes the optimiser's options and `persistence` has a standing QA gate that the date-range lookup must not table-scan. This index is on `EndUtc`, which availability predicates *do* reference — so unlike `find-by-booker`'s `BookerEmail` index, "it touches a column nothing else queries" is **not** available as an argument here. Run the claims-read suite and say what was observed.

## 4. The job

- [ ] 4.1 Add `BookerRetentionJob : IDistributedBackgroundJob` in `UBookIt.Persistence`. **Not `IRecurringBackgroundJob`** — see design D1; every cleanup job Umbraco itself ships uses the distributed one, and it carries the lease that makes a load-balanced site run it once.
- [ ] 4.2 `Name` is the durable lease key in Umbraco's own table. Fix it as the constant `"UBookItBookerRetention"`. Never derive it from a type name, an assembly name or a version — renaming it orphans a row and silently restarts the schedule.
- [ ] 4.3 Verify `IDistributedBackgroundJob` and `ExecuteAsync(CancellationToken)` against the **pinned 17.6.2** assembly, not the 17.8 source in `ref/`. Already checked once during explore; re-check after any package bump, because the package floats at `>= 17.6.2`.
- [ ] 4.4 Return immediately when `RetentionDays` is null. No query, no scope, no log line per run — an off feature must cost nothing observable.
- [ ] 4.5 Compute the cutoff from the injected `TimeProvider`, never `DateTimeOffset.UtcNow`. The whole feature is a clock comparison; a test that cannot move the clock cannot test it.
- [ ] 4.6 Inject `IServiceScopeFactory`. The job is a **singleton** resolved from the root container and `IBookingService` is **scoped** — capturing it would hold one `DbContext` for the life of the application. One scope per batch.
- [ ] 4.7 The loop: take a batch from the **head** of the due set, erase each through `IBookingService.EraseBookerAsync`, repeat until a batch comes back empty. **Never `Skip`.** See 7.3 — this is the defect the design most exists to prevent.
- [ ] 4.8 Anti-spin: **a batch in which no booking was successfully erased ends the run**, with an error logged. A booking that fails to erase still matches the due predicate and would otherwise be selected for ever.
- [ ] 4.9 Catch per booking, log, continue. `IBookingService.EraseBookerAsync` throws `InvalidOperationException` on an erased-but-unreadable row; letting that abort the run lets one bad row block retention for everything behind it, permanently.
- [ ] 4.10 Honour the `CancellationToken` between batches. An interrupted sweep is safe because erasure absorbs — that is the argument, and it is only true if we stop *between* units rather than mid-write.
- [ ] 4.11 Batch size 100, as a named constant. Not configurable — a knob nobody can reason about is surface, not flexibility.
- [ ] 4.12 **Log counts and booking ids, never a person.** `booker-erasure` names retention reporting as exactly the feature that would give contact details a second durable home. Grep the finished job for any interpolation of a booker property.

## 5. Composition

- [ ] 5.1 Register with `builder.Services.AddSingleton<IDistributedBackgroundJob, BookerRetentionJob>()`. Note there is no `AddDistributedBackgroundJob` extension — Umbraco registers its own this way too.
- [ ] 5.2 Register **unconditionally**, whether or not retention is configured. Conditional registration would make the setting's effect depend on startup configuration in a second, invisible way.

## 6. Documentation

- [ ] 6.1 Document the setting: name, unit, that it is off unless configured, that the period runs from the booking's **end**, and that it is read at startup so a change needs a restart.
- [ ] 6.2 **Document the one-way door in its own right, not as a caveat.** Enabling a 90-day period on three years of bookings erases almost all of them on the first run, within minutes, irreversibly. There is no confirmation step because there is nowhere to put one — which makes the documentation the only thing standing between a site owner and that outcome.
- [ ] 6.3 Document the boundaries: every status, never a booking whose interval has not ended, and erasing sooner remains available on request.
- [ ] 6.4 Add to the erasure documentation that a booking's details may disappear with nobody having erased them, and point to the retention setting. Without it, the first such booking is a bug report.
- [ ] 6.5 Check the privacy-relevant docs do not now contradict each other — `docs/backoffice.md` describes erasure as an operator action.

## 7. Verification

- [ ] 7.1 Settings tests over an in-memory `IConfiguration`: absent → null; blank → null; `"abc"` → null; `"0"` → null; `"-5"` → null; `"90"` → 90. Assert the **error is logged** for the written-but-unreadable cases and **not** logged for absent.
- [ ] 7.2 Store tests for the due-read: a booking ended before the cutoff is returned; one ended after is not; one already erased is not; `take` is honoured; ordering is stable across calls.
- [ ] 7.3 **The paging test must span more than one batch.** Fixtures must exceed the batch size — with fewer, taking-the-head and skipping-by-offset behave identically and the test cannot fail. This is the single most important assertion in the change: get it wrong and a broken sweep erases exactly the first batch and reports success. Consider making the batch size injectable for tests *only* if it does not become a public knob.
- [ ] 7.4 **Mutation-check it**: change the loop to `Skip(processed).Take(batch)` and confirm 7.3 fails. If it passes, the fixture set is too small and the test is worthless — fix the fixtures, do not weaken the assertion.
- [ ] 7.5 **Mutation-check the anti-spin guard**: make the erase always fail and confirm the run terminates rather than looping. A test that only ever sees successful erasures cannot see this guard at all.
- [ ] 7.6 **Mutation-check the off switch**: set `RetentionDays` to null with due bookings present and confirm nothing is erased *and* no query is issued.
- [ ] 7.7 **Mutation-check status-blindness**: add a status filter to the due-read and confirm a test fails. Fixtures must include Requested, Confirmed, Cancelled and Declined, all past the cutoff. `find-by-booker` R1 shipped a guard that enforced one half of a two-part rule; a fixture set of one status is the same failure.
- [ ] 7.8 Future-dated bookings: a booking cancelled long ago whose interval has not ended is **not** erased. This is the decision Chris signed off; it needs a test or it is just prose.
- [ ] 7.9 Idempotence: run the sweep twice and assert no stored state changed the second time, **including every recorded erasure instant**. Advance the clock between runs, or the assertion holds for the wrong reason.
- [ ] 7.10 Interruption: cancel between batches, assert the erased ones stayed erased and the unreached ones are still due, then complete on a second run.
- [ ] 7.11 Guard that no endpoint runs the sweep. Extend `find-by-booker`'s action enumeration rather than writing a second one — and note **its** lesson: that guard was blinded once by only inspecting GETs.
- [ ] 7.12 Guard that the due-read's signature carries no contact detail in or out. **Mutation-check by widening its return type to `BookingListItem`** and confirming the guard fires — a guard on the method name would not.
- [ ] 7.13 Integration test on real SQL Server: the migration's applied set, and the sweep end-to-end over more than one batch.
- [ ] 7.14 **Every test written to close a review finding gets mutation-checked before it is believed, ONE ASSERTION AT A TIME.** `find-by-booker` R3: two documentation assertions were mutation-checked as a group by deleting the section; one was satisfied by prose elsewhere and could never have failed.

## 8. Modified requirements — the guarantee diff

A `## MODIFIED Requirements` entry replaces body *and* scenarios; anything not restated is deleted
with nothing in the diff resembling a deletion. Two requirements are being replaced.

- [ ] 8.1 `booker-erasure` → *Only a caller permitted to read contact details may erase them* (renamed). **Establish where the requirement ENDS before diffing** — reading to the end of a too-short window is how `booker-erasure` itself dropped four scenarios while asserting it had dropped none. Carried forward: the sensitive-data requirement, the "in addition to section access" clause, the gate-is-a-property-not-a-condition clause, the irreversibility rationale, and all three original scenarios verbatim. Added: the unattended carve-out with four obligations, the tripwire clause, three scenarios. **Dropped: nothing.** Verify that claim against the file rather than against this sentence.
- [ ] 8.2 `booker-erasure` → *What erasure does not reach is documented*. Carried forward: both boundaries, both original scenarios, and the trailing note about the superseded sentence. Added: the timer as a third explanation, one scenario. **Dropped: nothing.**
- [ ] 8.3 `persistence` → *Package composition registers persistence and Core services*. Carried forward: the full registration list, the time-zone default and its warning, both original scenarios. Added: the job's registration, the asymmetric resolution rule, the scoped-dependency clause, four scenarios. **Dropped: nothing.**
- [ ] 8.4 The new index is an **ADDED** requirement, deliberately not a modification of *Schema shape and naming*. That requirement is ~115 lines enumerating the whole schema; restating it wholesale to add one index risks dropping a guarantee for no gain, and nothing it says becomes false. Confirm that judgement still holds at apply time — if the index turns out to need a column change, it becomes a modification and the diff gets done properly.

## 9. Sweep — sibling specs this change falsifies

Looking outward at requirements not being touched. This has found something on five consecutive
changes.

- [ ] 9.1 `sensitive-data` — its purpose names retention as a surface that must obey it. Check the "no filter over contact details" scenarios still hold: the due-read takes a date and returns ids, so they should, but confirm by reading rather than by assuming.
- [ ] 9.2 `booker-erasure` — *Booker contact details SHALL have exactly one durable home* explicitly names "retention reporting" as a feature that would create a second. We are not building one; confirm the job's logging does not become one by accident.
- [ ] 9.3 `booking-management` — does anything there describe erasure as operator-initiated in a way this makes false?
- [ ] 9.4 `bookings` — does anything assert a booking's booker is always readable, or that stored state changes only through a request?
- [ ] 9.5 `packaging` — does the shipped-package description enumerate what runs at startup?
- [ ] 9.6 **The capability this change makes a difference to is the one most certain to be affected, not the one it modifies.** `find-by-booker` R1 found `booker-erasure` falsified because it removed that capability's documented boundary. Here the equivalent is `booker-erasure`'s *documentation* requirement — already caught in 8.2, so the sweep's job is to find the one that is **not** already on this list.

## 10. Release chore, not a gate

- [ ] 10.1 `Directory.Build.props` is still `0.1.0` while 0.3.0 has now broken the published API four times. Nothing is published and the first release is `17.0.0`, so this blocks nothing — recorded here only so it stops being rediscovered as an open item every change.
