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
   - Otherwise, fail and list the runs found (or say none was found).
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
  project, a `.snupkg` must exist where `IncludeSymbols` is true, none where it is false, and there
  must be no other files.
- **Each `.nuspec`.** Read from the zip, `<version>` must equal the tag. `<repository>` must have
  `url` = `https://github.com/${{ github.repository }}` (a trailing `.git` is allowed) and `commit` =
  the tagged SHA.
- **SourceLink.** Every `release/src/*/obj/Release/*/*.sourcelink.json` produced by this pack must
  have each value begin
  `https://raw.githubusercontent.com/${{ github.repository }}/<sha>/`. There must be at least one
  such file per project that has symbols, so an empty glob cannot pass.

The `.nupkg`/`.snupkg` files are uploaded as artifact `packages`, with 7-day retention.

**Before trusting this script, confirm it can fail.** Run it once against a pack whose version has
been deliberately edited, and once against a directory with a package deleted. This follows the
standing rule that a guard must be shown to fire. It is recorded in the tasks.

### D5. The publish job

- `if: github.event_name == 'push'`: a dry run never reaches it, and so never requests approval.
- `environment: release`; `needs: [check, pack]`.
- `download-artifact` → `setup-dotnet` with `dotnet-version: 10.0.x`. There is no `global.json`
  here without a checkout, and the push command does not depend on the SDK band.
- **Pre-flight, before any credential.** For each downloaded `.nupkg`, derive the ID by stripping
  `.<tag>.nupkg`, then query `https://api.nuget.org/v3-flatcontainer/<id-lowercase>/index.json`
  for the tag's version.
  - If every package is already present, fail with "nothing was published". The run never logs in.
  - Otherwise, record which are present, for the summary.
- **`NuGet/login`** with `user: CNorwood69`, immediately before the push, because the key lasts
  one hour.
- **Push.** Push each library `.nupkg` that is not yet present, then `UBookIt.<tag>.nupkg` last.
  Pushes use `--skip-duplicate`, to cover a race with a concurrent attempt. Each `.snupkg` travels
  with its `.nupkg`, because `dotnet nuget push` picks up a sibling symbol package.
- **The step summary** lists each package as *pushed* or *already present*.

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

**The nuget.org Trusted Publishing policy**, owned by `CNorwood69`:
- repository owner `Chris-N2`;
- repository `UBookIt`;
- workflow `publish.yml`;
- environment `release`;
- **scope: new versions of existing packages only.** A release never creates a package ID, so a
  misused token cannot claim one either. The cost: a newly added packable project fails with a
  `403`. Libraries go first, so the meta-package is then not pushed. The remedy is to widen the
  scope and re-run the publish job, which pushes only what is missing.
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

## Risks / Trade-offs

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
