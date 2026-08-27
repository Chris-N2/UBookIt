# packaging

## Purpose

What uBookIt installs into an Umbraco site when a consumer adds the package, what the
package owns thereafter, and what the site owns.

This capability exists because installing schema into someone else's database is the
one thing uBookIt does that removing a NuGet reference does not undo. The risk it
governs is not code but ownership: which of a site author's changes survive a uBookIt
release, and whether they can find that out before losing work rather than after.

## Requirements

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
uses the compiled copy and never consults a site's file. Documenting that route would
send a site author to a path where their work has no effect and no error explains why.
What the documentation offers SHALL be something a site can actually do.

**The documentation SHALL NOT assert more than has been measured.** The override failure
is measured on a development site. It has not been measured without runtime
compilation, where the site's own override is itself compiled and precedence falls to
application-part ordering — expectation and measurement SHALL be distinguishable to a
reader. This clause exists because the
requirement it replaces asserted a mechanism nobody had run, and a correction that
repeats the habit is not a correction.

#### Scenario: A site author can find out whether their change survives
- **WHEN** a site author asks whether an edit of theirs will survive a uBookIt release
- **THEN** the package's documentation answers it, distinguishing an ordinary release from one carrying a migration step

#### Scenario: The documented customisation route works
- **WHEN** the documentation names a way to change how the booking page looks
- **THEN** following it changes what the site renders

#### Scenario: An unavailable route is stated as unavailable
- **WHEN** a site author wants to restyle the markup inside the booking flow
- **THEN** the documentation says plainly that this is not yet supported, rather than offering a route that silently does nothing

### Requirement: The package installs nothing on paths the site owns
The package SHALL install exactly what it needs to put a booking page on a site — a
document type and one template — and SHALL NOT write, replace **or remove** files,
schema or configuration a site did not ask for. In particular it SHALL NOT install
partial views, stylesheets, scripts, arbitrary files, data types, dictionary items or
languages.

("Or remove" is carried from the clause this replaces. Nothing in the import path can
remove anything, so it is theoretical today — but it was in the dropped guarantee, and
dropping half a clause on the grounds that it is currently unreachable is how the whole
clause went missing in the first place.)

**This guarantee was carried by the requirement that described customisation-by-override,
and was dropped when that requirement was rewritten after the override mechanism proved
not to exist.** It survives its original home because it never depended on it: whether or
not a site can override the package's views, the package has no business writing into
paths the site owns. Restated as its own requirement so that the next rewrite cannot take
it with it.

The check SHALL be an **allowlist** of the manifest sections the package declares, not a
list of forbidden ones. A denylist fences only what someone thought of: the first version
named four file-writing sections and let `DataTypes`, `DictionaryItems` and `Languages`
straight through, each of which installs schema or configuration into a consumer's site.

**The check SHALL be made against the manifest that actually installs.** Umbraco looks
for an embedded `package.zip` **before** the XML manifest and falls back to the XML only
when no zip exists, so a zip added later silently becomes the real manifest and every
check above starts fencing a file that installs nothing. Demonstrated: a zip declaring
stylesheets and a branded template left every check green while the shipped document
type was not installed at all and a rogue template wrote a file into the site. Fencing
the wrong artefact is indistinguishable from not fencing at all.

#### Scenario: A manifest section the package has not justified fails
- **WHEN** the package manifest declares any section beyond those the package needs
- **THEN** the check fails, naming the section, whether or not it writes files

#### Scenario: The fence cannot pass by fencing nothing
- **WHEN** the manifest declares no sections at all
- **THEN** the check fails rather than being trivially satisfied

#### Scenario: A second manifest cannot displace the one that is checked
- **WHEN** the package embeds a manifest that would take precedence over the one the checks read
- **THEN** the check fails, because a fence around an artefact that no longer installs is no fence

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

### Requirement: Front-end assets ship with the assembly, not into the site
Any front-end asset the package ships — a stylesheet, and any script or file that may
follow — SHALL be delivered as a **static web asset** carried in the package and served
from the package's own virtual path, and SHALL NOT be delivered by the package
migration manifest.

**This does not restate the manifest fence, and deliberately does not name forbidden
manifest sections.** The requirement *The package installs nothing on paths the site
owns* already prohibits installing stylesheets, scripts, partial views and arbitrary
files, and it does so with an **allowlist** of the sections the package declares —
which is strictly stronger than any list of forbidden ones and which that requirement
argues for explicitly. Enumerating sections here would reintroduce the denylist that
fences only what someone thought of. What is added here is the **positive** obligation:
that a front-end asset has a delivery mechanism at all, and which one.

**Which mechanism is used decides who can never be fixed again, and that is why this is
a requirement rather than an implementation detail.** A manifest import writes a real
file into the consuming site, and because the package's migration plan is run-once by
design, that file is then never touched again. Some of this package's CSS carries
accessibility weight — focus visibility, the derivation of muted and border colours
from the host's text colour, minimum target size — so a stylesheet the site owns is a
stylesheet in which an accessibility fix shipped in a later release reaches **no
existing install, ever**, with nothing reporting that it did not. A static web asset is
replaced by the upgrade like any other part of the assembly, which is the behaviour an
accessibility fix requires.

The consequence for ownership SHALL be documented as it is for schema: the site does
not own the asset and does not edit it. What the site owns is its **appearance**, and
the supported route to changing that is the published token and class contract rather
than a copy of the file.

#### Scenario: Installing the package creates no stylesheet in the site
- **WHEN** the package is installed into a site and its migration runs
- **THEN** no stylesheet file appears anywhere the site owns, and the asset is served from the package's own path

#### Scenario: An upgrade replaces the shipped asset
- **WHEN** a release changes the shipped stylesheet and an existing install is upgraded
- **THEN** the site serves the new stylesheet, without the site editing anything and without an editor's work being overwritten

#### Scenario: A front-end asset with no declared delivery mechanism fails
- **WHEN** the package ships a front-end asset that is neither a static web asset nor covered by an allowed manifest section
- **THEN** the check fails, naming the asset — so an asset cannot arrive by an undecided route

#### Scenario: Appearance is documented as the site's, the file as the package's
- **WHEN** a site author reads the packaging documentation
- **THEN** it states that the package owns the asset and the site owns its appearance, and names the token and class contract as the route
