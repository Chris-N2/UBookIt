## ADDED Requirements

### Requirement: The package can be installed
A site SHALL be able to obtain uBookIt as a package and use it, by installing **one** thing.

**The package SHALL declare no dependency it does not ship.** Every uBookIt package a
consumer resolves SHALL be one this repository produces, so that adding the package succeeds
rather than failing to restore. A dependency on an assembly that exists only inside this
repository is not a dependency — it is a build that has never been asked to be consumed.

**Every assembly a working installation needs SHALL be reachable from that one package.**
Specifically: the domain, the persistence and migrations, the backoffice management API and
its client, and **the front-end rendering** — the Razor views, the theming mechanism and the
shipped stylesheet. The last of those is named explicitly because it is the one nothing else
references, so its absence is silent: the backoffice works, the booking page renders nothing,
and no error is raised anywhere.

**Package versions SHALL be produced together and SHALL match.** A uBookIt package depending
on a different version of a uBookIt package is a resolution nobody chose.

**Everything the packages carry SHALL be produced by the build that packs them.** A build
artifact that is not under version control and is not built by the build is absent from a
clean checkout, and packing then succeeds while shipping less than it claims. This is not a
convenience requirement about developer setup: it is the same silent absence as a missing
assembly, and it is *harder* to notice, because the working copy where anyone would check has
usually been made complete by hand already.

#### Scenario: One install is enough
- **WHEN** a site adds the single uBookIt package to a newly created Umbraco project
- **THEN** restore succeeds, and the site has the backoffice section, the management API, the delivery API and the front-end rendering available

#### Scenario: Nothing is declared that is not published
- **WHEN** the produced packages' dependencies are inspected
- **THEN** every uBookIt dependency names a package this repository also produces, at the same version

#### Scenario: The front-end rendering is in the box
- **WHEN** a site installs the package and publishes a booking page
- **THEN** the flow renders, using views and a stylesheet that arrived with the package rather than being copied into the site

#### Scenario: The backoffice client is in the box
- **WHEN** the packages are built from a checkout containing only what version control carries
- **THEN** the backoffice package contains the compiled client bundle and its manifest, rather than whatever an earlier manual build happened to leave in the working copy

#### Scenario: The aggregate does not drift
- **WHEN** a new packable assembly is added to the solution
- **THEN** the single package a site installs is required to reach it, rather than silently omitting it

### Requirement: The package says what it is and what may be done with it
The published package SHALL carry, in its own metadata: who produces it, a description of what
it does, where its source lives, a **licence**, and a readme.

**The repository SHALL carry a licence file.** A package that installs schema into somebody
else's database, and that describes itself as open source, cannot leave the terms of use to be
assumed — and a reader deciding whether they may adopt it should not have to ask.

**The version an editor sees SHALL be the version they installed.** The backoffice Packages
screen reads the package manifest, so a manifest version that does not track the package's own
reads as broken or abandoned.

Metadata SHALL NOT be left at framework defaults. A package whose author is its own assembly
name and whose description is "Package Description" tells a prospective consumer that nobody
has looked at it.

#### Scenario: The package identifies itself
- **WHEN** the produced package's metadata is inspected
- **THEN** it names its authors, describes what it does, links to its source, states its licence, and carries a readme — none of them a framework default

#### Scenario: The terms are in the repository
- **WHEN** somebody reads the repository to decide whether they may use it
- **THEN** a licence file states the terms

#### Scenario: The manifest version matches the package
- **WHEN** an editor views the installed package in the backoffice
- **THEN** the version shown is the version of the package they installed

### Requirement: Installability is proved by installing
The claim that the package can be installed SHALL be established by **installing it into a
site created from scratch**, and not by inspecting the built artifact.

The reason is specific rather than general: every defect this requirement exists to prevent —
a dependency on an unpublished package, an assembly left out entirely, a manifest at version
zero — was present in a build that reported complete success. **Inspection is what missed
them.** The development site references the projects directly, so it cannot see any of them
either; only a site that resolves the package can.

**What was verified SHALL be recorded, and its limits with it.** A verification performed once
is evidence about a moment. Where no automated guard exists, the record SHALL say so plainly
rather than allowing a performed check to be read as a standing one.

#### Scenario: The proof is a real installation
- **WHEN** installability is claimed
- **THEN** the evidence is a site created from scratch that resolved the package from a feed, ran, and took a booking

#### Scenario: The absence of a guard is stated
- **WHEN** the verification is recorded
- **THEN** it states that it was performed rather than automated, so that it is not later mistaken for a regression test
