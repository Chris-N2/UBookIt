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
- **A version number cannot be reused**, even after unlisting. uBookIt is at `18.3.0`, and
  that number is spent the moment it is pushed, successfully or not. This is no longer
  hypothetical: `17.0.0` was published on 2026-09-15 carrying a readme whose documentation links
  were relative, every one of them resolved against nuget.org rather than the repository, and
  none of it could be corrected in place. `17.0.1` exists because of it.
- **Unlisting is not deletion.** An unlisted package stays resolvable by exact version, so
  anything published by mistake remains installable by anyone who knows the number.

The practical consequence: **check the produced `.nuspec`, not the source, before you approve the
publish.**

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

- **CI verifies, but does not yet gate.** Every push to `main` or `dev/v18` is built and
  tested unattended, as is the latest push to any other branch (see *Continuous integration*
  below). No branch protection requires a green run, so an untested push is visible rather than
  prevented.
- **What the feed answers when a symbol package is pushed while its predecessor is still pending
  has not been seen**, because no push has met that case yet. If it isn't the wording the workflow
  expects, the step fails closed rather than reporting success.
- **The packages are owned by a personal account**, not by the organisation. So is the Trusted
  Publishing policy. Read *Trusted Publishing setup*, including its 403 section, before moving
  either.

## Continuous integration

Three GitHub Actions workflows, held **identical on `main` and `dev/v18`** together with
`global.json`, `scripts/ci/` and `Directory.Build.props` (all of it except the `<Version>` line,
which is the one thing the lines legitimately disagree on). A push to either line fails its parity
step while those files
differ from the other line's tip — so after changing CI on one line, **expect that line's run to be
red until the same change is pushed to the other**, then re-run it. The red is true: at that moment
the lines do differ.

- **`ci`** — every push to either line, each with its own run that nothing cancels; the
  latest push to any other branch; and pull requests into either line. On Linux, from a clean
  clone with full history: the client's tests, a Release build with warnings as errors, the three
  .NET suites against a SQL Server started for the run, and `openspec validate --all --strict`.
  **A green run means every suite demonstrably ran**: the integration fixture runs with
  `UBOOKIT_TEST_DB_REQUIRED=true`, so an unreachable database fails instead of skipping, and
  `scripts/ci/Assert-TestResults.ps1` fails the run if any test project on disk did not report or
  any test was skipped. `scripts/ci/Assert-ClientTestResults.ps1` does the same for the client's
  tests, from the report vitest writes: a skipped or todo test, or a report that ran none, fails
  the run. It publishes nothing and holds no secrets.
- **`audit`** — Mondays 06:00 UTC, and on demand from the Actions tab. Audits the NuGet graph of
  **both** lines for known vulnerabilities, direct and transitive, and fails on any. Each line is
  restored and listed as Debug and again as Release, so an advisory reachable only from the graph
  that ships is still found. There is one narrow exception: a project that is not packable can
  accept an advisory it suppresses (`NuGetAuditSuppress`), for one no reachable version can fix
  yet. What a project suppresses, and whether it is packable, are taken from MSBuild's evaluation
  of it, as Debug and as Release, so imports count, and so does anything else MSBuild sees. The audit reports each accepted
  finding as a notice, and fails on a suppression that:
  - matches nothing (stale);
  - is declared in one configuration but not the other;
  - is declared by a project that is packable in either configuration; or
  - is declared by a project that targets more than one framework.

  **What the audit cannot see:** a package the .NET SDK prunes because the project references the
  ASP.NET Core shared framework (for example `System.Security.Cryptography.Xml`; the full list is
  `packagesToPrune` in a project's `obj/project.assets.json`) is not in the graph, so an advisory
  against it is never reported here. Every site runs the framework's copy of such an assembly, so
  only the site's .NET runtime fixes it. The trigger to check is a .NET security release: if it
  names a package on that list, the next release's changelog tells sites which runtime they need
  (the `packaging` spec's requirement on advisories).

  Today the only accepted advisories are `UBookIt.TestSite`'s, for ImageSharp 3.1.12, which it
  reaches through Umbraco; see the comment there. Advisories are deliberately *not* errors in the `ci` build (see `Directory.Build.props`): an advisory is
  published against a package, not introduced by a commit, and would otherwise fail unrelated
  work. **GitHub disables scheduled workflows after 60 days without repository activity** — if the
  project goes quiet, run it by hand.
- **`publish`** — a pushed release tag, or a dry run started by hand. Checks the tag, packs and
  verifies the release, then publishes it after a maintainer approves. It is the only workflow
  that can obtain a credential, and only in its last job. See *Publishing a release*.

**To reproduce CI's build locally**, set `CI=true` for the build (`$env:CI='true'` in PowerShell):
the warnings-as-errors rule and its advisory exception both live in `Directory.Build.props`, not in
the workflow. `global.json` pins the SDK to the 10.0.3xx band for local builds as well as CI; a
machine without one is told so rather than silently building with another band.

## Publishing a release

**The route is: merge, push the tag, approve the run.** Pushing a tag of the form
`<major>.<minor>.<patch>` starts `.github/workflows/publish.yml`, which runs three jobs in order:

1. **check** — `scripts/ci/Assert-ReleaseTag.ps1`. It refuses the tag unless:
   - the tag equals the `<Version>` the tagged commit declares;
   - the commit is on the published line whose declared major matches (`17.x` on `main`,
     `18.x` on `dev/v18`, worked out from each line's own `<Version>`);
   - `ci` succeeded for a push of that commit to that line.

   If that `ci` run is still going, it waits for up to an hour, so a tag can be pushed straight
   after the merge.
2. **pack** — packs the tagged commit from a clean clone, then `scripts/ci/Assert-PackedRelease.ps1`
   checks what came out:
   - exactly one package per packable project, and nothing else;
   - a symbol package exactly where one is expected;
   - every `.nuspec` at the tag's version, naming this repository and the tagged commit;
   - every SourceLink URL pointing at this repository at that commit.
3. **publish** — waits in the GitHub environment `release` until a maintainer **approves** it
   (the run page shows *Review deployments*). It then exchanges the run's identity token for a
   one-hour key (Trusted Publishing) and pushes the libraries, then the `UBookIt` meta-package.
   Each `.nupkg` and each `.snupkg` goes on its own. The run summary marks every package
   *published* or *already present*, read from what the feed answered to its push, and every
   symbol package *symbols submitted* or *symbols pending*. A symbol package is never counted as
   published, because the feed accepts the same one again with the same answer, so its answer
   cannot say whether it was new. A re-run after a partial push therefore completes it. A run
   that published no package fails rather than reporting success, even when it resubmitted
   symbols; if you re-ran to repair symbols, check the package page. The job never checks out
   the repository, so no code from it runs while the key exists.

**What that replaces, and what it does not.** From a fresh clone, every time:
- the stale-artifact and commit-SHA problems below cannot arise;
- of *Verify, do not assume*, the run itself checks each package's version, repository, commit,
  symbols and SourceLink;
- authors, licence, readme, icon, project URL, description and title are checked to be
  **present**, and not framework defaults, by `PackageCompositionTests`, against a pack of the
  same commit. The same tests check that the readme and icon are inside each package.
  `VersionTruthTests` checks that the declared project URL is the public home. Both suites run
  in `ci` for that commit, which the check job requires to have passed.

**Nothing checks that those values are right, and copyright is checked by nothing at all.** So
read the `.nuspec` files from the run's `packages` artifact before you approve (see *Verify, do
not assume*). Every one of them is frozen at push. The run also makes the tag-before-package
order automatic, because the tag is what starts it. **It does not replace:**
- *Before any push* — the version bump and the prose it moves are still yours;
- the tagged screenshot `curl` in *Tag the release* — **do it before you approve**;
- *After the push* — indexing, the package page, the Marketplace.

**A dry run** proves the pipeline without publishing: Actions → *publish* → *Run workflow*.
Choose the line's branch under *Use workflow from*, and give an existing tag. It runs check and
pack in full and never reaches the publish job, so nothing is approved and no credential is
requested. It works for tags older than the workflow, because the scripts come from the branch
the run was started from.

**There is no hand route.** If the workflow cannot publish (for example, nuget.org's token
exchange is down, or the workflow itself is broken), the release waits. If the workflow needs
fixing, fix it in a change of its own and then release through it. Do not create an API key and
push a local pack. That would skip the tag check, the clean-clone pack, the package verification,
the approval and the push order, and those checks are the reason the workflow exists. Every
release up to `17.2.1`/`18.1.1` was pushed by hand, and the sections below record what that cost.

## Trusted Publishing setup

One-time, outside the repository, and recorded here because a mismatch only shows up as a
failed token exchange at release time. Both halves name each other, so change them together.

**GitHub → Settings → Environments → `release`:**
- Required reviewer: `Chris-N2`.
- *Prevent self-review*: **off**. The person who pushes the tag is the person who approves it.
  With this on, nobody could.
- Deployment branches and tags: *Selected*, with a single **tag** rule `*.*.*`. So a job on a
  branch cannot enter the environment.
- *Allow administrators to bypass configured protection rules*: **on**, GitHub's default, and
  accepted. The only administrator is the maintainer who would approve anyway. Turn it off if the
  approval should bind an administrator too.

**nuget.org → your username → Trusted Publishing → the policy:**

| Field | Value |
|---|---|
| Owner | `CNorwood69`, the personal account that owns every package |
| Repository owner | `Chris-N2` |
| Repository | `UBookIt` |
| Workflow file | `publish.yml`, the file name only |
| Environment | `release` |
| Scope | new versions of existing packages only |
| Packages | the glob `UBookIt*` |

Notes on the policy:
- **The scope is deliberately narrow.** A release never creates a package ID, so a misused token
  cannot either. What that costs, when a new package is added, is check 2 in the section below.
- **The policy belongs to the account that owns the packages.** Transferring them to the
  organisation means creating a new policy under the new owner. The workflow's `NUGET_USER` then
  changes to match.
- **One policy serves both lines**, because the workflow file is the same on both. Renaming
  `publish.yml` or the environment breaks it.

### A 403 usually means the policy's OWNER, not the key

The workflow pushes with the one-hour key that the token exchange returns. So a policy problem can
surface in either of two places, and **neither has yet been seen on this workflow**:

- **At the login step**, when the exchange refuses the run's token. nuget.org marks a policy
  *inactive*, with a warning on its Trusted Publishing page, when the organization that owns it is
  locked or deleted, or when the member who created it leaves that organization.
- **At the push**, as `403 (The specified API key is invalid, has expired, or does not have
  permission to access the specified package.)`. That one message covers several conditions,
  and it names only the key. That sends you to check the thing least likely to be wrong, since
  the exchange has just issued the key. This 403 cost the first publish attempt, when the key was
  made by hand.

Check these in order:

1. **Can the policy's owner publish?** The owner is your account or a nuget.org **organization**
   you belong to, and the policy applies to the packages that owner owns. And an organization has
   its own email address that must be confirmed separately from yours. nuget.org requires that address
   to be distinct from any member account's. So if you give the organization the address you
   already use, it stays permanently unconfirmed. That is what made the first publish's hand-made
   key fail with this 403. **To isolate it, open the policy on the Trusted Publishing page**: an
   ownership problem shows there as a warning on an inactive policy. If the owner is an
   organization, also check its address is confirmed on the organization's own page. It has not
   been observed whether an unconfirmed address marks the policy inactive, or only fails the push.
2. **Does the scope cover what is being pushed?** The policy is scoped to new versions of existing
   packages (see the notes above), so it cannot create an ID that does not exist yet. A newly added
   packable project therefore fails its push with this 403. Libraries go first, so the
   meta-package is not pushed either. Widen the scope, then re-run the publish job, which
   completes the release and marks what was already there.
3. **Does the package selection match it?** The selection is the glob in the table above. A
   package whose ID the glob does not match is refused the same way. **An empty selection**
   refuses every package, and shows only at the first real release. Correct the selection, then
   re-run the publish job.
4. **Is the package ID already owned by somebody else?** If
   `GET https://api.nuget.org/v3-flatcontainer/<id>/index.json` returns 404, the ID is unclaimed.

Ownership is **not** a reason to delay a push. A package can be transferred to an organization
afterwards from its Manage Owners page, and owners can be added and removed freely. Unlike the
metadata, ownership is not a one-way door. The policy does not move with it, though: see the
ownership note above.

## Before any push

1. **The URLs must be the public ones.** `Directory.Build.props` carries
   `PackageProjectUrl` and `RepositoryUrl`; `VersionTruthTests.The_package_points_at_a_public_home`
   asserts they are `https`, not a known-private host, and name the repository the source lives
   in. It cannot check the URL actually resolves — open it in a browser once, logged out.
2. **The version must be the one you mean.** `<Version>` in `Directory.Build.props` is the only
   place it is **declared**; every package and the backoffice manifest derive from it.
   **Prose is a different matter, and a bump has to move it by hand.** Three documents state a
   version in words, in a URL or in a command — `README.md` in **three** places and this runbook
   in two — and none of them derives anything:

   | Where | What moves |
   |---|---|
   | `README.md`, *Versions and the API promise* | the sentence naming the version uBookIt is currently at |
   | `README.md`, the opening and *What it looks like* | **every** screenshot URL, each pinned to the release tag |
   | `README.md`, throughout | **every** documentation link — pinned to the release tag too, not to a branch, for the reason the screenshots are |
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

   The suite fails when any of the first five disagree with `<Version>` — `VersionTruthTests`
   for the README and runbook sentences, the screenshot URLs and the documentation links,
   `ChangelogTests` for the changelog entry — so those five are a checklist to work through in one
   go rather than a risk of shipping half done. **The last row is different: nothing checks it.** The worked commands
   in *Tag the release* are illustrative text, invisible to every guard, so that row is the one
   to re-read by eye.

   **Statements about history do not move**: the sentence naming the first release, and the one
   naming the version the public API was declared stable from. Both are pinned against the
   archived release change, and editing them forward turns them into falsehoods and turns the
   suite red. Never find-and-replace the version across the repository.
3. **The suite must be green from a clean build.** Never accept a `--no-build` run as evidence —
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

The order that makes both right is: correct the properties, move the remote, then rebuild from
nothing. **The workflow makes that order a property of where it packs, not a sequence somebody
follows.** Its pack job clones the public repository afresh at the tagged commit. So the
properties are the ones that commit declares, the remote *is* the public repository, and there are
no earlier build outputs. Its verification then fails the run if any SourceLink URL names another
repository or another commit. Nothing about this order is left to a maintainer, and there are no
local pack steps to follow for a release.

**A fresh clone is what keeps a stale package out.** `--no-incremental` governs the *build*; it
does not force `GenerateNuspec`, which skips when its outputs look up-to-date
(`Skipping target "GenerateNuspec" because all output files are up-to-date`). In a working tree
that has packed before, a `.nupkg` made before the remote moved can survive a clean rebuild
untouched and be picked up as if it were new. That was the exact failure this document existed to
prevent, and it hid inside the manual procedure's own instructions. A clone that has never been
built has no earlier `.nupkg` to survive. That is why the workflow packs from one, and why a
package packed in a working copy is never a release candidate.

### Verify, do not assume

The workflow verifies, for every package, the version, repository, commit, symbols and SourceLink.
It does that before the publish job can be approved (see *Publishing a release*). **It does not
verify the values a person chose**, such as authors, copyright, licence, description, icon and
readme. So before you approve, download the run's `packages` artifact from the run page, unzip it,
and read what was produced:

```powershell
# Run in the folder the artifact was unzipped into. A .nupkg is a zip; PowerShell reads one
# without unzip. `[0-9]` selects the meta-package, not the UBookIt.Core/Persistence/... libraries.
Add-Type -AssemblyName System.IO.Compression.FileSystem

$found = @(Get-ChildItem 'UBookIt.[0-9]*.nupkg')
if ($found.Count -ne 1) { throw "Expected exactly one UBookIt meta-package, found $($found.Count): $($found.Name -join ', ')." }

$pkg = [IO.Compression.ZipFile]::OpenRead($found[0].FullName)
$reader = New-Object IO.StreamReader ($pkg.GetEntry('UBookIt.nuspec').Open())
$reader.ReadToEnd() -split "`n" |
  Select-String -Pattern 'projectUrl|repository|license|authors|copyright|icon|readme|version'
$pkg.Dispose()
```

**`icon` and `readme` are in that pattern deliberately.** Each names a file that must also BE in
the package; the build never compares the declaration against the item that packs the file, and
both are frozen at push. A declaration pointing at nothing renders as a blank on nuget.org for the
life of that version.

**Each package carries its own metadata, so check all five** — the commands above read one as
an example. (Five `.nupkg`, but only four `.snupkg`: the `UBookIt` meta-package contains no
assemblies and sets `IncludeSymbols=false`.)

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

**The check job refuses a tag whose commit no published line contains**, so the workflow only
ever packs a commit the public repository already has. That is why the release tag goes on the
pushed merge commit of the line being released. Tag anything else and the run stops before it
packs.

## Tag the release before you approve the publish

**The packed readme's screenshots AND its documentation links are addressed to a git tag named
after the version**, like this:

```
https://raw.githubusercontent.com/Chris-N2/UBookIt/18.3.0/docs/images/booking-flow.png
```

That is deliberate. A readme is frozen at push and can never be corrected, but the images in it
are fetched live every time somebody opens the package page. Addressed to a branch, this
release's page would show whatever the repository holds years from now — a screenshot of a
screen that has since changed, or a gap where a renamed file used to be. Addressed to a tag, it
shows what this release shipped, permanently.

**So the tag has to exist, and has to be pushed, before the package goes.** Until it does, every
image on the package page is a broken image **and every documentation link on it is dead** — and
after the push neither can be corrected, because the readme carrying those addresses is frozen.

**Both kinds are pinned, and for the same reason with different consequences.** An unpinned image
would show a screenshot of a screen that has since changed; an unpinned *link* sends the reader to
documentation for a version they are not running — and with two published lines, potentially to
the other line's documentation entirely, silently, because the file exists on both branches.

**And these addresses move with the version.** They are written out in `README.md`, not derived
from anything — the guards check that they agree with `<Version>`, they do not update them. That
is true of the screenshot URLs and of every documentation link alike; bumping one set and not the
other is the half-done state the checklist above exists to prevent. A
bump therefore edits the readme's image refs in the same commit as `Directory.Build.props`; see
*Before any push*, step 2, for the full list of what a bump touches.

```bash
git tag 18.3.0                  # on the pushed merge commit you are releasing
git push origin 18.3.0
curl -sI https://raw.githubusercontent.com/Chris-N2/UBookIt/18.3.0/docs/images/booking-flow.png
```

The `curl` is the point of the step: a `200` means the address the readme carries resolves. Do it
before you approve the publish, because afterwards it is too late to matter.

**Pushing a release tag starts publishing.** The `git push origin <tag>` above triggers the
`publish` workflow (see *Publishing a release*), which stops at the approval. So the `curl` goes
between the tag push and your approval.

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

## After the push

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

**Open the package page, look at the screenshots, and follow a documentation link.** Every image
must render and every link must land. This is the only
check that ever sees what a consumer sees: no test can reach nuget.org, and a rejected or missing
image is reported in a warning shown only to you as the owner, so a broken page is silent to
everybody else. If one is missing, the usual cause is the tag — confirm
`https://raw.githubusercontent.com/Chris-N2/UBookIt/<version>/docs/images/<file>` answers, and
push the tag if it does not. The readme itself cannot be corrected for this version.

The Umbraco Marketplace picks the package up separately and on its own schedule. There is nothing
to submit: it lists every package that carries the `umbraco-marketplace` tag and depends on
Umbraco. **Only the `UBookIt` meta-package carries that tag.** The libraries install an
incomplete product, so a listing for one of them invites the silent failure the meta-package exists
to prevent. `PackageCompositionTests` fails if any other package carries it, or if `UBookIt`
does not. The meta-package has no Umbraco dependency of its own but is listed through its
children.

**The tag cannot be taken back from a version already pushed.** Until `17.2.1`/`18.1.1`, every
package carried it, and `UBookIt.Persistence`, `UBookIt.Web` and `UBookIt.Backoffice` got
listings of their own. **The Marketplace dropped them once their latest version was untagged.**
This was seen on 2026-09-25, after the rescan that followed `17.2.1`/`18.1.1`: only `UBookIt` was
listed, showing the `18.x` line and support for Umbraco 17 and 18. That was one observation, not a
rule the Marketplace promises, so still check `https://marketplace.umbraco.com/package/<id>` after
a release that changes the tag.

**The listing's description is the meta-package's `<Description>`**, and it too is frozen at push.
It names the Umbraco major derived from `<Version>`, because `UBookIt.csproj` is shared by both
lines. `18.0.0` and `18.1.0` shipped describing themselves as for Umbraco 17 when the major was
written by hand.

**The listing's author details come from `umbraco-marketplace.json` at the repository root.** That
covers the publisher's description, website and logo. The Marketplace looks for the file in the
folder the package's project URL points to. That URL is this GitHub repository, so it reads the
file from the root of `main`, at its own sync. It does not read it from any package, so **changing
the file needs no release**. The logo it names is `docs/images/ndd-logo.png`, served from `main`.

- **The file is on `main` only.** The Marketplace reads only the default branch. A copy on
  `dev/v18` would be read by nothing and free to drift, and so would its test.
- **It may carry only `$schema` and `AuthorDetails`.** One listing serves both lines, so a `Title`
  or `Description` there would override every line's description with one sentence.
  `MarketplaceListingFileTests`, on `main`, refuses:
  - any other top-level field;
  - author text that names an Umbraco major in the form "Umbraco 17";
  - an author name that differs from the packed `<authors>`.

  A logo URL into this repository must be the raw `main` form already in the file, naming a file
  git tracks. Other forms, such as GitHub's `refs/heads/main` link, are refused. A logo hosted
  anywhere else is checked only for being an `https` `.png` or `.jpg`.
- **If the listing has not changed after a reasonable wait,** ask the Marketplace to resync the
  package through its single-package sync endpoint, described in Umbraco's
  [listing documentation](https://docs.umbraco.com/umbraco-dxp/marketplace/listing-your-package).
  Do not keep editing the file.
- **The "Explore more packages by …" link is not set by this file.** It follows the NuGet owners of
  the package.
- **Seen on the listing on 2026-10-19,** at the first sync after the file merged. The author card
  showed the logo, the description and a "Read more about the developer" link to the website. The
  link read "explore more packages by CNorwood69 or Norwood-Design-and-Development", one name per
  NuGet owner. The listing's own icon is the package icon (`assets/icon.png`), not this file's logo.
  That is one observation, not a rule the Marketplace promises.

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
