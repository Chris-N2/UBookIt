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

### Requirement: The package declares which Umbraco majors it accepts

A NuGet dependency version is a **minimum**, so declaring `Umbraco.Cms.Web.Website 18.2.0` says
"18.2.0 or higher" and nothing more. For as long as uBookIt published one line this was merely
imprecise; it is why the Umbraco Marketplace was able to describe uBookIt as running on **v17 and
v18**, with nothing a resolver reads to contradict it.

With two lines published against two different Umbraco majors, an unbounded dependency is not
imprecise but wrong: a resolver asked for uBookIt would have no machine-readable way to tell which
line a site's Umbraco can take, and the constraint would live only in prose — the readme, this
spec and `CLAUDE.md`, none of which a package manager reads.

**That is no longer the state of the feed, and the sentences above are written in the past tense
for that reason.** From `17.1.2` on the 17 line and `18.0.0` on the 18 line, every published
version carries the bound in its packed nuspec, `18.1.0` included — so a resolver *can* now tell
the lines apart, and the requirement below is satisfied rather than merely stated. **What remains
unbounded is `17.0.0`–`17.1.1` and always will**, for the reason the retrofit paragraph gives: a
published version keeps the metadata it shipped with. A claim that uBookIt runs on both majors is
therefore still supportable from those four versions alone, and from nothing newer.

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

#### Scenario: The newest version of each line tells a resolver which line it is
- **WHEN** the most recently published version of either line is inspected by a package manager
- **THEN** its `Umbraco.Cms.*` dependencies carry an upper bound, so the major it accepts is machine-readable rather than stated only in prose
