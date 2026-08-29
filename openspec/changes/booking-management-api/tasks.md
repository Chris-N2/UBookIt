## 1. Measure the authorization mechanism before building on it

- [ ] 1.1 **Does the user-group editor offer `UBookIt.Section` as a grantable section?** Run the TestSite, open a user group in the backoffice, and look. Sections are declared in the client manifest and there is **no server-side section registry** in Umbraco 17 — no `ISectionService`, no section collection — so this cannot be answered from C#. **Needs a backoffice login.**
- [ ] 1.2 If it is grantable, grant it and read back what lands in `IUser.AllowedSections` — the **exact string**, not the assumption. Built-in sections store a short alias (`content`) while the manifest name is `Umb.Section.Content`; uBookIt's manifest declares `alias: "UBookIt.Section"`, which is shaped like a name. The handler compares against whatever this actually is.
- [ ] 1.3 **If the section is not grantable, STOP and report.** The fallbacks — a server-side section registration, or the documented `RequireRole` against a user-group alias — change what a site administrator has to configure, which is a decision rather than an implementation detail. Do not pick one silently.
- [ ] 1.4 Record what was measured, so §5's documentation states it rather than asserting it.

## 2. The authorization policy

- [ ] 2.1 Add the requirement and handler: `IAuthorizationHelper.TryGetUmbracoUser`, then `AllowedSections` contains the alias measured in 1.2. Mirrors Umbraco's own `AllowedApplicationHandler`, which is `internal` — the ingredients are public, the handler is not.
- [ ] 2.2 Register the policy from a composer, **adding the OpenIddict validation scheme**. A backoffice API policy without that scheme rejects an authenticated user and says nothing useful about why.
- [ ] 2.3 Apply it on `UBookItBackofficeApiControllerBase`, replacing `SectionAccessContent` — so all three controllers move together rather than leaving two authorization stories in one section.
- [ ] 2.4 **Guard the guarantee, not the attribute.** Assert that a user holding the package's section is authorized and a user without it is refused — not that some `[Authorize]` attribute is present. An attribute-presence test passes on exactly the wrong policy, which is the configuration this change exists to correct.
- [ ] 2.5 Assert the anonymous case still yields 401, which is the guarantee this requirement already carried and must not lose.

## 3. The endpoint

- [ ] 3.1 Add `BookingsController` with `GET bookings`, taking `from`/`to` dates, optional statuses, optional resource ids, `skip`/`take` — mirroring `ResourcesController`'s shape and using the shared base.
- [ ] 3.2 Convert the window server-side: start of `from`, and start of the day **after** `to`, through `SiteBookingSettings.TimeZoneId`. Naming a Monday and a Sunday covers all of Sunday.
- [ ] 3.3 Bound the **dates** before converting, so an over-wide window is reported in terms of what the caller sent. A DST boundary makes a date span a variable number of hours, so validating only the converted instants can refuse a window the caller would consider legal.
- [ ] 3.4 Purpose-built DTOs; no domain or store type in the contract. Map `BookingSummary` one-for-one — no field added at the HTTP layer.
- [ ] 3.5 Map failures through the existing `ApiResults` so `date-range-too-large` and `date-range-invalid` surface as they do elsewhere. Check whether either needs adding to the status mapping rather than falling through to 400 by accident.
- [ ] 3.6 Return the unpaged total alongside the page, matching the existing list endpoints.

## 4. Guards that would catch the real mistakes

- [ ] 4.1 **The last named date is included in full** — a booking late on the `to` date is returned. The off-by-one here is invisible until someone misses a booking.
- [ ] 4.2 **A DST boundary.** A window spanning a spring-forward and a fall-back date returns the bookings an operator would expect, and the site-local day is what defines the edge rather than a fixed 24 hours.
- [ ] 4.3 **Omitted filters match the port's defaults**, asserted against the port rather than against a restated expectation — so the endpoint cannot drift into defaulting for itself.
- [ ] 4.4 **No domain type in the contract**, asserted over the controller's signatures and DTOs the way the existing containment test asserts over `IBookingStore`.
- [ ] 4.5 Mutation-check the window conversion: shift it by a day, or drop the end-of-day handling, from a clean build, and confirm a test fails. **Restore with an edit, never a timestamp-preserving copy.**

## 5. Client, docs and close

- [ ] 5.1 Regenerate the TypeScript client and commit it, as every prior API change has.
- [ ] 5.2 Document the authorization change for site administrators: which section now grants access, and that a user with Content but not uBookIt loses access while a user with uBookIt but not Content gains it. This is a behaviour change to a shipped surface.
- [ ] 5.3 Full solution build at **zero** warnings.
- [ ] 5.4 Full test suite green, compared against the 1631 baseline.
- [ ] 5.5 `openspec validate --all --strict`.
- [ ] 5.6 **Re-read both delta specs against the code before syncing**, and diff the `resource-management` guarantees rather than the prose — two requirements are replaced wholesale and one of them is the authorization guarantee.
- [ ] 5.7 Sweep the sibling specs this change could falsify. `packaging` and `default-frontend` both make statements about what the package installs and requires; an authorization change is exactly the kind of thing that falsifies a sentence written elsewhere.
- [ ] 5.8 Hand to `qa-review` in a **fresh context or subagent**.
