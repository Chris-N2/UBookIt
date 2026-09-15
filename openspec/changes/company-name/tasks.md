# Tasks — company-name

## 1. The name

- [x] 1.1 `Directory.Build.props`: `Authors`, `Company`, `Copyright` → `Norwood Design &amp;
      Development Ltd.` (XML-escaped ampersand — a bare `&` fails the build).
- [x] 1.2 `LICENSE`: the copyright line names the correct entity. Plain text: literal `&`.
- [x] 1.3 `README.md`: licence footer matches. Plain text: literal `&`.

## 2. The guard (design D1–D3)

- [x] 2.1 Derive the name from `<Company>`, XML-decode it, assert `LICENSE` and `README.md`
      state it. Anti-vacuity: fail if the declaration is missing or empty.
- [x] 2.2 Assert `Authors` and `Company` agree — distinct NuGet fields, nothing else stops the
      listing and the assemblies disagreeing about the publisher.
- [ ] 2.3 Mutation-check against a COMMIT, each mutant verified to DIFFER first: change
      `<Company>` alone → fails naming the stale file; revert `LICENSE` alone → fails; make
      `Authors` disagree with `Company` → fails.

## 3. Verification

- [x] 3.1 Full suites green at Release, client green, `--no-incremental` 0 warnings. Recount at
      HEAD. Run `dotnet build` and `dotnet test --no-build` as SEPARATE steps — the recorded
      pack-contention flake only appears when one invocation does both.
- [x] 3.2 `openspec validate --all --strict` clean.
- [x] 3.3 **Pack and read the produced `.nuspec`** — `authors` and `copyright` must show the
      full legal name with a literal `&`. The props file is not the artifact; the artifact is.

## 4. QA

- [ ] 4.1 QA round(s) — fresh subagent, no editing of the tree while it runs (㉟ round 1's
      CRITICAL came from exactly that).

## 5. Sync + archive

- [ ] 5.1 Sync the ADDED requirement into `openspec/specs/packaging/spec.md`; falsified-sentence
      sweep BY PATTERN, wrap- and decoration-normalised, **including the lines edited by hand**.
- [ ] 5.2 Archive; merge after QA approval.

## 1.4 Scope addition, mid-apply (Chris, 2026-09-16)

- [x] The README footer links the company site, `https://www.norwood-development.co.uk`.

**Where it goes, and why NuGet has no other slot.** A `.nuspec` has exactly one project link
(`PackageProjectUrl`), and it is correctly the GitHub repository — an OSS consumer clicking
"Project website" wants the source and the docs, not a company page. `owners` is not rendered
by nuget.org (the listing shows the account that pushed). **But `PackageReadmeFile` is set, so
`README.md` ships inside every package and nuget.org renders it as the listing body** — a link
in the footer is therefore on the NuGet page, which is what was actually being asked for.

**Deliberately not guarded.** The publisher NAME is guarded because the licence depends on it
and five copies had already drifted. A company link is attribution, not a claim a consumer acts
on, and a guard asserting a URL is present would be the over-guarding this project keeps having
to unwind. Recorded so the asymmetry is a decision rather than an oversight.

**One property of the timing worth stating**: the README is packed INTO the package, so
`17.0.0`'s copy points at that URL permanently. The site is a placeholder today and fills in
this week (Chris) — fine, because the link target changes without the package changing. It
would NOT have been fine to link a URL that might move.
