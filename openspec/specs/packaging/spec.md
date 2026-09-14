# packaging

## Purpose

How a consumer obtains uBookIt at all — which packages exist, that installing one of them
is enough, and what that package says about itself and the terms it is offered under —
then what uBookIt installs into an Umbraco site when a consumer adds it, what the package
owns thereafter, and what the site owns.

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
safe; how a site changes the page's appearance; and **how a site changes the markup inside
the flow**.

**The package SHALL NOT claim a customisation route it does not have.** Placing a file in
the consuming site at the same path as one of the package's views does **not** work, and
the documentation SHALL continue to say so: those views are compiled into the package
assembly without source checksums, so ASP.NET Core uses the compiled copy and never
consults the site's file. This remains the trap a site author falls into first, and it is
not made less likely by a supported route existing elsewhere — so it SHALL be documented as
a route that does not work, not merely omitted in favour of the one that does.

**The route that does exist SHALL be documented as what it is: a theme.** The flow's markup
is customisable by supplying views from a **separate assembly** at a theme path, which the
package resolves ahead of its own. The documentation SHALL name this route and SHALL
distinguish it from the site-file override that does not work, because the two look alike
to a reader and only one of them has any effect.

**The documentation SHALL NOT assert more than has been measured.** Expectation and
measurement SHALL be distinguishable to a reader. This clause exists because a requirement
this one replaces asserted a mechanism nobody had run, and a correction that repeats the
habit is not a correction. Two specific limits follow from it:

- The site-file override failure was measured on a development site. It has **not** been
  measured without runtime compilation, where the site's own override is itself compiled
  and precedence falls to application-part ordering.
- The theme route SHALL NOT be documented as working in a configuration in which it has not
  been measured. It has been measured in **two**: under Umbraco page rendering on a
  development site, and in a host with runtime compilation absent from the dependency
  closure — which is what a fully precompiled production site runs. Both SHALL be stated,
  and nothing beyond them SHALL be claimed. The second measurement was taken because the
  first alone would have repeated the habit this clause exists to correct.

The documentation SHALL also state what a theme's author owns: a theme's markup is the
theme's, and the package's markup and accessibility guarantees describe the views the
package ships.

#### Scenario: A site author can find out whether their change survives
- **WHEN** a site author asks whether an edit of theirs will survive a uBookIt release
- **THEN** the package's documentation answers it, distinguishing an ordinary release from one carrying a migration step

#### Scenario: The documented customisation route works
- **WHEN** the documentation names a way to change how the booking page looks
- **THEN** following it changes what the site renders

#### Scenario: The route for changing the flow's markup is documented and works
- **WHEN** a site author wants to restyle or replace the markup inside the booking flow
- **THEN** the documentation names the theme route, and following it changes what the site renders

#### Scenario: The route that does not work is still documented as not working
- **WHEN** a site author places their own file at the path of one of the package's views
- **THEN** the documentation has already told them this has no effect, and names the theme route instead — rather than leaving the failed attempt to be discovered

#### Scenario: A claim beyond what was measured is not made
- **WHEN** the documentation describes either the override failure or the theme route
- **THEN** it states the configuration in which the behaviour was measured, and does not assert it for a configuration in which it was not

#### Scenario: A theme's ownership is stated
- **WHEN** a site author or theme author reads the ownership documentation
- **THEN** it states that a theme's markup is the theme author's, and that the package's markup and accessibility guarantees describe the views the package ships
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

### Requirement: What a site can subscribe to is documented
The package raises notifications a consuming site can handle, and those SHALL be documented
in the package's own documentation. **An extension point nobody is told about is not one** —
a site that does not know a hook exists will either go without or reach past the contracts
into the database, which is the outcome every port in this package exists to prevent.

The documentation SHALL name each notification, state when it is raised, say what it carries,
and show how a site subscribes.

**It SHALL state what the package sends, to whom, and under what configuration** — so that
neither "uBookIt notified them" nor "uBookIt notified nobody" is ever assumed. This is the
sentence most likely to be discovered the expensive way: by a customer arriving for a booking
that was cancelled, or by a customer receiving a message the site did not know it was sending.

Specifically, the documentation SHALL state that the package sends **nothing by default**, name
the configuration that enables each direction of sending, and state that a site's mail
configuration alone does not enable any of it. **It SHALL also state what those messages do not
carry**: that a message to a site's own recipients identifies a booking without the booker's
contact details, and that seeing those details remains governed by the backoffice.

**It SHALL state the limits of what a subscriber is promised**, and specifically that a
handler which throws is a notification nobody receives, because the package neither retries
nor queues. A half-stated promise about delivery is worse than none: it is relied on and then
found out during an incident. **The same limits SHALL be stated of the package's own messages** —
they are sent once, are not retried, are not queued, and a failure to send is not reported to the
person who booked.

#### Scenario: A site author can find the hooks
- **WHEN** a site author looks for a way to react to a booking
- **THEN** the documentation names the notifications, when each is raised, what it carries, and how to subscribe

#### Scenario: What the package sends is stated, not implied
- **WHEN** a site author reads what the package does when a booking is placed or cancelled
- **THEN** it says what the package sends, to whom, and what configuration enables it, and that anything else a booker receives is the site's to send

#### Scenario: The default is stated
- **WHEN** a site author reads the notification documentation without having configured anything
- **THEN** it says the package sends nothing until configured, and that configuring the host's mail server does not by itself enable sending

#### Scenario: What internal messages withhold is stated
- **WHEN** a site author reads what is sent to a site's own recipients
- **THEN** it says those messages carry no booker contact details, and that access to those details remains governed by the backoffice

#### Scenario: The delivery limit is stated
- **WHEN** a site author reads what they are promised about a notification or a message the package sends
- **THEN** it says a handler that throws will not be retried or queued, and that a message the package fails to send is not retried, not queued, and not reported to the booker

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
- **THEN** restore succeeds, and the site has the backoffice section, the management API and the front-end rendering available, and the delivery API installed — present and off, until the site enables its directions as the `delivery-api` capability defines

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

### Requirement: How a site supplies its own message content is documented
The package's documentation SHALL state that a site can supply the content of the messages the
package sends, and SHALL give an author everything needed to do it: **the complete set of message
names**, **where a view goes**, **what each view receives**, and **what a view may state about the
message**.

**An extension point nobody is told about is not one** — the same reasoning that already requires
the notifications to be documented. This one is more acute, because the alternative to finding it
is worse than going without: a site that wants different wording and cannot find this will take
over sending instead, and thereby inherit the sending conjunction, the erased-booker rule and the
personal-data rules that the package is tested for and their own handler will not be.

**The documentation SHALL state the limit of the medium**: that a message carries either HTML or
plain text and never both, so supplying HTML content means sending no plain-text alternative, and
that this follows from the mail abstraction the package sends through rather than from a choice
this package made.

**It SHALL state which of the package's promises stop applying to supplied content and which do
not.** An author needs to know that the words become theirs — including whether the message
correctly describes the booking's state — while the gating, the audiences, the erased-booker rule
and the exclusion of booker contact details from messages to a site's own recipients continue to
hold regardless of what they write.

#### Scenario: An author can supply content from the documentation alone
- **WHEN** an author reads the documentation
- **THEN** they can find every message name, where to put a view, what the view receives, and how to state a subject and a content type, without reading the package's source

#### Scenario: The single-body limit is stated
- **WHEN** an author reads how to supply HTML content
- **THEN** the documentation states that no plain-text alternative is sent with it, and why

#### Scenario: What narrows and what does not is stated
- **WHEN** an author reads what supplying content makes them responsible for
- **THEN** the documentation distinguishes the wording, which becomes theirs, from the gating, audiences, erased-booker rule and internal-message exclusion, which do not

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

### Requirement: The package points a consumer at a public home

A package page is a dead end if the links out of it are. The package SHALL declare a project
URL and a repository URL that are `https`, that do not name a host the publisher knows to be
private, and that identify the repository the source actually lives in — so a consumer who
follows either from a package feed or a marketplace listing arrives somewhere they can read
the code.

Whether a URL genuinely resolves SHALL NOT be asserted by an automated check: reaching the
network would make the check slow, fail offline, and go red for reasons outside this
repository. It is verified by a person before the first publish, and the publishing
documentation SHALL say so.

**The metadata a consumer sees and the source-fetch URLs a debugger follows come from
different places**, and the package SHALL document that: the declared URLs come from the build
properties, while symbol-package source links are derived from the repository's own remote. A
package built before its remote moved therefore carries stale source links even when its
declared URLs are correct, so the documentation SHALL state the order — correct the
properties, move the remote, then rebuild — and SHALL require the produced artifacts to be
inspected rather than assumed.

Because a published version's metadata cannot be corrected in place on a public feed, the
publishing documentation SHALL state what a push makes permanent — including that symbol
source links identify a specific commit, so a package built from a commit absent from the
public repository resolves to nothing for every consumer and cannot be repaired.

#### Scenario: The declared URLs are public and name the source repository

- **WHEN** the package's declared project and repository URLs are inspected
- **THEN** both are `https`, neither names a host known to be private, and both identify the
  repository the source lives in

#### Scenario: Reachability is a human check, not an automated one

- **WHEN** the automated checks over package metadata are examined
- **THEN** none of them requests the URL over the network, and the publishing documentation
  names the manual check instead

#### Scenario: The ordering that makes source links correct is documented

- **WHEN** a maintainer consults the publishing documentation before a release
- **THEN** it states that symbol-package source links follow the repository remote rather than
  the declared URLs, gives the order that makes both correct, and requires the produced
  artifacts to be inspected before pushing

#### Scenario: Publishing from an unpublished commit is documented as unrecoverable

- **WHEN** a maintainer consults the publishing documentation
- **THEN** it states that source links identify a specific commit, and requires the commit
  being packaged to be one the public repository already has

#### Scenario: What a push makes permanent is documented

- **WHEN** a maintainer consults the publishing documentation
- **THEN** it states that a pushed version's metadata cannot be edited and that a version
  number cannot be reused
