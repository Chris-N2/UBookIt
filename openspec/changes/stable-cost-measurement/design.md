## Context

`AvailableDatesCostTests` was written in `available-dates`, under design decision D7. It times
`AvailabilityService.GetBookableStartsAsync` for a one-day range and for the 30-day window. Each
width gets one warm-up call and then one contiguous loop of 20 calls. The test asserts on
`mean(window) / mean(oneDay) < days × 4`. The measured values on 2026-09-24 were 0.024 ms against
0.312 ms, a ratio of 12.9×, so the test normally sits about 9× inside its limit.

The failure arithmetic: the window loop's timed span is about 6 ms. For the test to fail at 120×,
the window's mean has to reach about 2.9 ms, which means roughly 50 ms of stall somewhere in that
6 ms span. That is the size of a gen-2 GC pause while the other test classes are allocating in
parallel (xUnit runs classes in parallel by default, and this project sets nothing to stop it). It
is also the size of a few scheduler time slices lost on a loaded 4-core CI runner.

**Measured during apply** (throwaway harness, 16 cores, 8 allocation-churn load threads, 200 trials
per run; full record in tasks 1.3):

| Method | Failures | Median ratio | Worst ratio |
| --- | --- | --- | --- |
| Current: wall-clock, contiguous means, one loop after the other | 86/200 (twice) | 89–99× | 3,900–4,500× |
| Wall-clock, interleaved batches, minimum per call | 23–51/200 | 27–63× | 520–590× |
| **Thread CPU time**, same sampling | **0/200** | **24.7×** (25.4× with no load) | **41×** |

The first version of this design proposed the second row. Its reasoning was that load long enough
to spoil every window batch would also spoil the one-day batches in between. That is false when
pauses come more often than a batch lasts: no window batch gets through clean, while the shorter
gaps between pauses still let the other width through. See proposal.md for why this matters now.

## Goals / Non-Goals

**Goals:**
- Pauses and preemption, however frequent, cannot fail the test. The one condition that still can
  (memory exhaustion) identifies itself in the failure message. See Risks.
- The test still fails when the projection's cost is genuinely super-linear, at the same threshold.
- The test can never pass without having measured anything. Every way the measurement can be
  invalid ends in a failure that says so.
- All of this is **demonstrated**, on Windows and on Linux, not argued.

**Non-Goals:**
- Making the printed number a precise benchmark. It is still an indicator, and BenchmarkDotNet is
  still not warranted for one guard.
- Isolating the test from parallel execution. CPU time makes that unnecessary, and a test that only
  passes when run alone would say less about the real suite.
- Verifying macOS. No macOS machine is available. The macOS branch is written against the
  documented constant and recorded as unverified.

## Decisions

### D1. Time is the calling thread's CPU time, not wall-clock time

A thread that is paused by garbage collection, preempted by the scheduler, or waiting on another
process accumulates no CPU time. Every source of the flake is time during which the measuring
thread was not running, so measuring only the time it did run removes the flake at its source and
does not merely dilute it.

*Alternatives, each rejected on evidence:*
- **Wall-clock time with better sampling:** 23–51/200 failures under load (see Context).
- **`ProcessThread.TotalProcessorTime`:** on Windows it is backed by `GetThreadTimes`, which only
  updates in steps of about 15.6 ms. A whole window batch lasts about 1 ms.
- **`Process.TotalProcessorTime`:** covers the whole process, so it includes every other test class
  running in parallel, which is exactly the noise being removed.

### D2. One native source per platform; an unknown platform fails

- **Windows:** `QueryThreadCycleTime(GetCurrentThread(), out ulong)`, in CPU cycles.
- **Linux:** `clock_gettime(CLOCK_THREAD_CPUTIME_ID = 3, out timespec)`, in nanoseconds.
- **macOS:** `clock_gettime` with `CLOCK_THREAD_CPUTIME_ID = 16`, in nanoseconds.

The assertion is on a **ratio**, so it has no units, and cycles on one platform against
nanoseconds on another does not matter. Only a single platform's units are ever divided by each
other. The platform is chosen with `OperatingSystem.IsWindows()` / `IsLinux()` / `IsMacOS()`. Any
other platform **fails** with a message naming it. A skip is not an option either: the unit
project uses xUnit 2, which has no runtime skip, and CI rejects skips anyway.

The native calls stay in the test project, which ships nothing, so no production code or package
gains them.

### D3. A batch that switches thread is discarded, and too few valid batches fail

CPU time read on two different threads is meaningless, and the subtraction could underflow. Each
batch records `Environment.CurrentManagedThreadId` before and after. If the thread changed, the
batch is discarded. The harness saw **no** switches, but the xUnit 2 runner installs its own
synchronization context, so the test cannot assume the harness's behaviour holds.

If fewer than **10 of the 25** batches are valid for either width, the test **fails** with a
message saying the measurement could not be taken. That is a real signal: something made the call
asynchronous. The recorded line prints the valid batch count for each width, so a creeping
discard rate is visible before it becomes a failure.

### D4. Interleave, take the minimum, and make the two batches last about the same time

The ABBA interleaving and the per-call minimum are kept, but they now do a smaller job: absorbing
what CPU time still includes, mainly garbage collection that the measuring thread's own
allocations trigger and then run on that thread. **They absorb it under ordinary GC load, not at
memory exhaustion:** see the first risk below. Batches are sized to take about the same time,
because the wall-clock runs showed that longer batches are the likelier to be hit: **4 window
calls** (about 1.25 ms) against **40 one-day calls** (about 1 ms), over **25 rounds**.

### D5. Keep the guarantee exactly: `ratio < days × 4`, one warm-up per width

The threshold, the widths (one day against `AvailableDateWindow.Compute(today, 90, MaxQueryRangeDays)`),
the busy seven-day resource and the warm-up all stay as they are. Raising the number was ruled out,
because it weakens the only guard on that cost.

### D6. Prove it with a throwaway harness, on both platforms CI and development use

The harness lives in the session scratchpad and is **never committed**. It runs the old method and
the final method side by side, 200 trials, under 8 allocation-churn load threads.

- **Windows:** the old method fails a measurable share (met: 86/200). The final method fails
  **0/200**.
- **Linux, in a Docker .NET SDK container:** the same bar, **plus** a no-load sanity check that the
  CPU-time ratio matches the wall-clock ratio (about 25×). That check is what catches a wrong
  native signature or clock ID, which would produce plausible-looking numbers that mean nothing.
- **Mutation:** temporarily make the projection quadratic inside `AvailabilityService`. The new test
  must fail. Then revert, and confirm `git diff -- src/` is empty.
- **Guard proofs:** temporarily force every batch to count as a thread switch, and force the
  unknown-platform branch. Each must fail with its message. Then revert both.

*Alternative:* keep the harness in the suite. Rejected, because a permanent load-generating test
would itself be the kind of noise this change removes.

## Risks / Trade-offs

- **[Known limit, observed: at memory exhaustion the test can still fail.]** CPU time includes
  garbage collection that the measuring thread triggers and then runs itself. On Linux, with the
  heap at **2.4 GB in a 1.96 GB VM** (swapping, and killed by the kernel seconds later), every
  window batch triggered an expensive gen-1 collection. The cheapest window batch cost **758 µs**
  per call against a normal 65–96 µs, while the one-day cost stayed normal (4.0 µs), giving
  **187.5×**. It was not seen in 400 trials under ordinary heavy GC load (200 on Windows, 200 on
  Linux with a heap of about 400 MB). CI runners have 16 GB and the suite is far from that, and a
  suite at memory exhaustion is failing anyway. → **Accepted and made diagnosable, not
  engineered away:** the failure message carries the process's GC collection counts and heap
  size, so a failure like this identifies itself. Two alternatives were rejected. Discarding
  batches that saw a gen-1 or gen-2 collection only turns this failure into "could not measure",
  and GC counts are process-wide, so another thread's collection would discard a clean batch. A
  no-GC region per batch is ended by any other test's allocations and throws when it ends.
- [A wrong native signature or clock ID returns numbers that look plausible.] → The Linux no-load
  sanity check in D6 compares against the wall-clock ratio on an idle container.
- [The xUnit 2 synchronization context moves a batch to another thread.] → D3 discards such
  batches, fails the test if too few survive, and prints the counts.
- [The minimum under-reports cost that only shows up sometimes, such as a quadratic path that
  allocates enough to trigger GC in only some batches.] → The guard targets an order-of-magnitude
  algorithmic change, and that shows up in *every* window batch. The D6 mutation checks this
  directly.
- [CPU time includes cache and frequency effects from other cores' load.] → These are small next to
  a 120× limit: the median under load was 24.7× against 25.4× with no load.
- [macOS is unverified.] → Stated in the non-goals. A macOS contributor who sees the
  unknown-platform failure, or a strange ratio, has a named place to look.

## Migration Plan

1. Branch from `main` and implement. Run the suites per project, sequentially, with the client
   built first.
2. The maintainer pushes the branch and opens a PR into `main`. Check that PR's run for
   `continuous-integration` 10.3: the `pull_request` event fired, the concurrency group is
   `ci-refs/pull/N/merge`, and parity ran with `-Line main`. Merge only after QA approval.
3. Cherry-pick onto `dev/v18` in the same sitting and run the unit suite there. Its push run is
   checked too.

Rollback is a revert of the test change.
