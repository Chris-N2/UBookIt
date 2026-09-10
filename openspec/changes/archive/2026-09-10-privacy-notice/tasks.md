## 1. The setting

- [x] 1.1 Add `PrivacyPolicyUrl` to `SiteBookingSettings` as `string?`. Null means no link — nothing else does.
- [x] 1.2 Add `PrivacyPolicyUrlSettingKey = "UBookIt:PrivacyPolicyUrl"` beside the other key constants.
- [x] 1.3 Resolve it: absent, blank, or **not usable as a link** → `null`. Validate it is an absolute http/https URI, or a site-relative path. Do **not** accept `javascript:` or `data:` — this value goes into an `href` on a public page, so what is refused matters more here than for any other setting.
- [x] 1.4 Log an **error** when a value was written and could not be used; log **nothing** when absent. Follow `ErrorIfRetentionUnreadable` exactly — same shape, same reasoning, and put it beside it in `RunUBookItMigrations`.
- [x] 1.5 Keep the resolution an `internal static` on the composer, so a unit test can call it with an in-memory `IConfiguration`.

## 2. The view model

- [x] 2.1 Add a `PrivacyNoticeView` carrying **values, not sentences**: `RetentionDays` (`int?`) and `PolicyUrl` (`string?`). No pre-composed string, no HTML — design D3, and the front-end contract invariant.
- [x] 2.2 Add it to `IBookingFormView`. **BREAKING — published contract.** `theming` makes the view model types a compatibility promise; declare it in the proposal (done) and here.
- [x] 2.3 Populate it in both flows' view construction from `SiteBookingSettings`. **One source** — do not thread the retention days from anywhere else, or the notice and the sweep acquire two copies that can disagree.
- [x] 2.4 Do **not** put the composed sentences on the model. Turning facts into prose is the view's job; putting it on the model would make English part of the contract a theme receives.

## 3. Rendering

- [x] 3.1 New shared partial `_PrivacyNotice.cshtml`, called from `_YourDetails.cshtml` **before the submit button**, inside the fieldset — reading order is the association, per design D6.
- [x] 3.2 Compose the four statements: what (name, email, phone where given), why (to hold, identify and confirm the booking), how long (from `RetentionDays`), who can see it (backoffice users Umbraco permits to see sensitive data).
- [x] 3.3 The retention sentence, both branches: a period → state it; `null` → *kept until removed, no automatic removal period is set*. **Never omit the sentence** — retention is off by default, so the omission would be the usual rendering.
- [x] 3.4 Render the policy link only when configured; no empty link, no placeholder, no "#".
- [x] 3.5 No control of any kind. No checkbox, no button, no `aria-describedby` wiring a paragraph does not need. It is prose.
- [x] 3.6 DONE. `ubookit-privacy` added as a BLOCK (declared in both the published set and the block list, which are separate assertions). Deliberately NOT reusing `ubookit-notice`: the stylesheet marks that one with a left rule as "an explanation the visitor has to act on", and a privacy notice is disclosure rather than an instruction. It takes a measure and no colour at all — not even the muting `ubookit-hint` uses, because reduced contrast on the paragraph telling somebody what happens to their personal data is the wrong trade.
- [x] 3.7 It renders in **both** flows. `_YourDetails.cshtml` is shared, so this should be free — confirm it rather than assume it, and confirm the service flow's confirmation step does not also need one (it collects nothing).

## 4. Delivery API

- [x] 4.1 Add the retention read under the existing versioned route. A small dedicated endpoint — **not** a member on `ResourceReadModel` or `ServiceReadModel`, per design D4.
- [x] 4.2 The response distinguishes "no period configured" from any number, and **not by `0`** — the same distinction `RetentionDays` being `int?` exists to preserve.
- [x] 4.3 No prose in the response. Number only.
- [x] 4.4 Anonymous, like every delivery endpoint; confirm it inherits the base controller's stance rather than restating it.
- [x] 4.5 DONE — checked rather than assumed: `GeneratedClientTests` asserts over the backoffice client only, and the delivery OpenAPI document is generated at runtime rather than checked in, so there was nothing to regenerate. Recorded because "nothing to do" and "nobody looked" read identically in a task list.

## 5. Documentation

- [x] 5.1 Document the setting, what the notice states, and that it appears at the point contact details are collected.
- [x] 5.2 **State that it is not a privacy policy** and that a site still needs one — the notice describes what this package does, and nothing else.
- [x] 5.3 Document what it says when no retention period is configured, so a site owner reading the default rendering knows it is deliberate.
- [x] 5.4 **In the theming docs**: a theme replacing the contact-details view decides whether the notice renders at all. Say why this one is worth calling out — a theme dropping the time-picker breaks visibly, a theme dropping the notice does not break at all.
- [x] 5.5 Add `_PrivacyNotice.cshtml` to the published building-blocks list a theme may call, wherever that list lives.

## 6. Verification

- [x] 6.1 Settings tests: absent → null; blank → null; `javascript:alert(1)` → null; a relative path → accepted; an absolute https URL → accepted. Assert the **error is logged** for written-but-unusable and **not** for absent.
- [x] 6.2 DONE, **and the restore of that mutation silently failed** — see 6.11. The permissive resolver fails 8 of the 10 refusal cases.
- [x] 6.3 Rendering tests over **all four combinations** — period × no period, link × no link. `default-frontend` requires a view render every state its model can express, and this model now has four.
- [x] 6.4 **Mutation-check the no-period branch**: make it omit the retention sentence and confirm a test fails. That branch is the DEFAULT rendering, so a test that only ever seeds a configured period would leave the common case unguarded — the fixture trap from ㉕, where a single-batch fixture could not see the paging defect.
- [x] 6.5 Assert the rendered period **equals** what the retention sweep would act on, from one source. Not two separate assertions that each happen to say 90 — the guarantee is that they cannot disagree, so read both and compare.
- [x] 6.6 Assert the notice renders **before** the submit control in document order, not merely that both are present.
- [x] 6.7 Assert the notice offers no form control, and that a booking still completes with nothing ticked.
- [x] 6.8 Delivery API tests: a configured period; no configured period distinguishable from a number; no prose in the body; anonymous access.
- [x] 6.9 DONE, every assertion mutated individually with the mutation asserted to have applied first. **Two reported NOT APPLICABLE rather than MISSED** (the phrases wrap across lines, so a literal replace matched nothing) — which is the round-1 lesson from ㉕ working. Re-run against contiguous fragments, one was then a genuine **MISS**: the theming guide's narrative about the notice had no test at all, only the table row. Test added; all four of its assertions now mutation-checked.
- [x] 6.10 Check the `sensitive-data` membership-snapshot guard: it fires when `BookingModel` or `BookerModel` gains a member. This change adds no wire member to either, so it should stay green — confirm rather than assume.
- [x] 6.11 **A mutation check must verify its RESTORE, not just its mutation.** Restoring `UBookItPersistenceComposer.cs` with `mv` preserved the backup's original timestamp, leaving the source three seconds OLDER than the compiled DLL — so MSBuild skipped it and kept the mutant binary. The targeted re-run went green against the permissive resolver still compiled in. Only the full suite caught it, and committing on that targeted green would have shipped a `javascript:` href straight to a public page. This is the stale-build trap this repo already documented for `Copy-Item`, arriving through `mv`: **touch the file after restoring, and re-run the assertions the mutation broke.**

## 7. Modified requirements — the guarantee diff

A `## MODIFIED Requirements` entry replaces body *and* scenarios; anything not restated is deleted
with nothing in the diff resembling a deletion.

- [x] 7.1 `persistence` → *Package composition registers persistence and Core services*. Carried forward: the registration list, the time-zone default and its warning, the retention-job registration, the asymmetric-resolution paragraph, the scoped-dependency paragraph, all six scenarios. Added: the policy-link paragraph and two scenarios. **Dropped: nothing** — verified mechanically (6 → 8 scenarios, 7 → 8 SHALLs, 0 dropped) by a script that extracts each requirement to its own boundary, not by eye.
- [x] 7.2 CONFIRMED at apply time. Nothing in `delivery-api` became false: no existing endpoint changed shape, the versioned-contract requirement enumerates no endpoints, and the new read is additive under the existing route. `ADDED`, not `MODIFIED`.
- [x] 7.3 DONE — `ChangeDeltaIntegrityTests` green, and it is the authority rather than `openspec validate --strict`.

## 8. Sweep — sibling specs this change falsifies

Looking outward at requirements not being touched. This found something on five consecutive
changes and nothing on the sixth.

- [x] 8.1 `default-frontend` → *A view renders every state its model can express* / *Every branch a view carries can be taken*. **Satisfied, and they did fire.** Both rules failed the moment the partial existed without fixtures, naming it explicitly. Four crossed combinations added to `FormStates`, plus the one-day case.
- [x] 8.2 `default-frontend` → *The styling contract is a stable class vocabulary*. `ubookit-privacy` added deliberately as a block; the vocabulary test and the block-prefix test are separate assertions and both were updated knowingly.
- [x] 8.3 `theming` → *The building blocks a theme may call are a promised contract*. **It does enumerate them** — in `UBookItThemeContract.SharedPartials` and in the guide's table, with a test tying the two together. Both updated. No spec delta needed: the requirement promises the list is published, not what is on it.
- [x] 8.4 `theming` → *A themed rendering's markup is the theme author's*. **Reaches the notice by its own terms** — it already extends to "behaviour a view's markup determines" and says so once rather than per surface. No amendment; a documentation paragraph instead, because the consequence is invisible in a way other omissions are not.
- [x] 8.5 `booking-retention` → *Off SHALL be distinguishable from a period*. **Satisfied, and this change is the "later feature" that clause names.** `RetentionDays` stays `int?` end to end — settings, view model, delivery model — and two tests assert null survives as null rather than becoming 0.
- [x] 8.6 `sensitive-data` — nothing constrains what may be said publicly about the mechanism; the capability is about who may READ contact details, and the notice reveals none. Checked, not assumed.
- [x] 8.7 `booker-erasure` — checked: nothing there requires the front end to mention erasure on request, and the notice does not. Deliberate: an erasure request goes to the site, not to the package, and the package has no way to know how a site wants to be asked. The site's own policy link is where that belongs.
- [x] 8.8 **The one not already listed: `default-frontend`'s markup rules found a real defect.** `No_tag_helper_reaches_the_page` failed because I wrote `<partial name="..." />` — a tag helper, in a package that deliberately registers none. Rewritten as `Html.PartialAsync`. That is the sweep earning its place: the capability this change makes a difference to was the rendering one, and the guard that caught it was one I had not thought about.
