## Context

- **The current route.** Releases are pushed from a laptop:
  - `dotnet nuget push "src/**/bin/Release/*.nupkg"` with a 30-day API key;
  - after a manual checklist in `docs/publishing.md`: pack from the pushed merge commit, tag
    before pushing, delete stale artifacts, and read the `.nuspec` and `sourcelink.json` by eye.
- **Package ownership.** All five packages (`UBookIt`, `UBookIt.Core`, `UBookIt.Persistence`,
  `UBookIt.Web`, `UBookIt.Backoffice`) are owned by the personal nuget.org account `CNorwood69`
  (per the search API's `owners`).
- **The two lines.** `main` (17.x) and `dev/v18` (18.x) are never merged, and each release tag sits
  on a merge commit on its line (`17.2.1` → `6f0da77`). `ci.yml` runs on every push to either line,
  with one concurrency group per commit. So every commit on a line gets a `push` run that is never
  cancelled.
- **Parity.** `scripts/ci/Assert-LineParity.ps1` holds everything under `.github/`, `global.json`
  and `scripts/ci/` identical on both lines.
- **How Trusted Publishing works on nuget.org.** A policy names a repository owner, a repository, a
  workflow **file name** and optionally an environment. GitHub's OIDC token is exchanged for an API
  key valid for one hour. Each token can be exchanged exactly once. For a public repository the
  policy is active at once.
- **Which workflow file GitHub runs.** A tag push runs the workflow file **as it stands at the
  tagged commit**. `workflow_dispatch` runs the copy on the branch chosen in the UI; the button
  exists only once the file is on the default branch.

## Goals / Non-Goals

**Goals:**
- Pushing a release tag is the whole release action. A maintainer approval is the only other human
  input.
- The one-hour credential exists only in a job that runs no repository code.
- The runbook's manual verification becomes checks that fail a run.
- The pipeline can be proved against real, already-published tags before it is trusted with a new
  release.

**Non-Goals:** as in the proposal. In brief: no fallback removal, no branch protection, no
index-wait, no ownership move, no tag rulesets, and no change to local builds.

## Decisions

### D1. Three jobs, with the credential isolated in the last

`check` → `pack` → `publish`, chained by `needs`.

- **`check` and `pack`** hold `contents: read`. `check` also holds `actions: read`, to query CI
  runs.
- **`publish`** declares `permissions: id-token: write` and nothing else. It runs in environment
  `release`, has no `actions/checkout`, and gets the packages from `pack`'s run artifact.

*Alternative rejected:* a single job that builds and pushes. It is simpler, but the credential
would share a job with `npm ci` and an MSBuild run of the whole solution: thousands of third-party
packages executing code while `id-token: write` is held. Splitting costs one artifact upload.

Workflow-level `permissions` are `contents: read`. Each job then narrows or overrides that for
itself.

### D2. Two checkouts: the tools, and the release

Every job that needs the repository checks out **two** trees:

- the workflow's own commit (`github.sha`) at the root, which provides `scripts/ci/`;
- the release tag at `release/`, with full history (`fetch-depth: 0`, needed for the containment
  and version checks).

On a tag push the two are the same commit. On a dry run (`workflow_dispatch`) the tools come from
the branch the run was started from, and the release is an older tag that has no
`scripts/ci/Assert-ReleaseTag.ps1` of its own. This is the pattern `audit.yml` already uses, and
for the same reason.

The tag comes from `github.ref_name` on push and from the `tag` input on dispatch. It is resolved
with `git rev-parse "refs/tags/<tag>^{commit}"`. A dispatch naming a tag that does not exist fails
there.

### D3. The release checks: `scripts/ci/Assert-ReleaseTag.ps1`

A PowerShell script, to match the other `scripts/ci/` scripts, run from the tools checkout against
`release/`. In order, each failure is a `::error` naming the value:

1. **Shape.** The tag must match `^\d+\.\d+\.\d+$`. The trigger's filter (`[0-9]+.[0-9]+.[0-9]+`)
   is not anchored the same way in every case, so the script checks again rather than trusting it.
2. **Declared version.** The first whole-line `<Version>` in `Directory.Build.props` at the tagged
   commit must equal the tag. This is the same first-whole-line rule the parity script uses, and
   deliberately so.
3. **Line.**
   - For each published line (`main`, `dev/v18`), read the major declared at the tip of
     `origin/<line>`.
   - Exactly one line must match the tag's major. If none does, fail: "no published line has
     major N".
   - The tagged commit must be an ancestor of that line's tip (`git merge-base --is-ancestor`).
   - Deriving the line from the declared versions means no table needs editing when `main` moves
     to 21. The two branch names are the same list the parity script holds.
4. **Verified.**
   - Query `GET /repos/{repo}/actions/workflows/ci.yml/runs?head_sha=<sha>&event=push` with
     `gh api`, keeping runs whose `head_branch` is the matched line.
   - If any has `conclusion == success`, pass.
   - If any is `queued` or `in_progress`, poll every 30 s, for up to 60 minutes (`ci` has a
     45-minute timeout).
   - Otherwise, fail and list the runs found (or say none was found). *(Revised at QA round 1:)*
     "none found" is first waited out for up to 5 minutes, because a tag pushed with its merge can
     beat the run's registration. Up to 3 consecutive API errors are retried rather than ending
     an hour's wait.
   - The job's `timeout-minutes` is 75, so the step's own bound fires first and gives a message
     rather than a timeout.

*Alternative rejected:* running the full verification again inside `publish.yml`. That duplicates
45 minutes of CI and a SQL Server container. It also proves only that the suite passes *now*, not
that the commit on the line was verified. Requiring the line's own run is exactly the claim
`ci.yml`'s comment already makes.

### D4. The verified pack

In `release/`:

- `setup-dotnet` from **`release/global.json`**, so the release is built with its own pinned SDK;
- `setup-node` 22 and `npm ci` in the client directory, as `ci.yml` does;
- then
  `dotnet pack release/UBookIt.slnx -c Release -p:ContinuousIntegrationBuild=true -o $RUNNER_TEMP/packages`.

`CI=true` is set by Actions, so warnings are errors, as they are in verification.

**`ContinuousIntegrationBuild=true`** on the command line only. It makes the embedded PDB paths
deterministic (`/_/…` rather than the runner's path), which is the recommended setting for packages
built in CI. It is not set in `Directory.Build.props`, because doing so would change local builds,
and would move parity-checked content for no local benefit.

Then `scripts/ci/Assert-PackedRelease.ps1` checks the following. Every expectation is derived, so
none of it is a hand-kept list:

- **The package set.** The expected set is each project in `release/UBookIt.slnx` whose
  `IsPackable` is true, with its `PackageId` and `IncludeSymbols`, read with
  `dotnet msbuild -getProperty`. Against it, exactly one `<PackageId>.<tag>.nupkg` must exist per
  project, and there must be no files other than those and the expected `.snupkg`s.
- **Symbols, from the package's contents** *(revised at QA round 1)*. A package "carries
  assemblies" if its `.nupkg` has any `lib/**/*.dll`.
  - Such a package must have a `.snupkg` holding the `.pdb` beside every one of those assemblies.
  - A package without assemblies must have no `.snupkg`.
  - `IncludeSymbols` is checked **against** the contents, and disagreement fails.

  The first version took the expectation *from* `IncludeSymbols`. So setting it false on a
  library that ships a dll made the missing symbols expected, and QA's reproduction passed with
  "3 symbol packages". A guard that reads the setting whose mistake it exists to catch cannot
  catch it.
- **Each `.nuspec`.** Read from the zip, `<version>` must equal the tag. `<repository>` must have
  `url` = `https://github.com/${{ github.repository }}` (a trailing `.git` is allowed) and `commit` =
  the tagged SHA.
- **SourceLink, per assembly.** Every assembly a package carries must have
  `<assembly>.sourcelink.json` under its project's `obj/Release`. Every map there must have each
  value begin `https://raw.githubusercontent.com/${{ github.repository }}/<sha>/`.

The `.nupkg`/`.snupkg` files are uploaded as artifact `packages`, with 7-day retention.

**Before trusting this script, confirm it can fail.** Run it once against a pack whose version has
been deliberately edited, and once against a directory with a package deleted. This follows the
standing rule that a guard must be shown to fire. It is recorded in the tasks.

### D5. The publish job

- `if: github.event_name == 'push'`: a dry run never reaches it, and so never requests approval.
- `environment: release`; `needs: [check, pack]`.
- `download-artifact` → `setup-dotnet` with `dotnet-version: 10.0.x`. There is no `global.json`
  here without a checkout, and the push command does not depend on the SDK band.
- **Order, before any credential.** For each downloaded `.nupkg`, derive the ID by stripping
  `.<tag>.nupkg`. A file not at the tag's version, or a missing `UBookIt` package, fails here.
  Output: the libraries, then `UBookIt`.
- **`NuGet/login`** with `user: CNorwood69`, immediately before the push, because the key lasts
  one hour.
- **Push, one file at a time, classified by the feed's answer** *(revised at QA round 1)*. For each
  ID in order: push the `.nupkg` with `--no-symbols`, then its `.snupkg` if one exists. Each push
  uses `--skip-duplicate`, and `DOTNET_CLI_UI_LANGUAGE=en` keeps the CLI's messages stable. Each
  file is classified from its own output:
  - for a `.nupkg`: `Your package was pushed.` means *published*, and `already exists at feed`
    means *already present*;
  - for a `.snupkg`: `Your package was pushed.` means *symbols submitted*, and `already exists at
    feed` means *symbols pending*. A symbol package is **never** *published* *(revised at QA
    round 2, below)*;
  - a non-zero exit means *refused*, and the step fails;
  - anything else means *unrecognised*, and the step fails rather than guessing.

  If no `.nupkg` was *published*, the step fails with "Nothing published", and states how many
  symbol packages were submitted.

**Why a symbol package can never count as published** *(QA round 2)*. nuget.org documents (*Push
Symbol Packages*, learn.microsoft.com/nuget/api/symbol-package-publish-resource) that "symbol
packages with the same ID and version can be submitted multiple times". It answers 201 each time,
and 409 only while an earlier submission "is not available yet". The round-1 version counted every
accepted push, so re-running a finished release got four 201s from its symbol packages and went
green, reporting four files published. Its own harness had shown the flaw in the partial re-run
case, where a resubmitted `.snupkg` was counted, and it was not read. The consequence of
counting packages only is deliberate: a re-run whose sole effect is resubmitting symbols ends
red, saying so. The run cannot know whether that repaired anything, so it does not claim to
have.
- **The step summary** is a table of every file and its classification.

**Why not the first design's flat-container pre-flight.** It decided "already present" from
`v3-flatcontainer`, which does not show a pushed version until validation and indexing finish.
A re-run inside that window saw published packages as missing. `--skip-duplicate` then turned
nuget.org's 409 into exit 0, and the run reported all five as pushed. That was QA round 1's first
MAJOR.

**Why each file separately.** Observed with the real CLI against a real feed (BaGet, in Docker):
- With `--skip-duplicate`, a 409 exits 0 and prints `already exists at feed`. Only a new package
  prints `Your package was pushed.`
- A combined push whose `.nupkg` already exists **never attempts its `.snupkg`**. So a release
  whose package landed and whose symbols failed could never be repaired by re-running a combined
  push.

Pushing the `.snupkg` alone fixes that. The re-run case was verified end to end: Core's package
already present, its symbols submitted.

The only credential-free check left is the order step. A re-run of a complete release now logs in
before learning nothing is new. That costs one token exchange and publishes nothing.

The "meta-package last" rule names `UBookIt` explicitly. It is the product's identity, not a list
that drifts.

Concurrency: group `publish-<tag>`, `cancel-in-progress: false`.

### D6. The one-time setup outside the repository

**The GitHub environment `release`:**
- Required reviewer: `Chris-N2`.
- **"Prevent self-review" off.** The same person pushes the tag and approves; with it on, nobody
  could approve.
- Deployment branches and tags: *selected*, with a single **tag** rule `*.*.*`. So a job on a
  branch ref cannot enter the environment even if the workflow were edited to try.
- **Administrators may bypass: on** (GitHub's default, read back as `can_admins_bypass: true`).
  This is accepted, because the only administrator is the approving maintainer. The runbook
  records it, and says how to make the approval bind an administrator too.

**The nuget.org Trusted Publishing policy**, owned by `CNorwood69`:
- repository owner `Chris-N2`;
- repository `UBookIt`;
- workflow `publish.yml`;
- environment `release`;
- **scope: new versions of existing packages only.** A release never creates a package ID, so a
  misused token cannot claim one either. The cost: a newly added packable project fails with a
  `403`. Libraries go first, so the meta-package is then not pushed. The remedy is to widen the
  scope and re-run the publish job, which completes the release and marks what was already
  present (D5).
- **packages: the glob `UBookIt*`.** It needs no edit as packages come and go, and the scope above
  already stops it from reaching new IDs. **An empty package selection is the first-push `403`
  trap again**: it fails only at the first real release.

One policy serves both lines, because the file name is the same on both.

The runbook records every one of these values. The environment name and file name appear in the
workflow *and* the policy, and a mismatch shows up only as a failed token exchange at release time.

### D7. Where it lands, and in what order

- A feature branch → a PR into `main` (CI verifies it) → merge. Then cherry-pick the same commits
  to `dev/v18` in the same sitting. Until that lands, `main`'s parity step is red, and correctly
  so.
- Chris creates the environment and the policy. Neither is needed until a real tag push.
- **Dry runs**, from the Actions tab: `17.2.1` using the workflow from `main`, and `18.1.1` using
  the workflow from `dev/v18`. Both must be green, with the publish job absent. That exercises
  every job except `publish` against real releases.
- The first real use is the next tidy patch. The manual fallback stays available for it.

### D8. The runbook

In `docs/publishing.md`:

- **Pushing** becomes: *push the tag; approve the run; then the After-the-push checks*.
- A new subsection lists what the run checks, so the maintainer knows which manual checks are now
  done for them and which are not (the package page, and restorability).
- **Setup** gives D6's values.
- The existing API-key text moves under **Fallback: pushing by hand**, unchanged in substance. The
  403 guidance stays, and applies equally to a policy owner that cannot publish.
- *What is still outstanding* loses the "No Trusted Publishing" line.

The order section (*tag before push*) stays true, and becomes automatic: the tag *is* the trigger.

The feed-mention guard (`Every_mention_of_the_feed_is_accounted_for`) and the `SaysOnce` pins will
react to the rewrite. Their counts are updated sentence by sentence, as that guard's section of the
runbook requires, not relaxed.

### D9. The confinement is guarded, not just reviewed *(added at QA round 1)*

**Revised at QA round 5: the tests now parse the workflows with YamlDotNet (test-only, MIT,
18.1.0) and assert over the tree.** Items 1–5 below record how the rules evolved while the tests
read text. Each rule now holds over the parsed YAML, so how a key or value is written makes no
difference. What remains of the text reading is a backstop, described at the end of this section.

`tests/UBookIt.Tests/PublishingWorkflowTests.cs` reads every workflow (`*.y*ml`, so a `.yaml`
cannot slip past) and asserts:

1. **Identity tokens.** Every non-comment line of every workflow that mentions `id-token` is
   found, **wherever it sits**, including a workflow-level block after `jobs:` and a flow-style
   map. Exactly one is allowed: `id-token: write` inside the `publish` job's line span. There is
   no `write-all` anywhere, and every workflow declares workflow-level `permissions:`.
2. **The publish job.** It uses only `actions/download-artifact`, `actions/setup-dotnet` and
   `NuGet/login` (an allow-list, so a checkout by any action is refused), and it runs no `git` or
   `gh`. It keeps `environment: release`, `if: ${{ github.event_name == 'push' }}` and
   `needs: [check, pack]`.
3. **Pinning.** Every `uses:` anywhere on a line, block or flow style, quoted or not, is
   `owner/repo[/path]@<40 hex>`, and at least one was found.

5. **Plain YAML only** *(added at QA round 3)*. Outside block scalars, there may be no quoted
   key, no backslash, and no anchor, alias or merge key. QA round 3 passed a quoted job name
   (`"sneak":`, whose lines fell into the publish job's span) and `"id\x2Dtoken": write`, which a
   real YAML parser decodes to `id-token` and no substring search sees. Round 2's scenario had
   been strengthened to "in any position or YAML style", which a line scanner cannot deliver. So
   the guard refuses every form it cannot read, and the claim holds by construction over what
   remains. Job headers also recognise quoted names, belt and braces. The publish-job check adds
   quoted and path-prefixed `git`/`gh`, and the repository's own URLs. What is left to review is
   named in the spec: a step fetching code from another host.
   *Revised at QA round 4.* A `- name: |` followed by `"uses": actions/checkout@v4` at the step's
   key column was "block body" to the guard and a sibling key to YAML. Quoted keys, anchors,
   aliases and merge keys are now refused on **every** line. Only backslashes stay scoped to YAML
   lines, because shell needs them and an escape cannot form a key without the quotes refused
   everywhere. The block model's `- key: |` indent is corrected to the key's column, and `uses` is
   read quoted too. Three rounds of holes came from teaching a line scanner one more YAML form;
   these rules no longer depend on that model.

*Revised at QA round 2.* The first version located grants by structure: job bodies, plus the lines
before `jobs:`. It required `uses:` at the start of a line. QA's `late.yml` (a workflow-level
`id-token: write` placed after `jobs:`, and `- { uses: actions/checkout@v4 }`) passed all four
tests. The rules now quantify over lines, and structure only locates the one allowed grant.
4. **The runbook.** Its policy table and environment heading name the environment, the file and
   the `NUGET_USER` the workflow actually uses, derived from the workflow rather than restated.

*Alternative first rejected, then adopted at QA round 5:* a YAML library in the test project.
Rounds 1–4 rejected it: the files were regular, and a dependency for four assertions seemed not
worth its maintenance. The text reading then lost to a neighbouring YAML form in four consecutive
rounds:
- round 2: a grant after `jobs:`, and flow-style `uses`;
- round 3: a quoted job name, and `"id-token"`;
- round 4: a quoted key inside what the scanner took for a block scalar;
- round 5: a `uses:` value on the next line, the `?` explicit key, and a flow mapping spanning
  lines.

Each fix taught the scanner one more form, and QA's round-5 diagnosis was that a parser ends the
series where another patch would not. So the rules are asserted over YamlDotNet's representation
model. Two things that model does not settle are refused rather than reasoned about. First,
anchors: the model shares an aliased node, so refusing anchors means no alias can exist. Second,
`<<` merge keys, which the model does not apply. More than one document is refused too. The
round-3/4 text rules survive only as a **backstop** against GitHub's parser and YamlDotNet ever
disagreeing: no quoted keys or anchors/aliases/merge keys on any line, and no backslash outside a
block scalar. *(Round 6:)* tags are refused in the tree too. The git/gh check is stated as
what it is, a check that git or gh is NAMED as a command word, after `=`, quotes or a path
separator, and before a word boundary. Whether shell *invokes* git cannot be decided from text
(`g''it`, base64, eval), so indirect invocation rests on review, and the spec says so. A precedent exists: AngleSharp is already a test-only parser here, for the same
reason.

Thirty-three mutations each failed the intended test: 14 by round 2, 7 at round 3, 3 at round 4,
5 at round 5 and 4 at round 6. Round 5's three forms contain no quote, escape, anchor, alias or merge key, so
only the parser path can catch them, and it does. They also include a new `.yaml` workflow
granting `write-all`, QA's `late.yml` verbatim, a flow-style permissions map, a `git clone` in the
publish job, a SHA-pinned third-party action there, an explicit `? id-token` key, and a second
YAML document.

## Risks / Trade-offs

- **A duplicate `.snupkg` is accepted, by nuget.org's own documentation.** *(This Risk said the
  opposite at round 1: that nuget.org was "expected to answer 409" and that anything else would
  "fail loudly". Both were false. A 201 prints `Your package was pushed.` and was counted. QA
  round 2 found it.)* The push step therefore never counts a symbol package as published. The
  one remaining unobserved answer is nuget.org's 409 for a symbol package that is still pending.
  The CLI's `--skip-duplicate` wording for a 409 was observed for packages, and is expected to be
  the same for symbols. If it is not, the push is *unrecognised* and the step fails. It does not
  miscount.

- **`NuGet/login` and the policy are not exercised until the first real release.** A dry run stops
  before `publish`. → The first real release is a patch, the manual fallback remains documented,
  and a failed token exchange publishes nothing, so a retry costs nothing. The policy values are
  recorded in the runbook to make a mismatch diagnosable.
- **A packable project added later changes the expected set without anyone deciding to publish
  it.** → That is the intended behaviour of a derived set. It would also appear in the pack step's
  output, and the approval is the moment to notice.
- **Waiting up to 60 minutes for CI uses runner time.** → Free on a public repository, and it only
  happens when a tag is pushed before CI finishes.
- **`ContinuousIntegrationBuild` makes CI-built PDBs differ from the laptop-built ones already
  published.** → Only the embedded paths change, to the documented CI form. SourceLink resolution
  is unaffected, and the pack check verifies it.
- **GitHub's environment protection is the approval gate. A maintainer with admin rights can edit
  the environment.** → Accepted. The maintainer is the gate.
- **The workflow file is taken from the tagged commit.** A tag on a commit that predates this
  change never triggers publishing. → That is correct: those versions are already published.
