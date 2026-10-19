## 1. Content (gate: supplied by Chris)

- [x] 1.1 Obtain from Chris a one- or two-sentence description of Norwood Design & Development Ltd. and the website URL. The logo is already chosen: `norwood-appicon-512.png` (512×512 PNG) from Chris's brand folder `D:\Documents\Work\Business Development\Branding\final_branding\Norwood Design Logo Concepts_Final\brand\png\`. Verify: the description and URL are in hand. Apply stops here until they are, and nothing is invented in their place.

## 2. The listing file

- [x] 2.1 Copy `norwood-appicon-512.png` byte-for-byte to `docs/images/ndd-logo.png` on a feature branch from `main`. Verify: the SHA-256 hashes of the source and the copy match, and `git ls-files docs/images/ndd-logo.png` lists it.
- [x] 2.2 Create `umbraco-marketplace.json` at the repository root. It holds `$schema` (`https://marketplace.umbraco.com/umbraco-marketplace-schema.json`) and `AuthorDetails` with `Name` `Norwood Design & Development Ltd.`, the supplied `Description`, `Url`, and `ImageUrl` `https://raw.githubusercontent.com/Chris-N2/UBookIt/main/docs/images/ndd-logo.png`. Use the Write tool, not a heredoc. Verify: the file parses (`Get-Content umbraco-marketplace.json -Raw | ConvertFrom-Json`) and has exactly those two top-level fields.

## 3. The guard

- [x] 3.1 Add a test class in `tests/UBookIt.Tests` covering every scenario of the new `packaging` requirement. The author name is compared with `<authors>` from the **packed** meta-package nuspec via `PackedSolution` (design Decision 1). Top-level fields are checked against the allow-list `$schema` and `AuthorDetails` (Decision 2). `Url` and `ImageUrl` must be absolute `https` URLs, and `ImageUrl` must end in `.png`/`.jpg`. An `ImageUrl` under this repository's `main` raw URL must resolve to a file in the repo. A missing file fails. Assertions take the parsed document as a parameter. Verify: the class passes against the real file.
- [x] 3.2 Prove each assertion can fire (design Decision 5). Run it against an in-memory or temp-directory mutation, for each of: a changed name, an `http://` `Url`, a `.gif` `ImageUrl`, a non-existent image path, an added `Title`, and the file absent. The real file is never edited. Verify: every mutation turns its assertion red with a message naming the field or path. Record the six observed failures in the QA handover.

## 4. Documentation

- [x] 4.1 Extend the Marketplace section of `docs/publishing.md` (around line 504). Say that the listing's author details come from the root `umbraco-marketplace.json`. Say that the Marketplace reads that file from `main` at its own sync, not from a package, so changing it needs no release. Say that the file is deliberately not on `dev/v18` and why. Say what to do if the listing has not updated (the single-package sync endpoint). Do not claim the listing has changed. Verify: the text covers all four points of the spec's documentation scenario, and the existing documentation tests still pass.

## 5. Verification and handover

- [x] 5.1 Run the full unit suite. Verify: green, and the count equals the `main` baseline (2185) plus the new tests, with no skips.
- [ ] 5.2 Run `openspec validate marketplace-author-details --strict` after the last commit. Verify: it passes.
- [x] 5.3 Record in the deferred obligations: the Trusted Publishing owner documentation (`docs/publishing.md:224`, `publish.yml:185` `NUGET_USER`) must be corrected and observed at the next release, the floor raise. Also: personal ownership of the packages should not be removed until that release has proven the organisation policy. Verify: the entry exists.
- [ ] 5.4 Hand over to a fresh QA subagent (`qa-review`), with build state, test counts and the mutation results stated as claims to verify, not facts.

## 6. After merge (outside the repo, not claimed before it happens)

- [ ] 6.1 After the PR merges to `main` and the Marketplace has synced, check `https://marketplace.umbraco.com/package/ubookit`. Verify by observation: the logo replaces the placeholder avatar, and the description and website appear. Record what was seen in `docs/publishing.md`. Do not expect the "Explore more packages by CNorwood69" link to change; it follows NuGet ownership (design, Risks). Record its state as observed.
