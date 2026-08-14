## Why

Today a service's role resolves to resources by type key alone: every `room` is
interchangeable, every `person` is interchangeable. Real booking systems are not
like that — "Service B may only be booked with Mary, because she holds the
certification" is the motivating case, and there is currently no way to express
it. Eligibility needs a second dimension.

This change adds that dimension in its smallest useful form: capability tags on
resources, required capabilities on a service role, and a subset check between
them. It also discharges an obligation change ⑦-2 deliberately left open —
`resource-not-eligible` is an oracle for pool membership, and ⑦-2's design
records that ⑧ must revisit it because capabilities are what break the
derivability that made it harmless.

## What Changes

- **`Resource` gains a capability set.** An open, extensible set of normalized
  keys, exactly as `Resource.Type` is an open normalized key. Empty is the
  default and means "carries no capabilities".
- **`ServiceRole` gains a required-capability set.** Empty is the default and
  preserves today's behaviour precisely.
- **Eligibility gains one term.** `Eligible(role) = r.Type == role.ResourceType
  ∧ role.RequiredCapabilities ⊆ r.Capabilities`. Everything downstream of
  candidate resolution — the duration intersection, the candidate loop, union
  availability, `Admits`, the two all-fail codes — is untouched.
- **A `CapabilitySet` value object** owns key validation, normalization, and the
  subset test in one place, following `ServiceDuration`'s precedent (private
  constructor, validating factory). It gives the capability set structural
  equality, which a bare `IReadOnlySet<string>` member on a record would not.
- **Capabilities are published on the delivery API.** Resource capabilities and
  role required-capabilities both appear in delivery read models. This is what
  discharges ⑦-2's D9 obligation: eligibility becomes derivable from public
  reads again, so the `resource-not-eligible` probe discloses nothing a caller
  could not already compute, and D9's reject-don't-ignore behaviour stands
  unchanged.
- **A derived capability vocabulary endpoint** on the management API, mirroring
  ⑦a's resource-type usage endpoint: a grouped projection over keys already in
  use, backing a type-ahead picker. Free entry remains allowed — the vocabulary
  is descriptive, not a closed list.
- **Capability editing in both backoffice editors** — a tag-style control on the
  resource workspace and on the service's role.
- **A pool diagnostic in the services editor**, reporting how many resources
  carry the role's capabilities as they are edited. It previews an *unsaved*
  role, so it cannot reuse `ResolveCandidatesAsync` (which takes a saved service
  id) and needs its own preview endpoint over `(resourceType, capabilities)`.
- **Two additive child tables** and one new migration.
- **BREAKING (source, unpublished):** `Resource.Create` gains a parameter and
  `ServiceRole` gains a member, so callers constructing either positionally must
  change. Nothing is published and the repository is private, so this is taken
  as a clean change rather than shimmed — the same call Chris made for
  `ServiceDuration` in ⑦-1. No database change is destructive; no existing row
  is rewritten.

## Capabilities

### New Capabilities

None. Every new requirement has an existing sibling in an existing spec — the
key-format rule sits beside the type-key rule in `resources`, the vocabulary
endpoint beside the type-usage endpoint in `resource-management`, the diagnostic
beside the type picker in `services`, the subset rule inside the eligibility
requirement in `service-booking`. Introducing a cross-cutting `capabilities`
spec would duplicate ownership rather than clarify it.

### Modified Capabilities

- `resources`: `Resource` carries a capability set; capability keys are
  extensible normalized keys under the same rule as type keys.
- `services`: `ServiceRole` carries required capabilities; the services editor
  edits them and reports the matching-resource count.
- `service-booking`: eligible-resource resolution gains the subset term.
- `resource-management`: resource CRUD carries capabilities; a capability usage
  endpoint; a role-preview endpoint; the resource editor edits capabilities.
- `delivery-api`: the resource read model and the service read model publish
  capabilities.
- `persistence`: two additive child tables and their store semantics.

## Non-goals

- **Client-specific exclusions are not capabilities.** "This client must not be
  offered Joan" is a private filter composed on top of eligibility
  (`Visible(client) = Eligible(role) ∖ exclusions`), not a tag. It differs in
  data sensitivity, in lifecycle, and in authentication context — it needs an
  identity the anonymous booking flow does not have. Modelling it as a
  capability would drag private data into a public contract.
- **No multi-role composition and no role count > 1.** `Service.Create` still
  rejects both. Capabilities are what *create* overlapping eligibility pools,
  which is what will make ⑨'s assignment problem real; ⑧ stays single-role so it
  cannot fire.
- **No front-end changes.** No Razor, no ViewComponent, no default-frontend
  spec delta. The data is published; displaying it belongs to ⑩'s
  service-oriented front-end.
- **No closed vocabulary.** No capability entity, no CRUD for tags, no display
  names, no rename or delete semantics. The vocabulary is derived from use.
- **The pool diagnostic reports capability matching only** — not whether the
  service is bookable. `ResolveCandidatesAsync` drops candidates on two grounds,
  eligibility *and* no duration overlap, and ⑧ checks only the first. The
  diagnostic's wording must claim only what it verifies; reporting "3 rooms can
  provide this service" when duration excludes them all would be the exact
  failure the diagnostic exists to prevent. Reporting the duration exclusion is
  change ⑧a.

## Impact

**Core** — `Resource`, `ServiceRole`, `Service.Create` validation, new
`CapabilitySet`, `ServiceBookingService.ResolveCandidatesAsync`, a shared
normalized-key predicate replacing the third copy of the kebab-case regex.

**Persistence** — two child tables, one new migration (not an amendment to ⑥'s),
row mappers, and capability hydration in the existing resource reads. The read
port gains **no new method**: the subset test runs in Core over hydrated
capabilities rather than being pushed into SQL, so eligibility has exactly one
implementation (design D5). `IResourceManagementStore` gains the usage
projection and the preview lookup — ⑦-2 deliberately kept `ListTypesAsync`
management-only, and that line holds here.

**Backoffice** — resource and service DTOs, mappers, two controller endpoints, a
regenerated client, capability editing in both Lit editors, the diagnostic
readout, and an accessibility baseline for the new controls.

**Web** — delivery read models and mappers only. No rendering changes.

**Tests** — unit coverage for `CapabilitySet` and subset eligibility, integration
coverage for the new tables and endpoints. Fixtures must include **overlapping**
capability pools (`{Mary} ⊂ {Mary, Frank}`) from the outset, so that the
assignment defect ⑨ must solve is not invisible to every test this change leaves
behind.
