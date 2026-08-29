## Why

`IBookingManagementStore` shipped last change and nothing can reach it. The backoffice has
sections for resources and services; bookings — the thing the package exists to collect —
have no screen and no endpoint. This is the middle slice: the HTTP surface the Lit screen
will consume, and the generated TypeScript client that makes it callable.

The controller pattern is already established twice over (`ResourcesController`,
`ServicesController`), so the endpoint itself is ordinary work. Two things are not:

**The authorization is wrong today, and adding a bookings endpoint is what makes it
matter.** `UBookItBackofficeApiControllerBase` authorizes on
`AuthorizationPolicies.SectionAccessContent` — access to Umbraco's **Content** section —
while the package registers and ships its **own** section, `UBookIt.Section`. Both
directions are wrong: a user granted the uBookIt section but not Content is refused by an
API for a section they can see, and a user granted Content but not uBookIt can call every
uBookIt endpoint for a section they cannot. That has been a tolerable mismatch for
resource and service configuration. It stops being tolerable when the payload is booker
names and email addresses.

**A booking's window is a question about local time, and the port takes UTC instants.** An
operator asks for "this week" meaning the site's week. Deciding where that conversion
happens decides what the screen has to know.

## What Changes

- **`GET ubookitbackoffice/api/v{version}/bookings`** — the windowed, paged, filtered list
  over `IBookingManagementStore`, with purpose-built DTOs. Query parameters mirror the
  port: window, statuses, resource ids, skip, take.
- **The window is expressed in site-local dates and converted server-side.** The endpoint
  takes `from` and `to` as dates, resolves them against `SiteBookingSettings.TimeZoneId`,
  and hands `BookingQuery.Create` the resulting instants. One timezone rule, applied where
  the timezone is already known — rather than every client reimplementing it, and getting
  it differently wrong at a DST boundary.
- **A uBookIt section access policy**, replacing `SectionAccessContent` on the shared
  controller base. Umbraco's own section handler (`AllowedApplicationHandler`) is
  `internal`, but everything it depends on is public: a requirement that checks the
  current backoffice user's `IUser.AllowedSections` for the package's section alias,
  resolved through `IAuthorizationHelper`. Registered from a composer, with the OpenIddict
  validation scheme a backoffice API policy requires — omitting that scheme is how a
  custom policy silently rejects an authenticated user.
  <br>**Conditional on task 1** — see Risks.
- **Generated TypeScript client regenerated and committed**, as with every prior API
  change.

## Non-goals

- **The Lit screen.** This ships the endpoint and the client that calls it; the section
  view, list and detail are the next change.
- **Any mutation.** No cancel, no create-on-behalf. `IBookingService.CancelAsync` exists
  and stays untouched — cancel-from-backoffice is its own change, and the only other v1
  verb.
- **Search by booker.** The port has no unwindowed lookup and this endpoint adds none.
- **Recording the service a booking was placed for.** *Decided and deliberately deferred:*
  `BookingRow` has no `ServiceId`, so a booking cannot report which service produced it.
  Chris's call is to add it **before there is production data** — the only consumer today
  is our own test database, and if a backfill proves awkward the existing bookings can
  simply be deleted, which will never be true again. It is not in this change because it
  is a domain and schema change with a migration, and bundling it with an HTTP surface
  would put two unrelated risk profiles in one review. **It should land before the Lit
  screen**, so the screen is built against the final row shape rather than gaining a
  column immediately afterwards.
- **Changing what the resources and services endpoints return.** They are touched only by
  the authorization fix.

## Capabilities

### New Capabilities
- *(none — the booking list endpoint extends `booking-management`)*

### Modified Capabilities
- `booking-management`: gains the HTTP surface — the endpoint, its parameters, the DTO
  contract, where the timezone conversion happens, and that the response carries no more
  than the port returns. **Its windowing requirement is also MODIFIED**, discovered at
  apply time: the guardrail compared raw elapsed time, so a window of exactly the maximum
  number of local dates was refused whenever it contained a daylight-saving fall-back —
  `show me October` failed on every European site running the default of 31, annually.
  Days are now counted whole, so a partial day over does not count. A relaxation, stated
  as one.
- `resource-management`: two requirements. **"Management endpoints require backoffice
  authorization"** is strengthened from "an Umbraco backoffice authorization policy" to
  one granting access to *the package's own section*. **"HTTP callers cannot reach raw
  booking storage"** currently says controllers "SHALL depend only on **the resource
  management port** and validated Core services" — which a bookings controller violates by
  existing. It is widened to the management **ports**, with the containment guarantee it
  was written for (no `IBookingStore`, no `Booking.Rehydrate` in any API-layer type)
  carried through unchanged and unweakened.

## Impact

- **`UBookIt.Backoffice`**: a `BookingsController`, booking DTOs and a mapper, the
  authorization requirement, handler and policy registration, and the regenerated client.
- **Who may call every uBookIt management endpoint changes** — a user with Content but no
  uBookIt section **loses** access, one with uBookIt but no Content **gains** it. **This is
  not a migration and nobody will notice it**, because the package has never been
  released: there are no installs, no configured user groups, and no consumer of the
  previous behaviour. It is simply the wrong policy being corrected before anything can
  depend on it, which is the same reasoning as fixing the booking/service schema gap while
  the only data is our own. The documentation therefore states what the package ships
  with, and does not carry an upgrade note for an upgrade that cannot happen.
- **Public API surface** (additive): the endpoint, its DTOs, and the policy name.
- **No Core change, no persistence change, no schema change, no migration.**

## Risks

**The authorization design rests on one unmeasured claim, and it is measured first.**

What the Umbraco 17 source does settle: there is **no server-side section registry** — no
`ISectionService`, no section collection. `IUser.AllowedSections` is a bare
`IEnumerable<string>` persisted per user group, and `SectionMapper` is a hardcoded list of
the nine built-in sections whose `GetName` explicitly *"falls back to the alias"* for
anything it does not recognise. That fallback exists for sections it does not know about,
which is a strong hint that a package's alias can live in `AllowedSections` — but a hint
is not a measurement.

What it does **not** settle, because the answer is client-side:

1. Does the **user-group editor** offer `UBookIt.Section` as a grantable section at all?
   Sections are declared in the backoffice client manifest, so what the picker lists is a
   question about the running backoffice, not about C#.
2. If granted, does that alias reach `IUser.AllowedSections` server-side in the form the
   policy compares against?

There is also a **naming subtlety worth not tripping over**: for built-in sections the
value in `AllowedSections` is a short alias (`content`), while the manifest *name* is
`Umb.Section.Content`. uBookIt's manifest declares `alias: "UBookIt.Section"`, which is
shaped like a manifest name rather than like a stored alias. Whether the two must differ
is part of what task 1 measures.

**If it does not hold, stop and report rather than adjust.** The fallbacks are a
server-side section registration, or the documented `RequireRole` against a user-group
alias — but both change what a site administrator has to configure, which is a decision
rather than an implementation detail.
