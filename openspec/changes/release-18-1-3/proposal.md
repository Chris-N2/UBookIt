## Why

`umbraco-security-2026-10` (merged as `8bc5361`, archived as `836c653`) raised uBookIt's Umbraco
minimum to **18.2.1**, which closes four Umbraco advisories published on 6 October 2026:

- GHSA-w5jr-cmfx-544x (medium): document version endpoints read outside the user's start node.
- GHSA-7pj9-jmp9-p86q (medium): media deleted outside the user's start node.
- GHSA-h5wj-vpvj-mprp (low): upload extension restrictions are bypassed by a trailing period.
- GHSA-4c5q-6c64-96j7 (low): unauthenticated SignalR hubs disclose editorial activity.

**The new minimum reaches no site until it is on the feed.** The published `18.1.2` still declares
`[18.2.0, 19.0.0)`, so it installs on an unpatched Umbraco. Nothing else is waiting on `dev/v18`.

**Ruled a patch by the maintainer on 2026-10-11.** Raising a dependency's minimum asks something of
a site below it, which is the kind of change `CLAUDE.md` sends to a minor. The ruling holds
because:

- the step is a patch of Umbraco itself (18.2.0 → 18.2.1 inside the same major);
- it is a security fix;
- the package manager enforces the minimum, so a site cannot end up on a combination the package
  does not support.

The entry says plainly what a site below 18.2.1 has to do.

## What Changes

1. **The version: `18.1.2` → `18.1.3`**, in every place `docs/publishing.md`'s *Before any push*
   table lists. That table is the authority, not this proposal. History anchors don't move.
2. **A `18.1.3` changelog entry**, undated until it is live. Every claim is checked against the
   repository or the packed nuspecs, not against this list.
   - **What you have to do:** on a site below Umbraco 18.2.1, upgrade `Umbraco.Cms` to 18.2.1
     first, as the README already says. Otherwise nothing.
   - **What changed:**
     - **The minimum Umbraco is 18.2.1**, naming the four advisories. They are Umbraco's
       advisories, and uBookIt's own code is not what they describe.
     - **ImageSharp:** a site on Umbraco 18.2.1 may still report advisories against SixLabors
       ImageSharp 3.1.12. They arrive through Umbraco, not through uBookIt *(apply found
       `Umbraco.Cms` depends on it directly as well as through its imaging package, tasks 1.2)*,
       and no Umbraco release yet takes a fixed ImageSharp. Claimed only after the packed
       nuspecs and the audit confirm that no uBookIt package reaches ImageSharp.
   - Unlike `17.2.3`, there is no Entity Framework Core change: `18.1.2` already declared
     `10.0.11`, and its floor, Umbraco 18.2.0, already required it (QA round 1).
3. **Before tagging, the direct-link check** that `umbraco-security-2026-10` left open (its
   deferred obligation): on the 18.2.1 TestSite, navigate straight to
   `/umbraco/section/ubookit/view/bookings`. If it renders blank, repeat on 18.2.0 (`080dc6e`) and
   on `main`. If it is older than this release, it is recorded as debt. **If 18.2.1 introduced it,
   it is reported to the maintainer before tagging**: that is a decision for them, not something
   this change absorbs.
4. **Pack from the merged, pushed commit, tag it, verify, and publish through `publish.yml`.** Then
   date the entry and archive, per `docs/publishing.md`.

A **patch**: no API signature, schema or migration change.

## Non-goals

- **`17.2.3`.** It is its own change, `release-17-2-3`, on `main`, proposed in the same sitting.
  This change carries its procedure with the 18 line's values.
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
- Five packages at `18.1.3` on nuget.org, frozen at push.
