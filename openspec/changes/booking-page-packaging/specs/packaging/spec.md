## ADDED Requirements

### Requirement: A site author can publish a booking page without writing code
Installing the package SHALL give a site everything needed to put the booking flow on
a page: a document type an editor can create a page from, and a template that renders
the flow. Creating and publishing a page of that type SHALL render the booking flow,
with no Razor written by the site author and no knowledge of the ViewComponent's name
or of the query keys the flow reads.

This is the guarantee the package has never had. Every other part is installable and
tested, while putting it on a page has been undocumented manual work — a document type,
a template, `@inherits UmbracoViewPage`, a `Component.InvokeAsync` call, and the flow's
query key — evidenced nowhere but in a dev harness that ships with nothing.

#### Scenario: A published page of the shipped type renders the flow
- **WHEN** an editor creates and publishes a page of the shipped document type
- **THEN** the booking flow renders on it, without the site author having written any Razor

#### Scenario: The shipped schema actually reaches the site
- **WHEN** the package is installed into a site that does not have its schema
- **THEN** the document type and template exist afterwards, and a failure to install them fails loudly rather than leaving the site silently without them

### Requirement: The package ships nothing an editor is expected to change
Anything the package installs and re-imports SHALL carry nothing a site is expected to
customise. The shipped template SHALL delegate rendering and SHALL NOT carry markup,
styling or content of its own.

This is a hard constraint derived from measured behaviour, not a preference. A package
migration re-imports its entire manifest whenever that manifest changes for any reason,
and the re-import **overwrites** a template's contents and a document type's name, icon,
description and allow-at-root. A site that customised the shipped template loses that
work on the next release that touches schema — including a release that does not touch
the template at all.

A template that is a delegate makes this harmless: there is nothing in it to lose. A
template carrying real markup would make every future schema release destructive, and
silently, at startup.

#### Scenario: The shipped template carries no markup of its own
- **WHEN** the shipped template's contents are inspected
- **THEN** it delegates rendering and carries no markup, styling or content that a site would want to keep

#### Scenario: An upgrade that replaces the template loses nothing
- **WHEN** a release changes the shipped schema and the import replaces the template
- **THEN** nothing a site depends on is lost, because the template held nothing but the delegation

### Requirement: Customisation is by override, and the package never overwrites an override
A site SHALL change the booking flow's appearance by **overriding the package's views**,
and the package SHALL NOT install, replace or remove anything in that override path.
The supported customisation surface is therefore untouched by any upgrade, by
construction rather than by care.

The distinction that matters to a site author is ownership, and it SHALL be documented
rather than inferred: what the package owns and will replace on upgrade, and what the
site owns and the package will never write to.

#### Scenario: An overriding view survives an upgrade
- **WHEN** a site overrides one of the package's views and a later release changes the shipped schema
- **THEN** the site's overriding view is unchanged, because the package installs nothing on that path

#### Scenario: What an upgrade replaces is documented
- **WHEN** a site author needs to know whether a change of theirs will survive an upgrade
- **THEN** the answer is stated in the package's own documentation, including that renaming the shipped document type or changing its icon or description is reverted

### Requirement: Installation adds schema and never creates content
The package SHALL install a document *type* and SHALL NOT create documents. Which pages
exist, where they sit, and what they are called are the editor's decisions.

The manifest format is capable of carrying content, and not carrying any is a decision
rather than an omission. Installing a page would place a URL on a site that nobody asked
for and that an upgrade would then have opinions about.

#### Scenario: Installing creates no pages
- **WHEN** the package is installed into a site
- **THEN** no document is created, and the site's content tree is unchanged

#### Scenario: An editor's own properties survive an upgrade
- **WHEN** an editor adds a property to the shipped document type and a later release ships a new property of its own
- **THEN** the editor's property is still present and the new one has been added
