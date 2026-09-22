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

### Requirement: The package declares which Umbraco majors it accepts

A NuGet dependency version is a **minimum**, so declaring `Umbraco.Cms.Web.Website 18.2.0` says
"18.2.0 or higher" and nothing more. For as long as uBookIt published one line this was merely
imprecise; it is why the Umbraco Marketplace lists uBookIt as running on **v17 and v18**, which is
false, and why nothing a resolver reads has ever contradicted that.

With two lines published against two different Umbraco majors, an unbounded dependency is no
longer imprecise but wrong: a resolver asked for uBookIt has no machine-readable way to tell which
line a site's Umbraco can take, and the constraint exists only in prose — the readme, this spec
and `CLAUDE.md`, none of which a package manager reads.

So every `Umbraco.Cms.*` dependency a published uBookIt package declares SHALL carry an **upper
bound excluding the next Umbraco major**, and that bound SHALL be present in the packed nuspec
rather than only in the repository, because the nuspec is the only copy a consumer's resolver
ever sees.

**This is a restriction and it can be wrong in a direction an open bound cannot** — a host major
that would in fact have worked is refused. That is accepted deliberately rather than by default:
this project's own evidence is that an Umbraco major costs real work to support, and the failure
direction is right, because a resolver error naming the constraint is a better outcome than a
package that installs and fails inside somebody's site.

**The bound cannot be retrofitted.** Versions already on nuget.org keep the metadata they were
published with, so this requirement binds releases from here on and says nothing about
`17.0.0`–`17.1.1`.

#### Scenario: An unbounded Umbraco dependency fails

- **WHEN** a packed uBookIt package declares a dependency on an `Umbraco.Cms.*` package
- **AND** that dependency carries no upper bound
- **THEN** the suite fails, naming the dependency, because the published package would claim to
  support every future Umbraco major

#### Scenario: The bound is read from the packed nuspec

- **WHEN** the bound is checked
- **THEN** it is read from the produced `.nupkg`'s nuspec rather than from the repository's
  package-management file, because the nuspec is what a consumer's resolver reads and the two can
  disagree

#### Scenario: The bound excludes the next major and admits its own

- **WHEN** a packed dependency's range is inspected
- **THEN** it admits the Umbraco major this line targets, including its later minors and patches,
  and excludes the next major

#### Scenario: A uBookIt package's own dependencies are not bounded by this

- **WHEN** a packed uBookIt package declares a dependency on another uBookIt package
- **THEN** this requirement does not apply to it, because those are versioned in lockstep by this
  repository and are already covered by `The package can be installed`
