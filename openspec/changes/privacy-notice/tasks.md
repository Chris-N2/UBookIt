## 1. The setting

- [ ] 1.1 Add `PrivacyPolicyUrl` to `SiteBookingSettings` as `string?`. Null means no link — nothing else does.
- [ ] 1.2 Add `PrivacyPolicyUrlSettingKey = "UBookIt:PrivacyPolicyUrl"` beside the other key constants.
- [ ] 1.3 Resolve it: absent, blank, or **not usable as a link** → `null`. Validate it is an absolute http/https URI, or a site-relative path. Do **not** accept `javascript:` or `data:` — this value goes into an `href` on a public page, so what is refused matters more here than for any other setting.
- [ ] 1.4 Log an **error** when a value was written and could not be used; log **nothing** when absent. Follow `ErrorIfRetentionUnreadable` exactly — same shape, same reasoning, and put it beside it in `RunUBookItMigrations`.
- [ ] 1.5 Keep the resolution an `internal static` on the composer, so a unit test can call it with an in-memory `IConfiguration`.

## 2. The view model

- [ ] 2.1 Add a `PrivacyNoticeView` carrying **values, not sentences**: `RetentionDays` (`int?`) and `PolicyUrl` (`string?`). No pre-composed string, no HTML — design D3, and the front-end contract invariant.
- [ ] 2.2 Add it to `IBookingFormView`. **BREAKING — published contract.** `theming` makes the view model types a compatibility promise; declare it in the proposal (done) and here.
- [ ] 2.3 Populate it in both flows' view construction from `SiteBookingSettings`. **One source** — do not thread the retention days from anywhere else, or the notice and the sweep acquire two copies that can disagree.
- [ ] 2.4 Do **not** put the composed sentences on the model. Turning facts into prose is the view's job; putting it on the model would make English part of the contract a theme receives.

## 3. Rendering

- [ ] 3.1 New shared partial `_PrivacyNotice.cshtml`, called from `_YourDetails.cshtml` **before the submit button**, inside the fieldset — reading order is the association, per design D6.
- [ ] 3.2 Compose the four statements: what (name, email, phone where given), why (to hold, identify and confirm the booking), how long (from `RetentionDays`), who can see it (backoffice users Umbraco permits to see sensitive data).
- [ ] 3.3 The retention sentence, both branches: a period → state it; `null` → *kept until removed, no automatic removal period is set*. **Never omit the sentence** — retention is off by default, so the omission would be the usual rendering.
- [ ] 3.4 Render the policy link only when configured; no empty link, no placeholder, no "#".
- [ ] 3.5 No control of any kind. No checkbox, no button, no `aria-describedby` wiring a paragraph does not need. It is prose.
- [ ] 3.6 Use the existing class vocabulary or add to it deliberately — `default-frontend` makes the class names a stable contract, so a new class is a contract addition, not a detail.
- [ ] 3.7 It renders in **both** flows. `_YourDetails.cshtml` is shared, so this should be free — confirm it rather than assume it, and confirm the service flow's confirmation step does not also need one (it collects nothing).

## 4. Delivery API

- [ ] 4.1 Add the retention read under the existing versioned route. A small dedicated endpoint — **not** a member on `ResourceReadModel` or `ServiceReadModel`, per design D4.
- [ ] 4.2 The response distinguishes "no period configured" from any number, and **not by `0`** — the same distinction `RetentionDays` being `int?` exists to preserve.
- [ ] 4.3 No prose in the response. Number only.
- [ ] 4.4 Anonymous, like every delivery endpoint; confirm it inherits the base controller's stance rather than restating it.
- [ ] 4.5 Regenerate the delivery OpenAPI document and the generated client if either is checked in. `GeneratedClientTests` exists — check what it asserts before assuming.

## 5. Documentation

- [ ] 5.1 Document the setting, what the notice states, and that it appears at the point contact details are collected.
- [ ] 5.2 **State that it is not a privacy policy** and that a site still needs one — the notice describes what this package does, and nothing else.
- [ ] 5.3 Document what it says when no retention period is configured, so a site owner reading the default rendering knows it is deliberate.
- [ ] 5.4 **In the theming docs**: a theme replacing the contact-details view decides whether the notice renders at all. Say why this one is worth calling out — a theme dropping the time-picker breaks visibly, a theme dropping the notice does not break at all.
- [ ] 5.5 Add `_PrivacyNotice.cshtml` to the published building-blocks list a theme may call, wherever that list lives.

## 6. Verification

- [ ] 6.1 Settings tests: absent → null; blank → null; `javascript:alert(1)` → null; a relative path → accepted; an absolute https URL → accepted. Assert the **error is logged** for written-but-unusable and **not** for absent.
- [ ] 6.2 **Mutation-check the URL refusal**: make the resolver accept any non-empty string and confirm the `javascript:` case fails. This value lands in an `href` on a public page; it is the one setting where being permissive is a vulnerability rather than a nuisance.
- [ ] 6.3 Rendering tests over **all four combinations** — period × no period, link × no link. `default-frontend` requires a view render every state its model can express, and this model now has four.
- [ ] 6.4 **Mutation-check the no-period branch**: make it omit the retention sentence and confirm a test fails. That branch is the DEFAULT rendering, so a test that only ever seeds a configured period would leave the common case unguarded — the fixture trap from ㉕, where a single-batch fixture could not see the paging defect.
- [ ] 6.5 Assert the rendered period **equals** what the retention sweep would act on, from one source. Not two separate assertions that each happen to say 90 — the guarantee is that they cannot disagree, so read both and compare.
- [ ] 6.6 Assert the notice renders **before** the submit control in document order, not merely that both are present.
- [ ] 6.7 Assert the notice offers no form control, and that a booking still completes with nothing ticked.
- [ ] 6.8 Delivery API tests: a configured period; no configured period distinguishable from a number; no prose in the body; anonymous access.
- [ ] 6.9 Documentation tests for 5.1–5.4, extending `BackofficeDocumentationTests` / `ThemeDocumentationTests` as appropriate. **Mutation-check every assertion ONE AT A TIME**, and — the round-1 lesson from ㉕ — **assert the mutation changed the file before believing the result**; a phrase wrapped across a line with markdown decoration is not matched by a literal replace, and the check then reports a false miss.
- [ ] 6.10 Check the `sensitive-data` membership-snapshot guard: it fires when `BookingModel` or `BookerModel` gains a member. This change adds no wire member to either, so it should stay green — confirm rather than assume.

## 7. Modified requirements — the guarantee diff

A `## MODIFIED Requirements` entry replaces body *and* scenarios; anything not restated is deleted
with nothing in the diff resembling a deletion.

- [ ] 7.1 `persistence` → *Package composition registers persistence and Core services*. Carried forward: the registration list, the time-zone default and its warning, the retention-job registration, the asymmetric-resolution paragraph, the scoped-dependency paragraph, all six scenarios. Added: the policy-link paragraph and two scenarios. **Dropped: nothing** — verified mechanically (6 → 8 scenarios, 7 → 8 SHALLs, 0 dropped), not by eye.
- [ ] 7.2 The delivery-API change is an **ADDED** requirement, not a modification of an existing endpoint requirement. Nothing in `delivery-api` becomes false: no existing endpoint changes shape, and the versioned-contract requirement does not enumerate endpoints. Confirm that judgement still holds at apply time.
- [ ] 7.3 **`ChangeDeltaIntegrityTests` is the authority on delta correctness, not `openspec validate --strict`.** ㉕ produced a delta that validated cleanly and would have synced as a duplicate requirement; the repo's own guard caught it. Run the suite before believing a delta is right.

## 8. Sweep — sibling specs this change falsifies

Looking outward at requirements not being touched. This found something on five consecutive
changes and nothing on the sixth.

- [ ] 8.1 `default-frontend` → *A view renders every state its model can express* and *Every branch a view carries can be taken*. Both now cover four new combinations. **Satisfied, not falsified** — but they are the requirements most likely to have a guard that enumerates branches and now needs to know about these.
- [ ] 8.2 `default-frontend` → *The styling contract is a stable class vocabulary*. A new class for the notice is an addition to a published contract, not a detail.
- [ ] 8.3 `theming` → *The building blocks a theme may call are a promised contract*. A new shared partial joins that contract. Does the requirement or its docs enumerate them?
- [ ] 8.4 `theming` → *A themed rendering's markup is the theme author's*. This change relies on it covering the notice; confirm it reaches by its own terms rather than needing an amendment.
- [ ] 8.5 `booking-retention` → *Off SHALL be distinguishable from a period.* This change is the "later feature" that clause was written for. **Check it is satisfied and say so** — it is the one requirement in the repo that was written in anticipation of this change.
- [ ] 8.6 `sensitive-data` — the notice tells visitors who can see their details. Does anything there constrain what may be said publicly about the mechanism?
- [ ] 8.7 `booker-erasure` — the notice does not mention erasure on request. Should it, and does any requirement there expect it to?
- [ ] 8.8 **The capability this change makes a difference to is the one most certain to be affected.** Find the one that is not already on this list.
