# Tasks — github-move-urls

## 1. The URLs

- [x] 1.1 `Directory.Build.props`: `PackageProjectUrl` → `https://github.com/Chris-N2/UBookIt`,
      `RepositoryUrl` → `https://github.com/Chris-N2/UBookIt.git`. Note the repository's own
      casing (`UBookIt`) differs from the old Azure path (`uBookIt`) — use the real one.
- [x] 1.2 Remove the `CORRECT THESE BEFORE THE FIRST PUSH TO A PUBLIC FEED` block; it becomes
      false with 1.1. Replace with a short note on what the URLs are for and the one-way-door
      property, pointing at `docs/publishing.md`.

## 2. The guard (design D1)

- [x] 2.1 Retire `The_publication_blocker_stands_while_the_private_urls_do` — it exists to fail
      here, and the cheap way to satisfy it is deletion, which would drop the protection.
- [x] 2.2 Replace with a guard on the positive property: both package URLs are `https`, are not
      a known-private host, and name the repository the source lives in. No network access
      (D1) — say why in the remarks.
- [x] 2.3 Mutation-check against a COMMIT: revert either URL to the Azure one → fails; switch to
      `http` → fails; point at a different repository → fails. Verify each mutant DIFFERS first.

## 3. The runbook (design D3)

- [x] 3.1 `docs/publishing.md`: what must be true before the first push; the ORDER that makes
      SourceLink correct and why it is load-bearing; what nuget.org will not let you undo; the
      API-key and `dotnet nuget push` mechanics; the Umbraco Marketplace note; and that
      `No_document_claims_the_package_has_reached_a_feed` is EXPECTED to fail in the publishing
      change and must be updated, never deleted.
- [x] 3.2 Guard the runbook's load-bearing claims with `DocumentationAssert`.

## 4. Ordering dependency — CHRIS'S ACTION, not absorbed (design D2)

- [x] 4.1 Chris switches the remote:
      `git remote set-url origin git@github.com:Chris-N2/UBookIt.git` (or `remote add` if he
      keeps Azure as a mirror), then pushes.
- [x] 4.2 AFTER 4.1: clean rebuild and repack, then VERIFY the emitted
      `obj/**/*.sourcelink.json` names github.com and not dev.azure.com. This change cannot
      verify it before the remote moves — record the result here rather than assuming.

## 5. Verification

- [x] 5.1 Full suites green at Release, client green, Release `--no-incremental` 0 warnings.
      Recount at HEAD; never accept a `--no-build` run as evidence.
- [x] 5.2 `openspec validate --all --strict` clean.
- [x] 5.3 `dotnet pack -c Release` and inspect the produced `.nuspec`: `projectUrl` and
      `repository url` are the GitHub ones, `license` is the MIT expression.

## 6. QA

- [ ] 6.1 QA round(s) — fresh subagent; report claims to verify rather than trust; treat each
      round's fixes as new code.

## 7. Sync + archive

- [ ] 7.1 SYNC the ADDED `packaging` requirement into `openspec/specs/packaging/spec.md`.
      (This task first said "no delta to sync (no requirement changes)" — corrected with the
      proposal when the tooling refused a change with no delta; QA round 1 found the
      contradiction still standing here, which is the record disagreeing with the change it
      records.) Then run the falsified-sentence sweep BY
      PATTERN, wrap- and decoration-normalised, over the whole tree — **including the lines
      edited by hand**, which is the region a sweep skips (㉟ R5).
- [ ] 7.2 Memory: retire `azure-devops-not-github` — it will be false the moment 4.1 runs;
      update the ㉟ handover's outstanding-item section.
- [ ] 7.3 Archive; merge after QA approval.

## Observed during apply

**The proposal's "no requirement changes" was wrong, and the tooling caught it.**
`openspec validate` refused a change with no delta. The honest reading: the guard written
here asserts a property **no spec stated** — a mechanism with no guarantee behind it, the
shape this project keeps catching. So `packaging` gains a real ADDED requirement (the
package points a consumer at a public home; the publishing docs state the source-link
ordering and what a push makes permanent), and the proposal's claim was corrected rather
than worked around.

**`PackageCompositionTests.The_backoffice_client_is_built_by_the_build_and_not_by_the_developer`
failed once, then passed twice.** It spawns `dotnet pack` *inside a test*; the failure came
when it ran immediately after a `dotnet build` in the same command chain (MSBuild node reuse
holding locks), and it passed both in isolation (13s vs the 1m52s of the failing run) and in a
clean full suite. **Not dismissed as environmental** — ㉞'s rule is that a flaky full-suite
failure is real — but investigated to a cause outside the product: a test that shells out to
the build system is contending with the build system. Pre-existing since ⑳. Recorded as a
fragility, not a defect: worth an isolation guard if it recurs.

## 2.3 mutation evidence (at commit `8a2c6f6`, each mutant verified to DIFFER first, tree restored)

| Mutant | Result |
|---|---|
| `PackageProjectUrl` reverted to the Azure URL | **Caught** |
| `RepositoryUrl` downgraded to `http://` | **Caught** |
| URLs point at a different repository | **Caught** |
| Runbook's "Do not delete it" sentence removed | **Caught** |
| A 4th `nuget.org` mention against an allowance of 3 | **Caught** — the per-occurrence count works |

## 5.3 — the packed artifact, and the ordering claim PROVEN rather than argued

`dotnet pack -c Release`, then the `.nuspec` a consumer actually sees:

```
<id>UBookIt</id>  <version>17.0.0</version>
<license type="expression">MIT</license>
<licenseUrl>https://licenses.nuget.org/MIT</licenseUrl>
<projectUrl>https://github.com/Chris-N2/UBookIt</projectUrl>
<repository type="git" url="https://github.com/Chris-N2/UBookIt.git" commit="8a2c6f6…" />
```

**And from the SAME build, the symbol package's source links still name Azure DevOps:**

```
UBookIt.Core.sourcelink.json →
  https://dev.azure.com/NorwoodDesignDev/uBookIt/_apis/git/repositories/uBookIt/items?…
```

This is design D2 demonstrated rather than asserted: **the visible metadata is already correct
and the debugger metadata is still wrong**, because one comes from the properties this change
edited and the other from a remote only Chris can move. A package pushed from this commit would
look right on nuget.org and send every consumer's debugger to a repository they cannot open —
the same defect as the project URL, hidden one layer down.

**Task 4 is therefore a genuine blocker on publication, not a formality**, and 4.2's
verification is the step that proves it cleared.

## 4.1 / 4.2 — DONE, and the verification found something neither the design nor QA anticipated

Chris moved the remote to `https://github.com/Chris-N2/UBookIt.git` (HTTPS rather than SSH —
no key was registered, and Credential Manager's account prompt let him pick the right identity;
his GitHub login is otherwise a client's, so that choice mattered). Then, same commit, clean
rebuild:

```
before:  https://dev.azure.com/NorwoodDesignDev/uBookIt/_apis/git/repositories/uBookIt/items?…
after:   https://raw.githubusercontent.com/Chris-N2/UBookIt/ce36ba09…/*
```

**The ordering claim is now proven end to end** rather than half-proven: properties → remote →
rebuild, and both surfaces correct.

### The finding: SourceLink embeds the COMMIT SHA, so a pack from an unpushed commit ships dead links

Visible in the URL above. The consequence is a one-way door nobody had named — packing from a
local-only commit, an unmerged branch, or a commit amended after packing produces a package
whose source links **404 for every consumer, permanently**, because a pushed version cannot be
corrected. It would pass every check in this change: the URLs are right, the host is right,
the nuspec is right. Only the SHA is unreachable.

Added to `docs/publishing.md` as its own section with the two commands that check it
(`git rev-parse HEAD`, `git branch -r --contains HEAD`), and guarded. **Note this was found by
doing the verification rather than by reasoning about it** — the design predicted the remote
would fix SourceLink and stopped there; looking at the actual output showed what else the URL
carried.

## QA round 1 — REJECT (1 CRITICAL, 6 MAJOR, 5 MINOR, 3 NIT), and the fix (2026-09-15)

### The CRITICAL is mine, and its cause is a process failure already in memory

`38d37ea` recorded "Added to `docs/publishing.md` … and guarded" while containing **only
tasks.md**. Verified: the section and the guard were absent at HEAD, tree clean.

**Cause: I left the QA agent running and kept editing the same working tree.** QA's mutation
testing restores files with `git checkout --`, which destroyed those edits while they were
uncommitted; my subsequent `git add -A` then committed only the file QA had not touched. The
7/7 test run I cited as evidence passed genuinely — *before* the restore. This is
`never-git-add-all-while-an-agent-runs` exactly, repeated, and the correct structural answer is
the one that memory already gives: **do not edit a tree an agent is mutating, and commit before
any concurrent verification.** Re-applied at `6c28a5a` and verified present at HEAD before
continuing.

- [x] R1.1 [CRITICAL] Runbook section + guard re-applied and confirmed in the commit's diff.
- [x] R1.2 [MAJOR] `publishing.md` claimed the feed guard "asserts that no document claims
      uBookIt is on a feed". Narrowed to what it does — a fixed vocabulary over the documents
      it walks, with its own remarks' limit restated. **The predecessor's six-round fault
      (a claim exceeding its mechanism) reappearing in a new document one layer out.**
- [x] R1.3 [MAJOR] The URL guard used `Contains`, so `github.com/Chris-N2/UBookIt-fork`
      passed (QA's M6, reproduced). Now anchored: `^https://github\.com/Chris-N2/UBookIt(\.git)?$`.
      **Fixed by SHAPE** — any repository merely extending our path would have shipped.
- [x] R1.4 [MAJOR] The spec's reachability scenario required the docs to name the manual
      check; nothing enforced it (deleting the sentence left the suite green). Guarded.
- [x] R1.5 [MAJOR] Task 7.1 still said "no delta to sync (no requirement changes)" after the
      proposal had been corrected — the record disagreeing with the change it records.
      Corrected, with the history kept.
- [x] R1.6 [MAJOR] `openspec/config.yaml` said "Azure DevOps (not GitHub) for hosting/CI" —
      falsified by the remote move, **in-repo, non-archive, and injected into every future
      OpenSpec artifact**, so it would have propagated the false claim indefinitely. No guard
      reads it: `LiveDocuments()` walks `*.md`/`*.cs`/`*.cshtml`/`*.ts`, not `*.yaml`.
- [x] R1.7 [MAJOR] **`dotnet pack` is incremental and `--no-incremental` does not govern it**
      (QA reproduced: `Skipping target "GenerateNuspec" because all output files are
      up-to-date`). A `.nupkg` built before the remote moved survived the runbook's own
      ordering and would be matched by the push wildcard — the failure the document exists to
      prevent, inside its own procedure. A delete step is now step 3, with the reason stated.
- [x] R1.8 [MINOR ×4] The verify block reads one of five packages (now says check all five);
      the push-order sentence contradicted its own wildcard (now states the wildcard pushes the
      meta-package first and why that is harmless); "the same applies to the pre-release
      framing guards" was wrong — they will NOT go red at publication (corrected).
- [x] R1.9 The allow-list count moved 3 → 4 as the runbook grew. **Both directions bite**: the
      new text tripped the guard immediately, and a deliberate over-count of 5 failed the
      dead-allowance check. The count doing its job in both directions, unprompted.

Still open from QA and handled in round 2's scope: the hardcoded `17.0.0` in the runbook, the
retirement narrowing (old guard read the whole file, new one reads two element values), and
the NITs.

At HEAD: 2699 .NET (1484 + 130 + 1085), Release no-incremental 0 warnings, 21 items strict.
