## ADDED Requirements

### Requirement: The Marketplace listing names its publisher from a root listing file

The Umbraco Marketplace builds a listing from the NuGet metadata of the package a site installs. It
also reads an optional `umbraco-marketplace.json` from the folder the package's project URL points
to. That URL is the GitHub repository, so the file is read from the root of the default branch, and
it takes effect at the Marketplace's next sync with no package release.

The repository SHALL carry that file at its root on the default branch, and its `AuthorDetails`
SHALL name the publisher. Its `Name` SHALL equal the packages' declared authors, so the two places
that name the publisher cannot disagree. Its `Description`, `Url` and `ImageUrl` SHALL be present.
`Url` SHALL be an absolute `https` URL. `ImageUrl` SHALL be an absolute `https` URL to a `.png` or
`.jpg` image. When `ImageUrl` points into this repository's default branch, the image SHALL exist
at that path in the repository.

One listing serves every published line, and one file on the default branch serves that listing.
So the file SHALL NOT state anything that differs between lines. In particular it SHALL NOT
override the listing's title or description, which come from the package metadata of each line.
To make that checkable rather than a matter of review, the file's top-level fields SHALL be limited
to an explicit allowed set: the schema reference and `AuthorDetails`. Adding any other field is a
change to this requirement.

The publishing documentation SHALL say where the listing's author details come from, that the file
is read from the default branch and not from any package, and that changing it needs no release.

#### Scenario: The listing file names the declared publisher

- **WHEN** the listing file at the repository root is read
- **THEN** it parses as JSON, and its `AuthorDetails.Name` equals the packages' declared authors

#### Scenario: A publisher name that disagrees with the package metadata fails

- **WHEN** `AuthorDetails.Name` differs from the packages' declared authors
- **THEN** the check fails and shows both values

#### Scenario: Incomplete author details fail

- **WHEN** `AuthorDetails` lacks a non-empty `Description`, or `Url` is not an absolute `https`
  URL, or `ImageUrl` is not an absolute `https` URL ending in `.png` or `.jpg`
- **THEN** the check fails and names the field

#### Scenario: An image the repository does not contain fails

- **WHEN** `ImageUrl` points into this repository's default branch at a path with no file in the
  repository
- **THEN** the check fails and names the path

#### Scenario: A field outside the allowed set fails

- **WHEN** the listing file has a top-level field other than the schema reference and
  `AuthorDetails`, for example a `Title` or `Description` that would override the metadata of
  every line at once
- **THEN** the check fails and names the field

#### Scenario: A missing listing file fails

- **WHEN** the repository root has no `umbraco-marketplace.json`
- **THEN** the check fails, so deleting the file cannot pass

#### Scenario: Where the author details come from is documented

- **WHEN** a reader consults the publishing documentation about the Marketplace
- **THEN** it says that the listing's author details come from the root listing file, that the
  Marketplace reads the file from the default branch and not from a package, and that changing it
  needs no release
