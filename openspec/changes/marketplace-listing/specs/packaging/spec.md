## ADDED Requirements

### Requirement: Only the package a site installs asks to be listed on the Umbraco Marketplace

The Umbraco Marketplace lists every NuGet package that carries the `umbraco-marketplace` tag and
depends on Umbraco. Umbraco's own guidance is to tag only the installable component. uBookIt's
installable component is the meta-package (the package a site installs, which reaches every other
package). A listing for one of the libraries leads a site author to install an incomplete product,
and a missing front-end assembly fails silently.

So of the packages this repository produces, **exactly one SHALL carry the `umbraco-marketplace`
tag, and it SHALL be the package a site installs**. Every other tag is unaffected by this rule.

The rule SHALL be checked against the **packed** metadata, not the build properties. The tag is
declared in one place and inherited in another, and the packed nuspec is the only artifact the
Marketplace reads.

A library version already published keeps its tag, because nuget.org does not allow a pushed
version's metadata to be edited. The publishing documentation SHALL say so. It SHALL NOT claim
that an earlier library listing disappears until that has been observed on the Marketplace.

#### Scenario: Only the installable package is tagged

- **WHEN** the packages this repository produces are packed and their metadata is inspected
- **THEN** the package a site installs carries the `umbraco-marketplace` tag, and no other
  package does

#### Scenario: A library that asks to be listed fails

- **WHEN** a library package's packed metadata carries the `umbraco-marketplace` tag
- **THEN** the check fails and names that package

#### Scenario: An installable package that stops asking to be listed fails

- **WHEN** the package a site installs is packed without the `umbraco-marketplace` tag
- **THEN** the check fails, so moving the tag off the libraries cannot pass by removing it from
  every package

#### Scenario: What the tag cannot undo is documented

- **WHEN** a reader consults the publishing documentation about the Marketplace
- **THEN** it says that listing follows from the tag without a submission, that only the
  installable package is tagged, and that library versions already published keep their tag

### Requirement: A package description names the Umbraco major its line targets

A package's description is the first thing nuget.org and the Marketplace show about it. Two lines
publish against two Umbraco majors from largely shared source, so a description written for one
line can ship on the other and be wrong there. That happened: the 18 line published a meta-package
describing itself as for Umbraco 17.

So **every Umbraco major a produced package's description names SHALL be the major its own
version declares**. The package a site installs SHALL name that major, so a site author can tell
from its description alone which Umbraco it runs on. A library description may name no major at
all.

The major SHALL be derived from the declared version, not written in the description by hand, so
the same source is correct on both lines. The check SHALL read the packed metadata.

#### Scenario: The installable package names its own major

- **WHEN** the packages are packed and the description of the package a site installs is read
- **THEN** it names Umbraco by the major of the package's own declared version

#### Scenario: A description naming another major fails

- **WHEN** any produced package's description names an Umbraco major other than its declared
  version's major
- **THEN** the check fails and names the package and the major it named

#### Scenario: An installable package that names no major fails

- **WHEN** the description of the package a site installs names no Umbraco major
- **THEN** the check fails, so the rule cannot pass by removing the claim it exists to keep true
