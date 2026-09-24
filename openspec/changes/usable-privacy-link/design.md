## Context

See proposal.md — *Why*. Measured facts:

- `ResolvePrivacyPolicyUrl` (`UBookItPersistenceComposer`, internal) trims, refuses blank, refuses
  any control character or backslash, then tries `Uri.TryCreate(value, UriKind.Absolute, …)` and
  keeps the value only for `http`/`https`; otherwise it accepts a value starting with one `/` and
  not two.
- **On Linux, `Uri.TryCreate("/privacy", UriKind.Absolute, …)` succeeds with scheme `file`**, so the
  site-relative branch is never reached. Observed in CI run `35977150698`: `A_usable_policy_link_is_accepted("/privacy")`
  and `("/legal/privacy-policy")` return `null`; `A_usable_policy_link_logs_nothing` logs the
  error. On Windows the same call fails and the value falls through to the relative branch.
- `SettingValidation.IsValid` (Backoffice, public class) handles `SettingValueKind.Url` with
  "absolute, http(s) only", on every platform — so it refuses `/privacy`. `PrivacyPolicyUrl` is the
  only setting of that kind. It refuses blanks before the kind switch, as the resolver does.
- `UBookIt.Persistence` already declares `InternalsVisibleTo("UBookIt.Backoffice")`.
- Every touched file is identical on `main` and `dev/v18`.

## Goals / Non-Goals

**Goals:** one definition of *usable*, platform-independent by construction; both callers use it;
a guard that fails if they ever disagree.

**Non-Goals:** as proposal.md. No change to any refusal.

## Decisions

### D1. Decide `/`-prefixed values before any URI parsing

The rule, in order: trim → blank refused → control character or backslash refused → **if it starts
with `/`: usable iff it does not start with `//`** (never parsed) → otherwise usable iff
`Uri.TryCreate(…, UriKind.Absolute, …)` succeeds with scheme `http` or `https`.

Moving the relative branch ahead of the absolute parse is the whole fix. The platform differences
in .NET's absolute parsing concern file-path forms. Rooted paths (`/…`) no longer reach the parser.
UNC and drive paths written with `\` are refused before it by the backslash rule. What remains,
such as `C:/x` (a `file:` URI on Windows, a URI with scheme `c` on Linux), reaches the parser but
is refused by the scheme check on both. So the answer can differ between platforms only if some
value parsed as `http`/`https` on one and not the other, and nothing here suggests one does. That
is an argument, not a proof, which is why the Linux CI run (D3, D4) is the actual guard.

*Alternative:* keep the order and additionally treat a `file:` result as "try the relative branch".
Rejected — it keeps the platform's opinion in the decision path and adds a special case that must
be remembered for every future parser quirk. The backslash refusal already set the precedent of
removing the question rather than depending on the platform to answer it consistently.

### D2. One internal pure function, two callers

`internal static bool TryGetUsablePolicyLink(string? value, [NotNullWhen(true)] out string? link)`
in `UBookItPersistenceComposer`, returning the trimmed value. `ResolvePrivacyPolicyUrl` becomes
"read configuration, call the rule". `SettingValidation`'s `Url` case calls the same function.

Internal, not public: `UBookIt.Persistence`'s public surface is frozen from `17.0.0`, and a new
public member is a compatibility promise nobody asked for. `InternalsVisibleTo` already exists for
exactly this caller.

The `site-settings` requirement that validation and resolution "remain separate checks" is kept:
there are still two call sites at two moments (write, and read-with-fallback). What stops is their
having two rules.

The validator's error becomes: *"Must be an http or https address, or a site-relative path
beginning with a single '/'."* Its comment claiming to mirror the resolver is replaced by one that
says it *calls* the resolver's rule, which is checkable.

### D3. Tests, and why the Linux guard is CI rather than a Windows test

- The existing resolver theories stay as they are — they are the specification of the rule, and
  on Linux they already fail against the old code (measured).
- **Seam test**: every value from both resolver theories (usable and unusable), plus a blank, fed
  to `SettingValidation.IsValid` for the `PrivacyPolicyUrl` descriptor; assert
  `IsValid == (resolved link is not null)`. Built from the same data source as the resolver
  theories (shared `TheoryData`), not a copied list, so a value added to one is automatically
  tested at the seam. Mutation: restore the validator's old absolute-only check → the seam test
  must fail on `/privacy` **on Windows**, because the validator's refusal of a relative path is
  platform-independent.
- **Refusal message** test: names both forms.
- **The Linux-only failure cannot be reproduced on the dev machine.** .NET exposes no switch to
  make Windows parse rooted paths as `file:` URIs. A test that *simulated* it would test the
  simulation. The honest guard is the three existing tests running on Linux — i.e. the
  `continuous-integration` workflow. Apply therefore verifies this change on Linux by running it
  through CI (D4), and records the before/after run URLs.
- A structural test that the rule does not call `Uri.TryCreate` before the `/` decision was
  considered and rejected: it would assert the mechanism (source order) rather than the guarantee,
  and the Linux run asserts the guarantee directly.

### D4. Sequencing with `continuous-integration`

`main` has no workflow yet (CI is paused on its own branch). So:

1. Branch `usable-privacy-link` from `main`; apply; local suite green on Windows.
2. **Linux proof:** rebase the `continuous-integration` branch onto `usable-privacy-link` and push
   it; its run must show the three previously failing tests passing and everything else green.
   (Rebasing, not merging, keeps CI's branch a straight line on top of the fix it depends on.)
3. QA on `usable-privacy-link`; archive; fast-forward `main`; cherry-pick to `dev/v18`.
4. `continuous-integration` resumes on top of the new `main`.
5. The patch releases follow **after** `continuous-integration` lands, so `17.2.1` and `18.1.1` are
   the first releases whose commits were verified unattended on Linux. This is a recommendation;
   the maintainer may release sooner — nothing in this change depends on the order.

## Risks / Trade-offs

- [Accepting a site-relative link on the settings screen widens what an editor can store] → it is
  exactly the set the resolver has always accepted and the documentation has always offered; the
  dangerous forms (`//`, `\`, control characters, other schemes) are refused before the branch.
- [The rule's trim now also applies at validation] → the validator stores the raw submitted text;
  resolution trims it. A value with surrounding spaces was already usable at resolution, so
  accepting it at the screen is the agreement the seam test asserts. Stored bytes are unchanged.
- [`SettingValidation` is a public class whose behaviour changes] → no signature changes; the
  change is a widening; called out in the CHANGELOG entry of the release changes.
- [The Windows suite cannot see a regression of the Linux defect] → accepted, D3. CI on Linux is
  the guard, which is the argument for landing `continuous-integration` before the releases.

## Migration Plan

None. A widening; no stored data changes. Rollback is reverting the commit.
