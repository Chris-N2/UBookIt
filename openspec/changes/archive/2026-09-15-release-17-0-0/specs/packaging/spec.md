# packaging — delta for release-17-0-0

## ADDED Requirements

### Requirement: The version a reader is told is the version the package carries

A version number is a claim, and a claim in prose drifts from the build the moment
somebody bumps one and not the other. Wherever the package's own documentation states
uBookIt's version, that version SHALL be the one `Directory.Build.props` declares — and
SHALL be verified against it rather than restated, so a later bump fails the check instead
of silently leaving the documentation describing a release nobody can install.

The documentation SHALL also state what the version means, because under this package's
scheme it does not mean what a reader would assume. The major tracks the Umbraco major
the package targets, so it SHALL NOT be read as a signal of a breaking change; the
package SHALL state that it departs from Semantic Versioning in exactly that respect,
rather than leaving a reader to infer it.

The versioning policy SHALL be stated where a consumer meets the version: the public
interface is kept as consistent as possible; a breaking change, where genuinely required,
lands in a minor version and never a patch, and ships with defaults or an upgrade path
that keep an existing site working.

#### Scenario: The documented version tracks the declared version

- **WHEN** the package's documentation states uBookIt's version
- **THEN** it is the version declared in `Directory.Build.props`, verified against it

#### Scenario: A bump that misses the documentation fails

- **WHEN** the declared version changes and a document still states the previous one
- **THEN** the check fails and names the document

#### Scenario: The major is not a breaking-change signal

- **WHEN** a reader consults the documentation to learn what a version change means
- **THEN** they are told the major tracks the Umbraco major, that this departs from
  Semantic Versioning, and that a breaking change appears in a minor rather than a major

#### Scenario: A patch never breaks a site

- **WHEN** the policy is stated
- **THEN** it says a patch release carries no breaking change, and that a breaking change
  in a minor arrives with defaults or an upgrade path for an existing site
