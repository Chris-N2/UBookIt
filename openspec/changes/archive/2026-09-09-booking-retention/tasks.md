## 1. The setting

- [x] 1.1 Add `RetentionDays` to `SiteBookingSettings` as `int?`. **Nullable, not `0`-as-off** — change ④ must distinguish "we erase after N days" from "we do not erase", and must not be able to publish the second as the first.
- [x] 1.2 Add `RetentionDaysSettingKey = "UBookIt:RetentionDays"` beside the existing key constants in `UBookItPersistenceComposer`.
- [x] 1.3 Resolve it in `ResolveSettings`. **Do NOT copy the `ResolveMaxQueryRangeDays` shape** — that one falls back to a working default and this one must not. Absent, blank, non-numeric, zero and negative all resolve to `null`.
- [x] 1.4 Log an **error** when a value was written and could not be read; log **nothing** when it is absent. The two must be distinguishable, because absence is a choice and a mistyped value is a fault that change ④ will otherwise publish as a lie. Note the existing time-zone precedent logs a *warning* — this is deliberately louder.
- [x] 1.5 Resolving the setting must not need the composer. Keep it an `internal static` on the composer as the others are, so a unit test can call it with an in-memory `IConfiguration`.

## 2. The due-read

- [x] 2.1 Add to `IBookingStore`: `Task<IReadOnlyList<Guid>> GetBookingIdsDueForErasureAsync(DateTimeOffset cutoffUtc, int take, CancellationToken)`. **BREAKING — published port**, the third such change in 0.3.0; declare it in the proposal as `booker-erasure` and `find-by-booker` each did for theirs.
- [x] 2.2 Document on the member *why* it returns ids and not rows: the unattended path must handle no contact detail, which is what lets `booker-erasure`'s access requirement survive an erasure with no caller. This is a security property, not a performance one — say so, or somebody will later "improve" it into returning `BookingListItem`.
- [x] 2.3 Do **not** add it to `IBookingManagementStore`. It is not a management read; it answers no user's question and returns nothing a screen could show.
- [x] 2.4 Implement in `SqlBookingStore`: `EndUtc < cutoffUtc && BookerErasedUtc == null`, ordered deterministically, `Take(take)`, `AsNoTracking()`, projecting to `b.Id`. **No status filter** — status-blind is a requirement, not an omission.
- [x] 2.5 Order by `EndUtc` then `Id`. Not for the caller's benefit — an unordered `TOP (n)` is free to return the same rows or different rows on each call, and the sweep's termination argument assumes progress.

## 3. Persistence

- [x] 3.1 Add the filtered index in `UBookItDbContext`: `HasIndex(b => b.EndUtc).HasFilter("[BookerErasedUtc] IS NULL")`.
- [x] 3.2 One additive migration. Check the generated SQL is `CREATE INDEX … WHERE` only — no column change, no data statement.
- [x] 3.3 Regenerate the model snapshot; confirm the diff is the index and nothing else.
- [x] 3.4 **Re-check the availability path.** DONE, and it needed the argument rather than the shortcut. The full integration suite (116) passes with the index present, claims-read and multi-claim concurrency included. The reasoning, recorded in `MultiClaimConcurrencyTests`: the claims read filters `StartUtc < to AND from < EndUtc`, which the existing `(StartUtc, EndUtc)` index still seeks on its leading column; the new index is filtered to un-erased rows and so cannot serve that query better, or at all, for erased ones. Original wording: A new index changes the optimiser's options and `persistence` has a standing QA gate that the date-range lookup must not table-scan. This index is on `EndUtc`, which availability predicates *do* reference — so unlike `find-by-booker`'s `BookerEmail` index, "it touches a column nothing else queries" is **not** available as an argument here. Run the claims-read suite and say what was observed.

## 4. The job

- [x] 4.1 Add `BookerRetentionJob : IDistributedBackgroundJob` in `UBookIt.Persistence`. **Not `IRecurringBackgroundJob`** — see design D1; every cleanup job Umbraco itself ships uses the distributed one, and it carries the lease that makes a load-balanced site run it once.
- [x] 4.2 `Name` is the durable lease key in Umbraco's own table. Fix it as the constant `"UBookItBookerRetention"`. Never derive it from a type name, an assembly name or a version — renaming it orphans a row and silently restarts the schedule.
- [x] 4.3 Verify `IDistributedBackgroundJob` and `ExecuteAsync(CancellationToken)` against the **pinned 17.6.2** assembly, not the 17.8 source in `ref/`. Already checked once during explore; re-check after any package bump, because the package floats at `>= 17.6.2`.
- [x] 4.4 Return immediately when `RetentionDays` is null. No query, no scope, no log line per run — an off feature must cost nothing observable.
- [x] 4.5 Compute the cutoff from the injected `TimeProvider`, never `DateTimeOffset.UtcNow`. The whole feature is a clock comparison; a test that cannot move the clock cannot test it.
- [x] 4.6 Inject `IServiceScopeFactory`. The job is a **singleton** resolved from the root container and `IBookingService` is **scoped** — capturing it would hold one `DbContext` for the life of the application. One scope per batch.
- [x] 4.7 The loop: take a batch from the **head** of the due set, erase each through `IBookingService.EraseBookerAsync`, repeat until a batch comes back empty. **Never `Skip`.** See 7.3 — this is the defect the design most exists to prevent.
- [x] 4.8 Anti-spin: **a batch in which no booking was successfully erased ends the run**, with an error logged. A booking that fails to erase still matches the due predicate and would otherwise be selected for ever.
- [x] 4.9 Catch per booking, log, continue. `IBookingService.EraseBookerAsync` throws `InvalidOperationException` on an erased-but-unreadable row; letting that abort the run lets one bad row block retention for everything behind it, permanently.
- [x] 4.10 Honour the `CancellationToken` between batches. An interrupted sweep is safe because erasure absorbs — that is the argument, and it is only true if we stop *between* units rather than mid-write.
- [x] 4.11 Batch size 100, as a named constant. Not configurable — a knob nobody can reason about is surface, not flexibility.
- [x] 4.12 **Log counts and booking ids, never a person.** `booker-erasure` names retention reporting as exactly the feature that would give contact details a second durable home. Grep the finished job for any interpolation of a booker property.

## 5. Composition

- [x] 5.1 Register with `builder.Services.AddSingleton<IDistributedBackgroundJob, BookerRetentionJob>()`. Note there is no `AddDistributedBackgroundJob` extension — Umbraco registers its own this way too.
- [x] 5.2 Register **unconditionally**, whether or not retention is configured. Conditional registration would make the setting's effect depend on startup configuration in a second, invisible way.

## 6. Documentation

- [x] 6.1 Document the setting: name, unit, that it is off unless configured, that the period runs from the booking's **end**, and that it is read at startup so a change needs a restart.
- [x] 6.2 **Document the one-way door in its own right, not as a caveat.** Enabling a 90-day period on three years of bookings erases almost all of them on the first run, within minutes, irreversibly. There is no confirmation step because there is nowhere to put one — which makes the documentation the only thing standing between a site owner and that outcome.
- [x] 6.3 Document the boundaries: every status, never a booking whose interval has not ended, and erasing sooner remains available on request.
- [x] 6.4 Add to the erasure documentation that a booking's details may disappear with nobody having erased them, and point to the retention setting. Without it, the first such booking is a bug report.
- [x] 6.5 Check the privacy-relevant docs do not now contradict each other — `docs/backoffice.md` describes erasure as an operator action.

## 7. Verification

- [x] 7.1 DONE. Six unreadable forms, plus present-and-readable, plus absent. **One correction from writing them:** blank now counts as *written* rather than absent, unlike the time-zone setting — an env var that resolved to nothing on a site that meant 90 is exactly the case the error exists for, and answering that ambiguity with silence is the wrong direction for this setting.
- [x] 7.2 DONE, in the integration suite against real SQL Server — before/after the cutoff, already-erased excluded, `take` honoured, ordering stable across calls, plus a test that the cutoff compares against the END and not the start (a straddling booking is not due).
- [x] 7.3 DONE. 250 fixtures — two full batches and a partial one. Batch size deliberately NOT made injectable: the constant is `internal` and the test reads it, so the fixture count tracks it automatically without becoming a public knob.
- [x] 7.4 DONE, **and the second half mattered more than the first.** Paging by offset fails the test, and only that test. Then the fixture count was cut to exactly one batch and the same broken sweep PASSED — so the 250 is not padding, it is the test. Recorded because a later tidy-up that trims the fixtures would silently disarm this.
- [x] 7.5 DONE, decisively: with the guard removed the run never returns — the test run had to be killed after ten minutes rather than failing. With it, the run ends and logs an error.
- [x] 7.6 DONE. Asserts `DueForErasureReads == 0`, not merely that nothing was erased — the second is satisfied by a sweep that queried and discarded the answer. Mutating the early return away fails it.
- [x] 7.7 DONE in **both** suites, and the split is the point. Adding a status filter to the in-memory double fails the unit theory (3 of 4 cases; `Confirmed` passes, which is why one status would see nothing). Adding it to `SqlBookingStore` leaves **all 21 unit tests green** and fails the integration theory. The unit tests establish the sweep is status-blind given a status-blind store; only the integration suite establishes the store is one.
- [x] 7.8 DONE — a booking cancelled a year ago whose slot is 180 days in the future is not erased.
- [x] 7.9 DONE, with the clock advanced 30 days between runs. Sharing one clock would have passed against an implementation that re-erases on every sweep.
- [x] 7.10 DONE — cancelled before the first batch, nothing erased, then a second run completes the work.
- [x] 7.11 DONE. `SensitiveDataRedactionTests` already records every action with its classification, so any new endpoint fails it. Added the structural half here: `BookerRetentionJob` is asserted **internal**, so no controller in another assembly can name it, and `IBookingService` is asserted to expose no erase taking a collection.
- [x] 7.12 DONE, and mutation-checked by widening the return type. Asserted over the parameter and return TYPES, not the method name.
- [x] 7.13 DONE — 12 integration tests, including the filtered index read from `sys.indexes` (the filter itself asserted, not just the index's presence) and the emitted SQL.
- [x] 7.14 DONE, **and it caught one of my own.** `The_due_query_emits_…` originally rebuilt the query inline and asserted over `ToQueryString()` of the copy — it passed against a production store carrying a `WHERE Status = 1`. Rewritten to run the real store through `CommandRecordingInterceptor` and assert on what SQL Server was actually sent; it now fails that mutant. A guard that watches a reconstruction of the thing it guards watches nothing.

## 8. Modified requirements — the guarantee diff

A `## MODIFIED Requirements` entry replaces body *and* scenarios; anything not restated is deleted
with nothing in the diff resembling a deletion. Two requirements are being replaced.

- [x] 8.1 `booker-erasure` → *Only a caller permitted to read contact details may erase them*. **Establish where the requirement ENDS before diffing** — reading to the end of a too-short window is how `booker-erasure` itself dropped four scenarios while asserting it had dropped none. Carried forward: the sensitive-data requirement, the "in addition to section access" clause, the gate-is-a-property-not-a-condition clause, the irreversibility rationale, and all three original scenarios verbatim. Added: one paragraph scoping it to erasure somebody asks for and pointing at the new requirement below. **Dropped: nothing.** Verify that claim against the file rather than against this sentence.
- [x] 8.1a **The unattended obligations are an ADDED requirement, not a rename of 8.1 — and `ChangeDeltaIntegrityTests` is why.** The first attempt renamed 8.1 to *Erasure is gated on the access to read what it destroys* and modified it under the new name. `openspec validate --strict` accepted it; the repo's own delta guard did not, and was right: a `MODIFIED` naming a requirement that does not exist in the main spec **syncs as a new requirement beside the one it meant to replace**, so the original caller-only wording would have survived alongside its own replacement — in a security requirement, silently. Two requirements is also the truer shape: the caller rule was never wrong, it was only ever about callers, and what was missing was a rule about non-callers.
- [x] 8.2 `booker-erasure` → *What erasure does not reach is documented*. Carried forward: both boundaries and both original scenarios. Added: the timer as a third explanation, one scenario. **Dropped:** the trailing italic note about the superseded "does not claim to find them all" sentence — it was `find-by-booker`'s record of a correction it had already made, not a guarantee, and it is preserved in that change's archive. Recorded here rather than left to be noticed.
- [x] 8.2a `bookings` → *Availability and placement service ports*. **Added in QA round 1; the change had no `bookings` delta at all and should have.** `booker-erasure` recorded ITS `IBookingStore` addition inside this requirement, and this is where a reader derives what that port is — so the due-read belongs here on the same terms. Carried forward: the whole body (both services, the dependency rules, the generation-port paragraph, the services/read-port/batched-claims/pure-projection paragraphs, the only-one-member clause), all eight scenarios, the existing **BREAKING** note for the erase operation, and both trailing italic notes. Added: the breaking-change note for the due-read, the identifiers-only restriction binding a substituted implementation, the no-status-filter clause, one italic note. **Dropped: nothing.**
- [x] 8.3 `persistence` → *Package composition registers persistence and Core services*. Carried forward: the full registration list, the time-zone default and its warning, both original scenarios. Added: the job's registration, the asymmetric resolution rule, the scoped-dependency clause, four scenarios. **Dropped: nothing.**
- [x] 8.4 The new index is an **ADDED** requirement, deliberately not a modification of *Schema shape and naming*. That requirement is ~115 lines enumerating the whole schema; restating it wholesale to add one index risks dropping a guarantee for no gain, and nothing it says becomes false. Confirm that judgement still holds at apply time — if the index turns out to need a column change, it becomes a modification and the diff gets done properly.

## 9. Sweep — sibling specs this change falsifies

Looking outward at requirements not being touched. This has found something on five consecutive
changes.

- [x] 9.1 CHECKED — holds. The due-read takes a cutoff instant and a batch size and returns ids, so no endpoint gains a contact-detail parameter and the free-text tripwire is untouched. The capability's *purpose* names retention as a surface that must obey the rule; it does, vacuously, by showing nobody anything.
- [x] 9.2 CHECKED — holds, and is now tested rather than intended. `Nothing_the_sweep_logs_carries_a_person` captures the booker's values before the sweep (they are gone afterwards) and asserts over everything the job emitted.
- [x] 9.3 CHECKED — holds, for a reason worth recording: *The port stays read-only… erasure included* is about `IBookingManagementStore`, and the due-read went on `IBookingStore`. That is a second, independent justification for task 2.3, arrived at from the other direction. The capability's four verbs are its **endpoint** verbs; retention adds no endpoint.
- [x] 9.4 CHECKED — holds. *The erased state is reachable only by erasing an existing booking… and by rehydrating one already erased* stays true: retention erases existing bookings through that same operation. The booking service gains no verb, and `IBookingStore` gaining a read does not disturb the dependency rules.
- [x] 9.5 CHECKED — `packaging` enumerates no startup or background behaviour at all, so there is nothing to falsify.
- [x] 9.6 **The sweep found nothing new, which ends a five-change streak — so here is what was actually searched, since "nothing found" is only a result if the search was real.** Every capability was grepped for erasure, retention, background/startup work, and claims about what changes stored state. Two near-misses were examined and rejected on their merits, both above: `booking-management`'s read-only port (wrong port) and `sensitive-data`'s anticipation of retention (satisfied, not falsified). The plausible explanation is structural: this change adds **no wire member, no endpoint and no model change** — the three things that falsified siblings on the previous five. The `sensitive-data` membership-snapshot guard, which fires the moment `BookingModel` or `BookerModel` gains a member, stayed green for that reason and is the evidence rather than my say-so.

## 10. Release chore, not a gate

- [x] 10.1 `Directory.Build.props` is still `0.1.0` while 0.3.0 has now broken the published API four times. Nothing is published and the first release is `17.0.0`, so this blocks nothing — recorded here only so it stops being rediscovered as an open item every change.
