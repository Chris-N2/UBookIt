## Context

`release-17-2-2` (archived on `main`) designed, reviewed and shipped everything this change carries.
Its decisions D1–D7 are not re-argued here. This design covers only how they reach `dev/v18`, and
what differs on that line. `dev/v18` is never merged with `main`: changes move across by patch or
cherry-pick, and each line carries only its own release archives.

## Goals / Non-Goals

**Goals:** `dev/v18` at `18.1.2` carries exactly `17.2.2`'s changes, with only the line-specific
differences, and publishes through `publish.yml`.

**Non-Goals:** new behaviour, or rewording beyond the line's facts.

## Decisions

### D1. Code, tests and `docs/configuration.md` move as `main`'s diff, applied, not rewritten

`git diff d6f0199 56f1837 -- src tests docs/configuration.md` is applied with `git apply --3way`.
Every file in that set is byte-identical between `d6f0199` (`main`'s base) and `14cf33f`
(`dev/v18`'s base), as checked at propose time. So the patch must apply with **no** conflicts. A
conflict would mean the lines have drifted: stop, and diff by hand. After it applies, `git diff
56f1837 -- <those paths>` on the v18 branch must be **empty**. The files end byte-identical to
`main`'s, which is the check that nothing was lost or doubled.

*Alternative rejected:* cherry-picking `fb9ce74`/`ee4c184`/`56f1837`. They also carry
`openspec/changes/release-17-2-2/`, `main`'s README, runbook, props and changelog. Each of those
would conflict or bring in `main`-only content that then has to be taken out again.

### D2. The README is `main`'s final README with four substitutions, verified by diff

`main`'s README at `56f1837` is taken, and then exactly these change:
1. `Bookings for Umbraco 17:` → `Bookings for Umbraco 18:`;
2. `and Umbraco 17.6.2 or later.` → `and Umbraco 18.2.0 or later.`;
3. the Requirements row → `dev/v18`'s current row, **word for word**;
4. every `17.2.2` → `18.1.2`.

Nothing else in the README names a line. The versioning section's "uBookIt `17.x` is for Umbraco
17" is an example, and it is identical on `dev/v18`'s current README.

**Verification:** `diff` the result against `main`'s README. Every differing line must be one of
the four. This is the same check `release-17-2-2` task 8.4 prescribed.

### D3. The runbook gets `main`'s 17.2.2 hunks, not `main`'s file

`dev/v18`'s runbook deliberately differs from `main`'s in a few paragraphs (for example, the tag
section's wording about two published lines). So `main`'s runbook diff
`d6f0199..56f1837 -- docs/publishing.md` is applied hunk by hunk with `--3way`. Its version
literals are then corrected to `18.1.2`: the *is at* sentence and the *Tag the release* example
URL and commands. The history sentences ("up to `17.2.1`/`18.1.1`") stay. The post-publication
Status bullet (`main`'s `39610f0`) is **not** ported as is. On this line, after `18.1.2` publishes,
both lines have released through the workflow, and it is rewritten to say so (task 8.2).

### D4. The changelog: copy `17.2.2`'s entry, write `18.1.2`'s

As `release-18-1-1` did for `17.2.1`, `main`'s dated `## 17.2.2 — 2026-09-26` entry is copied
**unedited** into the 17.x block, above `17.2.1`. The `18.1.2` entry leads with "The same changes
`17.2.2` made on the Umbraco 17 line", and then states:
- *What you have to do*: nothing;
- the limit, per address for the recipient list;
- the README rewrite. On this line, the readme is also the Marketplace listing.

The header's anchor moves to `#versions-and-the-api-promise`, as it did on `main`.

### D5. Release, and its fallback, exactly as 17.2.2's D4/D5

This covers:
- PR into `dev/v18` with a merge commit;
- the merge commit's own `ci` run;
- the tag `18.1.2`;
- before approving: the artifact downloaded from the run page into a new, empty folder, all five
  nuspecs read (`Umbraco.Cms.*` at **`[18.2.0, 19.0.0)`**, siblings at `18.1.2`, the description
  naming **Umbraco 18**), and README byte-identity with the tag and every address returning 200;
- then approval, the summary table pasted by Chris, and the feed polled per package.

The fallback is `release-17-2-2` D5's commands with `17.2.2` → `18.1.2`. The artifact expires 7 days
after the pack.

## Risks / Trade-offs

- **[`main` and `dev/v18` READMEs drift in a way the diff can't flag]** → D2's diff is the whole
  check, and the allowed set is four named kinds of line.
- **[The Marketplace doesn't rescan promptly]** → Out of our hands. Record when it shows the new
  readme; don't gate the archive on it.
- **[The 409 wording is observed and differs from `already exists at feed`]** → The step fails
  closed. Record the exact text. The classifier change is its own change, on both lines.
- **[A fix reaches one line only]** → This change is that risk being paid down. Its close-out
  records that both lines now carry `17.2.2`/`18.1.2`.
