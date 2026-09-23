## ADDED Requirements

### Requirement: A documentation link in the packed readme names the release it shipped with

The packed readme is frozen per published version, and the documents it links to are fetched live
from a repository that keeps moving. A link that names a **branch** therefore describes whatever
that branch holds today, on a package page nobody can correct.

This is the same failure the packed readme's images already guard against, and until this
repository published a second line it was invisible: with one line, `blob/main/docs/…` pointed at
the only documentation there was. **A second published line makes the branch component load-bearing
and wrong** — an `18.x` package whose readme links into `main` sends its reader to a different
product line's documentation, and does so silently, because the file exists on both branches.

So: every link in the packed readme that addresses a document in this repository SHALL name a git
ref that **does not move once published**, and that ref SHALL be **derived from the declared
version** rather than written out a second time — so a version bump cannot leave the links
pointing at the previous release while every guard stays green.

**This extends `Documentation a consumer follows from the package page resolves` rather than
replacing it, and that requirement is deliberately left intact.** That one proves link *shape* and
that the *path* names a file present in the working tree; its guard's own remarks state that the
git ref is invisible to it and name this work as owning the problem. The two are checked from
different sides on purpose: a link can have a correct ref and a dead path, or a live path and a
ref pointing at another line.

**What this does not claim** is unchanged from the requirement it extends: a guard proves the ref
is present and derived, not that the URL answers over the network. Reachability stays a human
check made once after the push, on the same terms already stated for the declared URLs and for
packed images — and because the ref is a release tag, that check **cannot pass before the tag is
pushed**, which the publishing runbook SHALL state.

#### Scenario: A documentation link naming a branch fails

- **WHEN** a link in the packed readme addresses a document in this repository
- **AND** the git ref it names is a branch rather than the released version
- **THEN** the suite fails, naming the link, because the page it resolves to will describe
  whatever that branch holds rather than what this version shipped

#### Scenario: A documentation link left at the previous release fails

- **WHEN** the declared version changes
- **AND** a documentation link in the packed readme still names the previous version's ref
- **THEN** the suite fails, because the readme would ship a reader the last release's
  documentation while every other guard stayed green

#### Scenario: The ref is derived, not repeated

- **WHEN** the ref in a documentation link is checked
- **THEN** it is derived from the declared version, so the readme cannot disagree with the package
  it is packed into

#### Scenario: A link to somebody else's site is accepted unchecked

- **WHEN** a readme link addresses a host that is not this repository
- **THEN** it passes without a ref check, because it names no release of ours to pin to — the same
  narrowing this capability already applies to images that belong to somebody else

#### Scenario: Reachability after tagging is a human check the runbook states

- **WHEN** the guard passes
- **THEN** it has proved the ref's shape and derivation only, and the publishing runbook states
  that a tag must exist before the links resolve, so a publish cannot satisfy the guard and still
  ship links that answer nothing

## MODIFIED Requirements

### Requirement: A release names the contract changes a consumer must act on

The package's versioning policy already promises that a breaking change is called out
explicitly rather than left to be discovered. A policy is not a callout. **Where a release
changes a published contract, the package SHALL name that change in documentation a consumer
can reach from the package page**, and SHALL name it per release rather than in aggregate, so
a reader upgrading between two specific versions can see what lies between them.

The callout SHALL state **what a consuming site has to do**, not only what changed. A member
added to a published interface without a default implementation stops an implementing site
compiling, and "gained a member" does not tell a reader that. The obligation is the fact the
consumer needs; the enumeration of members is how they discharge it.

**A published contract is also changed by a whole new type a host may implement, and such an
addition SHALL be named on the same terms.** It obliges a consumer to nothing — nothing stops
compiling, and a site that ignores it keeps the product it had — so the argument from obligation
that carries the member case does not reach it. What carries it is that an extension point
nobody is told about is not one: a site that does not know a seam exists will either go without
the capability or reach past the contracts into the database, which is the outcome every port in
this package exists to prevent. The entry SHALL therefore name the type, say what implementing it
gives the site, and say that nothing is required of a site that does not.

**The entry SHALL be required to exist for the declared version, and that requirement SHALL be
verified rather than asserted.** A release note written by hand and checked by nobody is stale
at the next release, which is the failure mode this package has already paid for with prose
that drifted from the build. The check SHALL fail when the declared version has no entry, or
an empty one — the same shape as the existing rule that documentation stating the current
version must equal the declared version.

Entries for releases that have already happened SHALL record what was true of them, and SHALL
NOT be edited to match a later release. They are history on the same terms as the version
anchors: a changelog that can be rewritten forward records nothing.

The callout SHALL NOT be duplicated into the package's frozen metadata. Metadata cannot be
corrected after a push, and a second copy of a changing document is a second source of truth;
a reference to the single copy is what the package page carries.

#### Scenario: A release that changes a published contract says so

- **WHEN** a release adds a member to a published interface with no default implementation
- **THEN** the release's entry names each affected interface and the member added
- **AND** states that a site implementing that interface will not compile until it adds them

#### Scenario: A release with no contract change still has an entry

- **WHEN** a release changes no published contract
- **THEN** it still has an entry, describing what it does contain
- **AND** the entry does not claim a breaking change that did not occur

#### Scenario: A bump that forgets the release notes fails

- **WHEN** the declared version changes and no entry exists for the new version
- **THEN** the check fails and names the version it could not find

#### Scenario: An empty entry does not satisfy the check

- **WHEN** an entry exists for the declared version but carries no content
- **THEN** the check fails, because a heading is not a callout

#### Scenario: A past release's entry is not moved by a later one

- **WHEN** a new release is prepared
- **THEN** the entries describing earlier releases are unchanged
- **AND** a check that compares them to the declared version does not require them to equal it

#### Scenario: A release that publishes a new extension point names it

- **WHEN** a release adds a new published interface a host site may implement
- **THEN** the release's entry names the interface, says what implementing it enables, and states that a site which implements nothing is unaffected
- **AND** the entry does not describe it as a breaking change, because nothing a consumer already wrote stops working
