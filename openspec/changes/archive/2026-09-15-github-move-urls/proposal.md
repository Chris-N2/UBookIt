# The GitHub move: package URLs, and a publishing runbook

## Why

uBookIt is versioned `17.0.0` and ready to publish, but `Directory.Build.props` still points
`PackageProjectUrl` and `RepositoryUrl` at a **private Azure DevOps organisation**. A consumer
following either from nuget.org or the Umbraco Marketplace reaches a sign-in wall. Chris has
created the public home — `github.com/Chris-N2/UBookIt` — so the URLs can finally be correct
rather than guessed, which is why `release-17-0-0` deliberately left them and recorded the
deferral instead.

**This is a one-way door.** nuget.org does not allow a pushed version's metadata to be edited,
so publishing with these uncorrected is not a commit away from fixed — it costs a version
number and leaves the bad listing permanently visible.

**A second surface was found while preparing this** and is the reason the change is not a
two-line edit: **SourceLink**. Every project emits `obj/**/*.sourcelink.json` pointing at the
Azure DevOps API, because SourceLink derives its URLs from the **git remote** rather than from
these properties. The symbol packages (`IncludeSymbols` + `snupkg` are both on) therefore tell
a consumer's debugger to fetch uBookIt's source from a private URL — the same failure as the
project link, in a place nobody looks until they are mid-debug. `RepositoryUrl` is also baked
into each assembly as an `AssemblyMetadataAttribute`. Both follow only from a **rebuild after
the remote changes**, which makes the ORDER of operations part of the deliverable.

## What Changes

- **`PackageProjectUrl` and `RepositoryUrl` become the GitHub URLs.** One edit, two lines.
- **The `CORRECT THESE BEFORE THE FIRST PUSH` warning block is removed**, because it will have
  become false — and leaving a warning that says to fix something already fixed is the same
  class of stale claim `release-17-0-0` spent six QA rounds removing.
- **`The_publication_blocker_stands_while_the_private_urls_do` is retired and replaced.** Its
  biconditional exists to fail in exactly this situation; satisfying it by deleting the guard
  and nothing else would throw away the protection. It is replaced by a guard that asserts the
  positive property the release now depends on: the package URLs are public, reachable-shaped,
  and name the repository the source actually lives in.
- **A maintainer-facing publishing runbook** (`docs/publishing.md`), because the one-way-door
  properties of a first publish are worth writing down once rather than rediscovering: what
  must be true before the push, the order that makes SourceLink correct, what cannot be undone,
  and the guard that is *designed* to fail at publication (the publication-claim allow-list) so
  it is read as a checklist rather than an obstacle.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `packaging`: **ADDED** requirement — the package points a consumer at a public home, and the
  publishing documentation states the ordering that makes source links correct plus what a push
  makes permanent.

**This section first said "no requirement changes", and that was wrong.** The guard written
here asserts a property no spec stated — a mechanism with no guarantee behind it, which is
precisely the shape this project keeps catching. The tooling refusing a change with no delta
was right, and the honest response was to write the requirement the guard had been silently
assuming rather than to work around the refusal.

## Non-goals

- **Switching the git remote.** Chris's action, and the change cannot verify its own SourceLink
  outcome until it has happened — recorded as an ordering dependency, not absorbed.
- **Publishing.** This makes publication safe; it does not perform it.
- **Renaming the package or reserving an `NDD.*` prefix.** Decided out for 17.0.0.
- **Retiring the `azure-devops-not-github` memory note** — memory, not repository, but it is
  carried in tasks so it is not lost.

## Impact

- `Directory.Build.props` (two URLs, one comment block).
- `tests/UBookIt.Tests/VersionTruthTests.cs` (one guard retired, one added).
- `docs/publishing.md` (new).
- No `src/**` behaviour, no schema, no API surface, no client.
