## Context

Every uBookIt setting is read from `IConfiguration` exactly once, at boot, by
`UBookItPersistenceComposer.ResolveSettings`, and the resulting `SiteBookingSettings` is registered
as a singleton and injected **by value** into roughly fifteen consumers across Core, Persistence,
Web and Backoffice. Two further settings types — `DeliveryApiSettings` and `UBookItThemeOptions` —
are bound at composition and affect how the application is *built* rather than how a request is
served.

That resolution code is not naive. It carries carefully asymmetric fallbacks, each chosen for which
failure is silent rather than which is convenient: an unreadable `MaxQueryRangeDays` becomes a
working default because the cost is a rejected query, while an unreadable `RetentionDays` becomes
*no retention* because the cost is irreversibly destroying personal data. Eleven scenarios in the
`persistence` spec pin that behaviour down.

The design problem is therefore not "how do we store settings in a table". It is **how to add a
second source without acquiring a second set of fallback rules**, because two sets would drift and
the drift would be invisible.

## Goals / Non-Goals

**Goals:**

- A stored value overrides a configured one, and the screen always shows what is being overridden.
- Exactly one implementation of every fallback rule, serving both sources.
- The tier boundaries are enforced by the server, not merely by what the client renders.
- No editable setting can destroy data.
- The captive-dependency defect this change enables is caught by a guard that would actually catch
  it.

**Non-Goals:**

- Caching stored settings across requests (see *Decision 5* — deliberately deferred, with the
  mechanism named).
- Any change to `FrontendSettings` or `DeliveryApiSettings` binding.
- A settings history or audit trail.

## Decisions

### Decision 1: Key/value rows, where the absence of a row is the reset

One row per overridden setting — `Key` (the configuration key, verbatim: `UBookIt:AutoConfirm`) and
`Value` (string) — rather than one row with a typed column per setting.

*Why not typed columns?* Two reasons, and the second is decisive:

1. Every future setting would need a migration, on a package whose migrations are additive-only.
2. **A nullable column cannot express "unset".** `PrivacyPolicyUrl = null` and `RetentionDays =
   null` are *meaningful values* in this domain — they mean "no link" and "no retention", and
   `SiteBookingSettings` documents at length that null must stay distinguishable from a default. A
   nullable column would conflate "the operator cleared this" with "the operator never touched it",
   which is precisely the distinction the reset action depends on.

With key/value rows, **"reset to configured value" is `DELETE FROM … WHERE Key = …`** — the stored
layer stops answering for that key and the configuration layer shows through again. No sentinel
value, no third state.

### Decision 2: The store produces a configuration layer, not a settings object

The store does not parse anything. It reads its rows and they are composed *over* the site's
configuration using the platform's own mechanism:

```
                 ┌──────────────────────────────┐
IConfiguration ─▶│ ConfigurationBuilder         │
(appsettings,    │   .AddConfiguration(site)    │──▶ effective IConfiguration
 env vars,       │   .AddInMemoryCollection(    │            │
 key vault)      │        stored rows)          │            ▼
                 └──────────────────────────────┘   ResolveSettings()  ← UNCHANGED
                          last source wins                   │
                                                             ▼
                                                   SiteBookingSettings
```

**This is the load-bearing decision.** `ResolveSettings` and every fallback rule inside it are
untouched and unaware there are now two sources. A stored value that cannot be read behaves exactly
as a configured value that cannot be read — same fallback, same error logged, same scenario. It is
why the `persistence` spec's eleven scenarios carry forward rather than needing eleven twins, and
why there is no second place for the retention fallback to be got wrong.

It also means the *whole* configuration stack keeps working underneath: a site using environment
variables or Key Vault gets those as the base layer, with stored values on top.

*Alternative considered:* a `SettingsResolver` that reads the store, parses values itself and merges
typed results. Rejected — it is a second implementation of every fallback, and the fallbacks are the
part of this system most expensively reasoned about.

### Decision 3: Validate on write; keep the fallback as defence in depth

Because a stored malformed value would fall back silently (correctly, by Decision 2), the *screen*
must not be able to create one. The controller validates on write and rejects, with the failure
reported against the field.

The resolver's fallback is not thereby redundant — it still covers a row written by a migration, a
direct database edit, or a value that was valid when stored and stopped being so. Validation is the
gate; the fallback is the floor. They are deliberately not the same check.

### Decision 4: Scoped lifetime, and the job resolves per unit of work

`AddSingleton(ResolveSettings(config))` becomes a scoped registration resolving through the store.
Every consumer's constructor is unchanged — this is a lifetime change, not a signature change, which
is what keeps the 17.0.0-frozen public surface intact.

**That is a claim about the SURFACE, not about consumer impact, and an earlier draft of this design
wrongly let it stand for both.** A consuming site that registered its own **singleton** taking
`SiteBookingSettings` in its constructor, or that resolved it from the root provider, was doing
something legal before and now fails at startup under `ValidateOnBuild`/`ValidateScopes` with
*"Cannot consume scoped service 'SiteBookingSettings' from singleton"*. Nothing inside the package
does either — every consumer here is already scoped — so no shipped code breaks. A site's might.
It therefore ships in a minor (17.1.0), which is where a behavioural break belongs, and carries an
upgrade note in `docs/configuration.md` naming the failure and the remedy (take an
`IServiceScopeFactory` and resolve inside a scope, as the retention job does).

`BookerRetentionJob` is the one blocker: a singleton holding `SiteBookingSettings` in its
constructor. It already takes `IServiceScopeFactory` and already creates a scope per batch, so it
resolves settings inside that scope instead. This is not a new constraint — the `persistence` spec
already requires the job to hold no scoped service; the change is what makes the requirement bite.

### Decision 5: No caching in this change

A scoped resolution means one query against a table of fewer than ten rows per scope. That is real,
and it is on the booking-page render path.

Caching is nonetheless deferred, because the naive version is *wrong* rather than merely premature:
uBookIt supports load-balanced Umbraco, and a per-server memory cache invalidated on write would
leave other servers serving stale settings indefinitely. Correct invalidation is Umbraco's
`ICacheRefresher` / distributed cache infrastructure, which is a meaningful piece of work with its
own failure modes and deserves its own change with a measurement behind it.

So: correctness first, one small indexed query, and the refresher named as the scaling path rather
than improvised here.

### Decision 6: Tiers are enforced server-side

The tier of each setting is declared once, server-side, and the write endpoint refuses any key that
is not tier 1 or tier 2. The client reads the same declaration to decide what to render.

A client that merely *declines to draw* an input for `RetentionDays` is not a control — the endpoint
is reachable directly. Given the tier boundary exists specifically to keep irreversible erasure out
of reach, the server has to be the one holding it. This mirrors the existing
`permissions` requirement that the client hides what the server would refuse, and the server remains
the truth.

### Decision 7: The tier-2 warning is static, and must be programmatically associated

The time zone warning states its consequence without computing anything:

> Availability rules are wall-clock in the site's time zone. Changing this makes a 9:00–17:00 rule
> mean 9:00–17:00 in the new zone. Existing bookings keep the actual times they were made for, so
> some may no longer fall inside their resource's hours.

True on every site, unconditionally — verified against storage: `OpenHoursRow` is `DayOfWeek` +
`TimeOnly` and `ExceptionRow` is `DateOnly` + `TimeOnly?`, neither carrying a zone, while bookings
are `StartUtc`/`EndUtc` as `DateTimeOffset`. Nothing is rewritten; the change is reversible.

**A known trap from the bookings screen (⑱): no `uui-*` control carries `aria-describedby`, and
`uui-label` is not a `<label>`.** A warning rendered as an adjacent paragraph is visually present
and programmatically unrelated to the control it warns about. The association has to be built
deliberately rather than assumed from proximity.

### Decision 8: The new verb reaches nobody on upgrade, and the section says so

`UBookIt.Settings` joins the vocabulary in all four places (server constants, verb policies, client
`permission-verbs.ts`, the `entityUserPermission` manifest), guarded by the existing test that fails
when the server and client vocabularies disagree.

`UBookItPermissionSeed` is one-shot behind the `permissions-seed` flag — already set on every 17.0.1
install — and its selection rule skips any group already holding a `UBookIt.*` verb. So the verb
reaches nobody, and **that is the intent**: auto-granting settings to every group that happens to
hold `Configure` would silently widen privilege on upgrade, which is exactly what the tier boundary
exists to prevent.

`UBookItVerbHandler` has no super-user bypass — it calls `context.Fail()` on a literal set
intersection — so even an Umbraco administrator must tick the box. That is recoverable (group
editing is Umbraco's permission, not uBookIt's) but not discoverable, so the section renders an
empty state naming the verb and where to grant it, rather than hiding and leaving the operator to
wonder whether the feature shipped.

## Risks / Trade-offs

**[The guard that will not catch the defect]** →
`BookerRetentionTests.The_job_captures_no_scoped_dependency` checks the job's constructor against a
**hardcoded array of six scoped types**. `SiteBookingSettings` is not among them, so it stays green
while the job captures a scoped dependency and holds one `DbContext` for the application's life —
silently, since the first run works. The mitigation is not a seventh entry: the guard must derive
the scoped set from the container's actual registrations, asserting *"holds nothing registered as
scoped"*. **It must be demonstrated failing against the unfixed job before it is trusted** — a
mutant leaving it green proves we do not know what it reads.

**[The configuration file becomes a lie]** → Once a value is stored, `appsettings` no longer
describes what runs, and a redeploy changing it does nothing with no error. Mitigated structurally
rather than by documentation: the screen always displays the configured value beside the effective
one, and reset is a first-class action, so the divergence is visible at exactly the place someone
would look.

**[A per-request query on the render path]** → Accepted for this change; see Decision 5. Bounded by
the table being tiny and keyed. Revisit with a measurement, not a hunch.

**[Wholesale replacement of a large requirement]** → The `persistence` requirement being modified
carries five SHALL blocks and eleven scenarios, most about fallbacks this change does not alter. A
rewrite that forgets one deletes a guarantee with nothing in the diff resembling a deletion.
Mitigated by Decision 2 — the fallbacks are *the same code* afterwards, so the scenarios are
restated rather than reimagined — and by the specs artifact enumerating each one's disposition
explicitly.

**[Tier drift between client and server]** → Two declarations of which setting is editable would
eventually disagree, and disagreement here means either a dead input or an unguarded one. Mitigated
by the server declaration being the only one, with the client reading it — and by Decision 6 making
the server refuse regardless.

## Migration Plan

One additive migration creating the settings table. Nothing existing is altered or dropped.

A fresh install and an upgraded install are indistinguishable afterwards: the table is empty, no key
is overridden, and every setting resolves from configuration exactly as before. **The change is
inert BEHAVIOURALLY until somebody is granted the verb and saves something**, which is also the
rollback story — deleting every row restores pre-change behaviour without touching the schema.

**"Inert" overstated it, though, and the correction matters.** `EffectiveConfiguration` calls
`store.GetAll()` unconditionally, so every scope that resolves `SiteBookingSettings` runs one
`SELECT` and forces a `DbContext` into existence — on every site, from the moment it upgrades,
whether or not anyone ever opens the screen. That is the cost Decision 5 defers rather than removes.
And a consuming site holding `SiteBookingSettings` in a singleton of its own now fails at startup
under `ValidateOnBuild` (see Decision 4), so the upgrade is not a no-op for everybody.

### Decision 9 (taken during apply): the active theme is not presented

`UBookIt.Backoffice` references Core and Persistence, not `UBookIt.Web`, where both
`DeliveryApiSettings` and `UBookItThemeOptions` live.

For delivery API exposure this costs nothing: the Backoffice reads
`UBookIt:DeliveryApi:EnableReads` / `EnablePlacement` straight from `IConfiguration`. Since those
are restart-bound and labelled as such, the configured value is the meaningful one to show anyway.

The theme has no configuration key at all — `AddUBookItTheme(...)` is a code call in the site's own
composer, and the `UBookIt:Theme` key is the TestSite's own invention, not the package's. Showing it
would require a `Backoffice → Web` project reference, inverting the layering permanently, to display
to a developer a string they wrote themselves in a file they own. It is therefore dropped from the
screen rather than reached for.

## Open Questions

- **Time zone input shape.** A free-text IANA id is what configuration accepts and what the resolver
  validates; a select of the host's known zones is kinder but couples the screen to
  `TimeZoneInfo.GetSystemTimeZones()`, whose contents differ between Windows and Linux hosts. Leaning
  free-text with validation on write, and the resolved zone's current offset echoed back as
  confirmation — but worth settling during apply.
- **Whether `MaxQueryRangeDays` is displayed at all.** It is tier 3 and developer-only, but unlike
  the restart-bound settings it is a value an operator might reasonably want to *see* when a date
  range is refused. Displaying it costs nothing; the question is only whether it adds noise.
