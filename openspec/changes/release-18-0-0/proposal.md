## Why

`dev/v18` builds, runs and is QA-approved on Umbraco 18, and none of that reaches Chris's site —
or anybody else's — until the line is on nuget.org. This publishes `18.0.0`, the first release of
the `18.x` line.

**It is also the change that makes the two-line world real**, and two things that were fine while
one line existed stop being fine the moment a second one publishes:

1. **The packed readme's documentation links name a branch.** Twelve of them read
   `/blob/main/docs/…`, which on an `18.x` package sends a v18 reader to the v17 line's
   documentation. `docs/` is byte-identical between the branches **today**, which is exactly what
   makes this a trap rather than a visible bug: it is correct now and becomes wrong the moment
   `17.2` ships site-wide closures to `main` — by which time the `18.0.0` readme is frozen and
   uncorrectable.

   **This obligation is already written down, in the guard that cannot see it.**
   `VersionTruthTests.The_readme_links_resolve_from_anywhere` states its own blind spot: *"Only
   the path is resolved, never the git ref … The branch rename planned for the Umbraco 18 work
   owns that problem."* This is that work.

2. **Nothing in the package metadata says which Umbraco a package is for.** NuGet dependency
   versions are *minimum* bounds, so `Umbraco.Cms.Web.Website 18.2.0` reads as "18.2.0 or
   higher" — and the same was true of the 17 line, which is why the Umbraco Marketplace lists
   uBookIt as running on **v17 and v18**, and why that listing is false. With two lines published
   against two different majors, a resolver that cannot tell them apart will hand a site the
   wrong one.

## What Changes

**The version, across the places that carry it** — `Directory.Build.props`, the readme's own
version sentence, the readme's four image pins, `docs/publishing.md`'s five literals, and a new
`CHANGELOG.md` entry. **Every one of those is already guarded to follow the declared version**,
so bumping it is what surfaces them; the suite going red on the bump is the guards working, not
a list somebody has to remember.

**The readme's documentation links pin to the release, not to a branch.** Same reasoning the
image pin already carries and the same mechanism: a readme frozen per version should show the
documentation *that version shipped*, and the ref should be derived from the declared version
rather than written out a second time.

**An upper bound on the host**, `[18.2.0,19.0.0)`, across the eight `Umbraco.Cms.*` dependencies.
Verified expressible rather than assumed: a range set on a `PackageVersion` under Central Package
Management flows verbatim into the packed nuspec as `<dependency version="[18.2.0, 19.0.0)" />`,
while an untouched sibling stays an open `18.2.0`.

**One screenshot retaken.** `bookings-screen.png` shows `uui-button`s with v17's square corners;
Umbraco 18 renders them fully pill-shaped. The other three shipped images are unaffected — the
two front-end ones are our own markup on the site's styling, and `availability.png` contains no
buttons at all. Established by looking, in `run-on-umbraco-18` §7.4.

## Non-goals

- **Not the 17 line's upper bound.** It cannot be retrofitted to published `17.0.0`–`17.1.1`, and
  adding it to `main` changes the package, so it needs its own release there. Named here because
  this change is what makes it urgent, and left out because a release is a unit of publication
  on one line.
- **Not 17.2's features.** Site-wide closures and bank holidays land on `main` first, per
  invariant 1.
- **Not a re-verification of the port.** `run-on-umbraco-18` is archived and QA-approved. This
  change publishes it; it does not re-litigate it.
- **Not the block library.** Still the first genuinely v18-only capability, still its own change.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

**`packaging` — two ADDED requirements, and `skip_specs` is deliberately NOT used.**

`release-17-1-1` shipped as `skip_specs: true` because it added no guarantee, and QA attacked
that rather than accepting it. The same attack here reaches the opposite answer: this release
adds two guarantees that did not exist, and both are about what a **published package** promises
rather than about how it was built.

1. **A documentation link in the packed readme names the release it shipped with.** The existing
   requirement `Documentation a consumer follows from the package page resolves` guards link
   *shape* and *local target existence*, and its guard's own remarks state that the git ref is
   invisible to it. A second published line is what turns that blind spot into a defect.
2. **The package declares which Umbraco majors it accepts.** No requirement in `packaging`
   currently says anything about host-version bounds — the closest, *The package SHALL declare no
   dependency it does not ship*, is about uBookIt's own packages.

**Both are ADDED rather than MODIFIED, following this capability's own precedent.** When the
image requirement was introduced it deliberately left
`Documentation a consumer follows from the package page resolves` intact and said so, accepting
one overlapping scenario rather than replacing a requirement wholesale. A `## MODIFIED` entry
replaces its requirement body and every scenario, and anything the new version forgets is deleted
with nothing in the diff resembling a deletion. There is no reason to take that risk here.

## Impact

| | |
|---|---|
| `Directory.Build.props` | `17.1.1` → `18.0.0` |
| `Directory.Packages.props` | Upper bound on eight `Umbraco.Cms.*` dependencies |
| `README.md` | Version sentence, four image pins, twelve documentation links |
| `CHANGELOG.md` | A new `18.0.0` entry — **undated until the feed confirms**, per the release order |
| `docs/images/bookings-screen.png` | Retaken on Umbraco 18 |
| `docs/publishing.md` | Five stale `17.1.1` literals, and what the two new guarantees ask of a publish |
| `openspec/specs/packaging` | Two ADDED requirements |
| Tests | Guards for both new requirements |

**The five published packages gain a real upper bound**, which is a restriction on what will
restore. That is the point, and it is worth stating plainly: a site on Umbraco 19 will be told
`18.x` does not apply, rather than being given it and failing later.
