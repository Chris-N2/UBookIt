## Context

See `proposal.md` — *Why*. Everything below rests on measurements taken before proposing, not on
recollection:

- **The version surface is five files**, found by grepping for the literal rather than by reading
  the runbook's table: `Directory.Build.props:41`, `README.md:23`, the readme's four image URLs,
  `docs/publishing.md` (five literals, not the four first counted) and a `CHANGELOG.md` entry that
  does not exist yet. **All of it is already guarded** — see D7.
- **The readme's twelve documentation links read `/blob/main/…`** — eleven documents plus
  `LICENSE`, which a hand-count missed and the guard did not — and `docs/` plus `CHANGELOG.md`
  are byte-identical between `main` and `dev/v18` today.
- **A version range works under Central Package Management.** Packed `UBookIt.Web` with
  `Version="[18.2.0,19.0.0)"` on one dependency and read the nuspec out of the `.nupkg`:
  `<dependency id="Umbraco.Cms.Web.Website" version="[18.2.0, 19.0.0)" />`, with the untouched
  sibling still `18.2.0`. **Seven** `Umbraco.Cms.*` entries exist — this design first said eight,
  from a `grep -c` that counted the comment line above them.
- **`ChangelogTests` derives released versions from the branch's own archive** via
  `release-(\d+)-(\d+)-(\d+)$`, so the two lines' changelogs stay self-consistent independently.

## Goals / Non-Goals

**Goals**

- `18.0.0` on nuget.org, installable on Umbraco 18 and **refused** on Umbraco 19.
- A packed readme that still tells the truth in a year, when the two lines have diverged.
- Every new guarantee guarded, because a readme is frozen per version and a link defect costs a
  version number.

**Non-Goals**

- Not changing how the 17 line is published. Its runbook is the same runbook.
- Not inventing a release process. The order in `docs/publishing.md` is load-bearing and was paid
  for; this follows it.

## Decisions

### D1 — The readme's documentation links pin to the release tag, not to `dev/v18`

A branch ref and a tag ref both fix the "wrong line" problem. Only a tag fixes the other half.

The readme is **frozen per published version** while the pages it links to are fetched live. A
link to `blob/dev/v18/docs/backoffice.md` would keep moving after `18.0.0` is published, so the
`18.0.0` package page would eventually describe behaviour `18.0.0` does not have — which is
exactly the failure the image pin already exists to prevent, stated in that requirement as *"an
address that tracks a moving branch leaves a published package page showing whatever the
repository holds today"*.

So documentation links take the same shape as images: **pinned to the release, and derived from
the declared version rather than written out a second time.** One mechanism, one place to get it
wrong, and a version bump cannot leave links pointing at the previous release while guards stay
green.

**Rejected: pin to `dev/v18`.** Fixes the branch, keeps the drift.
**Rejected: leave `main`.** Correct today by coincidence and wrong on a schedule.

### D2 — The upper bound is `[18.2.0,19.0.0)`, and the lower half is not a new decision

The lower bound is already what `Directory.Packages.props` declares; only the ceiling is added.
`19.0.0)` is exclusive, so every Umbraco 18 patch and minor resolves and Umbraco 19 does not.

**This is a restriction, and restrictions can be wrong in a way open bounds cannot.** If Umbraco
19 turns out to be compatible, a site that would have worked is refused. Accepted deliberately:
the 17 → 18 port needed real work in two composers and six test fixtures, so "the next major is
probably fine" is not a bet this project's own evidence supports. And the failure direction is
right — a resolver error naming the constraint beats a runtime failure inside somebody's site.

### D3 — `bookings-screen.png` is retaken and the other three are not

Not a blanket refresh. `run-on-umbraco-18` §7.4 compared all four against a running v18 backoffice
and found exactly one visibly wrong: `uui-button`s render pill-shaped on 18 and square in the
shipped image. The two front-end images are our own markup on the site's own styling, and
`availability.png` is fieldsets, time inputs and links with no button in it.

**The retake must happen before the tag**, because the image URLs resolve through the tag: the
`18.0.0` pin serves whatever `docs/images/` holds at the commit the tag names.

### D4 — Two ADDED requirements, no MODIFIED entry

Covered in the proposal. The short version: a `## MODIFIED` entry replaces a requirement wholesale
and silently deletes anything the new version forgets to restate, and this capability already
established the alternative — the image requirement was added *alongside*
`Documentation a consumer follows from the package page resolves`, deliberately leaving it intact
and accepting one overlapping scenario. Same shape here.

### D5 — The changelog entry ships undated, and the date is stamped after the feed confirms

Not a preference. `ChangelogTests.Every_released_version_is_dated` reads the **archive**, so an
entry becomes required-to-be-dated at archive time, not at publish time. Stamping early or
archiving early turns the suite red — verified twice, in both directions, during `17.1.0`.

The order is therefore: **push → tag → pack → publish → confirm per package on the
flat-container → stamp the date → commit → sync → archive.**

### D6 — `main` is untouched by this change

The 17 line's upper bound is real, urgent and **not this change**. A release is a unit of
publication on one line; mixing a `main` edit into a `dev/v18` release change is how the two
lines' histories stop being separable. It is recorded as an obligation instead.

## Risks / Trade-offs

- **The upper bound refuses a site that would have worked** → D2, accepted, with the reasoning
  stated rather than assumed.
- **A link or image pin that resolves only after the tag is pushed** → the tag precedes the pack
  in the runbook, and the post-publish human check on the rendered page is already required by
  the packaging capability. Both new pins ride that same step, and the runbook must say so.
- **The retaken screenshot shows dev-site residue** → the v18 database carries bookings from the
  live checks, including `K3RT-FR4D`. The shipped image needs a plausible, non-personal dataset;
  the existing images were curated for exactly this reason, and `17.1.1` set the precedent.
- **Two published lines, one repository, and a reader who lands on the wrong one** → partly
  what this change fixes, and partly permanent. `17.0.0`–`17.1.1`'s readmes are frozen with
  `main` links and cannot be corrected.
- ~~**A version literal nothing guards**~~ → **retracted at apply time; the premise was false.**
  See D7.

## Migration Plan

For a consumer: `18.x` installs against Umbraco 18 and carries the same schema, API surface and
behaviour as `17.1.1`. There is **no migration between the lines** — they are the same product
against different hosts, and a site moving from Umbraco 17 to 18 changes its uBookIt major with
it.

Rollback is the usual NuGet one: a published version cannot be withdrawn, only unlisted, and
`18.0.0` is spent whatever happens. That is why the readme pins are guarded rather than reviewed.

### D7 — `docs/publishing.md`'s literals are already guarded, and this change's own proposal said otherwise

**Written after apply began, because the claim was falsified by running the suite.** The proposal
and this design both stated that `docs/publishing.md`'s version literals are read by no guard —
inherited from a memory note saying the same thing — and framed the choice as "guard them or
record an obligation".

**Both were wrong.** `VersionTruthTests.Every_documented_version_is_the_declared_version` reads
every shipped document and fails when one states a version other than the declared one. Bumping
to `18.0.0` failed it immediately, naming `docs/publishing.md`. There is no dichotomy and no
obligation: the guard already exists, it fired at exactly the right moment, and the work was to
update five literals rather than four.

**Why it read as unguarded is the reusable part.** The literals went stale *at the previous
release* and were noticed only afterwards — which looks exactly like an absent guard and is
instead a guard that fires on the version bump, in a release where the bump and the noticing
happened in the wrong order. **"Nobody caught it last time" is evidence about the process, not
about the instrument.**

## Open Questions

- **The dataset for the retaken screenshot.** Whether the v18 dev database can be curated into the
  same shape the `17.1.1` image shows, or whether it needs setting up deliberately. Answerable at
  apply time, with the site running.
- ~~**Whether `docs/publishing.md`'s version literals get a guard or an obligation.**~~ Closed by
  D7: they already had one.
