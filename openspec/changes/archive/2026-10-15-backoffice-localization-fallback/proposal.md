## Why

A backoffice user whose language is anything other than US English sees uBookIt's **raw term
keys** — `ubookitResources_heading` and the like — on every uBookIt screen, instead of wording.
Chris reproduced it with a Spanish user. It has been true since the first release, on both lines.

The cause is one line of the client manifest. uBookIt registers its terms as a single
localization extension with `culture: "en-us"`. Umbraco loads, for the current user, only the
extensions whose culture is the user's regional culture, the user's language, or Umbraco's
fallback culture `en` (`UMB_DEFAULT_LOCALIZATION_CULTURE`). It then looks a term up in that
order. `en-us` is none of those three for anyone except an `en-US` user, so for everyone else
the dictionary is never consulted and every lookup misses. (It may well be loaded: the page boots
in the server's default UI language. Loaded is not consulted.)

That includes **British English**. Umbraco's "English (United Kingdom)" option *is* the culture
`en` (`Umb.Localization.EN`), so a UK user loads `en` and nothing else English. It does not
load `en-us`. So this is not only a defect for non-English sites; it is a defect for
most UK ones.

It is fixed in a patch because it is a defect with no change to any public surface.

## What Changes

- The client's one localization extension registers under Umbraco's fallback culture `en`
  instead of `en-us`. Every backoffice user whose language has no uBookIt translation of its
  own then reads English wording, never a key.
- An `en-US` user is unaffected. Their regional dictionary (`en-us`) no longer holds uBookIt's
  terms, and the lookup continues to the language dictionary `en`, which does.
- The extension's display name changes from "uBookIt English (United States)" to
  "uBookIt English". Its **alias stays `UBookIt.Localization.EnUS`**, so a site that refers to
  the extension by alias keeps working. The alias now names a culture it no longer registers
  under. That is a deliberate cost, accepted to keep the alias stable.
- The terms file keeps its name, `localization/en-us.ts`. The wording is US English, and the
  file name says so; only the culture it is registered under moves.
- The extension declares `weight: 200`, above Umbraco's default of 100. Sharing `en` means
  sharing a dictionary with any `en` wording a site supplies. Within one culture the dictionary
  registered last wins, and Umbraco registers in descending weight, so the higher weight makes a
  site's own `en` wording override uBookIt's rather than be overwritten by it (design D6; found
  by QA round 1).
- A new test fails if the manifest's localization culture ever differs from Umbraco's own
  fallback constant. It reads both from source, so a future Umbraco that changes the constant
  fails the test rather than silently re-breaking every non-`en-US` user.
- A handover to the release change (`release-17-3-2` / `release-18-2-2`), naming what its
  changelog entry must say. The version bump, changelog and publication are that change's, as
  they were for `17.3.1`.

No change is **BREAKING**. No public C# API, HTTP contract, view model, or schema changes.

## Non-goals

- **Translating uBookIt into any other language.** The terms stay English only. This change
  makes the English terms the fallback; it does not add `es`, `de`, `en-gb` or any other
  dictionary.
- **Renaming the alias or the file.** Both were weighed and kept (see What Changes).
- **Rewording the three existing spec sentences** that say the strings come from the package's
  localization "with `en-US` provided" (`booking-management` ×2, `resource-management` ×1). They
  stay true: the wording provided is US English. They sit inside large requirements, and
  replacing those wholesale to touch one clause risks dropping guarantees without anyone
  noticing. The promise that was missing is added as a new requirement instead.
- **The front end.** The shipped booking pages do not use the backoffice localization at all.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `resource-management`: ADDS one requirement — the backoffice's wording resolves for every
  backoffice language, falling back to English rather than showing a term key. No existing
  requirement is modified or removed.

## Impact

- `src/UBookIt.Backoffice/Client/src/section/manifest.ts` — the localization extension's
  `culture` and `name`.
- `tests/UBookIt.Tests/` — one new test reading the manifest and Umbraco's constant from
  `node_modules/@umbraco-cms/backoffice`.
- No `CHANGELOG.md` or version change here; both belong to the release change.
- **Both lines.** The manifest and terms file are identical on `main` and `dev/v18`. The change
  is ported to `dev/v18` by cherry-pick in the same sitting. On a v18 site the code that looks
  terms up is Umbraco 18's, not the 17.5.3 code read during explore, so v18 is proved by its own
  live check, not by analogy.
