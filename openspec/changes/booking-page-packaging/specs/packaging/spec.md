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

This is derived from measured behaviour, not preference. An import **overwrites** a
template's contents and a document type's name, icon, description and allow-at-root.
The import is run-once, so an ordinary release does not trigger it — but a release
carrying a migration step re-imports the **whole** manifest, including parts that
release did not change.

So the exposure is not "every release" but "every release we choose to make
destructive", and the shipped template is the only thing in the manifest a site would
plausibly have edited. Keeping it a delegate keeps the cost of that decision near
zero: there is nothing in it to lose. A template carrying real markup would make each
future schema release a choice between shipping the schema and destroying sites' work.

It also keeps the choice honest. If the shipped template held something valuable, the
pressure would be to avoid adding migration steps at all — which would mean never
shipping schema changes existing sites need.

#### Scenario: The shipped template carries no markup of its own
- **WHEN** the shipped template's contents are inspected
- **THEN** it delegates rendering and carries no markup, styling or content that a site would want to keep

#### Scenario: An upgrade that replaces the template loses nothing
- **WHEN** a release changes the shipped schema and the import replaces the template
- **THEN** nothing a site depends on is lost, because the template held nothing but the delegation

### Requirement: What the package owns and what the site owns is documented
A site author SHALL be able to find out, from the package's own documentation, which
of their changes survive a uBookIt release and which do not. Ownership SHALL be stated
rather than left to be inferred from behaviour, because the cost of inferring it wrongly
is discovering that work has been destroyed.

The documentation SHALL state at least: that the schema is imported once and an ordinary
release does not re-import it; that a release carrying a migration step does re-import,
replacing the template's contents and the document type's name, icon, description and
allow-at-root; that nothing is ever removed, so an editor's own properties and pages are
safe; and how a site changes the page's appearance.

**The package SHALL NOT claim a customisation route it does not have.** Overriding the
package's compiled views does not work — they carry no source checksums, so ASP.NET Core
uses the compiled copy and never consults a site's file, in development and in
production alike. Documenting that route would send a site author to a path where their
work has no effect and no error explains why. What the documentation offers SHALL be
something a site can actually do.

#### Scenario: A site author can find out whether their change survives
- **WHEN** a site author asks whether an edit of theirs will survive a uBookIt release
- **THEN** the package's documentation answers it, distinguishing an ordinary release from one carrying a migration step

#### Scenario: The documented customisation route works
- **WHEN** the documentation names a way to change how the booking page looks
- **THEN** following it changes what the site renders

#### Scenario: An unavailable route is stated as unavailable
- **WHEN** a site author wants to restyle the markup inside the booking flow
- **THEN** the documentation says plainly that this is not yet supported, rather than offering a route that silently does nothing

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
