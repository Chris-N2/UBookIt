## MODIFIED Requirements

### Requirement: The version a reader is told is the version the package carries

A version number is a claim, and a claim in prose drifts from the build the moment
somebody bumps one and not the other. Wherever the package's own documentation states
**the version uBookIt is currently at**, that version SHALL be the one
`Directory.Build.props` declares — and SHALL be verified against it rather than restated, so
a later bump fails the check instead of silently leaving the documentation describing a
release nobody can install.

Documentation also states versions that are **not** the current one: the release the public
API was declared stable from, and the first release the package ever made. These are
statements about history, and history does not move when a release does. Such an anchor SHALL
NOT be required to equal the declared version. It SHALL instead be required to equal the version
this repository's own **immutable record** names as the first release — the archived release
change under `openspec/changes/archive/` — and SHALL additionally equal every other anchor and
name no version later than the one declared.

**Agreement between anchors is explicitly not sufficient.** A version bump performed as a
repository-wide find-and-replace moves every anchor at once and by construction leaves them
agreeing, so a check that only compares them to one another passes the exact edit this
requirement exists to forbid. The pin SHALL therefore be to a record that documentation edits
cannot move, and SHALL derive the version from it rather than restating it. **The distinction is load-bearing rather
than pedantic: requiring every stated version to equal the declared one makes "the first
release is `17.0.0`" fail at the first patch release, and the obvious way to make it pass is
to edit it into a falsehood.**

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

- **WHEN** the package's documentation states the version uBookIt is **currently at**
- **THEN** it is the version declared in `Directory.Build.props`, verified against it
- **AND** a version the documentation states as *history* rather than as the current release is
  out of this scenario's scope, and is covered by the anchor scenarios below

#### Scenario: A bump that misses the documentation fails

- **WHEN** the declared version changes and a document still states the previous one as
  current
- **THEN** the check fails and names the document

#### Scenario: A version the documentation anchors to does not move with the release

- **WHEN** the declared version changes
- **AND** a document states the release the API was declared stable from, or the first
  release the package made
- **THEN** that statement is unaffected, because it records history rather than the present

#### Scenario: A bump that moves every anchor together fails

- **WHEN** a version bump edits every sentence naming the first release, uniformly, so that they
  all still agree with one another
- **THEN** the check fails, because the version is pinned to the repository's immutable archive
  rather than to the agreement of the sentences being edited

#### Scenario: The pinned version is derived, not restated

- **WHEN** the check establishes which version was the first release
- **THEN** it reads it from the archived release change's name, so no version literal appears in
  the check itself and the record it trusts is one no documentation edit can alter

#### Scenario: Anchors that disagree with each other fail

- **WHEN** two documents each name the version the package was first released at
- **AND** they name different versions
- **THEN** the check fails, since at most one of them can be true — the agreement is checked
  between them rather than against a literal written into the check

#### Scenario: An anchor later than the declared version fails

- **WHEN** a document names a first release or stability anchor later than the declared version
- **THEN** the check fails, because a package cannot have been first released in a version it
  has not reached

#### Scenario: The major is not a breaking-change signal

- **WHEN** a reader consults the documentation to learn what a version change means
- **THEN** they are told the major tracks the Umbraco major, that this departs from
  Semantic Versioning, and that a breaking change appears in a minor rather than a major

#### Scenario: A patch never breaks a site

- **WHEN** the policy is stated
- **THEN** it says a patch release carries no breaking change, and that a breaking change
  in a minor arrives with defaults or an upgrade path for an existing site

## ADDED Requirements

### Requirement: Documentation a consumer follows from the package page resolves

The readme packed into every package is rendered by hosts that are not the repository —
nuget.org's package page and the Visual Studio Package Manager among them — and those hosts
resolve a relative link against their own address, not against the repository. Every link in the
packed readme SHALL therefore be an absolute `https` URL, and every link that names a file in this
repository SHALL name a file that exists.

This is a guarantee about **shape and target**, not about reachability: a guard can prove a link
is absolute and that the path it names is present in the working tree. It cannot prove the URL
answers over the network, for the same reason `The package points a consumer at a public home`
states of the declared URLs — that remains a human check, made once, logged out.

The readme is frozen per published version. A link defect is not correctable in place and costs a
version number, which is why it is guarded rather than reviewed.

#### Scenario: A relative documentation link fails

- **WHEN** a link in the packed readme has a target that is not an absolute `https` URL
- **THEN** the suite fails, naming the link, because that target resolves against the package page
  rather than the repository and leads nowhere

#### Scenario: A relative image fails

- **WHEN** an image in the packed readme has a relative path
- **THEN** the suite fails, because nuget.org renders no image from a relative path and the reader
  is shown a gap with no indication anything was intended

#### Scenario: A link into this repository names a file that is not there

- **WHEN** a readme link names a path inside the declared repository
- **AND** no file exists at that path in the working tree
- **THEN** the suite fails, so a renamed or deleted document is caught before it is frozen into a
  published version rather than after

#### Scenario: The repository a link points into is the declared one

- **WHEN** a readme link addresses this project's own repository
- **THEN** the repository it names is derived from the declared `RepositoryUrl` rather than written
  out a second time, so a future move of the repository cannot leave the readme pointing at the
  old host while every guard stays green

#### Scenario: Reachability over the network is not claimed

- **WHEN** the guard passes
- **THEN** it has proved link shape and local target existence only, and the documentation says so,
  so no reader mistakes it for proof that the published page's links were followed

### Requirement: The publishing runbook records what a publish actually cost

The runbook already states what cannot be undone. It SHALL also record the failures encountered
performing a publish, where those failures are not diagnosable from the tooling's own output, and
those statements SHALL be pinned so they cannot be tidied away by somebody who was not there.

This exists because the first publish was blocked by a `403` whose message named only the API key,
while the actual cause was that the key's **owner** — a nuget.org organization with an
unconfirmable email address — could not publish at all. Nothing in the error, the CLI, or NuGet's
documentation connects the two. A lesson that costs an hour to rediscover and is invisible from
the failure itself is exactly the kind this document exists to hold.

#### Scenario: A failure whose message misdirects is recorded with its real cause

- **WHEN** a maintainer meets a `403` naming the API key
- **THEN** the runbook tells them to check whether the key's owner can publish, and how to isolate
  it, rather than leaving them to re-check the key the message accused

#### Scenario: An account problem does not block a release

- **WHEN** package ownership is not yet as intended
- **THEN** the runbook states that ownership can be transferred after publication, so a push is
  not delayed for something that is not a one-way door

#### Scenario: A pushed package that is not yet installable is not mistaken for a failed push

- **WHEN** a push succeeds and the package presents as unlisted while it validates
- **THEN** the runbook names that state as expected and gives the endpoint that actually indicates
  restore will work, so nobody pushes a second time

### Requirement: The package presents an icon

A package with no icon is rendered with a generic placeholder in nuget.org search results and in
the Visual Studio Package Manager, alongside packages that have one. The package SHALL declare an
icon, and the declared file SHALL be packed into every package the repository publishes, so the
family is identifiable as one thing rather than as five unrelated entries.

The icon is a single file declared once, for the same reason the version and the publisher are:
five packages carrying separately-specified icons can drift apart silently.

#### Scenario: Every published package carries the icon

- **WHEN** the packages are produced
- **THEN** each one declares an icon and physically contains the file it declares, so no package
  ships a declaration pointing at something absent

#### Scenario: The icon is within what NuGet accepts

- **WHEN** the icon file is inspected
- **THEN** it is a supported raster format, square, and within NuGet's one-megabyte limit, so the
  constraint is checked here rather than discovered at push time against a version number that
  cannot be reused

#### Scenario: A declaration naming a missing file fails

- **WHEN** the icon declaration names a path with no file at it
- **THEN** the build or the suite fails rather than producing a package whose icon silently does
  not render

#### Scenario: A metadata declaration naming an embedded file that is absent fails

- **WHEN** a package's manifest declares an embedded readme or icon
- **AND** the package does not contain a file at the declared path
- **THEN** the suite fails, because the declaration and the item that packs the file are two
  statements the build never compares
