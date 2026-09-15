# The publisher's legal name

## Why

The package names its publisher as **"Norwood Development"**. The company is **"Norwood Design
& Development Ltd."** (Chris, 2026-09-15). This is wrong in the `.nupkg`'s `Authors`, `Company`
and `Copyright`, in the `README`, and — the one that is not merely cosmetic — in the **`LICENSE`
file's copyright line**, which names the entity granting the MIT rights.

**It has to be right before the first push, not after.** nuget.org will not let a pushed
version's metadata be edited, and `Authors`/`Copyright` are metadata; the Umbraco Marketplace
listing takes them from the package too. Publishing first would mean a wrong legal entity
permanently visible on the listing for `17.0.0`, correctable only by burning a version number.
Chris raised it while starting the publishing runbook, which is exactly when the runbook says
to check what a push makes permanent.

## What Changes

- **`Directory.Build.props`**: `Authors`, `Company` and `Copyright` become the full legal name.
  The ampersand must be written `&amp;` — a bare `&` is invalid XML and fails the build, which
  is the one way this edit can go wrong quietly if only some files are checked.
- **`LICENSE`**: the copyright line names the correct entity. Legally the most significant line
  in the change, since it identifies who grants the licence.
- **`README.md`**: the licence footer matches.
- **A guard** so the three cannot drift again: the name is declared once in
  `Directory.Build.props` and `README`/`LICENSE` are checked against it, derived rather than
  restated — the idiom `VersionTruthTests` already uses for the version. Nothing checked this,
  which is why the wrong name sat in five places across every release build.

## Capabilities

### Modified Capabilities

- `packaging`: **ADDED** requirement — the publisher the package names is one name, declared
  once, and the licence and readme state that same name.

## Non-goals

- The archived change records name the old company in URLs and prose. **History, not touched.**
- No rename of the package, the assemblies, the namespaces or the repository — the publisher's
  name is not the product's name.
- Publishing. This clears a blocker for it.

## Impact

`Directory.Build.props`, `LICENSE`, `README.md`, one new guard in `tests/UBookIt.Tests`. No
`src/**` behaviour, no schema, no API surface.
