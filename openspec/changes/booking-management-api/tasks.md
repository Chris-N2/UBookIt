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
- [x] 4.6 **Live end-to-end verification against the running site**, added because the unit tests could not reach it: they exercise the handler in isolation, so a mistake in the composer's policy registration or the OpenIddict scheme would have passed every one of them. Via Swagger UI with a real PKCE token — `GET /bookings` returns **200**, `total: 75` with `take: 50` (so the total is genuinely unpaged), `status: "Confirmed"` as a **string** not an enum, multi-resource bookings returned **once** carrying both resources (the join fan-out would have shown as duplicates on real data), and two bookings sharing `2026-08-16T09:00:00` appearing adjacent and each exactly once. Unauthenticated: **401** on `/bookings` and `/resources`. Delivery API: still **200** anonymous, so the policy did not leak onto it.
- [x] 4.5 Mutation-checked, twice, restoring by edit. **(a) Dropping the end-of-day handling** (`toDate` instead of `toDate.AddDays(1)`) fails 6 tests. **(b) Treating the local date as UTC** (`DateTimeKind.Utc`, i.e. ignoring the site zone) fails 4 — including all three DST cases, which is the point: a fixed-offset fixture would not have caught it.

## 5. Client, docs and close

- [x] 5.1 Regenerated against the running site's swagger and committed. **Only `sdk.gen.ts` and `types.gen.ts` change** — the `wwwroot/App_Plugins` bundles are gitignored (`.gitignore:485`), so the built output is not a committed artifact after all. `listBookings` is generated at `/umbraco/ubookitbackoffice/api/v1/bookings`, and the wire types confirm the DTO fix landed: `status: string` and `statuses?: Array<string>`, not an enum. `tsc` typechecks and the 69 client tests pass.
- [x] 5.2 Document **what the package ships with**: the uBookIt section is what grants access to its management API, and a site grants it to a user group like any other section. **Not** an upgrade note — the package has never been released, so there is no prior behaviour for anyone to migrate from, and warning about one would be a fiction.
- [x] 5.3 Full solution build at **zero** warnings.
- [x] 5.4 Full test suite green, compared against the 1631 baseline.
- [x] 5.5 `openspec validate --all --strict`.
- [x] 5.6 Re-read; every delta clause traced to the code implementing it, and all eight `resource-management` guarantees and scenarios diffed clause-by-clause as carried. Three artifact corrections fell out of this pass, all recorded: the "behaviour change to a shipped surface" framing was false (nothing is released, so there is no migration and the docs must not invent one), design D1 still described the converted-span check that the whole-days fix replaced, and D3 still read as though task 1 were pending.
- [x] 5.7 Swept, clean. Two near-misses checked rather than assumed: `delivery-api` requires its endpoints be reachable anonymously and **NOT** behind a backoffice policy — unaffected, since delivery uses a different base in a different assembly carrying `[AllowAnonymous]`; and `availability` bounds an **inclusive date span** by the same `MaxQueryRangeDays`, which the whole-days change to `BookingQuery` brings into agreement with rather than divergence from. Nothing outside this change names the Content section.
- [x] 5.8 Hand to `qa-review` in a **fresh context or subagent**.

## 6. QA round 1 — REJECT, five must-fix

- [x] 6.1 **A day that begins twice.** `StartOfDayUtc` handled the spring-forward gap and not
  the fall-back ambiguity, so on a site in an ambiguous zone the day began at the *second*
  midnight and an hour of bookings fell outside a window that names their date. It now takes
  the **earlier** offset — the first midnight — which is the one an operator means. Fixtures
  added for `America/Havana` (ambiguity) and `America/Santiago` (gap). Mutation-checked:
  disabling the ambiguity branch fails `A_day_that_begins_twice_begins_at_the_first_one`.
  **Worth recording that the 400-day contiguity sweep did *not* fail** — with the bug both
  ends of each day shift together, so days still abut. A relative-consistency assertion is
  blind to an error that moves the whole boundary; only the absolute assertion sees it.
- [x] 6.2 **`Enum.TryParse` is not a name check.** It accepted `"1"` (the ordinal),
  `" Confirmed "` (whitespace) and — the one that matters — `"Confirmed,Cancelled"`, which
  it *combines* into a third value. A caller joining a repeated query parameter with commas
  is ordinary, and the response would have succeeded while filtering by something nobody
  asked for. Statuses are now matched against `Enum.GetValues<BookingStatus>()` by name,
  case-insensitively and nothing else. Mutation-checked: restoring `TryParse` fails four
  tests including the comma-joined case. The delta gained a scenario for each form.
- [x] 6.3 **The port's paging defaults were restated at the HTTP layer** — the very thing the
  requirement it implements forbids, one line below the status default that obeys it.
  `BookingQuery` now publishes `DefaultSkip`/`DefaultTake` and the controller passes those.
- [x] 6.4 **No test called the endpoint.** Every guard reached the mapper or the port
  directly, so the whole controller — binding, parsing, failure mapping, paging — was
  unasserted. `BookingsEndpointTests` now drives `ListBookings` against a recording store:
  the page and its unpaged total, cancelled bookings over HTTP, each refused status form,
  omitted-vs-supplied paging read off the port's own constants, omitted filters, the
  site-local window, and the over-wide refusal.
- [x] 6.5 **`booking-status-invalid` was undocumented and the proposal's Impact was false.**
  The delta gained the refusal scenarios (6.2); the Impact bullets now enumerate the actual
  public surface and state plainly that **Core does change** — the whole-days relaxation and
  the new failure code — where the earlier draft denied it.
- [x] 6.6 **One documentation helper, not two.** `AssertSentence` had been copied, the copy
  fixed after a false failure, and the original left standing with the defect beside it.
  Extracted to `Support/DocumentationAssert`; mutation-checked through a theme sentence that
  wraps mid-clause, which is exactly the case the defective twin missed.
- [x] 6.7 Client regenerated after `[BindRequired]`: `ListBookingsData.query` is now required
  and carries `from: string` / `to: string` rather than optionals — the generated shape of
  "the window SHALL NOT be optional". `GeneratedClientTests` pins it, and pins the filters as
  still optional so a blanket-required regeneration cannot satisfy it. Mutation-checked
  against the previous, optional generation.
- [x] 6.8 Clean-build gates, then QA round 2.

## 7. QA round 2 — REJECT, two MAJORs

Both were the same fault, and it is the one this project keeps making: **the guard watched a
downstream artifact instead of the guarantee.** Round 1's fixes were verified genuine by
mutation, but two of the links they created were themselves unguarded.

- [x] 7.1 **`[BindRequired]` was unguarded.** Deleting both attributes passed all 860 tests,
  and the endpoint then answered a windowless call with **200 and an empty page** over a
  window starting at `0001-01-01` — the succeeds-and-is-wrong pair the status requirement
  names as the worst a response can have. `GeneratedClientTests` (7.1's predecessor, task
  6.7) could not see it: `types.gen.ts` is a **committed** artifact, so it disagrees with the
  C# only after someone regenerates, leaving the guard a build behind the defect.
  `The_window_cannot_be_omitted_by_a_caller` now asks **MVC's own metadata provider** whether
  the parameters are binding-required — the question the binder itself asks, rather than a
  search for an attribute — and asserts the four filters are *not*, so a blanket-required
  signature cannot satisfy it. Mutation-checked: dropping `[BindRequired]` from `from` fails
  it. `GeneratedClientTests` stays as the client half, and now says in its own comment that
  it is only the half.
- [x] 7.2 **The authorization fix had no regression guard.** Restoring the exact defect this
  change exists to correct — `SectionAccessContent` on the shared base — passed **860/860**.
  Nothing tied the base's policy *name* to the composer's registration, or that registration
  to the requirement the handler answers. This is the failure task 2.4 wrote itself a warning
  about, and it shipped anyway: the round-1 tests assert the handler's answer, and nothing
  connected the handler to the attribute the endpoints actually carry.
  `The_policy_the_endpoints_name_is_the_one_the_handler_answers` now runs the **real**
  composer into a service collection and asks the **real** `IAuthorizationService` to
  authorize against the policy name **read off the attribute**, for a uBookIt user and a
  Content-only one. A wrong name does not fail an assertion — the policy does not exist and
  the framework throws, which is the loudest possible failure.
  `The_registered_policy_carries_the_backoffice_authentication_scheme` covers the OpenIddict
  scheme separately, because the test above hands the policy a principal directly and so
  cannot see a missing scheme. Mutation-checked, four ways: restoring the Content policy
  fails 2, dropping the scheme fails 1, dropping the requirement fails 2.
- [x] 7.3 **NIT — the documentation separator could cross a paragraph.** `\s` already
  spanned blank lines before the fix, so this was inherited rather than introduced, but the
  widened character class made a `*`-bulleted list crossable too. The separator now permits a
  single line break and never a blank one, and each end is anchored on a word boundary.
  Mutation-checked by splitting an asserted sentence across a paragraph break with every word
  intact and in order: it is now reported missing.
- [x] 7.4 **NIT — `GeneratedClientTests`' regex is pinned to the generator's emitted shape.**
  Left as written: a generator upgrade that renames the type fails *loudly*, with a message
  naming what to look at, rather than passing silently. Recorded rather than changed.
- [x] 7.5 **NIT — the TestSite README and the dead SQLite connection string were unlisted
  scope.** Correct: they were Chris's separate request, made while this change was open, and
  committed on their own (`cd6c3fb`). Recorded here so the change's own task list accounts
  for everything in its branch.
- [ ] 7.6 Clean-build gates, then QA round 3.
