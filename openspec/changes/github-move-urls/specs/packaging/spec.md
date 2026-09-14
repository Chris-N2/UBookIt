# packaging — delta for github-move-urls

## ADDED Requirements

### Requirement: The package points a consumer at a public home

A package page is a dead end if the links out of it are. The package SHALL declare a project
URL and a repository URL that are `https`, that do not name a host the publisher knows to be
private, and that identify the repository the source actually lives in — so a consumer who
follows either from a package feed or a marketplace listing arrives somewhere they can read
the code.

Whether a URL genuinely resolves SHALL NOT be asserted by an automated check: reaching the
network would make the check slow, fail offline, and go red for reasons outside this
repository. It is verified by a person before the first publish, and the publishing
documentation SHALL say so.

**The metadata a consumer sees and the source-fetch URLs a debugger follows come from
different places**, and the package SHALL document that: the declared URLs come from the build
properties, while symbol-package source links are derived from the repository's own remote. A
package built before its remote moved therefore carries stale source links even when its
declared URLs are correct, so the documentation SHALL state the order — correct the
properties, move the remote, then rebuild — and SHALL require the produced artifacts to be
inspected rather than assumed.

Because a published version's metadata cannot be corrected in place on a public feed, the
publishing documentation SHALL state what a push makes permanent.

#### Scenario: The declared URLs are public and name the source repository

- **WHEN** the package's declared project and repository URLs are inspected
- **THEN** both are `https`, neither names a host known to be private, and both identify the
  repository the source lives in

#### Scenario: Reachability is a human check, not an automated one

- **WHEN** the automated checks over package metadata are examined
- **THEN** none of them requests the URL over the network, and the publishing documentation
  names the manual check instead

#### Scenario: The ordering that makes source links correct is documented

- **WHEN** a maintainer consults the publishing documentation before a release
- **THEN** it states that symbol-package source links follow the repository remote rather than
  the declared URLs, gives the order that makes both correct, and requires the produced
  artifacts to be inspected before pushing

#### Scenario: What a push makes permanent is documented

- **WHEN** a maintainer consults the publishing documentation
- **THEN** it states that a pushed version's metadata cannot be edited and that a version
  number cannot be reused
