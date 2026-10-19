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
`.jpg` image.

A URL **reaches this repository** when its host is `github.com`, `githubusercontent.com` or any
subdomain of either, and the owner and repository appear as consecutive path segments, starting
at the first or second segment. Owner and repository are compared without regard to case. A host
outside those two domains that also serves the repository's files, such as a CDN mirror, counts
as hosted elsewhere. A logo URL that reaches this repository SHALL use one accepted form: the
raw URL of the default branch, with the owner and repository spelled as in the package's project
URL, and no query, fragment or dot segments. In that form the image SHALL be a file the repository
**tracks**, matched case-sensitively, because GitHub serves only committed files and is
case-sensitive. A URL into this repository in any other form SHALL be refused, so that it cannot
bypass the existence check. This covers another branch, the `refs/heads/` form, the
`github.com/…/raw/…` form and a different letter case. A logo hosted outside this repository is
checked only for its scheme and extension.

One listing serves every published line, and one file on the default branch serves that listing.
So the file SHALL NOT state anything that differs between lines. In particular it SHALL NOT
override the listing's title or description, which come from the package metadata of each line.
Two parts of this are checked rather than left to review:
- The file's top-level fields SHALL be limited to an explicit allowed set: the schema reference and
  `AuthorDetails`. Adding any other field is a change to this requirement.
- No text value in `AuthorDetails` SHALL name an Umbraco major in the vocabulary `Umbraco`,
  whitespace, digits (case-sensitive). That is the same vocabulary, with the same limit, as the
  package-description check. Other phrasings, such as "Umbraco v18", are not seen by the check and
  rest on review.

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

#### Scenario: An image the repository does not track fails

- **WHEN** `ImageUrl` is in the accepted form and names a path the repository does not track,
  including a path that differs from a tracked file only in letter case
- **THEN** the check fails and names the path

#### Scenario: A logo URL into this repository in another form fails

- **WHEN** `ImageUrl` reaches this repository in a form other than the accepted one. Examples: the
  `refs/heads/` form, the `github.com/…/raw/…` form, the `raw.github.com` host, the
  `media.githubusercontent.com/media/…` form, another branch, a different letter case for the owner
  or repository, or dot segments
- **THEN** the check fails and says the URL is not the accepted form, whether or not the file
  exists

#### Scenario: Author text naming an Umbraco major fails

- **WHEN** a text value in `AuthorDetails` contains `Umbraco`, whitespace and digits, for example
  "Umbraco 17"
- **THEN** the check fails and names the field and the text

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
