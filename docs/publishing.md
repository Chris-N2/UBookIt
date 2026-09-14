# Publishing uBookIt

Maintainer-facing. Everything here exists because **a first publish has one-way doors**: the
mistakes below cost a version number rather than a commit.

## What nuget.org will not let you undo

- **A pushed version's metadata cannot be edited.** Project URL, repository URL, licence,
  description, icon — all of it is frozen at push. A wrong URL is fixed by publishing a *new
  version*, and the wrong one stays visible on the version history forever.
- **A version number cannot be reused**, even after unlisting. uBookIt is at `17.0.0`, and
  that number is spent the moment it is pushed, successfully or not.
- **Unlisting is not deletion.** An unlisted package stays resolvable by exact version, so
  anything published by mistake remains installable by anyone who knows the number.

The practical consequence: **check the produced `.nuspec`, not the source, before you push.**

## Status

**uBookIt has not been pushed to nuget.org.** This line is the publication status
`openspec/config.yaml` points at. It is deliberately phrased so the feed-arrival guard counts
it: changing it at publication changes the count, which trips
`No_document_claims_the_package_has_reached_a_feed` and hands you the checklist of every
other sentence that needs revisiting. That is the mechanism this document recommends to its
reader, applied to itself.

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
1. correct the properties        (committed AND PUSHED - see the SHA section below)
2. switch the git remote         git remote set-url origin <public URL>
3. DELETE the old artifacts      dotnet clean UBookIt.slnx -c Release
                                 rm -rf src/*/bin/Release
4. clean rebuild                 dotnet build UBookIt.slnx -c Release --no-incremental
5. pack                          dotnet pack UBookIt.slnx -c Release
6. VERIFY before pushing         (below)
```

**Step 3 is not housekeeping — without it steps 4 and 5 can produce nothing.** `--no-incremental`
governs the *build*; it does not force `GenerateNuspec`, which skips when its outputs look
up-to-date (`Skipping target "GenerateNuspec" because all output files are up-to-date`). A
`.nupkg` produced *before* the remote moved therefore survives this whole procedure untouched
and is then matched by the push wildcard below — the exact failure this document exists to
prevent, hiding inside its own instructions.

### Verify, do not assume

```bash
# the packed metadata a consumer sees
unzip -p src/UBookIt/bin/Release/UBookIt.*.nupkg UBookIt.nuspec | grep -iE "projectUrl|repository|license"

# where a debugger will be sent for source
cat src/UBookIt.Core/obj/Release/net10.0/UBookIt.Core.sourcelink.json
```

**Each package carries its own metadata, so check all five** — the commands above read one as
an example. (Five `.nupkg`, but only four `.snupkg`/`sourcelink.json`: the `UBookIt`
meta-package contains no assemblies and sets `IncludeSymbols=false`.)

If any `sourcelink.json` still names the old host, step 2 or step 3 did not happen: repack,
do not push.

## Publish only from a commit that is already on the public repository

SourceLink embeds the commit SHA, not just the repository:

```
https://raw.githubusercontent.com/Chris-N2/UBookIt/<commit>/*
```

So packing from a commit that has not been pushed produces a package whose source links
resolve to nothing - a 404 for every file, for every consumer, permanently, because a pushed
version cannot be corrected. A local-only commit, an unmerged branch, or a commit amended
after packing all produce this, and every other check in this repository still passes: the
URLs are right, the host is right, the nuspec is right. Only the SHA is unreachable.

**Pack from the merge commit on main, after it is pushed**, and confirm the SHA the pack will
embed is one the public repository actually has:

```bash
git rev-parse HEAD                       # the SHA the pack will embed
git branch -r --contains HEAD            # must list origin/main
```

## Pushing

An API key from nuget.org (Account → API Keys), scoped to push, then for each package:

```bash
dotnet nuget push "src/**/bin/Release/*.nupkg" \
  --api-key <key> --source https://api.nuget.org/v3/index.json --skip-duplicate
```

The `.snupkg` symbol packages are pushed by the same command alongside their `.nupkg`.

The wildcard resolves alphabetically, so it pushes the `UBookIt` meta-package FIRST, before the
libraries it depends on. That is harmless — nuget.org validates each package independently and
does not require a dependency to exist at push time — but if you push them individually, push
the libraries first so the meta-package is never briefly uninstallable.

**Expect a delay**: indexing and validation take minutes, and the Umbraco Marketplace picks the
package up separately via the `umbraco-marketplace` tag it already carries.

## The guard that is SUPPOSED to fail when you publish

`VersionTruthTests.No_document_claims_the_package_has_reached_a_feed` scans the documents it
walks for a fixed vocabulary of feed-arrival phrasings and requires every hit to be classified.
It is not a proof that no document anywhere makes the claim — a wording outside that vocabulary
passes, as its own remarks state — but it turns the claims it does know about into a list you
must work through.

**When you publish, that guard will start failing. Do not delete it.** It is an allow-list:
`AcceptedPublicationMentions` absorbs claims that become true, one at a time, each with a reason
and an occurrence count. The failure is the checklist of every sentence that needs revisiting
now the package is public — which is exactly what you want at that moment. `openspec/specs/bookings/spec.md`
already carries one such sentence, waiting.

The pre-release framing guards are different and will **not** go red: they assert that certain
sentences are ABSENT, and publishing does not bring them back. Only the feed-arrival guard above
is designed to fail at this moment.
