# The 17.0.0 release

## Why

The roadmap's last row. Every feature row is shipped and the pre-release defect list is
clear (`release-readiness-fixes`, merged 2026-09-14), so what remains is making the
package *say* what it now is: the version becomes `17.0.0`, the documents that still
describe a `0.1.0` pre-release stop doing so, and the compatibility promise — which
CLAUDE.md says starts **at publication** — gets a written policy before publication
rather than after.

Two claims in the repository are falsified the moment the number changes, and neither is
guarded today: `README.md` says uBookIt is at `0.1.0` and tells a reader to treat
contracts as settled "from `1.0.0`" — a version that will never exist under the
Umbraco-major scheme — and `docs/mvp.md` says "the first version is `0.1.0`". The ㉙
lesson (a README carrying three falsified claims because no guard read it) applies
directly: the fix is not only to correct them but to tie them to the number they
describe.

## What Changes

- **The version becomes `17.0.0`** — one property in `Directory.Build.props`. The
  backoffice manifest version is stamped from it at build time (with a build-time guard
  already in place), so the editor-facing version follows without a second edit.
- **`README.md`'s version note is rewritten** for what 17.0.0 means: the major tracks the
  Umbraco major, so it does not signal a breaking change, and leaving `0.x` is the
  stability promise rather than reaching `1.0`.
- **The breaking-change policy is stated publicly for the first time** (settled by Chris,
  2026-09-14, superseding the earlier "adopt Umbraco's schedule" decision): the public
  interface stays as consistent as possible; where a breaking change is genuinely
  required it lands in a **minor** version (`17.x.0`) and never a patch, with sensible
  defaults or an upgrade path so an existing site keeps working; parallel API versions are
  deliberately not used while the product is young. **This deviates from strict SemVer and
  the documentation says so plainly** — with the major pinned to the Umbraco major, "a
  minor may break, a patch never does" is the contract, and a reader who assumes SemVer
  without being told would be misled.
- **`docs/mvp.md`'s version sentence** is corrected, and its own instruction ("a living
  record until v1 ships, and then it becomes the README's account of what the package is
  for") is honoured or explicitly declined — the document cannot both ship and still call
  itself pending.
- **A guard ties the documented version to `Directory.Build.props`**, so a future bump
  cannot leave the prose behind. Today nothing reads the README's version claim at all.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: **ADDED** requirement — the version a reader is told matches the version
  the package carries, and the versioning and breaking-change policy is stated where a
  consumer will meet it. (An addition; no existing requirement is replaced, so no
  guarantee-diff is owed. The existing "The manifest version matches the package"
  requirement is untouched and still holds.)

## Non-goals

- **Publishing to NuGet.** A manual step Chris performs after merge; nothing in this
  change pushes a package.
- **The GitHub move.** `Directory.Build.props` carries `PackageProjectUrl` and
  `RepositoryUrl` pointing at Azure DevOps, and its own comment names this as the one
  place they change. They are **knowingly left wrong-for-the-future here** because the
  destination URL does not exist yet — flagged to Chris rather than guessed, and the
  tasks carry it so it cannot be lost. A package published before they are corrected
  would send consumers to a repository they cannot reach.
- **The 17.1.0 roadmap** (settings screen first) — a separate piece of work after this.
- **Any behaviour change.** No feature, no schema, no API surface moves in this change;
  if something would have to, that is a signal the release is not ready rather than a
  reason to widen this.

## Impact

- `Directory.Build.props` (the version), `README.md`, `docs/mvp.md`,
  `roadmap/version_roadmap.md` (marking the row done).
- `tests/UBookIt.Tests`: a version-truth guard, plus whatever documentation guards the
  new policy sentences earn.
- No `src/**` code, no client, no schema, no spec behaviour change.
