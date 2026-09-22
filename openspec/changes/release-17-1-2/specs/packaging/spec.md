## ADDED Requirements

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
