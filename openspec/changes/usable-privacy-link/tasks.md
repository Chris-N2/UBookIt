## 1. Branch and baseline

- [ ] 1.1 After the maintainer has pushed the proposal commit, branch `usable-privacy-link` from `main`; record `main`'s SHA and a clean-build baseline (unit / integration / rendering / client), and confirm `SiteSettingsTests` is fully green on Windows (it is expected to be: the defect is Linux-only)

## 2. The rule (design D1, D2)

- [ ] 2.1 Extract `internal static bool TryGetUsablePolicyLink(string? value, out string? link)` in `UBookItPersistenceComposer` with the D1 order (trim → blank → control/backslash → `/` branch → absolute http(s)); `ResolvePrivacyPolicyUrl` calls it. Keep the existing explanatory comments with the code they explain, and add one at the `/` branch stating why it precedes parsing (the Linux `file:` reading, with the CI run id). Verify: `SiteSettingsTests` green, unchanged
- [ ] 2.2 `SettingValidation`'s `Url` case calls the rule; refusal message "Must be an http or https address, or a site-relative path beginning with a single '/'."; replace the "mirrors the resolver" comment with one saying it calls the resolver's rule. Verify by build + the tests in 3

## 3. Tests (design D3)

- [ ] 3.1 Move the two resolver theories' data to shared `TheoryData` members so the seam test reads the same source; verify the resolver theories still report the same case count as before
- [ ] 3.2 Seam test: for every value in both sets plus a blank, `SettingValidation.IsValid(<PrivacyPolicyUrl descriptor>, value)` equals "the resolver yields a link". Mutation: restore the validator's absolute-only check → red on `/privacy` and `/legal/privacy-policy` on Windows. Mutation: restore the resolver's old order → green on Windows (expected — record it; it is why 5.1 exists)
- [ ] 3.3 Test that the refusal message names both forms; and that `/privacy` is accepted by the settings endpoint end to end (via `SettingsControllerTests`' harness), stored, and read back as the effective value
- [ ] 3.4 Mutation-check each refusal family still holds at the seam: temporarily drop the backslash rule → the seam test and `An_unusable_policy_link_resolves_to_none` go red; restore

## 4. Prose

- [ ] 4.1 Outward grep for the rule's vocabulary (`site-relative`, `absolute http`, `http or https`, `PrivacyPolicyUrl`, `mirrors the resolver`, `policy link`) across `docs/`, `openspec/specs/`, `src/`, `tests/`, `README.md`, the client dictionary; record each hit's verdict. `docs/backoffice.md`'s "An absolute `https://` URL or a site-relative path" is expected to be made true, not changed
- [ ] 4.2 `openspec validate --all --strict` passes; the change's ADDED requirement does not restate or contradict *The package states only what it can keep true, and the site links its own policy* (read both side by side and record)

## 5. Linux proof (design D4)

- [ ] 5.1 Rebase `continuous-integration` onto `usable-privacy-link` and push it; record the run URL; the three `SiteSettingsTests` cases that failed in run `35977150698` must now pass, the results check must be green, and the whole run must be green. If anything else fails, classify it per the CI change's D8 before proceeding

## 6. Handover, then both lines

- [ ] 6.1 Handover written here; QA subagent spawned per `CLAUDE.md` with: what is claimed, the CI run URLs to verify, and the instruction to treat each fix as new code
- [ ] 6.2 After QA approval and archive: fast-forward `main`; cherry-pick the fix commit(s) to `dev/v18`; rebuild the client after the branch switch; build + all four suites on `dev/v18`; record counts
- [ ] 6.3 Record for the release changes (`release-17-2-1`, `release-18-1-1`): the CHANGELOG entry text — a Linux fix and a settings-screen widening, both non-breaking, `SettingValidation` behaviour widened with no signature change
