## 1. Measure the authorization mechanism before building on it

- [x] 1.1 **Measured: YES.** The user-group collection lists `uBookIt Section` among Administrators' sections, alongside the built-ins. The section is grantable and already granted.
- [x] 1.2 **Measured: `UBookIt.Section`**, read from `umbracoUserGroup2App` on the running site. A custom section stores its **manifest alias verbatim**, where the built-ins store short lowercase names (`content`, `media`, `users`). The two shapes genuinely differ, which is exactly why this was checked rather than inferred — reasoning from `SectionMapper`'s alias fallback gave the same answer, but that was a hint, not evidence.
- [x] 1.3 Not triggered: the section is grantable, so no fallback was needed and no configuration decision falls to Chris.
- [x] 1.4 Recorded here, on `Constants.SectionAlias`, and on `UBookItSectionHandler`. A test ties the constant to the client manifest so the two cannot drift.

## 2. The authorization policy

- [x] 2.1 Add the requirement and handler: `IAuthorizationHelper.TryGetUmbracoUser`, then `AllowedSections` contains the alias measured in 1.2. Mirrors Umbraco's own `AllowedApplicationHandler`, which is `internal` — the ingredients are public, the handler is not.
- [x] 2.2 Register the policy from a composer, **adding the OpenIddict validation scheme**. A backoffice API policy without that scheme rejects an authenticated user and says nothing useful about why.
- [x] 2.3 Apply it on `UBookItBackofficeApiControllerBase`, replacing `SectionAccessContent` — so all three controllers move together rather than leaving two authorization stories in one section.
- [x] 2.4 **Guard the guarantee, not the attribute.** Assert that a user holding the package's section is authorized and a user without it is refused — not that some `[Authorize]` attribute is present. An attribute-presence test passes on exactly the wrong policy, which is the configuration this change exists to correct.
- [x] 2.5 Assert the anonymous case still yields 401, which is the guarantee this requirement already carried and must not lose.

## 3. The endpoint

- [x] 3.1 Add `BookingsController` with `GET bookings`, taking `from`/`to` dates, optional statuses, optional resource ids, `skip`/`take` — mirroring `ResourcesController`'s shape and using the shared base.
- [x] 3.2 Convert the window server-side: start of `from`, and start of the day **after** `to`, through `SiteBookingSettings.TimeZoneId`. Naming a Monday and a Sunday covers all of Sunday.
- [x] 3.3 Bound the **dates** before converting, so an over-wide window is reported in terms of what the caller sent. **This went further than the task anticipated.** Bounding dates was not enough: `BookingQuery.Create` compared raw elapsed time, so a 31-date window containing a fall-back resolved to 31 days and an hour and was refused by the port — meaning `show me October` failed on any European site running the default guardrail, every year. The port now counts **whole** days, which is a relaxation of a shipped guarantee and carries a MODIFIED delta on `booking-management` plus an updated `BookingQueryTests` case. The endpoint bounds dates only; a window it accepts is one the port accepts.
- [x] 3.4 Purpose-built DTOs; no domain or store type in the contract. Map `BookingSummary` one-for-one — no field added at the HTTP layer. **Caught myself violating this:** the first draft put the `BookingStatus` enum on the wire, in and out. `ServiceModels` sets the precedent by carrying its duration kind as a `string`, and an enum is a domain type whose members are a versioning commitment. Status is now a name in both directions, parsed on the way in.
- [x] 3.5 Checked. Both date codes correctly fall through to 400 — they are caller errors. **Two findings:** `time-zone-invalid` also falls through to 400 although it is a *site misconfiguration* rather than the caller's fault; left as-is because the message names the site's zone plainly and changing shared mapping would affect other endpoints, but flagged for review. And a new stable code was needed: `booking-status-invalid`, because status names arrive as strings (see 3.4) and an unrecognised one must be refused rather than silently dropped, which would return a page filtered by something other than what was asked for.
- [x] 3.6 Return the unpaged total alongside the page, matching the existing list endpoints.

## 4. Guards that would catch the real mistakes

- [x] 4.1 **The last named date is included in full** — a booking late on the `to` date is returned. The off-by-one here is invisible until someone misses a booking.
- [x] 4.2 **A DST boundary.** A window spanning a spring-forward and a fall-back date returns the bookings an operator would expect, and the site-local day is what defines the edge rather than a fixed 24 hours.
- [x] 4.3 **Omitted filters match the port's defaults**, asserted against the port rather than against a restated expectation — so the endpoint cannot drift into defaulting for itself.
- [x] 4.4 **No domain type in the contract**, asserted over the controller's signatures and DTOs the way the existing containment test asserts over `IBookingStore`.
- [x] 4.5 Mutation-checked, twice, restoring by edit. **(a) Dropping the end-of-day handling** (`toDate` instead of `toDate.AddDays(1)`) fails 6 tests. **(b) Treating the local date as UTC** (`DateTimeKind.Utc`, i.e. ignoring the site zone) fails 4 — including all three DST cases, which is the point: a fixed-offset fixture would not have caught it.

## 5. Client, docs and close

- [ ] 5.1 Regenerate the TypeScript client and commit it, as every prior API change has.
- [ ] 5.2 Document the authorization change for site administrators: which section now grants access, and that a user with Content but not uBookIt loses access while a user with uBookIt but not Content gains it. This is a behaviour change to a shipped surface.
- [ ] 5.3 Full solution build at **zero** warnings.
- [ ] 5.4 Full test suite green, compared against the 1631 baseline.
- [ ] 5.5 `openspec validate --all --strict`.
- [ ] 5.6 **Re-read both delta specs against the code before syncing**, and diff the `resource-management` guarantees rather than the prose — two requirements are replaced wholesale and one of them is the authorization guarantee.
- [ ] 5.7 Sweep the sibling specs this change could falsify. `packaging` and `default-frontend` both make statements about what the package installs and requires; an authorization change is exactly the kind of thing that falsifies a sentence written elsewhere.
- [ ] 5.8 Hand to `qa-review` in a **fresh context or subagent**.
