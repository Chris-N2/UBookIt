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
  The existing refusals — blank, control characters, backslashes, protocol-relative `//`, and every
  other scheme — are unchanged.
- **The resolver and the settings screen's validator both use that rule.** They remain two
  separate checks, as `site-settings` requires; they stop being two rules. The screen's refusal
  message names both accepted forms.
- **A guard over the seam**: for every input in the resolver's usable and unusable tables, the
  validator accepts exactly when the resolver resolves a link.
- **Both lines.** The touched files are identical on `main` and `dev/v18`; the change lands on both
  and is followed by patch releases `17.2.1` and `18.1.1` (their own release changes).

**Behaviour changes, both widenings of what is accepted, neither breaking:** a site-relative link
now resolves on Linux as it always did on Windows; the settings screen now accepts a site-relative
link it used to refuse. No value that was accepted before is refused now. No public signature
changes.

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

- **Changing what is refused.** The allow-list, and every refusal the current tests pin, stay
  exactly as they are.
- **Validating the link's target** (that the page exists, or is a privacy policy). The package does
  not render or validate a site's policy, per `privacy-notice`.
- **Making the Linux case detectable by a test run on Windows.** .NET's parsing difference cannot
  be reproduced on the dev machine; the fix removes the dependence *by construction* (a `/` value
  never reaches the parser), and the existing tests become the platform guard once CI runs them on
  Linux. See design.
- **The releases themselves** — procedure-driven, in `release-17-2-1` and `release-18-1-1`.

## Impact

- `src/UBookIt.Persistence/Composing/UBookItPersistenceComposer.cs` — the rule, extracted as an
  internal pure function the resolver calls.
- `src/UBookIt.Backoffice/Settings/SettingValidation.cs` — the `Url` kind calls the same rule
  (Backoffice already sees Persistence internals); message reworded; comment corrected.
- `tests/UBookIt.Tests/SiteSettingsTests.cs` and a validator/seam test.
- `docs/backoffice.md` — no change expected (it already states the two accepted forms, and becomes
  true on Linux); the release changes carry the CHANGELOG entries.
- Package bytes change (`UBookIt.Persistence`, `UBookIt.Backoffice`): a patch release on each line.
