## Why

`umbraco-security-2026-10` (merged as `29f020d`, archived as `b007be8`) raised uBookIt's Umbraco
minimum to **17.7.1**, which closes four Umbraco advisories published on 6 October 2026:

- GHSA-w5jr-cmfx-544x (medium): document version endpoints read outside the user's start node.
- GHSA-7pj9-jmp9-p86q (medium): media deleted outside the user's start node.
- GHSA-h5wj-vpvj-mprp (low): upload extension restrictions are bypassed by a trailing period.
- GHSA-4c5q-6c64-96j7 (low): unauthenticated SignalR hubs disclose editorial activity.

**The new minimum reaches no site until it is on the feed.** The published `17.2.2` still declares
`[17.6.2, 18.0.0)`, so it installs on an unpatched Umbraco. Nothing else is waiting on `main`.

**Ruled a patch by the maintainer on 2026-10-11.** Raising a dependency's minimum asks something of
a site below it, which is the kind of change `CLAUDE.md` sends to a minor. The ruling holds
because:

- the step stays inside Umbraco's own major (17.6.x → 17.7.1, an Umbraco minor plus its patch);
- it is a security fix;
- the package manager enforces the minimum, so a site cannot end up on a combination the package
  does not support.

The entry says plainly what a site below 17.7.1 has to do.

## What Changes

1. **The version: `17.2.2` → `17.2.3`**, in every place `docs/publishing.md`'s *Before any push*
   table lists. That table is the authority, not this proposal. History anchors don't move.
2. **A `17.2.3` changelog entry**, undated until it is live. Every claim is checked against the
   repository or the packed nuspecs, not against this list.
   - **What you have to do:** on a site below Umbraco 17.7.1, upgrade `Umbraco.Cms` to 17.7.1
     first, as the README already says. Otherwise nothing. *(Corrected at apply, tasks 1.2 and
     QA rounds 1–2: a site's own direct reference to any package whose minimum Umbraco 17.7.1
     raises must be raised to match, stated as a rule with examples, not as a list.)*
   - **What changed:**
     - **The minimum Umbraco is 17.7.1**, naming the four advisories. They are Umbraco's
       advisories, and uBookIt's own code is not what they describe.
     - **The minimum Entity Framework Core SQL Server provider is 10.0.11**, because Umbraco 17.7.1
       requires it. `UBookIt.Persistence` declares what Umbraco needs.
     - **ImageSharp:** a site on Umbraco 17.7.1 may still report advisories against SixLabors
       ImageSharp 3.1.12. They arrive through Umbraco, not through uBookIt *(apply found
       `Umbraco.Cms` depends on it directly as well as through its imaging package, tasks 1.2)*,
       and no Umbraco release yet takes a fixed ImageSharp. Claimed only after the packed
       nuspecs and the audit confirm that no uBookIt package reaches ImageSharp.
3. **Pack from the merged, pushed commit, tag it, verify, and publish through `publish.yml`.** Then
   date the entry and archive, per `docs/publishing.md`.

A **patch**: no API signature, schema or migration change.

## Non-goals

- **`18.1.3`.** It gets its own change, `release-18-1-3`, on `dev/v18`, proposed in the same
  sitting with its own entry. That includes the deep-link check `umbraco-security-2026-10` left
  open, which belongs to the 18 line.
- **The tidy items** in the deferred obligations. They take effect on merge without a release.
  The ones a consumer sees ride with the next minor.
- **The audit exception.** It concerns `UBookIt.TestSite`, which does not ship. The entry
  mentions ImageSharp only as a site meets it.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

(none; `skip_specs: true`). The release binds no new requirement.

## Impact

- `Directory.Build.props` (`<Version>`, which CI's parity check exempts), `README.md`,
  `docs/publishing.md`, `CHANGELOG.md`.
- Five packages at `17.2.3` on nuget.org, frozen at push.
