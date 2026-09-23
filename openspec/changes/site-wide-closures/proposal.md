## Why

A site that shuts for Christmas, a stocktake or a bank holiday has to say so **once per
resource, per date**, through the per-resource exception editor. Twenty resources and eight
public holidays is a hundred and sixty hand-typed exceptions a year, and the only record that
they were one decision is in the operator's head. Nothing in the package can express "the
organisation is closed", so nothing can be inherited, audited, or later imported from a holiday
feed.

This change introduces the missing thing — a site-level list of closure dates — and makes it the
foundation the public-holiday seam (a separate change) writes into.

## What Changes

- **A site closure list.** Each entry is a calendar date and a required short label
  ("Christmas Day", "Stocktake"). At most one closure per date. Full-day only: partial-day
  closures remain the per-resource exception's job.
- **A closure closes every resource for that date**, taking precedence over both the weekly
  open-hours pattern and any date exception the resource carries of its own.
- **A resource may opt out of an individual closure**, by closure id. Opting out means the
  closure does not reach that resource at all: the date then resolves exactly as it would if the
  closure did not exist — the resource's own exception if it has one, otherwise the weekly
  pattern.
- **A new backoffice view**, `Closures`, alongside Resources, Services and Bookings. Writing
  requires `UBookIt.Settings`; reading requires `UBookIt.Configure` or `UBookIt.Settings`. A user
  who can read but not write is told so, rather than shown controls that would be refused. The
  list shows upcoming closures by default, with past ones available on request.
- **The resource editor grows a "Global closures" group** listing every closure with a per-closure
  opt-out, so an operator who cannot reach the Closures view can still see what is being inherited
  and exempt this resource from it.
- **The resource editor marks a superseded exception.** Where a resource carries an *override*
  exception on a closure date it has not opted out of, the editor says that exception is currently
  superseded. The marker fires only where the outcome actually differs — a resource whose own
  exception is itself a closure is closed either way, and calling that "superseded" would be noise.
- **Two additive tables**, `uBookItSiteClosure` and `uBookItResourceClosureOptOut`, and one
  additive EF Core migration. Deleting a closure cascades to its opt-outs.
- **The management contract grows**: the resource request carries opt-out closure ids
  (full-replacement, like every other part of it), and the resource response carries the closure
  list projected with each entry's excluded flag and each superseded exception marked.

**No breaking change.** Every addition to the public surface is an optional parameter or a new
member on a response model; no member is added to any published interface (`IResourceStore`,
`IBookingStore` and friends are untouched), no existing signature changes, and no stored data is
rewritten. A site that never creates a closure behaves exactly as it does at `17.1.2`. This lands
in `17.2.0` — a minor, because it is a feature, per the versioning table the README publishes.

**No destructive schema change.** Both tables are new; the cascade from closure to opt-out deletes
only rows this change introduces.

## Non-goals

- **Closures are never named through the delivery API or the front end.** A closed date looks
  exactly as a per-resource closure looks today: absent from availability, with no reason given.
  Explaining site policy to anonymous visitors is a disclosure decision the `delivery-api` spec
  has already settled against for open hours and exceptions, and it is the first step toward
  shipping a calendar.
- **No partial-day site closures.** "Closing early on Christmas Eve" stays a per-resource
  exception. A site-wide half day is expressible as one, and building it here duplicates a
  mechanism that exists.
- **No recurrence.** "Every 25 December" is not expressible; each year's date is its own entry.
  Recurrence is what the public-holiday feed is for, and inventing a second answer before that
  change exists would leave two.
- **No import, no external source.** The `IPublicHolidaySource` seam and any implementation of it
  belong to the next change; this one ships the list it will write into.
- **No count of affected bookings.** Closures never cancel bookings already placed, and the view
  states that unconditionally rather than computing a number that can be stale between render and
  read.
- **No automatic pruning of past closures.** A past closure is the record of why a date was shut.
  The package already destroys data on a timer in exactly one place, under its own spec; a second
  such mechanism is not worth a tidier list.

## Capabilities

### New Capabilities
- `site-closures`: the site closure list — its shape, the two-verb access split, per-resource
  opt-out, the precedence ladder against weekly hours and date exceptions, and what the backoffice
  view and the resource editor must show about inheritance.

### Modified Capabilities
- `availability`: *Date exceptions* — a date may now carry both a resource's own exception and a
  site closure, so the "at most one exception per resource per date" rule needs restating as a rule
  about the resource's own exceptions, with the precedence between the two layers made explicit.
  *Free-time computation* — closures enter the inputs to effective open hours.
- `permissions`: *Access within the section is decided by four verbs* — `UBookIt.Settings` now also
  reaches the closure list, and `UBookIt.Configure` reaches reading it and setting a resource's
  opt-outs. Still four verbs; what two of them govern is wider.
- `bookings`: *Placement validation pipeline* — `outside-open-hours` is described as "open hours
  with exceptions applied", which no longer names everything that closes a date.
- `resource-management`: *Workspace editor for a resource* and the CRUD contract — the editor gains
  the Global closures group and the superseded-exception marker; the request and response models
  gain opt-outs and the projected closure list.
- `persistence`: three **added** requirements — the two new tables, closure hydration on the read
  path, and opt-out write-path uniqueness. Added rather than folded into the existing schema and
  store requirements because that is the shape this spec already uses: responsibilities, flags,
  settings and cancellation secrets each arrived as their own requirement rather than by rewriting
  *Schema shape and naming*, and capability hydration arrived as its own rather than by rewriting
  *Store implementations honour Core semantics*. Recorded in `design.md` (D9) so the choice is
  visible as a choice.

## Impact

**Code**

- `UBookIt.Core`: `AvailabilityConfiguration` gains an applicable-closure layer consulted by
  `EffectiveWindows`; `Resource` gains its opt-out ids; a `SiteClosure` value type; an
  `ISiteClosureStore` read port and a management port; new failure codes.
- `UBookIt.Persistence`: two entity rows, one additive migration, closure hydration folded into
  `ResourceRowMapper`/the resource stores so every read path carries closures, and the store
  implementations for the new ports. Closures are read **once per store call**, not per resource,
  so a services query over a candidate pool does not become N+1.
- `UBookIt.Backoffice`: a closures controller behind the two-verb split, contract models, the
  `Closures` section view, the resource editor's Global closures group and superseded marker, new
  localization terms, regenerated OpenAPI client.
- `UBookIt.Web`: expected to need no change — closed dates simply carry no availability — which is
  a claim to verify against the running site rather than assert.
- `UBookIt.Tests`: precedence-ladder unit tests, hydration and opt-out round-trip integration
  tests, a seam test through the production entry point (not one test either side of the join),
  and client tests for the new view and the editor group.

**Compatibility**

- `IResourceStore` is a published port, and closures reach the domain at hydration. **A host that
  implements its own resource store therefore owns applying closures**, exactly as it owns applying
  open hours and exceptions today. This is stated in the docs rather than left to be discovered.

**Docs**

- The closures feature in the README feature list, the setup guide, and a note in the theming/
  front-end docs that a closed date is indistinguishable from any other unavailable date.
