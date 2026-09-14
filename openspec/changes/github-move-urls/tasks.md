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
- [ ] 2.3 Mutation-check against a COMMIT: revert either URL to the Azure one → fails; switch to
      `http` → fails; point at a different repository → fails. Verify each mutant DIFFERS first.

## 3. The runbook (design D3)

- [x] 3.1 `docs/publishing.md`: what must be true before the first push; the ORDER that makes
      SourceLink correct and why it is load-bearing; what nuget.org will not let you undo; the
      API-key and `dotnet nuget push` mechanics; the Umbraco Marketplace note; and that
      `No_document_claims_the_package_has_reached_a_feed` is EXPECTED to fail in the publishing
      change and must be updated, never deleted.
- [x] 3.2 Guard the runbook's load-bearing claims with `DocumentationAssert`.

## 4. Ordering dependency — CHRIS'S ACTION, not absorbed (design D2)

- [ ] 4.1 Chris switches the remote:
      `git remote set-url origin git@github.com:Chris-N2/UBookIt.git` (or `remote add` if he
      keeps Azure as a mirror), then pushes.
- [ ] 4.2 AFTER 4.1: clean rebuild and repack, then VERIFY the emitted
      `obj/**/*.sourcelink.json` names github.com and not dev.azure.com. This change cannot
      verify it before the remote moves — record the result here rather than assuming.

## 5. Verification

- [ ] 5.1 Full suites green at Release, client green, Release `--no-incremental` 0 warnings.
      Recount at HEAD; never accept a `--no-build` run as evidence.
- [x] 5.2 `openspec validate --all --strict` clean.
- [ ] 5.3 `dotnet pack -c Release` and inspect the produced `.nuspec`: `projectUrl` and
      `repository url` are the GitHub ones, `license` is the MIT expression.

## 6. QA

- [ ] 6.1 QA round(s) — fresh subagent; report claims to verify rather than trust; treat each
      round's fixes as new code.

## 7. Sync + archive

- [ ] 7.1 No delta to sync (no requirement changes). Still run the falsified-sentence sweep BY
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
