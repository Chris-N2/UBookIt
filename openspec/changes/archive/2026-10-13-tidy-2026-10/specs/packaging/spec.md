## MODIFIED Requirements

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

