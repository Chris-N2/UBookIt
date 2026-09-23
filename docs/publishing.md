# Publishing uBookIt

Maintainer-facing. Everything here exists because **a first publish has one-way doors**: the
mistakes below cost a version number rather than a commit. It is written for EVERY push, not
only the first: `17.0.0` is already out, and all of it applies unchanged to the next release.

## What nuget.org will not let you undo

- **A pushed version's metadata cannot be edited.** Project URL, repository URL, licence,
  description, icon, **authors and copyright** — all of it is frozen at push. Authors and
  copyright are named explicitly because they have been wrong in this repository before, and
  a list that omits them is how that goes unnoticed. A wrong URL is fixed by publishing a *new
  version*, and the wrong one stays visible on the version history forever.
- **A version number cannot be reused**, even after unlisting. uBookIt is at `17.2.0`, and
  that number is spent the moment it is pushed, successfully or not. This is no longer
  hypothetical: `17.0.0` was published on 2026-09-15 carrying a readme whose documentation links
  were relative, every one of them resolved against nuget.org rather than the repository, and
  none of it could be corrected in place. `17.0.1` exists because of it.
- **Unlisting is not deletion.** An unlisted package stays resolvable by exact version, so
  anything published by mistake remains installable by anyone who knows the number.

The practical consequence: **check the produced `.nuspec`, not the source, before you push.**

## Status

**uBookIt reached nuget.org on 2026-09-15; the first release is `17.0.0`.** All five packages are
indexed and restorable.

**That sentence is phrased so two guards can see it, and both halves are deliberate.** It names
**nuget.org**, so `Every_mention_of_the_feed_is_accounted_for` counts it and removing the feed's
name leaves an unconsumed allowance that fails. It states the version in the *first release*
phrasing, so `The_documented_anchors_do_not_move` pins that number against
`openspec/changes/archive/` — the version here cannot be edited into a release that never happened.

**It lost the first of those properties once, in the change that wrote it.** The original said
*"uBookIt `17.0.0` was published on 2026-09-15"*, which names no feed and matches nothing in the
guard's vocabulary — leaving this the one sentence in the repository that could say anything at all
about a package feed with nothing reacting. QA proved it by editing it to claim a version that does
not exist, on a date years away, with the whole suite green. **A sentence whose entire job is to be
watched has to be written so the watcher can see it**, and "it is watched" is a claim to verify by
mutation rather than assert in prose.

**What is NOT machine-checked here — named in full, because naming one exemption implies the rest
is covered:**

- **The date.** Nothing derives it.
- **The polarity of every sentence in this section.** The pins are substring matches, and a
  substring match cannot see a negation: inserting a "not" into the status claim above satisfies
  every guard exactly as well as the true sentence does. QA proved it, with that negation and with
  a count inflated to fifty packages "indexed but not restorable" — both pass green.

  *(The proof is described rather than quoted, deliberately. An earlier draft reproduced the
  falsified sentence verbatim, which duplicated the pinned phrase — and a pinned phrase quoted
  twice pins nothing, because any one occurrence satisfies it. `DocumentationAssert.SaysOnce`
  now enforces that; the sentence you are reading is why it exists.)*
- **The count and the indexing claim.** "All five packages are indexed and restorable" is derived
  from nothing.

So the guards keep this section *visible* and keep the version *honest*; they do not make it true.
**Only the feed can do that** — `GET https://api.nuget.org/v3-flatcontainer/<id>/index.json`, per
package. Anyone editing this section should re-read it against that, not against a green suite.

When this status changed at publication it tripped the accounting guard, which handed over the list
of sentences to revisit — this document, plus one in `openspec/specs/bookings/spec.md` that had been
waiting to become true and now is. Each was judged individually and either rewritten or admitted to
the allow-list with its own reason and count. **The guard was not relaxed to make the failure go
away** — that failure was the deliverable.

A second guard went red that nobody had predicted: a sentence pinned in this file was written in
the future tense about publication, and publication happening made it read as a prediction about
the past. The guarantee survived the rewording; the wording did not. **Expect pinned prose to
expire at the events it describes.**

**What is still outstanding**, stated here so this section cannot be read as "everything is done":

- **There is no CI, anywhere.** Nothing builds or tests this repository except a maintainer's
  machine, and nothing prevents an untested push.
- **No Trusted Publishing.** Pushes use an API key, which nuget.org now caps at 30 days.
- **Not submitted to the Umbraco Marketplace.** The package carries the `umbraco-marketplace` tag
  the listing is picked up from, but the submission has not been made.
- **The packages are owned by a personal account**, not by the organisation — see the key
  ownership note under *Pushing*.

## Before any push

1. **The URLs must be the public ones.** `Directory.Build.props` carries
   `PackageProjectUrl` and `RepositoryUrl`; `VersionTruthTests.The_package_points_at_a_public_home`
   asserts they are `https`, not a known-private host, and name the repository the source lives
   in. It cannot check the URL actually resolves — open it in a browser once, logged out.
2. **The git remote must already be the public one.** This is not cosmetic: see the ordering
   section below.
3. **The version must be the one you mean.** `<Version>` in `Directory.Build.props` is the only
   place it is **declared**; every package and the backoffice manifest derive from it.
   **Prose is a different matter, and a bump has to move it by hand.** Three documents state a
   version in words, in a URL or in a command — `README.md` in two places and this runbook in
   two — and none of them derives anything:

   | Where | What moves |
   |---|---|
   | `README.md`, near the top | the sentence naming the version uBookIt is currently at |
   | `README.md`, *What it looks like* | **every** screenshot URL, each pinned to the release tag |
   | `docs/publishing.md` | the *"uBookIt is at"* sentence under **What nuget.org will not let you undo** |
   | `CHANGELOG.md` | a new entry for the version, its heading left undated until it is live |
   | `docs/publishing.md`, *Tag the release* | **every** version literal in that section below — the example URL, and the `git tag`, `git push` and `curl` commands |

   (The rows are described rather than quoted on purpose, and the reason is narrower than it
   looks: **quoting a pinned sentence here reproduces it**, and a pinned sentence that appears
   twice stops pinning the original. It is not that a version number is unsafe anywhere in this
   file — the tag section below carries several, and `<version>` appears in a URL further down,
   all green. `Every_documented_version_is_the_declared_version` matches one phrasing, not every
   number it can find.)

   > **The `## Status` line is NOT on that list, and must not be edited at a release.** The
   > sentence opening that section names the date of the first publish and the version it was —
   > an **anchor**, a statement about history, pinned by `The_documented_anchors_do_not_move`
   > against the archived release change. Moving it forward makes it false and turns the suite
   > red. An earlier draft of this very table told you to edit it, three lines above the
   > paragraph saying never to: exactly the self-contradiction this checklist exists to prevent.
   >
   > (Described, not quoted — for the reason the *Status* section itself gives: a pinned
   > sentence reproduced a second time stops pinning anything, and the draft that quoted it
   > here also added a feed mention the accounting guard had not been told about.)

   The suite fails when any of the first four disagree with `<Version>` — `VersionTruthTests`
   for the README and runbook sentences and the screenshot URLs, `ChangelogTests` for the
   changelog entry — so those four are a checklist to work through in one go rather than a risk
   of shipping half done. **The last row is different: nothing checks it.** The worked commands
   in *Tag the release* are illustrative text, invisible to every guard, so that row is the one
   to re-read by eye.

   **Statements about history do not move**: the sentence naming the first release, and the one
   naming the version the public API was declared stable from. Both are pinned against the
   archived release change, and editing them forward turns them into falsehoods and turns the
   suite red. Never find-and-replace the version across the repository.
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
                                 Remove-Item -Recurse -Force src/*/bin/Release
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

```powershell
# The packed metadata a consumer sees. A .nupkg is a zip; PowerShell reads one without unzip.
Add-Type -AssemblyName System.IO.Compression.FileSystem

# More than one match here IS the stale-artifact defect Step 3 exists for - a .nupkg from an
# earlier version surviving the rebuild - so say so rather than failing with a null reference.
$found = @(Get-ChildItem src/UBookIt/bin/Release/UBookIt.*.nupkg)
if ($found.Count -ne 1) { throw "Expected exactly one .nupkg, found $($found.Count): $($found.Name -join ', '). Delete them and repack (Step 3)." }

$pkg = [IO.Compression.ZipFile]::OpenRead($found[0].FullName)
$reader = New-Object IO.StreamReader ($pkg.GetEntry('UBookIt.nuspec').Open())
$reader.ReadToEnd() -split "`n" |
  Select-String -Pattern 'projectUrl|repository|license|authors|copyright|icon|readme|version'
$pkg.Dispose()

# Where a debugger will be sent for source
Get-Content src/UBookIt.Core/obj/Release/net10.0/UBookIt.Core.sourcelink.json
```

**`icon` and `readme` are in that pattern deliberately.** Each names a file that must also BE in
the package; the build never compares the declaration against the item that packs the file, and
both are frozen at push. A declaration pointing at nothing renders as a blank on nuget.org for the
life of that version.

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

## Tag the release before you push the package

**The packed readme's screenshots are addressed to a git tag named after the version**, like
this:

```
https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.0/docs/images/booking-flow.png
```

That is deliberate. A readme is frozen at push and can never be corrected, but the images in it
are fetched live every time somebody opens the package page. Addressed to a branch, this
release's page would show whatever the repository holds years from now — a screenshot of a
screen that has since changed, or a gap where a renamed file used to be. Addressed to a tag, it
shows what this release shipped, permanently.

**So the tag has to exist, and has to be pushed, before the package goes.** Until it does, every
image on the package page is a broken image.

**And the screenshot URLs move with the version.** They are written out in `README.md`, not derived
from anything — the guard checks that they agree with `<Version>`, it does not update them. A
bump therefore edits the readme's image refs in the same commit as `Directory.Build.props`; see
*Before any push*, step 3, for the full list of what a bump touches.

```bash
git tag 17.2.0                  # on the commit you are packing from
git push origin 17.2.0
curl -sI https://raw.githubusercontent.com/Chris-N2/UBookIt/17.2.0/docs/images/booking-flow.png
```

The `curl` is the point of the step: a `200` means the address the readme carries resolves. Do it
before the package push, because afterwards it is too late to matter.

> **Nothing automated can catch a missing tag.** `VersionTruthTests` proves each image names an
> allow-listed host, resolves to a file in this working tree, and is pinned to the declared
> version. It cannot see whether the tag exists, because the tag lives on a remote — and it
> cannot see whether nuget.org rendered anything, because that happens on nuget.org. Both are
> human checks, and this is the only place they are written down.

nuget.org renders readme images only from [an allow-list of
hosts](https://learn.microsoft.com/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images-and-badges);
`raw.githubusercontent.com` is on it and plain `github.com` is not. **A rejected image is
reported in a warning visible only to the package owner** — so nobody outside the project will
ever tell you the page is broken.

## Pushing

You need an API key from nuget.org. The route there is not obvious any more: **Account → API Keys
now lands on Trusted Publishing**, and the API-key form is behind a link on that page.

One command pushes everything. It is written for PowerShell, which is the shell this project is
driven from — **do not carry a trailing `\` over onto a second line**, that is bash syntax and
PowerShell will treat the two lines as two commands:

```powershell
dotnet nuget push "src/**/bin/Release/*.nupkg" --api-key <key> --source https://api.nuget.org/v3/index.json --skip-duplicate
```

There is **no `dotnet nuget setapikey`**. That command belongs to `nuget.exe`, which the .NET SDK
does not install, so the key goes on the command line. To keep it out of PSReadLine history, read
it into a variable first with `Read-Host` and pass the variable.

The `.snupkg` symbol packages are pushed by the same command alongside their `.nupkg`.

### A 403 usually means the key's OWNER, not the key

`403 (The specified API key is invalid, has expired, or does not have permission to access the
specified package.)` is one message covering several conditions, and it names only the key — which
sends you to check the thing that is least likely to be wrong. This cost the first publish attempt.
In the order worth checking:

1. **Can the key's owner publish at all?** A key can be owned by your account or by a nuget.org
   **organization**, and an organization has its own email address that must be confirmed
   separately from yours. nuget.org requires that address to be distinct from any member
   account's, so giving the organization the address you already use leaves it permanently
   unconfirmed — and a key owned by it returns exactly this 403. **The fastest way to isolate
   this is a second key owned by your personal account**: if that works, the key was never the
   problem.
2. **Is the scope "push new packages and package versions"?** The narrower *push only new versions
   of existing packages* cannot create an ID that does not exist yet, so a first publish fails on
   every package.
3. **Is the package ID already owned by somebody else?** `GET https://api.nuget.org/v3-flatcontainer/<id>/index.json`
   returning 404 means the ID is unclaimed.

Ownership is **not** a reason to delay a push: a package can be transferred to an organization
afterwards from its Manage Owners page, and owners can be added and removed freely. Unlike the
metadata, ownership is not a one-way door.

### After the push

A push that succeeds does not mean a package anyone can install yet. Three states follow:
**accepted**, then **validating** (malware and signature checks), then **indexed**. Throughout the
first two the package page presents as unlisted and `dotnet add package` cannot find it — that is
normal and needs no action. Nothing here is a flag you set; there is no "push as unlisted" option.

The signal that restore will actually work is the flat-container endpoint, which is what restore
itself reads:

```powershell
curl https://api.nuget.org/v3-flatcontainer/ubookit/index.json
```

Search on the website lags further behind still. If an hour passes with no progress, check the
package page and your email — nuget.org reports a validation failure by mail.

**Open the package page and look at the screenshots.** Every image must render. This is the only
check that ever sees what a consumer sees: no test can reach nuget.org, and a rejected or missing
image is reported in a warning shown only to you as the owner, so a broken page is silent to
everybody else. If one is missing, the usual cause is the tag — confirm
`https://raw.githubusercontent.com/Chris-N2/UBookIt/<version>/docs/images/<file>` answers, and
push the tag if it does not. The readme itself cannot be corrected for this version.

The wildcard resolves alphabetically, so it pushes the `UBookIt` meta-package FIRST, before the
libraries it depends on. That is harmless — nuget.org validates each package independently and
does not require a dependency to exist at push time — but if you push them individually, push
the libraries first so the meta-package is never briefly uninstallable.

The Umbraco Marketplace picks the package up separately, via the `umbraco-marketplace` tag it
already carries, on its own schedule.

## The guard that failed when we published — and what it guards now

`VersionTruthTests.Every_mention_of_the_feed_is_accounted_for` scans the documents it walks for a
fixed vocabulary of feed phrasings and requires every hit to be registered in
`AcceptedPublicationMentions` with a reason and an exact occurrence count. It is not a proof that
no document anywhere makes a claim — a wording outside that vocabulary passes, as its own remarks
state — but it turns the mentions it does know about into a list somebody has to work through.

**A guard that goes red at publication is doing its job. Do not delete it.** The cheapest way to
make one of these pass is always to remove it, and that trades the only thing watching a set of
sentences for a green run. Work the list instead; the red is the checklist.

That is not hypothetical advice inherited from before the first release — it is what happened. Before
`17.0.0` shipped, it asserted an ABSENCE: no document may say the package is on a feed, because it
was not. That premise expired the moment the push succeeded, so the guard was renamed and its
message rewritten rather than left asserting "nothing is pushed" in a repository whose package is
installable. It now asserts something weaker but still worth having: **every mention of the feed is
deliberate and counted.**

What that still catches: a document quietly gaining "available on nuget.org" about something that
is not — the Marketplace listing, a version that was never pushed, a package that does not exist.
Because the counts are exact, editing a document that mentions the feed forces a fresh look at
whether the new text is true, which is the property that made this worth keeping.

What it cannot catch, stated so nobody assumes otherwise: it does not verify that anything claimed
about the feed is actually so. Only the feed can answer that. `GET
https://api.nuget.org/v3-flatcontainer/<id>/index.json` is the cheapest check.

The pre-release framing guards are different and did **not** go red: they assert that certain
sentences are ABSENT, and publishing does not bring them back.
