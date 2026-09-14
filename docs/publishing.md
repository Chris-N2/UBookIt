# Publishing uBookIt

Maintainer-facing. Everything here exists because **a first publish has one-way doors**: the
mistakes below cost a version number rather than a commit.

## What nuget.org will not let you undo

- **A pushed version's metadata cannot be edited.** Project URL, repository URL, licence,
  description, icon — all of it is frozen at push. A wrong URL is fixed by publishing a *new
  version*, and the wrong one stays visible on the version history forever.
- **A version number cannot be reused**, even after unlisting. `17.0.0` is spent the moment it
  is pushed, successfully or not.
- **Unlisting is not deletion.** An unlisted package stays resolvable by exact version, so
  anything published by mistake remains installable by anyone who knows the number.

The practical consequence: **check the produced `.nuspec`, not the source, before you push.**

## Before the first push

1. **The URLs must be the public ones.** `Directory.Build.props` carries
   `PackageProjectUrl` and `RepositoryUrl`; `VersionTruthTests.The_package_points_at_a_public_home`
   asserts they are `https`, not a known-private host, and name the repository the source lives
   in. It cannot check the URL actually resolves — open it in a browser once, logged out.
2. **The git remote must already be the public one.** This is not cosmetic: see the ordering
   section below.
3. **The version must be the one you mean.** `<Version>` in `Directory.Build.props` is the only
   place; every package and the backoffice manifest derive from it.
4. **The suite must be green from a clean build.** Never accept a `--no-build` run as evidence —
   it can execute stale assemblies and report green.

## The order that makes SourceLink correct

**This order is load-bearing, not tidiness.** Two surfaces carry a repository URL and they come
from different places:

| Surface | Derived from |
|---|---|
| `projectUrl` / `repository url` in the `.nupkg` | `Directory.Build.props` |
| `AssemblyMetadataAttribute("RepositoryUrl", …)` in each assembly | `Directory.Build.props`, at build |
| **SourceLink URLs in the `.snupkg`** | **the git remote**, at build |

So a package built before the remote moved carries the old SourceLink URLs *even if the
properties are right* — and a consumer stepping into uBookIt in a debugger is sent to a
repository they cannot open.

```
1. correct the properties        (committed, in the repo)
2. switch the git remote         git remote set-url origin <public URL>
3. clean rebuild                 dotnet build UBookIt.slnx -c Release --no-incremental
4. pack                          dotnet pack UBookIt.slnx -c Release
5. VERIFY before pushing         (below)
```

### Verify, do not assume

```bash
# the packed metadata a consumer sees
unzip -p src/UBookIt/bin/Release/UBookIt.17.0.0.nupkg UBookIt.nuspec | grep -iE "projectUrl|repository|license"

# where a debugger will be sent for source
cat src/UBookIt.Core/obj/Release/net10.0/UBookIt.Core.sourcelink.json
```

Both must name the public repository. If `sourcelink.json` still names the old host, step 2 or
step 3 did not happen — repack, do not push.

## Pushing

An API key from nuget.org (Account → API Keys), scoped to push, then for each package:

```bash
dotnet nuget push "src/**/bin/Release/*.nupkg" \
  --api-key <key> --source https://api.nuget.org/v3/index.json --skip-duplicate
```

The `.snupkg` symbol packages are pushed by the same command alongside their `.nupkg`. Push the
libraries before the `UBookIt` meta-package if you push them individually, so the meta-package's
dependencies resolve for the first person who installs it.

**Expect a delay**: indexing and validation take minutes, and the Umbraco Marketplace picks the
package up separately via the `umbraco-marketplace` tag it already carries.

## The guard that is SUPPOSED to fail when you publish

`VersionTruthTests.No_document_claims_the_package_has_reached_a_feed` asserts that no document
claims uBookIt is on a feed — true until the moment it is.

**When you publish, that guard will start failing. Do not delete it.** It is an allow-list:
`AcceptedPublicationMentions` absorbs claims that become true, one at a time, each with a reason
and an occurrence count. The failure is the checklist of every sentence that needs revisiting
now the package is public — which is exactly what you want at that moment. `openspec/specs/bookings/spec.md`
already carries one such sentence, waiting.

The same applies to the pre-release framing guards: they are the record of what had to change,
not an obstacle to changing it.
