# Tasks — release-17-0-0

## 1. The version itself

- [x] 1.1 `Directory.Build.props`: `<Version>0.1.0</Version>` → `17.0.0`.
- [x] 1.2 Verify the stamped manifest follows: build and confirm
      `wwwroot/App_Plugins/UBookItBackoffice/umbraco-package.json` carries `17.0.0`
      (the csproj replaces the `0.0.0` placeholder from `$(Version)` and fails the build
      if the placeholder is gone). `PackageCompositionTests` asserts it in the packed
      output — run it rather than assuming.

## 2. Version truth in the prose (design D1–D3)

- [x] 2.1 `README.md`: rewrite the `0.1.0` note for 17.0.0 — what the major means (tracks
      the Umbraco major, NOT a breaking-change signal), the departure from SemVer stated
      plainly, the breaking-change policy (minor, never patch; defaults or an upgrade
      path), and that leaving `0.x` is the stability promise. The dead "`1.0.0`" must be
      gone, not reworded around.
- [x] 2.2 `docs/mvp.md`: correct the `0.1.0` sentence; mark the document as the
      historical scope record it now is (design D3 declines folding it into the README —
      record the decline, it is instructed by the document itself).
- [x] 2.3 `CLAUDE.md` Conventions: the compatibility promise gains the minor-version
      rule, so the repository's own instructions are not the stalest account of the
      policy.
- [x] 2.4 `roadmap/version_roadmap.md`: mark the 17.0.0 row done.

## 3. Guards (design D1)

- [x] 3.1 A version-truth guard: parse `<Version>` from `Directory.Build.props` and
      assert every document that states a uBookIt version states THAT one. Derived, never
      hard-coded — a hard-coded `17.0.0` moves the drift rather than closing it.
      Anti-vacuity: the guard must fail if it finds no version claim at all.
- [x] 3.2 `DocumentationAssert` guards for the policy sentences a consumer acts on: the
      major is not a breaking-change signal, the SemVer departure, minor-not-patch, and
      the upgrade-path promise.
- [x] 3.3 Mutation-check both, against a COMMIT: bump the props version alone → 3.1
      fails naming the stale document; delete a policy sentence → 3.2 fails. Verify each
      mutant DIFFERS before trusting its verdict.

## 4. The deferred URLs (design D4) — NOT fixed here

- [x] 4.1 Record, and report to Chris, that `Directory.Build.props`'
      `PackageProjectUrl` / `RepositoryUrl` still name Azure DevOps and MUST be corrected
      before the package is published, once the GitHub URL exists. A blocker on
      publication, not on merge.

## 5. Verification

- [x] 5.1 Full suites green at Release, client suite green, Release `--no-incremental`
      build 0 warnings. Recount totals at HEAD for the QA handover — never reuse a
      previous count, and never accept a `--no-build` run as evidence (㉞'s lesson).
- [x] 5.2 `openspec validate --all --strict` clean.
- [x] 5.3 Pack and inspect: `dotnet pack -c Release` and confirm the produced packages
      carry `17.0.0` and the README, per the packaging spec's existing requirements.

## 6. QA

- [ ] 6.1 QA round(s) — fresh subagent, reused across rounds; report claims for it to
      verify rather than trust; treat each round's fixes as new code.

## 7. Sync + archive (after QA approval)

- [ ] 7.1 Sync the ADDED delta into `openspec/specs/packaging/spec.md`, then the
      falsified-sentence sweep wrap-normalised BY PATTERN over the whole tree — candidates
      to expect: anything saying uBookIt is pre-release, "0.1.0", "1.0.0", "not yet
      published", "the API may still move", or describing the roadmap as unfinished.
- [ ] 7.2 Update memory: the versioning note's "open question" (majors for non-LTS
      Umbraco) and the Azure DevOps note, which the GitHub move will stale.
- [ ] 7.3 Archive; merge after QA approval.

## Records

### 3.3 mutation evidence (at commit `074c126`, tree restored clean after each)

| Mutant | Result |
|---|---|
| `<Version>` → `17.1.0`, prose untouched | **Caught** — names README.md and both numbers |
| README's version claim reworded away (`uBookIt is at ...` removed) | **Caught** — anti-vacuity message says the claim is gone or reworded |
| "not a breaking-change signal" → "not a release signal" | **Caught** — policy guard names the missing sentence |
| "documented upgrade path" → "documented upgrade note" | **Caught** — policy guard names the missing sentence |

**One mutant was a no-op and its "pass" proved nothing** — a multi-line PowerShell
`Replace` against a CRLF file matched nothing, and the test passed because the file was
unchanged. Caught by printing whether the mutant differed, which is why that check is in
the loop. ㉞ recorded this lesson; it recurred within one change of being written down.

### 4.1 BLOCKER ON PUBLICATION, not on merge — the repository URLs

`Directory.Build.props` still carries:

- `PackageProjectUrl` → `https://dev.azure.com/NorwoodDesignDev/uBookIt`
- `RepositoryUrl` → `https://dev.azure.com/NorwoodDesignDev/uBookIt/_git/uBookIt`

Both are **private Azure DevOps URLs**, and the repository is moving to GitHub (Chris,
2026-09-14). They are deliberately NOT guessed here. **A package pushed to NuGet with
these uncorrected sends every consumer — and the Umbraco Marketplace listing — to a
repository they cannot open**, and NuGet.org does not allow a pushed version to be
edited: correcting it later costs a version number. The file's own comment names this as
the one place they change.

**Reported to Chris. Must be corrected in the same commit as, or before, the first push.**
