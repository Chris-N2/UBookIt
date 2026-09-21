## Context

See `proposal.md` — *Why*. What matters for the approach is that this release differs from the
two before it in one way that changes the procedure: **the packed readme now contains images**,
addressed to a git tag named after the version. That adds a step that did not exist for `17.0.1`
or `17.1.0`, and it is a step no test can verify.

Two rules from previous releases are load-bearing here and are not re-derived:

- **The ordering.** Publish → confirm on the flat-container endpoint → stamp the changelog date →
  commit → *then* sync and archive. `ChangelogTests.Every_released_version_is_dated` reads the
  **archive**, so archiving before the date is stamped turns the suite red. Verified twice, in
  both directions, during `17.1.0`.
- **The pack traps.** `dotnet pack` is incremental and `--no-incremental` does not govern it, so
  stale artifacts survive the procedure and get pushed by the wildcard; and SourceLink embeds the
  commit SHA, so packing from a commit that is not on the public repository produces source links
  that 404 permanently.

## Goals / Non-Goals

**Goals**

- Get the corrected documentation in front of consumers, which it has never been.
- Close the broken-image window on the repository's landing page.
- Leave the runbook's new tag step **proven by use** rather than only written down — this is its
  first release.

**Non-Goals**

- Not a re-verification of the change being published. It is QA-approved, synced and archived.
- Not an improvement to the release procedure. If this release finds a gap in the runbook, the
  gap is recorded and fixed; the procedure is not redesigned mid-release.

## Decisions

### D1 — The tag goes up before the package, and it is the step most likely to be skipped

The runbook says so, but this is the first release where it matters, and the failure is silent:
a missing tag makes every screenshot on the package page a broken image, and nuget.org reports
that **only to the package owner**. Nobody outside the project would ever tell us.

**The tag is pushed and then verified by fetching one image URL over the network** before the
package goes. A `200` is the proof; the suite cannot provide one, because a tag lives on a remote
and rendering happens on nuget.org.

**Alternative rejected:** tag afterwards, since the readme is frozen either way. It leaves a
window in which the package page is live and broken, and the whole argument for pinning to a tag
was to avoid exactly that kind of uncorrectable state.

### D2 — The bump is made by reading the runbook's table, not from memory

Five literals across four files, one of which — the *Tag the release* worked examples — **is
checked by nothing**. The guarded four fail the suite if they lag, so they cannot be forgotten;
the fifth can. This project has now recorded three wrong counts about this very list, which is
why the table in `docs/publishing.md` is the authority and this document does not restate it.

### D3 — The changelog entry ships undated

The heading carries no date until the feed confirms indexing. An entry dated at authoring time is
a claim about an event that has not happened, and the release could still fail validation.
`ChangelogTests` enforces the dating only against the archive, which is what makes the ordering in
D4 work.

### D4 — Archive last, after the date is stamped

Publish, confirm, stamp, commit, sync, archive. Not because it is tidy, but because
`Every_released_version_is_dated` reads the archive: archiving an undated entry turns the suite
red, and stamping a date before the feed confirms would be a false claim.

### D5 — Verify the pack rather than trust it

Five `.nupkg` and four `.snupkg`, all `17.1.1`; repository commit equal to HEAD; icon and readme
declared **and present**; SourceLink SHA equal to HEAD; **zero relative links in the packed
readme**; and — new this release — **the four image URLs present and pinned to `17.1.1`**. The
last is the only item on that list that did not exist before.

## Risks / Trade-offs

- **The tag is forgotten, and the package page shows four broken images permanently** → D1's
  ordering plus the `curl` check before the push. The readme cannot be corrected afterwards, so
  this is the one that costs a version number. Highest-consequence risk in this release.
- **The unguarded *Tag the release* literals are missed** → they are illustrative, so a stale
  `17.1.1` there misleads the *next* releaser rather than breaking this release. Recorded as a
  known blind spot rather than treated as covered.
- **A stale `.nupkg` from an earlier pack is pushed by the wildcard** → `dotnet clean` before the
  pack, and D5's inspection of what was produced.
- **`17.1.0`'s package page keeps the false claims for ever** → true, and accepted: a packed
  readme is frozen. `17.1.1` becomes the default landing page, which is the whole remedy
  available.

## Migration Plan

None for consumers: no API, schema or configuration change. `17.1.1` is install-compatible with
`17.1.0` in both directions.

Rollback: a pushed version cannot be withdrawn, only unlisted — and unlisting is not deletion.
There is nothing here that would warrant it.

## Open Questions

None. The procedure is settled; what this release adds to it is one step, and D1 states it.
