## MODIFIED Requirements

### Requirement: A release is published by pushing its tag
Pushing a release tag to the public repository SHALL start the publishing workflow for that tag,
and that workflow SHALL be the route by which a release reaches nuget.org. A release tag is
`<major>.<minor>.<patch>`, optionally followed by a hyphen and a prerelease label: one or more
non-empty, dot-separated identifiers of letters, digits and hyphens (for example `19.0.0-rc1` or
`19.0.0-rc.2`). It has no prefix and no build metadata. A prerelease tag
publishes exactly as a release tag does, and is held to every check a release tag is. A push of
any other tag or of a branch SHALL NOT publish anything. The packages pushed SHALL be built from
the tagged commit, packed from a clean clone within the same workflow run. They SHALL NOT be
packages built anywhere else or carried over from an earlier run.

#### Scenario: A release tag starts publishing
- **WHEN** a maintainer pushes the tag `17.2.2` to the public repository
- **THEN** the publishing workflow starts for the commit that tag names, without anyone triggering
  it by hand

#### Scenario: A prerelease tag starts publishing
- **WHEN** a maintainer pushes the tag `19.0.0-rc1` on a commit that declares `19.0.0-rc1`
- **THEN** the publishing workflow starts for that commit, and every check that applies to a
  release tag applies to it

#### Scenario: Other refs do not publish
- **WHEN** a branch is pushed, or a tag that is not a release tag (such as `v17.2.2`, `17.2`,
  `17.2.2-`, `17.2.2-rc..1` or `17.2.2+build.5`)
- **THEN** the publishing workflow does not start, or ends without publishing and names the tag
  as the reason

#### Scenario: What is pushed is what this run built
- **WHEN** the workflow publishes
- **THEN** every package pushed was packed from the tagged commit earlier in the same run

### Requirement: A release tag is checked before anything is built
Before packing, the workflow SHALL fail, naming the reason, when any of these holds:

- the tag is not the `<Version>` declared by the tagged commit, compared whole, including any
  prerelease label;
- the tagged commit is not contained in the published line whose current declared major equals
  the tag's major (the published lines being those the repository lists, see
  `continuous-integration`, *The published lines are listed once*), or no published line has that
  major;
- the tagged commit has no successful verification run for a push to a published line.

When a verification run for the tagged commit is still queued or in progress, the workflow SHALL
wait for it, for a bounded time, rather than fail at once. It SHALL fail when that run ends in
anything but success, or when the bound is reached. A failed check SHALL leave nuget.org
untouched.

#### Scenario: A tag that disagrees with the declared version is refused
- **WHEN** the tag `17.2.2` is pushed on a commit whose `<Version>` is `17.2.1`
- **THEN** the workflow fails before packing, naming both versions, and nothing is published

#### Scenario: A release tag on a prerelease commit is refused
- **WHEN** the tag `19.0.0` is pushed on a commit whose `<Version>` is `19.0.0-rc1`, or the tag
  `19.0.0-rc1` on a commit whose `<Version>` is `19.0.0`
- **THEN** the workflow fails before packing, naming both versions, and nothing is published

#### Scenario: A tag on the wrong line is refused
- **WHEN** a tag with major `18` names a commit that is contained in `main` but not in `dev/v18`
- **THEN** the workflow fails before packing, naming the line the tag's major requires

#### Scenario: A prerelease tag is placed on its line by its major
- **WHEN** the tag `19.0.0-rc1` names a commit contained in `dev/v19`, whose tip declares a `19`
  version, and that commit's push run on `dev/v19` succeeded
- **THEN** the check passes, naming `dev/v19` as the line

#### Scenario: A tag on an unpushed or feature-branch commit is refused
- **WHEN** a release tag names a commit that no published line contains
- **THEN** the workflow fails before packing, and nothing is published

#### Scenario: A commit whose verification failed is refused
- **WHEN** the tagged commit's verification run on its published line failed or was cancelled
- **THEN** the workflow fails before packing, naming that run

#### Scenario: Tagging straight after a merge waits for verification
- **WHEN** the tag is pushed while the tagged commit's verification run is still in progress
- **THEN** the workflow waits for that run, and proceeds only once it has succeeded
