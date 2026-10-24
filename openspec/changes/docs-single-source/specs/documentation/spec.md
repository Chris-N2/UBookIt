## Purpose

Defines uBookIt's published documentation as a single source that a consumer, such as the
maintainer's own site, can render at any release tag. It covers the manifest naming what is
published and the contract that manifest offers, the agreement between the manifest and the pages,
links that resolve wherever the pages are rendered, a Getting started path through the default
flow, and an email template reference that cannot fall behind the code it documents.

## ADDED Requirements

### Requirement: A manifest names the published documentation

The repository SHALL carry a documentation manifest at `docs/docs.json`, and it SHALL be the only
statement of which pages are published, in what order and for whom. A consumer that renders the
documentation SHALL need nothing beyond the manifest and the files it names, fetched at one tag.

The manifest SHALL be a JSON object holding:

- `schema`, the integer `1`;
- `headingIds`, the string `github`, naming the rule by which the pages' links expect headings to
  be given identifiers (see the links requirement);
- `sections`, an ordered array. Each section has a `title`, an `audience` and `pages`:
  - `audience` is `site` for people setting up and running uBookIt, or `developer` for people
    extending it;
  - `pages` is an ordered array. Each page has a `file`, a `title` and a `summary`.

A page's `file` SHALL be a bare file name in `docs/`, ending in `.md` and containing no path
separator. Its `summary` SHALL be a single line of plain text.

**The manifest is a published contract, and it evolves additively.** A later version MAY add
fields, sections, pages and new `audience` values, and a consumer is expected to ignore what it does
not recognise. Renaming or removing a field, or changing a field's meaning, SHALL increment
`schema` and SHALL be called out as a breaking change in its change's proposal.

**Which pages are listed is not part of the contract.** Pages may be added, removed, renamed or
reordered in any release, and a consumer renders what the manifest at that tag lists.

#### Scenario: A consumer renders the documentation from the manifest alone
- **WHEN** a consumer reads `docs/docs.json` at a release tag
- **THEN** it finds the published pages in order, grouped into sections with an audience each, and each page's title and summary
- **AND** every file it names can be fetched from `docs/` at the same tag

#### Scenario: The manifest declares its contract version
- **WHEN** the manifest is read
- **THEN** it carries `schema` with the value `1` and `headingIds` with the value `github`

#### Scenario: A malformed entry fails
- **WHEN** a page entry has a `file` containing a path separator, not ending in `.md`, or a `summary` spanning more than one line, or a section has an `audience` other than `site` or `developer`
- **THEN** the test suite fails, naming the entry

### Requirement: The manifest and the pages agree

Every file the manifest lists SHALL exist in `docs/`, and no file SHALL be listed twice. Every
Markdown file in `docs/` SHALL be either listed or named as deliberately unlisted. The deliberately
unlisted pages SHALL be stated in one place the test suite reads, and they are the maintainer's
pages: `mvp.md` and `publishing.md`.

Each listed page's `title` SHALL be the text of that page's single level-one heading.

The README's *Documentation* list SHALL link every listed page, so a reader of the packed readme
on nuget.org can reach everything the manifest publishes.

#### Scenario: A listed page that does not exist fails
- **WHEN** the manifest lists a file that is not in `docs/`
- **THEN** the test suite fails, naming the file

#### Scenario: A new page left out of the manifest fails
- **WHEN** a Markdown file is added to `docs/` and is neither listed nor named as deliberately unlisted
- **THEN** the test suite fails, naming the file

#### Scenario: A title that differs from its page fails
- **WHEN** a page's level-one heading is changed and its manifest `title` is not
- **THEN** the test suite fails, naming the page and both texts

#### Scenario: A page the README does not link fails
- **WHEN** the manifest lists a page that the README's *Documentation* list does not link
- **THEN** the test suite fails, naming the page

#### Scenario: The maintainer pages are not published
- **WHEN** the manifest is read
- **THEN** it lists neither `mvp.md` nor `publishing.md`

### Requirement: Links between published pages resolve wherever they are rendered

A relative link in a listed page SHALL name either a listed page, optionally with a fragment, or
a non-Markdown file beneath `docs/`, such as an image, that exists. It SHALL NOT name a page that is
not listed, and SHALL NOT leave `docs/`. A consumer renders only the listed pages, so a link to
anything else is dead on the consumer's site even where it works on GitHub.

A fragment, whether on another page or on the same page, SHALL match a heading on its target page,
with the heading converted to an identifier the way GitHub converts it. The manifest's `headingIds`
field states this rule, so that a consumer rendering the pages knows the identifiers it must produce
for those fragments to land.

Absolute links are not constrained by this requirement.

#### Scenario: A link to a moved section fails
- **WHEN** a listed page links `notifications.md#template-sets` and that heading is no longer on `notifications.md`
- **THEN** the test suite fails, naming the linking page, the link and the missing heading

#### Scenario: A link to an unpublished page fails
- **WHEN** a listed page links `mvp.md`, `publishing.md`, or any file outside `docs/`
- **THEN** the test suite fails, naming the link

#### Scenario: A missing image fails
- **WHEN** a listed page references an image beneath `docs/` that does not exist
- **THEN** the test suite fails, naming the reference

### Requirement: Every statement the documentation is required to make is published

Wherever a requirement in this repository obliges *the documentation* to state something, that
statement SHALL appear on a page the manifest lists. A statement present only in an unlisted page,
or only in the README, does not satisfy such a requirement, because a reader of the published
documentation never sees it. The README may state it as well.

**There are exactly two exceptions, and each is a requirement that names a different home for its
statement:**

- **How the maintainer builds and publishes the package.** A statement about how the package is
  built, packed, verified, tagged or pushed to a feed is satisfied by `docs/publishing.md`, which is
  deliberately unlisted because a site using uBookIt has no use for it. The exception is decided by
  the statement's **subject**, not by the requirement's wording: it covers a requirement that says
  "the publishing documentation" and one that says only "the documentation" alike, such as the
  order in which a package's declared URLs, its repository remote and its build must be corrected.
  It does **not** cover anything a site owner needs in order to install, upgrade or choose a
  version: which version to install, what a version number means, what an upgrade changes, and
  what the package requires are never publishing statements, whoever else needs them too.
- **The per-release callouts.** A requirement obliging the package to name a release's contract
  changes per release is satisfied by `CHANGELOG.md`, which a consumer reaches from the package
  page. A published page may point readers to it, but it is not a documentation page and the
  manifest does not list it.

No other statement is exempt. In particular, a statement a requirement places "where a consumer
meets the version" or in "the package's own documentation" is not exempt because the README also
carries it.

#### Scenario: The two exceptions are the only ones
- **WHEN** a requirement obliges the documentation to state something
- **THEN** the statement is on a listed page, unless its subject is how the maintainer builds and publishes the package, in which case it is in `docs/publishing.md`, or the requirement names the per-release changelog as its home, in which case it is in `CHANGELOG.md`

#### Scenario: A publishing statement is exempt by subject, whatever its requirement calls it
- **WHEN** a requirement says "the documentation SHALL state" the order in which a package's declared URLs, its remote and its build are corrected
- **THEN** that statement is satisfied by `docs/publishing.md`, because its subject is how the package is built and published

#### Scenario: What a site owner needs is never a publishing statement
- **WHEN** a statement concerns which version to install, what a version number means, what an upgrade changes, or what the package requires
- **THEN** it is on a listed page, even where `docs/publishing.md` also states it

#### Scenario: A statement the README carries is published too
- **WHEN** a requirement's statement about installing or versioning is in the README, such as the database the package requires or what its version numbers mean
- **THEN** a listed page states it as well, and a test reads that page

#### Scenario: Moving a section keeps what it was required to say
- **WHEN** content that a requirement obliges the documentation to state moves from one page to another
- **THEN** each of those statements is still present on a listed page, and the test that asserts it reads the page it now lives on

#### Scenario: A guard that asserts an absence is shown able to fail
- **WHEN** a documentation test asserting that a page does **not** make a claim is pointed at a different page
- **THEN** it has been shown to fail against a copy of that page carrying the claim, so that it cannot pass merely because it reads a page the content has left

### Requirement: A Getting started page takes a site through the default flow

The documentation SHALL include a Getting started page, listed first in the `site` section, that
takes a site owner from installing the package to a visitor's booking arriving by email, through
the default booking page only. In order, it SHALL cover:

1. the requirements, including the database with its lowest supported version and the lowest
   supported Umbraco version;
2. installing the package, with a version that keeps it on the line for the site's Umbraco major,
   and what uBookIt's version numbers mean;
3. granting the uBookIt section, and the separate question of who sees bookers' details;
4. creating a resource and its opening hours;
5. creating a service;
6. creating and publishing the booking page;
7. turning emails on and confirming one is delivered;
8. where to go next.

Each step that a reference page covers in more depth SHALL link that page rather than restate it.
The requirements step is the exception: it states the requirements in full itself. The
page SHALL NOT describe building a front end on the delivery API, and it SHALL link the delivery API
documentation for that.

**The lowest supported Umbraco version that Getting started states, and the one the README states,
SHALL equal the lower bound the packages declare for Umbraco**, so that raising the floor without
updating either page fails the test suite.

#### Scenario: Following the page produces a working booking
- **WHEN** a site owner follows Getting started on a new Umbraco site meeting the requirements
- **THEN** they have a published booking page, a booking placed through it, and an email about that booking delivered, without needing any page except the ones it links

#### Scenario: A floor raise that misses a page fails
- **WHEN** the Umbraco lower bound the packages declare is raised
- **AND** Getting started or the README still states the previous version
- **THEN** the test suite fails, naming the page and both versions

### Requirement: The email template reference is complete against the code

The documentation SHALL include an Email templates page in the `developer` section that holds
everything a site author needs in order to write message views without reading the source:

- the folder views are read from;
- the file for each message, with who receives it and when;
- the `@inherits` line for each model, giving full type names;
- a reference table for each model listing every member with what it carries and when it is `null`;
- template sets;
- what falls back to uBookIt's own wording and when;
- templates supplied by a package;
- how to replace a message entirely.

**The reference is checked against the code, so it cannot fall behind it.**

- The files listed SHALL be exactly one per message kind the package defines, named as the package
  looks them up.
- The folder stated SHALL be the folder the package reads.
- Each type named in an `@inherits` line SHALL exist.
- Each model's table SHALL list exactly that model's public members, inherited ones included, each
  with a non-empty description.

A member added to a model, or a message kind added to the package, SHALL fail the test suite until
the page documents it.

The notifications page SHALL keep its heading for writing message content, with a pointer to the
Email templates page beneath it, so that a link made to that heading before the move still lands on
a route to the content.

#### Scenario: A site author writes a set's views from the page alone
- **WHEN** a site author reads the Email templates page
- **THEN** they can name every view file a template set may hold, write the `@inherits` line for each audience's model, and know every member that model carries, without opening the source

#### Scenario: An undocumented model member fails
- **WHEN** a public member is added to either message model and not to that model's table
- **THEN** the test suite fails, naming the model and the member

#### Scenario: A documented member that no longer exists fails
- **WHEN** a model's table lists a member the model does not have
- **THEN** the test suite fails, naming the model and the member

#### Scenario: A new message kind fails until documented
- **WHEN** a message kind is added to the package and the page's file table does not list its view
- **THEN** the test suite fails, naming the kind

#### Scenario: An old link to the notifications page still leads to the content
- **WHEN** a reader follows a link to the notifications page's heading for writing message content
- **THEN** they land on that heading and find a link to the Email templates page

#### Scenario: Removing the pointer fails
- **WHEN** the heading is renamed or removed from the notifications page, or the link beneath it is
- **THEN** the test suite fails, even though no published page links that heading any more
