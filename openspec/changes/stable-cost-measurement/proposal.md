## Why

`AvailableDatesCostTests` fails at random. It has failed twice on clean full-suite runs: **195.5×** on
Windows during `release-17-0-1` QA, and **136×** on Linux CI run `35983001536`. Its limit is 120×, and
it normally measures about 13×. `docs/publishing.md` makes a green suite from a clean build a pre-push
gate, and CI now enforces that gate unattended on both lines. A gate that fails at random trains
people to re-run until it goes green, and that is how a real failure gets waved through. The next
patch releases (`17.2.1` / `18.1.1`) are meant to be the first ones verified by CI, so this needs
fixing first.

**The cause is the measurement, not the machine.** Each measurement is the **mean** of one
**contiguous** loop, and the two loops run **one after the other**. The timed spans are tiny: about
0.5 ms for the twenty one-day reads and about 6 ms for the twenty window reads (measured
2026-09-24: 0.024 ms against 0.312 ms, ratio 12.9×). A single stall of around 50 ms during the window
loop is enough to fail the test; a GC pause or the scheduler taking the CPU away for a moment
would do it. A mean passes that stall straight through into the result. Running the two loops one
after the other means a stall hits one measurement and not the other, so the ratio does not cancel
load out. A previous note claimed it did, and that claim was disproved on 2026-09-24.

**Better sampling alone does not fix it; wall-clock time is the deeper problem** (measured during
apply, recorded in tasks 1.3). Under induced load the current method failed 86/200 trials.
Interleaved wall-clock sampling that takes the minimum still failed 23–51/200. The same sampling,
timed in the thread's own CPU time, failed **0/200**. A stall is time during which the thread is
not running, and only CPU time leaves it out.

## What Changes

- The test measures **the calling thread's CPU time**, not wall-clock time, over **interleaved
  batches of equal duration**, and takes the **minimum** per call for each width. Pauses and
  preemption add no CPU time to the thread. The minimum absorbs what is left, such as garbage
  collection run on the measuring thread itself.
- The CPU time comes from one native call per platform: `QueryThreadCycleTime` on Windows, and
  `clock_gettime(CLOCK_THREAD_CPUTIME_ID)` on Linux and macOS. The native calls live in the test
  project only. Any other platform **fails with a message naming it**; it never passes without
  measuring.
- A batch that **switches thread** is discarded, because CPU time across two threads means nothing.
  If too few batches are valid, the test **fails with an explicit "could not measure" message**,
  never a quiet pass.
- **The guarantee is unchanged:** reading *n* days must cost less than `n × 4` times reading one
  day. The same shape and the same threshold. The number is **not** raised.
- The recorded line (`[available-dates] …`) is kept, so the cost is still written where a person
  will see it.
- The fix is proved both ways before it is accepted:
  1. **Reproduce the flake.** The current test fails under induced load at a measurable rate, and
     the new one does not under the same load. This is proved on Windows, and on Linux in a Docker
     container, since CI runs on Linux. macOS is not verified, and the record says so.
  2. **Show the guard can still fire.** A temporarily injected super-linear cost in the projection
     fails the new test. The injection is reverted afterwards.
- The change lands on `main` **by pull request**, not fast-forward. That PR's run is the "first real
  PR" that `continuous-integration` task 10.3 is still waiting for, and the run gets checked against
  that task. The change is then cherry-picked to `dev/v18` in the same sitting.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. **`skip_specs: true`.** The test guards a design decision from `available-dates` (D7:
measure the cost, do not cache it). It does not guard a spec requirement, and no product behaviour
changes. This change deliberately does **not** promote "the window read is linear" into a spec
requirement. That is a separate decision, and only worth making if someone wants the cost
guaranteed and not just watched.

## Non-goals

- Raising the threshold, adding a retry attribute, or moving the test out of CI. Each of these
  weakens or hides the only guard on that cost.
- Counting operations deterministically inside `AvailabilityService`. There is no hook for it
  without instrumenting production code. The store round-trip count is already asserted elsewhere
  (`AvailableDatesTests`).
- Fixing the other known suite fragility: `PackageCompositionTests` shells out to `dotnet pack`.
  That one has a different cause and is a separate obligation.
- Caching the window read.

## Impact

- `tests/UBookIt.Tests/AvailableDatesCostTests.cs`, on both lines. It gains the per-platform
  thread-CPU-time calls, possibly in a small helper under `tests/UBookIt.Tests/Support/`. No
  production code, public API, schema, package or docs change.
- No version bump. The change ships in the next patches (`17.2.1` / `18.1.1`) with whatever else
  they carry.
- CI: the PR run closes `continuous-integration` 10.3. The result is recorded in this change's
  tasks and not in the archived change, which is never modified.
