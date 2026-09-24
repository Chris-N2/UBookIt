## Why

The first run of the `continuous-integration` change, on a clean Linux runner, found that **a
site-relative privacy policy link is refused on Linux hosts**. `ResolvePrivacyPolicyUrl` asks
`Uri.TryCreate(value, UriKind.Absolute, …)` first, and on Linux .NET reads `/privacy` as an
absolute `file:` URI; the http(s) allow-list then refuses it. A Linux-hosted site configuring
`UBookIt:PrivacyPolicyUrl` as `/privacy` — the exact example `docs/backoffice.md` gives — gets
no policy link on its booking form and an error at every startup. Bookings are unaffected. The
defect is in every tagged release on both lines (since `5e04fb7`, privacy-notice); three existing
tests catch it, but only on Linux, which is why nothing had.

Reading the code for the fix found a second, platform-independent mismatch on the same setting:
**the settings screen refuses every site-relative link** ("Must be an absolute http or https
address") while the resolver and the documentation accept one. Its comment says it "mirrors the
resolver"; it does not. An editor can set the documented form only through configuration.

Both are the same question — *is this a usable policy link?* — answered in two places, differently
on two platforms and differently in two layers. This change answers it once.

## What Changes

- **One rule for a usable policy link**, decided without platform-dependent parsing: a value
  beginning with `/` is judged as a site-relative path (one leading slash, not two) and **never**
  handed to absolute-URI parsing; any other value must parse as an absolute `http` or `https` URI.
  The site's existing refusals — blank, control characters, backslashes, protocol-relative `//`,
  and every other scheme — are unchanged; the settings screen gains the ones it lacked (see
  *Behaviour changes*).
- **The resolver and the settings screen's validator both use that rule.** They remain two
  separate checks, as `site-settings` requires; they stop being two rules. The screen's refusal
  message names both accepted forms.
- **A guard over the seam**: for every input in the resolver's usable and unusable tables, plus
  padded usable values, the validator accepts exactly when the resolver resolves a link.
- **Both lines.** The touched files are identical on `main` and `dev/v18`; the change lands on both
  and is followed by a release on each line, in their own release changes: **patch releases
  `17.2.1` and `18.1.1` if the maintainer's decision in tasks.md 6.3 finds the settings screen's
  narrowing patch-eligible, otherwise minors `17.3.0` and `18.2.0`.**

**Behaviour changes — two widenings and one narrowing:**

- **The resolver (what the site renders) only widens.** A site-relative link now resolves on Linux
  as it always did on Windows. No value the site used before is refused now.
- **The settings screen widens:** it accepts a site-relative link it used to refuse.
- **The settings screen also NARROWS** — found by QA, and the proposal first claimed the opposite.
  Its old check was "parses as absolute, scheme is http(s)", with no control-character or
  backslash rule, so it accepted and stored values such as `https:\\evil.example/x` (which .NET
  parses as `https://evil.example/x` — an off-site https link) or an http(s) URL with an interior
  tab or NUL. The site has
  always refused every one of those, so the screen was storing links that silently never rendered.
  It now refuses them. `SettingValidation.IsValid` is public, so this is a narrowing of a public
  method's behaviour, with no signature change.

**The case offered for treating the narrowing as non-breaking** — an argument for the maintainer's
decision, not the decision: it refuses only values the site already treated as absent. A site that
stored one would see no change on its public pages — no link before, no link after. What would
change is that an editor typing one is told, which is what `site-settings` asks of validation
("stops the screen creating a value that would silently fall back"). **Patch-eligibility is the
maintainer's decision, recorded in tasks.md 6.3**, because `CLAUDE.md` puts a break in a minor,
never a patch, and this must be decided on the record rather than implied.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `privacy-notice`: gains a requirement stating which policy links are usable, that the answer is
  the same on every hosting platform, and that the settings screen and the resolver give the same
  answer. Added as a new requirement, not by modifying an existing one — the existing requirement
  says an unusable link is treated as absent and does not define "usable", so nothing in it is
  replaced.

## Non-goals

- **Changing what the SITE refuses.** The allow-list, and every refusal the resolver's tests pin,
  stay exactly as they are. (The settings screen's refusals do change, deliberately, to match the
  site's — see *Behaviour changes* above.)
- **Validating the link's target** (that the page exists, or is a privacy policy). The package does
  not render or validate a site's policy, per `privacy-notice`.
- **Making the Linux case detectable by a test run on Windows.** .NET's parsing difference cannot
  be reproduced on the dev machine; the fix removes the dependence *by construction* (a `/` value
  never reaches the parser), and the existing tests become the platform guard once CI runs them on
  Linux. See design.
- **The releases themselves** — procedure-driven, in their own release changes, numbered per the
  6.3 decision.

## Impact

- `src/UBookIt.Persistence/Composing/UBookItPersistenceComposer.cs` — the rule, extracted as an
  internal pure function the resolver calls.
- `src/UBookIt.Backoffice/Settings/SettingValidation.cs` — the `Url` kind calls the same rule
  (Backoffice already sees Persistence internals); message reworded; comment corrected.
- `tests/UBookIt.Tests/SiteSettingsTests.cs` and a validator/seam test.
- `docs/backoffice.md` — no change expected (it already states the two accepted forms, and becomes
  true on Linux); the release changes carry the CHANGELOG entries.
- Package bytes change (`UBookIt.Persistence`, `UBookIt.Backoffice`): a release on each line —
  patch or minor per the maintainer's decision in tasks.md 6.3.
