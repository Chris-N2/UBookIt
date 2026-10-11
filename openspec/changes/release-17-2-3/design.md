## Context

`17.2.2` was the first release through `publish.yml`, and its procedure worked first time: tag on
the merge commit, `check` and `pack` green, verify the artifact, then approve in `release`. This
release repeats it. The only new content is the Umbraco floor `umbraco-security-2026-10` already
merged.

## Decisions

### D1. Same route as `17.2.2`

Branch `release-17-2-3` from `origin/main`, merge by PR with "Create a merge commit", and tag the
merge commit once its own `ci` push run is green. There is no hand route. If the workflow cannot
publish, the release waits (`docs/publishing.md`, *There is no hand route*).

### D2. The nuspecs are read for the floor, not just for the version

The point of this release is the dependency ranges. Before approving, read every `Umbraco.Cms.*`
dependency in the artifact's nuspecs. Each must be `[17.7.1, 18.0.0)`.
`Microsoft.EntityFrameworkCore.SqlServer` must be `10.0.11`. Confirm that no package declares
SixLabors.ImageSharp, directly or through anything uBookIt declares. A nuspec with the old floor
means the pack did not take the merged `Directory.Packages.props`, so reject the run.

### D3. The ImageSharp sentence is a claim about what a site sees

uBookIt's packages don't reach ImageSharp. A site still does, through `Umbraco.Cms`. The entry
says that so an advisory warning on the site isn't read as uBookIt's. **It names no fix date**,
because none exists.

### D4. `18.1.3` follows in its own change, in the same sitting

The same shape on `dev/v18`, at `18.2.1`. The 18 line has one extra gate before tagging: the
direct-link blank view from `umbraco-security-2026-10` is checked on the release commit. A
regression introduced by 18.2.1 is reported to the maintainer before anything is tagged.

## Risks / Trade-offs

- **[A site below 17.7.1 updates uBookIt without reading the entry]** → NuGet raises the Umbraco
  packages uBookIt depends on and leaves `Umbraco.Cms` where it is. The README's requirements row
  and the entry both say to upgrade `Umbraco.Cms` first. That's the most a package can do; the
  range itself is enforced.
- **[Stale build output after switching lines]** → Before the clean build, delete
  `src/*/bin/Release`, `src/*/obj/Release` and the backoffice `App_Plugins` output, and build the
  client first. The expected unit total on this line is **2029**.
