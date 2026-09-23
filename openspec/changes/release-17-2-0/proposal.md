# Release `17.2.0`

## Why

Two features are merged to `main` and released to nobody. `17.1.2` is what a site installs today,
and it has neither site-wide closures nor the public holiday import.

**This is a minor, and the reason is the policy rather than the size of the work.** The major
tracks the *Umbraco* major, so it cannot signal anything about uBookIt's own compatibility; a
patch is for changes a site need not read about. Two capabilities and a new published interface
are neither.

**Nothing here is breaking.** `IPublicHolidaySource` is new, not changed: no member is added to a
published interface, no signature moves, and a site that upgrades and registers nothing gets the
same product it had plus a closure list. That is worth stating rather than assuming, because the
release entry has to tell a reader what upgrading asks of them, and for this release the honest
answer is "nothing".

## What this change is for

It exists to spend a version number correctly, once. The risk is not that the code is wrong — it
has been through two QA cycles and is archived — but that **a release is a set of edits nothing
derives and only some things guard**, and the parts nothing guards are exactly the parts that have
cost a version number before.

### The specific hazards, named

- **`17.0.0` shipped nine relative documentation links.** nuget.org resolves a relative link
  against the *package page*, so every one 404'd; a packed readme is frozen per version and cannot
  be corrected. `17.0.1` exists for no other reason. **The images are worse than the links**: a
  relative image never renders, and a remote one renders only from an allow-listed domain — which
  is why every screenshot URL is pinned to a release tag, and why **the tag must exist before the
  pack**, not after it.
- **A version number cannot be reused**, even after unlisting, so a mistake is permanent and the
  only remedy is another number.
- **Five documents state a version and none derives it.** Four are guarded — the two
  "uBookIt is at" sentences, the screenshot URLs and the changelog entry. **The fifth is not
  guarded at all**: every version literal in `docs/publishing.md`'s *Tag the release* section is
  illustrative text no test reads. It survived `17.1.2` untouched by luck, and it is recorded as
  an outstanding item rather than discovered again here.
- **Two sentences must NOT move**: the `## Status` anchor naming the first publish, and the
  sentence naming the version the public API was declared stable from. Both are pinned against
  archived changes, and "fixing" them turns the suite red — an earlier draft of the runbook's own
  checklist told the reader to edit one of them three lines above the paragraph forbidding it.
  **Never find-and-replace a version across this repository.**
- **The changelog entry is dated at publication, not before.** `ChangelogTests` reads the archive,
  so archiving this change before the date is stamped turns the suite red.
- **`dev/v18` is a separate release.** `18.1.0` carries the same two features and is its own
  change on its own branch; this one must not edit it, and neither may silently assume the other
  happened.

## What changes

Nothing in `src/`. This release is version metadata, a changelog entry, documentation that states
a version, and the act of publishing — plus whatever the pre-publish verification turns up.

## Impact

**Version metadata**

- `Directory.Build.props`: `<Version>` `17.1.2` → `17.2.0`. Every package and the backoffice
  manifest derive from it.

**Documents that state a version and derive nothing**

- `README.md`: the *uBookIt is at* sentence, and every screenshot URL re-pinned to the `17.2.0`
  tag.
- `docs/publishing.md`: the *uBookIt is at* sentence under *What nuget.org will not let you undo*;
  and, unguarded, every version literal in *Tag the release*.
- `CHANGELOG.md`: a new `17.2.0` entry, **undated until the packages are live**.

**What the entry must say**

- That upgrading asks nothing of a site.
- Both capabilities, in the terms a consumer meets them: a site-wide closure list every resource
  inherits unless it opts out, and an operator-triggered public holiday import.
- **`IPublicHolidaySource` by name**, as a new published interface a host may implement, saying
  what implementing it enables and that a site implementing nothing is unaffected. Nothing in
  `packaging` requires this today — see *Affected specs*, where that gap is closed as part of this
  release rather than worked around in it.
- That the package ships no holiday data for any country.

**Verification before the push, and against the feed rather than by reasoning**

- The packed readme's links and images resolve from the package page, on the tag being published.
- Each of the five packages appears on `api.nuget.org/v3-flatcontainer/<id>/index.json`. The
  website lags and a package presents as unlisted while validating; the flat container is the only
  answer that counts.

## Affected specs

**`packaging` is modified in one requirement**, and the reason is this release rather than this
release's convenience.

*A release names the contract changes a consumer must act on* is written about **members added to
existing interfaces**: its body reasons from obligation — an added member stops an implementing
site compiling, so the entry must say so — and all four of its scenarios describe that shape.
`17.2.0` publishes a **whole new interface**, which obliges a consumer to nothing: nothing stops
compiling, and a site that ignores it keeps the product it had. Under the requirement as written,
naming `IPublicHolidaySource` is optional, and the argument that would make it mandatory does not
apply.

It should be mandatory anyway, for the reason `packaging` already gives elsewhere about
notifications: **an extension point nobody is told about is not one.** A site that does not know
the seam exists will either go without the capability or reach past the contracts into the
database — the outcome every port in this package exists to prevent. So the requirement gains that
case explicitly, with its own reasoning rather than by stretching the obligation argument to cover
something it does not fit.

**This was found by QA on `public-holiday-import` and deliberately not fixed there**, because it
reaches every release rather than that change — and it is fixed here rather than deferred because
this is the first release it actually binds.

**No other capability is modified.** Recorded as considered rather than left silent. If the
pre-publish verification finds a documented claim this release falsifies, that becomes a further
modification and is proposed here rather than fixed quietly.

## Out of scope

- `18.1.0`, which is its own change on `dev/v18`.
- The three deferred cosmetic nits from `public-holiday-import`, and the other standing tidy items.
  A release is the wrong place to spend them: they would arrive untested in the one change whose
  mistakes cannot be taken back.
