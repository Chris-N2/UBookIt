# The sibling-falsification sweep

What this change makes untrue elsewhere. Run over every spec in `openspec/specs/`, not only the
ones the change touches, because the sentence a change falsifies is usually in a requirement
nobody is editing.

## Checked

All 23 specs: `availability`, `booker-erasure`, `booking-emails`, `booking-management`,
`booking-retention`, `bookings`, `default-frontend`, `delivery-api`, `email-templates`,
`packaging`, `permissions`, `persistence`, `privacy-notice`, `resource-management`, `resources`,
`responsibility`, `self-service-cancellation`, `sensitive-data`, `service-booking`, `services`,
`site-closures`, `site-settings`, `theming`.

Swept for: closures; holiday/feed/import; ports and extension points; exhaustive enumerations of
endpoints, verbs and failure codes; claims about startup, background or scheduled work; claims
about outbound network calls; and absolute words ("exactly", "only", "every", "no other").

## Found, and acted on

| Spec | The sentence | What this change does to it |
|---|---|---|
| `site-closures` | *The closures view*: "listing each closure's date and label with actions to create, edit and delete" | Exhaustive enumeration, now incomplete. **MODIFIED** — the enumeration is made conditional on the site and the user's verb. |
| `site-closures` | Keyboard scenario: "creating, editing, deleting and revealing past closures are all reachable and operable" | The most complex control on the view had no accessibility scenario. **MODIFIED**, plus a scenario of its own. |
| `site-closures` | *Management endpoints*: "list, create, update and delete" | Three endpoints missing. **MODIFIED**. |
| `site-closures` | *Read and written by different verbs*: "**Both** SHALL be enforced by the server" | "Both" named two acts; there are now four. **MODIFIED**. |
| `site-closures` | Same requirement: "Where a user may read but not write, the package SHALL say so" | Sits beside this change's "absent and unexplained" rule and looks like its opposite. **MODIFIED** to distinguish the axes: that rule is about the **verb**, this one about a **capability the site does not have**. |
| `permissions` | The `UBookIt.Settings` bullet, and the three-act rationale for how closures split across the verbs | The verb now also reaches previewing, which is on neither side of the "deciding versus exempting" line the rationale draws. **MODIFIED** — the bullet, the reach, and a fourth act with its own reasoning. |
| `permissions` | "the settings reach the site's retention posture, its anonymous exposure and the addresses bookers' details are sent to" | Enumeration of reach, now missing "invoking the site's own code on an operator's behalf". **MODIFIED**. |
| this change's own delta | "This is the same decision the delivery API makes" | Overclaimed. The delivery API's rule forbids **any** signal separating disabled from never-existed; the probe returns exactly such a signal in a body member. **Narrowed** — the reasoning carries over, the rule does not, and the delta now says why the weaker rule is sufficient here (the probe sits behind authentication and the same verb). |
| this change's own delta | Silent on the verb gating the source probe | `permissions` requires every management action to classify into exactly one verb, and an unclassified one is a named failure. The code already gates it on `UBookIt.Settings`; only the spec was silent. **Stated**, with a scenario. |

## Found, and judged not falsified — with the evidence

- **`resource-management`: "Unauthenticated requests SHALL receive 401; the endpoints SHALL NOT be
  reachable anonymously under any configuration shipped by the package."** The concern was that an
  absent import endpoint answers 404 before it answers 401, which would make this enumeration need
  an exception. **Measured on the running dev site with no source registered:** all three holiday
  endpoints answer **401** unauthenticated, because `[Authorize]` runs before the source check.
  The guarantee holds unchanged, and a scenario pinning it has been added to `site-closures` so
  that the ordering is asserted rather than assumed.

- **`site-closures`: "A closure id that does not exist SHALL yield a 404 problem-details
  response."** The concern was that the capability now has a 404 that is not problem details.
  **Measured:** the no-source 404 body is `{"type":"NotFound","title":"This site has no public
  holiday source.","status":404}` — problem details with a type member. The family's reading is
  safe, and the requirement has been extended to say so explicitly rather than left to inference.

- **`packaging`: "A capability landing SHALL retire the sentences it falsifies."** Triggered, and
  it yields nothing: no shipped markdown asserted that uBookIt had no holiday support or that
  closures could only be entered by hand. Verified by reading every holiday- and closure-related
  sentence in `README.md` and `docs/` **as they stood at `HEAD` before this change**. No needle is
  owed to `RetiredClaims`, and one is not added, because a needle that matches nothing is the
  failure mode that guard's own control exists to catch.

- **`site-settings`: "Access to the settings screen and to every endpoint behind it SHALL require a
  permission verb of its own."** One-directional and still true: it says the screen requires the
  verb, not that the verb reaches only the screen. Left unmodified deliberately — `permissions` is
  the spec that enumerates what each verb reaches, and that enumeration is where this change's
  addition belongs. Recorded so that the next reader knows it was considered rather than missed.

- **`bookings`: "`UBookIt.Core`'s package references — there are none, and every port it declares
  is a type it declares itself."** Not falsified, but binding on the new port. `IPublicHolidaySource`
  and `PublicHoliday` are declared in `UBookIt.Core` and use BCL types only (`DateOnly`, `string`,
  `IReadOnlyList<T>`, `CancellationToken`) — no DI, HTTP or framework type on the signature.

- **`persistence`: "A closure row SHALL hold its id, its date, and its label."** Corroborating
  rather than falsified: it already pins this change's "no record of import origin" rule at the
  storage layer, and any attempt to record origin would have to falsify it first.

- **`delivery-api`.** Nothing falsified: the closure list is not exposed there, and an imported
  closure is indistinguishable from any other to every public route.

## Carried forward, not closed here

- **`packaging`'s extension-point documentation rule is scoped to notifications** — "The package
  raises notifications a consuming site can handle, and those SHALL be documented". This change
  adds the first port whose purpose is to supply data *into* the package rather than to keep
  callers out of the database, and the requirement's own rationale already generalises past its
  scope ("the outcome every port in this package exists to prevent"). The obligation is discharged
  in fact — the port is documented in `docs/configuration.md` — but the requirement does not yet
  demand it. **Widening it is its own change**, not a rider on this one, because it reaches every
  port the package has rather than the one being added.

- **The `17.2.0` changelog entry** covers this change and `site-wide-closures` together, and is
  written at release with the date stamped at publication — the procedure `docs/publishing.md`
  records. This change adds a **new published interface a host may implement**, which is a
  contract addition a consumer acts on; `packaging`'s scenario for contract changes is written
  about members added to existing interfaces and does not describe this shape. The release entry
  must name it regardless.

- **`site-closures`' Purpose** still reads "and so that a later public-holiday feed has somewhere
  to write." No longer accurate in its mechanism: this is not a feed and nothing writes on its own
  — it is a host-implemented port, read only when an operator asks. A Purpose is prose rather than
  a requirement, so it is corrected when the deltas are synced into `openspec/specs/` at archive
  rather than through a `MODIFIED Requirements` entry.
