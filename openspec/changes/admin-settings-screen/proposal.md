## Why

Every uBookIt setting lives in `appsettings` and nowhere else. That was a deliberate choice at
0.5.0 — a settings *screen* had to be admin-only, which needed permissions that did not exist yet,
so config-file settings kept the change small and removed the dependency. Those permissions arrived
in 0.10.0, and the reason is gone.

What remains is that the people who most often need to change these settings cannot. Whether
bookings need approval, whether the booker is emailed, who internally is told, and what the privacy
notice links to are decisions belonging to whoever runs the bookings — not to whoever can edit a
JSON file and redeploy. Today every one of them is a developer ticket.

## What Changes

- **A settings screen in the uBookIt backoffice section**, gated on a new permission verb, showing
  every uBookIt setting with its current effective value and where that value came from.
- **Settings gain a second source: the database, which wins over `appsettings`.** A new table and an
  additive migration. The screen always shows the `appsettings` value a stored value is overriding,
  and "reset to configured value" is a first-class action, so the configuration file never becomes a
  lie a redeploy cannot correct.
- **Settings are sorted into three tiers by who is competent to judge them**, under one governing
  principle: *policy and communication are the operator's; cost, safety and data lifetime are the
  developer's.*

  | Tier | Settings | Behaviour |
  |---|---|---|
  | 1 — editable | `AutoConfirm`, `Notifications:SendBookerEmails`, `Notifications:InternalRecipients`, `PrivacyPolicyUrl` | edit, save, reset |
  | 2 — editable, warned | `TimeZoneId` | as tier 1, plus a static consequence statement |
  | 3 — read-only | `RetentionDays`, `MaxQueryRangeDays`, `DeliveryApi:EnableReads`, `DeliveryApi:EnablePlacement` | displayed with source; the last two labelled restart-bound |

  The **active theme is not presented at all** — decided during apply. It has no configuration
  key, it is a code registration in the site's own composer, and `UBookIt.Backoffice` does not
  reference `UBookIt.Web`; showing it would couple the management assembly to the rendering one
  in order to tell a developer something they wrote themselves.

- **`SiteBookingSettings` resolution moves from singleton to scoped**, resolved from a store that
  falls back to `IConfiguration`. Every constructor signature is unchanged, so the public API
  *surface* frozen at 17.0.0 is untouched.

  **It is not, however, free of consumer impact, and an earlier draft of this proposal wrongly said
  it was.** A consuming site that registered its own **singleton** taking `SiteBookingSettings` in
  its constructor, or that resolved it from the root provider, was doing something legal before and
  will now fail at startup under `ValidateOnBuild`/`ValidateScopes` with *"Cannot consume scoped
  service"*. Nothing inside the package does this — every consumer here is already scoped — but a
  site might. It ships in a minor (17.1.0), which is where a behavioural break belongs, and it
  carries an upgrade note telling such a site to take a scope or an `IServiceScopeFactory`.
- **A new permission verb, `UBookIt.Settings`**, joining the existing three across the server
  constants, the verb policies, the client vocabulary and the `entityUserPermission` manifest.
- **The new verb is deliberately not granted to anybody on upgrade**, and the section explains its
  own absence rather than simply hiding.
- **`docs/configuration.md`**, which does not exist today: the settings are currently documented
  across four unrelated documents, and the screen makes a single reference necessary.

### Not a breaking change, and the two places that could have been

Neither of these is a break, but both are the kind that hides:

- **The public surface is untouched, but the lifetime is a behavioural change for consumers.**
  `SiteBookingSettings` keeps its shape and every consumer inside the package keeps its constructor.
  A *site* holding it in a singleton of its own is affected — see above. Called out here rather than
  left implied, because "no signature changed" and "nothing can break" are not the same claim.
- **No destructive schema change.** One new table; nothing is altered or dropped.

### Non-goals

- **Making `RetentionDays` editable.** It stays config-only, and this is a decision rather than an
  omission. Erasure is irreversible, its consequence is deferred to the next hourly sweep rather
  than visible at save time, and a warning accurate enough to be useful would have to compute a
  count — which can be wrong, and a dialog that lies about erasure is worse than no dialog. This
  preserves a property worth more than the flexibility: **nothing reachable from the settings screen
  destroys data.**
- **Making the restart-bound settings editable.** Delivery API exposure is decided while the MVC
  application model is built, with disabled directions' selectors removed — that is what makes
  "absent, not refused" structural, and a runtime toggle would dismantle it. The theme is fixed at
  composition and its view-location cache is not keyed by theme name. Both are displayed, neither is
  edited.
- **Per-setting locking in configuration** (e.g. a `Locked: [...]` list). The permission grant is
  already the control: a site that does not want its operators changing settings does not grant the
  verb. A second mechanism would add a way for the two to disagree.
- **Seeding the new verb to existing groups.** See below — the absence is the decision.
- **Editor-editable email content.** It depends on this screen and belongs on the operator's side of
  the principle, but it is its own change.
- **Per-service setting overrides.** `AutoConfirm` is site-wide today and stays so; nothing here
  forecloses the additive per-service override already anticipated in `SiteBookingSettings`.

## Capabilities

### New Capabilities

- `site-settings`: Where a setting's value comes from and which source wins; the three tiers and
  what each permits; the screen's obligation to show the overridden configuration value and offer a
  reset; the static consequence statement on the time zone; and the guarantee that no editable
  setting can destroy data.

### Modified Capabilities

- `persistence`: The requirement *"Package composition registers persistence and Core services"*
  states that `SiteBookingSettings` is **bound from the `UBookIt` configuration section** and
  registered at composition. Both the source and the lifetime change. The same requirement also owns
  the captive-dependency guarantee for the retention job, which this change puts under real load for
  the first time.
- `permissions`: The requirement *"Access within the section is decided by three verbs"* is false
  with a fourth — its title, its body and its scenarios. The requirement *"Existing section-granted
  groups are seeded once"* must state that the new verb is outside the seed and why.

**Both are wholesale replacements, and `persistence`'s is large** — five SHALL blocks and eleven
scenarios, most of them about resolution fallbacks for settings this change does not otherwise
touch (retention, auto-confirm, privacy link, time zone). Per the project rule, the specs artifact
carries every one forward explicitly or records it as a deliberate drop with its reason. None is
expected to be dropped: the fallback behaviour is unchanged, it simply now applies to the
configuration layer beneath the store.

### Deliberately unmodified

The sweep for sibling specs this change falsifies came back clean, and the reason is worth
recording: `bookings`, `privacy-notice`, `booking-emails` and `booking-retention` all phrase their
settings as *"the site's configured X"* — source-agnostic. Adding the database as a second way to
configure something does not falsify any of them. Only `persistence` names the mechanism.

## Impact

### The landmine this change walks onto

`BookerRetentionJob` is a singleton that takes `SiteBookingSettings` in its constructor. That is
legal today because the settings are a singleton. **The moment they become scoped, the job captures
a scoped dependency and holds one `DbContext` for the life of the application** — silently, because
it works perfectly on the first run.

The `persistence` spec already forbids exactly this, and a guard already exists for it. **The guard
will not catch it.** `BookerRetentionTests.The_job_captures_no_scoped_dependency` checks the job's
constructor parameters against a **hardcoded list of six scoped types**; `SiteBookingSettings` is
not among them, and would not be added by anything in this change that was not looking for it. The
guard stays green while the defect it was written to prevent ships.

This is the project's most-repeated lesson — a rule that checks the mechanism rather than the
guarantee — so the fix is not to add a seventh type to the list. **The guard must derive the scoped
set from the container's actual registrations**, so that it asserts *"the job holds nothing
registered as scoped"* rather than *"the job holds none of these six names"*. It must be shown
failing against the unfixed job before it is trusted.

### Carried in as an unblocker

`[ProducesResponseType(StatusCodes.Status403Forbidden)]` is removed from
`BookingsController.FindBookingsByBooker` and `.EraseBooker`. Umbraco's own
`BackOfficeSecurityRequirementsOperationFilterBase` also adds a 403 to those operations, and the
collision threw `SwaggerGeneratorException: An item with the same key has already been added. Key:
403` — which blocked generating the API client this change needs. Pre-existing and latent, since
nothing had regenerated the client since the filter's behaviour and these attributes last met. The
403 remains in the published contract (the filter supplies it), verified in the generated
`types.gen.ts`. Recorded here rather than left as a silent edit to a security-relevant controller.

### Code

- `UBookIt.Core` — `SiteBookingSettings` unchanged in shape; a new port for reading and writing
  stored settings.
- `UBookIt.Persistence` — new entity, table and additive migration; the store implementation;
  `UBookItPersistenceComposer.ResolveSettings` becomes the configuration-layer fallback beneath it;
  the registration lifetime changes; `BookerRetentionJob` resolves settings per unit of work.
- `UBookIt.Backoffice` — new `Constants.Verbs.Settings` and `VerbPolicies.Settings`; a new settings
  controller; the client vocabulary, manifest and a new Lit view; the section's empty state.
- `UBookIt.Web` — no change; `FrontendSettings` and `DeliveryApiSettings` stay config-bound.

### Tests

The scoped-lifetime change touches the resolution path of every consumer of `SiteBookingSettings`,
so the existing suites around availability, bookings, emails, privacy and retention are the
regression surface even though none of their behaviour changes.

### Docs

`docs/configuration.md` (new, and the single reference the package currently lacks), plus the
upgrade note that the settings screen is invisible until somebody grants the verb.

### Release

17.1.0. This is a feature and cannot ship as `17.0.x` — the README publishes, and the suite guards,
that a patch carries fixes and internal changes only.
