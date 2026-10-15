## Context

See proposal.md — Why. Umbraco's lookup code, read in the installed
`@umbraco-cms/backoffice` 17.5.3 (`dist-cms`):

```
packages/core/localization/registry/localization.registry.js
  loads the localization extensions whose culture (lower-cased) is one of:
    the user's base name  ·  the user's language  ·  'en' (always)
  e.g. a user on a regional culture such as 'es-mx': 'es-mx', 'es', 'en'
  registers them in DESCENDING weight (:141), default weight 100 (:86)

libs/localization-api/localization.controller.js:81-100
  term(key):  primary (base name)  →  secondary (language)  →  fallback ('en')

libs/localization-api/localization.manager.js
  UMB_DEFAULT_LOCALIZATION_CULTURE = 'en'
  registerLocalization: a second dictionary with the same code is MERGED key by key (:44-46),
  so within one culture the dictionary registered LAST wins any key both define
```

What changes for each kind of user. "Consulted" is the lookup chain. Loaded dictionaries also
persist across a language change, so `en-us` is often loaded for everyone (the page boots in the
server's default UI language, `en-US` here). Loaded is not the question; consulted is.

| User language | Dictionaries consulted          | uBookIt terms today | Under `en` |
|---------------|---------------------------------|---------------------|------------|
| `en-US`       | `en-us`, `en`                   | `en-us` ✓           | `en` ✓ (second step) |
| UK (`en`)     | `en`                            | **keys**            | `en` ✓     |
| Spanish (`es`)| `es`, `en`                      | **keys**            | `en` ✓ (fallback) |

Umbraco itself registers both `en` and `en-US` (`core/localization/manifests.js:78,85`), so `en`
is a culture Umbraco expects packages to use. Its `en` **is** the "English (United Kingdom)"
option a user picks (`Umb.Localization.EN`, :76-78); there is no `en-GB` in Umbraco's list. A
site whose default UI language is set to `en-GB` in configuration would load `en-gb` and `en`,
which the fix also covers.

## Goals / Non-Goals

**Goals:** the requirement in `specs/resource-management/spec.md`; a guard that fails if the
registration drifts from Umbraco's fallback; both lines in one sitting.

**Non-Goals:** in proposal.md. One more at design level: no DOM test environment is introduced
to exercise the lookup end to end. The live check is the end-to-end proof. That debt is already
recorded and is not this change's.

## Decisions

### D1 — Register once, under `en`; do not also keep `en-us`

The one extension's `culture` becomes `"en"`. A second extension under `en-us` pointing at the
same file was considered and rejected. It adds nothing, because `en` is always loaded and is
the second step for an `en-US` user anyway. And it would put the same keys in two
dictionaries, so a site overriding wording under `en` would see its override ignored by
`en-US` users, which is surprising.

### D2 — Keep the alias `UBookIt.Localization.EnUS` and the file name `en-us.ts`

Decided with Chris during explore. The alias is how a site would unregister or replace the
extension. It is not in the C# public API, but changing it would break such a site silently,
and the package has a fair number of downloads. The name now names a culture it does not
register under. The `name` (display) changes to "uBookIt English". The file keeps its name
because about 20 tests and docs read it by path, and its contents really are US English.

### D3 — Merging into Umbraco's own `en` dictionary is safe

Because same-code dictionaries merge key by key, ours joins Umbraco's `en` rather than
replacing it. Our top-level sections are all prefixed `ubookit…`, except `user`, which holds one
key of our own (`permissionsEntityGroup_ubookit`) that Umbraco does not define. That is the
same exposure the `en-us` registration already had against Umbraco's `en-US` dictionary. So no
Umbraco key is overwritten. The guard in D4 does not check this; the live check does, by
looking at Umbraco's own screens as well as ours.

`$dir` and `$weight` are also merged: ours sets `ltr`, as Umbraco's `en` does. Weight is not
inert: within one culture it decides which dictionary registers last and so wins a shared key.
See D6. *(Corrected, QA round 1: this paragraph first said weight "has no effect on lookup",
which was false.)*

### D6 — Weigh above the default, so a site's own `en` wording wins

Sharing `en` means sharing a dictionary with any `en` wording a site registers, typically to
reword uBookIt (or, before this fix, to work around it for UK users). Within one culture the
LAST dictionary registered wins a shared key. Umbraco orders localizations by weight **twice**:
the extension registry's `byType` sorts by `weight || 0` (`extension.registry.js:85`, an ABSENT
weight counting as 0), and the localization registry then sorts by `weight ?? 100` (`:86`,
`:141`, stable on ties). So without a weight of its own, uBookIt and a site's weightless
dictionary tie under both sorts, and **which one registers last, and wins, is decided by the
order the extensions arrived in**, which neither uBookIt nor Umbraco promises. A site's override
might win on one load and lose on another. Under `en-us` that collision did not exist for a site
using `en`. *(Found by QA round 1, derived from Umbraco's source. QA round 2 corrected the
mechanism: round 1 asserted that uBookIt's bundle always registers after a site's and so always
overwrites it. That was never observed; see 8.4.)*

So the extension sets `weight: 200`, above Umbraco's default of 100. It then sorts first under
BOTH orderings (200 beats an absent weight's 0, and beats `?? 100`), so it registers before every
default-weight `en` dictionary, the site's and Umbraco's own, and any of them overrides
uBookIt's terms on a shared key. That is the right priority for a package's defaults, and it
also means uBookIt can never overwrite an Umbraco key (strengthening D3).

Rejected alternative: keep the default and tell sites to give their override a weight below
100. Every site would carry that burden, and getting it wrong fails silently. Chris chose the
weight, 2026-10-14.

The guard (D4) holds the weight above Umbraco's default, read from Umbraco's source
(`extension.weight ?? N` in the registry), for the same reason it reads the culture from there.

### D4 — The guard compares two source files, in the C# test project

The client has no DOM test environment, and importing Umbraco's localization manager runs code
that touches `document`. So a vitest cannot import the constant. The test lives in
`tests/UBookIt.Tests`, next to `ClientLocalizationKeyTests`, and reads text:

- the culture from the localization extension in `Client/src/section/manifest.ts`. It must find
  **exactly one** `type: "localization"` entry, and fail if it finds none, so that a renamed
  file or reshaped manifest cannot make it pass vacuously;
- `UMB_DEFAULT_LOCALIZATION_CULTURE` from
  `Client/node_modules/@umbraco-cms/backoffice/dist-cms/libs/localization-api/localization.manager.js`.
  If that file or the constant is missing, the test **fails**; it does not skip. CI runs
  `npm ci` before `dotnet test`, so the file exists there.

It asserts the two are equal (case-insensitively, as Umbraco lower-cases both), naming both in
the failure message. Comparing against Umbraco's constant rather than the literal `"en"` means a
future Umbraco that changes its fallback fails this test instead of quietly re-breaking every
non-`en-US` user.

It is proved able to fire before it is trusted: with the culture set back to `"en-us"` it
fails, and with the manifest's localization entry removed it fails.

A second test in the same class holds the weight (D6) above Umbraco's default, read from the
registry's `extension.weight ?? N`. Proved at its boundary: weight removed fails, `100` fails,
`101` passes.

**On dev/v18 the guard reads 17.5.3's constant.** That line's client still depends on
`@umbraco-cms/backoffice` `^17.5.3` (unchanged since the scaffold), while an 18 site runs
Umbraco 18's backoffice code at runtime. So on v18 the guard proves the registration matches
the package the client is *built* against, not the code that *runs*. The v18 live check covers
the runtime. Whether v18's client should build against the 18 package is a separate question,
recorded as an observation, not settled here.

## Risks / Trade-offs

- [Umbraco 18's lookup differs from 17.5.3's] → The v18 live check runs on the 18 TestSite with
  Spanish and UK English (`en`) users, not inferred from the 17 reading.
- [A key of ours collides with an Umbraco `en` key] → D3; and the live check looks at Umbraco's
  own Users section and a content screen as an `en-US` user, where a collision would show.
- [The alias is now misleading] → Accepted (D2); a comment beside it says why it was kept.
- [A browser holds the old bundle after upgrade] → The manifest lives in a content-hashed chunk
  (`bundle.manifests-<hash>.js`), but it is reached through the entry file
  `u-book-it-backoffice.js`, which is **not** hashed (`vite.config.ts` fixes its name). Whether
  a cached entry is re-fetched after upgrade has not been verified. The live check therefore
  loads the backoffice with a normal reload, not a hard one, and the release changelog tells an
  operator who still sees keys to hard-refresh once.

## Migration Plan

No data or configuration migration. A site upgrades the package and reloads the backoffice.
Rollback is installing the previous patch, which restores the old (broken) behaviour.
