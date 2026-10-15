## Context

`BookingFlowViewComponent.InvokeAsync` resolves a `FlowEntry` from an author-supplied id or the
`ubBook` query. With no subject it renders `View("Catalogue", await catalogue.BuildAsync(preserved))`.
`BookingCatalogue` already composes the entries (every service, plus every `DirectlyBookable`
resource, in one ordering). The packaged Booking Page template calls the component with no
arguments, so the no-subject path is what every packaged site's visitors see first.

`FlowEntry` distinguishes an author-named subject (`NamedByAuthor: true`, no token) from one
carried by the URL (`false`, token carried into every later step's form, and into the
Post-Redirect-Get target). The user settled that a skipped catalogue is the second kind.

Editable settings live on `SiteBookingSettings` (Core), which is **scoped** and resolved through
`UBookItPersistenceComposer.EffectiveConfiguration`, so a stored value wins and takes effect on the
next request. `FrontendSettings` is a singleton bound from configuration only and is not
store-composed. That decides where the new setting can live (D3).

## Goals / Non-Goals

**Goals:**
- A no-subject entry with exactly one catalogue entry renders that entry's flow, carrying its
  token, unless the site turns this off.
- The switch is an editable backoffice setting, default on, resolved exactly as `AutoConfirm` is.
- No published signature changes; no new view, model, theme contract entry, endpoint or migration.

**Non-Goals:**
- See the proposal. In particular: no delivery API change, no availability-based counting, and
  no change to the legacy `Booking` component.

## Decisions

### D1. The decision is made in the dispatcher, on the path that already builds the catalogue

On the no-subject path the dispatcher already calls `BuildAsync`. After building, if the setting is
on and `Entries.Count == 1`, it dispatches to the sole entry's flow with that entry's
`Subject.Token` as the flow token, through the same `ServiceAsync` / `ResourceAsync` methods a
`ubBook` query reaches. Otherwise it renders the catalogue as today.

The rule itself ("given this catalogue and this setting, which subject, if any?") is a **pure
function**, kept **internal** and unit-tested directly, for the reason `FlowEntry` gives: a
ViewComponent needs a host to exercise, and the 0 / 1 / 2 / setting-off matrix should not need one.
`UBookIt.Web` already grants `InternalsVisibleTo` to both test projects. Suggested shape:
`internal static BookingSubject? SoleChoice(CatalogueModel catalogue, bool skipSingleChoice)`. It
should sit beside `FlowEntry`, not on it: `FlowEntry` is public, and an internal static member on a
public type is fine, but it reads as part of a surface it is not.

*Alternatives rejected:*
- **Change `FlowEntry.Resolve` to take the catalogue.** That changes a published signature, and an
  overload has bitten this project before (2026-10-11).
- **Count inside `BookingCatalogue`.** That class composes what is bookable. Whether to show it is a
  separate question, and `BuildAsync` is published.
- **A 302 to `?ubBook=…`.** A ViewComponent renders inside the host page and cannot redirect it.
  Doing it in middleware or the template would be a second dispatcher.

### D2. "Chosen", not "author-named": the token is carried

The skipped entry is dispatched with the token, as a `ubBook` query would be. So the date step's
GET form, the POST's `Subject` field and the PRG redirect all carry `ubBook=…`. Consequences:
- Every step after the first is linkable and survives the site later adding a second thing.
- The first URL (`/book`) carries no subject. A visitor who goes back to it is skipped again, to the
  same flow. There is no loop, because no flow view links back to the catalogue.
- The confirmation is reached via a PRG redirect that names the subject, so it is read in the flow
  branch and never at the catalogue branch. This is the case `BookingSurfaceController.cs:143`
  warns about ("land the visitor back at the catalogue, losing the confirmation").

### D3. The setting lives on `SiteBookingSettings`, key `UBookIt:SkipSingleChoiceCatalogue`

The user chose the editable tier: what a visitor is shown is communication, under the documented
rule. Editable means store-composed, and only `SiteBookingSettings` is. `FrontendSettings`' remark
says render concerns stay out of Core, but `PrivacyPolicyUrl` is already a render concern on
`SiteBookingSettings`. The deciding constraint is "editable", and only one type can meet it.

- Property: `public bool SkipSingleChoiceCatalogue { get; init; } = true;`. An additive init
  property with a default, so no published constructor or `with` expression changes meaning.
- Key: top-level `UBookIt:SkipSingleChoiceCatalogue`, like `UBookIt:AutoConfirm`. It is not put
  under `UBookIt:Frontend`, because that section is bound by a different, config-only, singleton
  type. A key there that silently obeyed the store while its neighbours ignored it would mislead
  anyone reading the section.
- Resolution, mirroring `AutoConfirm` exactly: `IsSkipSingleChoiceCatalogueConfigured` (presence),
  `ResolveSkipSingleChoiceCatalogue` (`!bool.TryParse(...) || value`), and
  `ErrorIfSkipSingleChoiceCatalogueUnreadable` added to `RunUBookItMigrations.Report`.

*Name, open to veto at review:* `SkipSingleChoiceCatalogue` says what the setting does when it is
true, which is the polarity the screen's checkbox shows. The rejected alternative,
`ShowSingleChoiceCatalogue` defaulting to false, would mean an upgraded site's stored `true`
changes behaviour in the opposite direction from everything else on the screen.

### D4. Getting the setting into the component without changing its published constructor

`BookingFlowViewComponent`'s constructor is public. Add a **second** public constructor that also
takes `SiteBookingSettings`, marked `[ActivatorUtilitiesConstructor]` so that MVC's
ViewComponent activator (which goes through `ActivatorUtilities`) selects it. Keep the existing
constructor. It has no settings, so it behaves as the default (skip on), which is what a
hand-constructed component on a site with no stored or configured value would get anyway.

**Apply must prove the activator picks the new constructor** through a real host render, not by
reading the attribute: a rendering test with the setting **off** and one catalogue entry must render
the catalogue. If the old constructor were chosen, the default (on) would skip it, so this test
fails when activation is wrong. Add the old constructor's shape to `PublishedCallShapeTests`.

*Alternative rejected:* resolving `SiteBookingSettings` from `HttpContext.RequestServices` inside
`InvokeAsync`. It hides the dependency from the constructor, where every other one is declared. It is
the fallback only if `ActivatorUtilitiesConstructor` turns out not to be honoured, and the
rendering test above is what would reveal that.

### D5. Backoffice: catalogue entry, setting text, two terms

- `SettingCatalogue.All`: `new(UBookItPersistenceComposer.SkipSingleChoiceCatalogueSettingKey,
  SettingTier.Editable, SettingValueKind.Boolean)` in the operator's block.
- `SettingText`: map the key to `Bool(settings.SkipSingleChoiceCatalogue)`.
- `en-us.ts`: `skipSingleChoiceCatalogueLabel` / `…Description`. Proposed copy:
  *"Skip the catalogue when there is only one choice"* / *"On, a visitor to the booking page goes
  straight to the only service or resource on offer. Off, they choose it from a list of one,
  which suits a page whose own text explains the choice."* The screen renders booleans
  generically. `settingSlug` derives `skipSingleChoiceCatalogue`, which the terms must match. The
  localization fallback registers `en-us` terms for `en` too, so no second file is needed. Confirm
  the existing term-key guard covers the new pair.

### D6. Cost

There are no extra reads. The no-subject path already builds the catalogue, and the skip then runs
the chosen flow's reads, which a `ubBook` request would have run anyway. One request does both, once.

### D7. Themes

The theme contract is unchanged, and a theme's `Catalogue` view is still used whenever the
catalogue renders. With the setting on, it is not rendered for a catalogue of one. `docs/theming.md`
gets one sentence so a theme author is not surprised. The package claims nothing about themed markup
in either direction (invariant 5), and the visitor is still told what they are booking by whichever
flow view renders, themed or not.

## Risks / Trade-offs

- **[Upgraded sites change behaviour]** A site with one entry stops showing its catalogue on
  upgrade, and Chris's own site is one. → Ship in a minor. The release entry's *What you have to
  do* names the setting and how to turn it off on the screen or in configuration. Recorded as a
  task for the release change, since changelog entries are written there.
- **[The activator chooses the wrong constructor]** The skip would then ignore the setting. → The
  setting-off rendering test (D4) fails in exactly that case.
- **[A catalogue that becomes one entry mid-flow]** A visitor on a skipped flow whose site gains a
  second entry keeps working, because their URLs carry the token (D2). The reverse case, where the
  catalogue was showing two and drops to one, still works: the visitor chose from the list, and
  their choice is in the URL.
- **[`configuration.md` says an upgrade "behaves exactly as it did before"]** That sentence, at line
  26, is about the settings store being empty, and it stays true of the store. But the next sentence
  a reader meets will no longer be true of this release as a whole. → The docs task rereads it and
  qualifies it if needed.
- **[Port]** `dev/v18` needs the same change. Last time a fix reached only one line; port in the
  same sitting and run the generators (client build, OpenAPI unchanged) against a running v18 site.
