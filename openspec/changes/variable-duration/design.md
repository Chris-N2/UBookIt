## Context

`BookingConstraints` already gives every resource a duration range (`MinDuration`/`MaxDuration`, default 30 min–8 h), and `BookingService.PlaceAsync` already validates an arbitrary requested length against it (`granularity`, `duration-too-short`, `duration-too-long`). Variable-length booking is therefore not a placement change — the placement pipeline needs nothing new.

Two things block it. `Service.Duration : TimeSpan?` has only two states (a fixed length, or null meaning "the fulfilling resource's minimum"), and no UI lets anyone choose a length: `BookingFormBuilder.BookingDuration(resource)` returns `constraints.MinDuration` unconditionally, and the chosen duration is rendered as narrative text in the form's legend rather than as an input.

This change was originally scoped as part of booking-via-service. Exploration split it out: building the duration model first, on the direct-resource path, means the service-booking slice consumes a finished model rather than threading a user-chosen length back through a completed flow.

Nothing in this repository is published. `Service` is not yet reachable from any booking path — no code outside the management CRUD edge and its tests reads `Service.Duration`. That is what makes a clean type change affordable now and expensive later.

## Goals / Non-Goals

**Goals:**

- Express all three real-world duration intents with a model that makes invalid states unrepresentable: a fixed length, a bounded variable length, and an unbounded variable length that defers to the resource.
- Let a visitor choose a booking length in the default front-end, no JavaScript required, without regressing anyone who ignores the new control.
- Give every front-end (default Razor, the future DevExpress package, headless clients) one availability query from which any length can be answered, so the maximum bookable length is discoverable rather than guessed.
- Leave the direct-resource booking path from change ⑤ behaviourally intact.

**Non-Goals:**

- Making a service bookable. Eligibility, union availability, and the candidate loop are ⑦-2.
- Capabilities (⑧), multi-role composition (⑨), per-resource service overrides.
- JavaScript progressive enhancement of the booking form.

## Decisions

### D1 — `Service.Duration` becomes a `ServiceDuration` value object, not extra nullable fields

`ServiceDuration` is a sealed value object with a private constructor and two validating factories: `Fixed(TimeSpan)` and `Variable(TimeSpan? min, TimeSpan? max)`. It exposes the kind and the bounds; `Service.Duration` becomes non-nullable of this type.

*Alternatives considered.* Keeping `Duration : TimeSpan?` and adding `MinDuration?`/`MaxDuration?` alongside gives eight field combinations of which six are invalid, with the validity rule (an XOR between "fixed" and "range") re-implemented independently in Core, `ServiceRowMapper`, the management DTO, `ServiceModelMapper`, `services-editor.element.ts`, and eventually the delivery models. Six copies of one rule is precisely the shape of defect that passes every test and diverges in the field. A mode enum beside three nullable fields fixes the ambiguity but still lets the enum disagree with the fields.

*Why now.* CLAUDE.md makes the public API a compatibility promise **once published**. Nothing is published, the repository is private, and no booking path consumes `Service.Duration`, so the cost of this change is a migration and a handful of call sites. After publication it would be a breaking change to a shipped contract.

### D2 — The late-bound "resource minimum" kind is dropped; `inherit` is redefined

The editor already names its non-fixed mode `inherit` (`services-editor.element.ts:8`). Today it inherits the resource's *minimum*; after this change it inherits the resource's *range*, i.e. `Variable(null, null)`.

The dropped capability is genuinely expressive: "each resource's own minimum" is a late-bound *fixed* length, which cannot be written as a static `(min, max)` pair — two barbers whose minimum appointment is 30 and 45 minutes respectively can no longer be modelled by one service. That is accepted knowingly. It is per-resource service configuration wearing a duration-mode costume, it belongs with ⑧'s per-resource work, and carrying a third kind forever to serve it would complicate every consumer of the model. The redefinition also makes the zero-configuration default *useful* — an unconfigured service becomes "any length this resource allows", which is the room-hire case that motivated the change, instead of "always the shortest possible booking".

### D3 — Narrow-only resolution, and an empty intersection is an answer rather than an error

A service's bounds narrow the resource's range; they never widen it.

```
effective = [ max(svc.Min ?? res.Min, res.Min) , min(svc.Max ?? res.Max, res.Max) ]
```

A resource's maximum is a hard ceiling regardless of what a service says. When the intersection is empty, that resource simply cannot fulfil that service — not a validation failure, because a service spanning many resources cannot know each one's limits, and failing at configuration time would make a perfectly reasonable service undefinable.

Two consequences worth stating explicitly, because both are easy to get wrong:

- **Service bounds need not align to any granularity.** They are bounds, not lengths. The bookable lengths are the granularity multiples *within* the effective range (rounded up from the minimum, down from the maximum). A service minimum of 40 minutes against a 15-minute-granularity resource yields a first bookable length of 45.
- If no granularity multiple falls inside the effective range, the resource cannot fulfil the service — the same answer as an empty intersection.

The same intersection becomes ⑦-2's cheap candidate pre-filter: a resource excluded here is excluded from union availability and from the placement loop, so a slot is never offered by a resource that could not honour it.

### D4 — `ResolveAgainst` ships now, even though its consumer is ⑦-2

`ServiceDuration.ResolveAgainst(BookingConstraints)` is implemented and unit-tested in this change although nothing calls it at runtime until services become bookable.

*Rationale.* The resolution *is* the meaning of the value object — a duration model that cannot say what it resolves to on a given resource is half a model, and D3's semantics would otherwise be specified with nothing to verify them against. It is a short pure function on the type itself, not a service, so it carries no wiring. Change ⑥ set this precedent deliberately: its "Service duration semantics" requirement fixed the contract with the note that it takes effect when booking-via-service ships.

*Trade-off acknowledged.* This is unconsumed code, which is normally worth challenging. The alternative — specify D3 now, implement in ⑦-2 — leaves a requirement no test exercises for a whole slice.

### D5 — A separate delivery endpoint, `GET resources/{id}/bookable-starts`

The existing `GET resources/{id}/slots?durationMinutes=` is unchanged. The new endpoint returns, for each aligned start, the minimum and maximum length bookable from it:

```
GET /umbraco/ubookit/api/v1/resources/{id}/bookable-starts?from=&to=
{ "resourceId": …, "zoneId": "Europe/London",
  "starts": [ { "startUtc": "…", "minDurationMinutes": 30, "maxDurationMinutes": 180 }, … ] }
```

*Alternative considered and rejected.* Making `durationMinutes` optional on `/slots` and returning a different response shape when it is absent — a conditional response body is hostile to typed clients and to OpenAPI generation.

*Naming.* `bookable-starts` over `slot-options` or `bookable-runs`: it says literally what each item is, and it does not overload "slot", which the delivery spec already binds to a start-plus-fixed-duration.

### D6 — Change ⑥'s migration is amended in place (destructive-schema approval record)

`persistence` requires additive-only migrations, with destructive changes needing explicit spec approval. Approval is recorded here: `20260807125020_AddServices` and its designer file are edited so that the services table carries the duration kind and bounds from the outset, rather than adding a second migration that partially reverses the first.

*Rationale.* No package has been published and no production database exists, so there is no upgrade path to preserve — the only affected database is the development TestSite, whose services have already been cleared. The migration history is an artifact that every future adopter reads; a clean single migration is worth more than an untidy pair that only ever made sense during pre-release development.

*Scope of the approval.* This is approval for this instance only. The standing additive-only requirement in `persistence` is not being weakened, and this change does not modify that requirement.

### D7 — The length control joins the existing step-1 GET form, not the POST form

The default front-end is a two-form flow: a GET form selects the date and reloads, then a POST form lists start times and collects details.

```
Step 1 (GET)    Date [14/08/2026]   Length [90 minutes ▾]   [Show times]
Step 2 (POST)   ( ) 09:00   ( ) 10:30   ( ) 14:00
                name / email / phone                        [Book]
```

Putting the length in step 1 means step 2 lists only starts that admit it, so no impossible start-plus-length combination can be submitted and no third round trip is needed. Defaulting the control to the resource's minimum makes the change a no-op for anyone who ignores it.

*Alternative considered.* Putting the length in step 2 alongside the start, with each start's maximum shown in its label and the pairing validated on submit. Rejected: it either needs a third round trip to bound the control, or accepts an invalid pairing and relies on an error message — worse for the accessibility baseline this package treats as a differentiator.

*Note on the query.* Choosing the length in step 1 does **not** make this a length-first design at the Core or API level. The start-first query (D8) still backs the form; the ViewComponent filters its results in-process. Rich clients consume the same query interactively.

### D8 — One computation, two projections

`SlotProjector` currently walks free intervals in granularity steps and emits a start wherever `[start, start + duration)` fits. The start-first query is the same walk, emitting for each qualifying start the run to the end of its containing free interval, clamped to the effective maximum and floored to a granularity multiple.

The fixed-length projection is derivable from the start-first result: `slots(L)` is the set of starts where `min ≤ L ≤ max`. Both projections are therefore implemented over one traversal rather than as two parallel walks, and a test asserts the equivalence for a spread of lengths. Two independent implementations that must agree are exactly what change ⑦a's QA found breaking in practice.

Lead time and horizon are applied identically in both, in the existing place.

### D9 — Core returns data; the view model composes the empty-state sentence

When no start admits the chosen length, the form states the longest length actually available that day rather than rendering an empty list. The longest-available figure is computed in `BookingFormBuilder` from the start-first results and carried on `BookingFormModel`; the Razor view renders the sentence.

Core returns no presentation strings. `BookingFormBuilder` is already the host-independent, unit-testable assembly point for the form, and keeping the sentence out of Core preserves the invariant that the front-end contract is data rather than widgets.

### D10 — `service-duration-invalid` stays a single code, with the field carrying the distinction

Invalid bounds (non-positive, sub-minute, minimum greater than maximum) reuse the existing `service-duration-invalid` code, with `DomainFailure.Field` identifying which input is at fault so the editor can associate the message with the right control for WCAG error identification. The services spec already requires that the editor render server-supplied messages rather than mapping codes to its own strings, so one code plus a field is coherent with the existing contract and avoids code proliferation for one logical rule.

### D11 — The length control is a `<select>` of granularity steps

*Alternative considered.* `<input type="number" min max step>`, which browsers validate natively. Rejected: native constraint-validation messaging varies across browsers and screen readers, so the WCAG 2.2 AA error-identification behaviour would be inconsistent and would still need duplicating server-side. A `<select>` of the valid lengths cannot express an invalid value at all, and it matches the discrete radio-list idiom the form already uses for start times.

Server-side validation is unconditional regardless; the control is an affordance, not a trust boundary.

### D12 — The management wire contract carries duration as one nested object

`DurationMinutes : int?` is replaced by a single nested member naming the kind explicitly:

```jsonc
"duration": { "kind": "fixed",    "minutes": 60 }
"duration": { "kind": "variable", "minMinutes": 45,   "maxMinutes": 120 }
"duration": { "kind": "variable", "minMinutes": null, "maxMinutes": null }
```

*Alternative considered.* Flat sibling fields (`durationKind`, `durationMinutes`, `durationMinMinutes`, `durationMaxMinutes`). Rejected for the same reason as D1: flat fields let the wire express combinations the domain cannot hold, so the mapper becomes a validator and the editor has to reason about which fields are meaningful for the current kind. A nested object with an explicit discriminator makes the invalid combinations unrepresentable in the contract, and mirrors the Core value object one-to-one so `ServiceModelMapper` stays a translation rather than an interpretation.

Deserialization of an unknown or absent `kind` is a 400 with `service-duration-invalid`, not a silent default — nothing depends on a legacy payload shape, so there is no back-compatibility rule to honour.

## Risks / Trade-offs

- **A coarse-granularity, wide-range resource produces a long `<select>`.** Option count is `(effectiveMax − effectiveMin) / granularity + 1` — 31 for the defaults (30 min–8 h at 15 min), but 133 for a 5-minute-granularity, 12-hour resource. → Accepted for this change; a long select remains keyboard- and screen-reader-navigable, which a free-text input with bespoke validation would not straightforwardly beat. Revisit if a real configuration makes it painful.
- **Dropping the late-bound minimum is unrecoverable without a model change.** → Deliberate (D2), recorded here and in the proposal's non-goals so ⑧ picks it up as per-resource configuration rather than reintroducing a third kind.
- **Amending a committed migration rewrites schema history.** Anyone who has already run the current `AddServices` migration gets a database that no longer matches it. → Only the TestSite is affected and it has been cleared; the implementation task list includes recreating it. Not repeatable after publication, and D6 records the approval as instance-specific.
- **A service can be configured whose bounds no resource can satisfy** (e.g. a 90-minute fixed length when every `room` caps at 60). Narrow-only makes this resolve to "no eligible resource" rather than a configuration error. → Out of scope here because nothing resolves services against resources yet; ⑦-2 should surface it, and it is the same class of problem as ⑦a's mistyped-type-key concern, which that change solved with a picker.
- **`ResolveAgainst` ships unconsumed.** → Justified in D4; flagged here so review treats it as a decision rather than an oversight.

## Migration Plan

1. Amend `20260807125020_AddServices` and its designer file in place; the services table gains a duration kind discriminator and nullable minimum/maximum minute columns, and loses `DurationMinutes`.
2. Drop and recreate the development database (or the uBookIt tables and the `__uBookItEFMigrationsHistory` rows) so the amended migration applies cleanly at TestSite startup.
3. No production rollback path is required or offered — no release has shipped.

## Open Questions

- Should the backoffice editor warn when a service's bounds cannot be satisfied by any currently defined resource of the role's type? The data to answer it exists (`ListTypesAsync` already backs the type picker), but the check is meaningless until services resolve against resources. Deferred to ⑦-2 with the rest of eligibility.
- Whether `bookable-starts` should later accept optional narrowing bounds so a caller can ask the question in service terms. Additive to a query API, so deliberately not decided now.
