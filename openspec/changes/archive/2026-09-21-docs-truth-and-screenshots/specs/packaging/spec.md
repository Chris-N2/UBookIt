## ADDED Requirements

### Requirement: The documentation a consumer reads does not deny what the package does

Everything a consumer can read about uBookIt before installing it — the packed readme above all,
and the documentation set it links to — SHALL NOT state that the package lacks a capability the
package ships. A sentence that was true when it was written becomes a false claim the moment the
feature lands, and it is **the negative sentences that decay silently**: nothing exercises them,
no reader reports them, and the person who would notice is the person who just built the thing
being denied.

**This is a claim about the population, not about a list of sentences.** The check SHALL be made
over every markdown file the repository ships to a reader, **discovered rather than enumerated**,
so a document added later is covered without anybody remembering to add it. An enumerated list is
how the readme itself — the file packed into all five packages — came to be in no guard at all.

**A capability landing SHALL retire the sentences it falsifies, as part of that capability's own
work.** This is the clause that carries the obligation: a sweep nobody is required to feed is a
sweep three consecutive changes can walk past, which is exactly what happened to
`booking-on-behalf`, `find-booking` and `self-service-cancellation`. Retiring a sentence means
both halves — deleting it from the document **and** adding it to what the sweep holds out — because
a deletion alone can be reintroduced by the next author who reads an older draft.

**No guard SHALL require a sentence that denies a shipped capability.** A positive assertion
pinning a false claim is worse than no guard: it reports green while the claim is wrong, and it
resists the correction, so the person fixing the document is told by the suite that they have
broken something. Where a list of what the package deliberately does not do is pinned, the pin
SHALL be reviewed when that list changes rather than only when it grows.

**A sentence held out SHALL be a literal the document could actually contain.** A needle that has
drifted from its document's wording — by a word, by a rewrap — matches nothing and passes always,
and is indistinguishable from a needle that is doing its job.

This requirement does not reach `openspec/`, `CLAUDE.md` or `.claude/`. Archived changes are a
historical record and are supposed to contain sentences that were true when written; CLAUDE.md is
this project's record of retired wordings, which it quotes on purpose.

#### Scenario: A shipped document denies a capability the package has

- **WHEN** any markdown file the repository ships to a reader states that the package cannot do
  something it does
- **THEN** the suite fails, naming the file and the sentence

#### Scenario: The readme is among the files checked

- **WHEN** the set of documents the check reads is inspected
- **THEN** it contains `README.md` — the file packed into every package — and the documentation
  set, discovered from the repository rather than from a list written by hand

#### Scenario: A document added later is covered without being registered

- **WHEN** a new markdown file is added under the documentation roots a consumer reads
- **THEN** it is checked by the same sweep, with no edit to the check

#### Scenario: A guard does not require a claim that has become false

- **WHEN** the suite asserts that a document says the package does not do something
- **AND** the package does that thing
- **THEN** that assertion is a defect in the suite, and the suite fails rather than holding the
  false sentence in place

#### Scenario: A retired sentence cannot be reintroduced

- **WHEN** a sentence a shipped capability falsified is written back into any shipped document
- **THEN** the suite fails, so the correction survives the next author as well as this one

#### Scenario: A needle that matches nothing is not mistaken for a passing check

- **WHEN** the check holds out a sentence
- **THEN** that sentence is a literal the document it guards could contain, verified rather than
  assumed, so a drifted wording is not read as a clean result

### Requirement: An image in the packed readme is rendered, and shows what its release shipped

The packed readme may carry images, and an image fails differently from a link: a package feed
renders **no image from a relative path and none from a host outside its allow-list**, and
nuget.org reports that only to the package's own owner. A reader is shown a gap with nothing
indicating anything was intended, and nobody outside the project is told.

So, in addition to being an absolute `https` URL: **every** image in the packed readme SHALL be
served from a host the package feed renders, and every image **addressed to this repository's own
content host** SHALL name a file present in this repository.

**The second half is deliberately narrower than the first, and the narrowing is not an
oversight.** An allow-listed image that belongs to somebody else — a build badge, say — has no
path in this working tree to resolve, and requiring one would either forbid badges or invite a
guard that pretends to check them. The rule is the same one this capability already applies to
links: shape and host for everything, local existence only for what is ours.

**This mostly extends the packed readme's link guarantee rather than restating it**, and the
requirement `Documentation a consumer follows from the package page resolves` is deliberately left
intact. **One arm does overlap, and saying so is cheaper than pretending otherwise:** a relative
image fails both this and that requirement's *A relative image fails* scenario. The duplication is
accepted — the two are worth checking from both sides, and dropping either would make one
requirement depend on the other's implementation — but it is overlap, not extension. Two things
are genuinely added that the other requirement does not cover. First, it holds a link to a file
in this repository only when that link addresses the repository's own document-browsing prefix;
an image cannot use that prefix — it serves a web page rather than image bytes, and that host is
**not** on the feed's allow-list — so every image necessarily takes the form that requirement
accepts **unchecked**. Second, a document link and an image differ in who learns they are broken.

**A reader of a published version SHALL see the images that version shipped.** The readme is
frozen per published version but an image it names is fetched live, so an address that tracks a
moving branch leaves a published package page showing whatever the repository holds today — a
screenshot of a screen that has since changed, or a gap where a renamed file used to be, on a
version nobody can correct. The address SHALL therefore be pinned to something that does not move
once published, and the pin SHALL be derived from the declared version rather than written out a
second time, so a version bump cannot leave the images pointing at the previous release while
every guard stays green.

**Whether the published page actually rendered the image remains a human check**, on the same
terms this capability already states for declared URLs: a guard proves shape, host and local
existence, and reachability is confirmed once, by a person, after the push. The publishing runbook
SHALL state that check and SHALL state what the pin requires of a publish, because an image
address that resolves only after a step the runbook does not mention is a step that will be
missed.

#### Scenario: An image from a host the feed will not render fails

- **WHEN** an image in the packed readme names a host the package feed does not render
- **THEN** the suite fails, naming the image, because the published page would show a gap and
  would report it to nobody but the package owner

#### Scenario: An image naming a file that is not in the repository fails

- **WHEN** an image in the packed readme resolves to a path in this repository
- **AND** no file exists at that path in the working tree
- **THEN** the suite fails, so a renamed or missing screenshot is caught before it is frozen into
  a published version

#### Scenario: An allow-listed image belonging to somebody else is accepted unchecked

- **WHEN** an image is served from an allow-listed host that is not this repository's content host
- **THEN** it passes, because it names no path here to resolve — and the guard reports that it
  checked host and shape only, rather than implying it verified a file

#### Scenario: The image address does not move after publication

- **WHEN** an image address in the packed readme is inspected
- **THEN** it is pinned to a ref that does not move once the version is published, so a later edit
  to the repository cannot change what a published package page shows

#### Scenario: The pin tracks the declared version

- **WHEN** the declared version changes and an image address still names the previous release
- **THEN** the suite fails, on the same terms as the documentation that states the current version

#### Scenario: What the pin asks of a publish is written down

- **WHEN** the publishing runbook is read before a release
- **THEN** it states what must exist for the packed readme's images to resolve, and that a person
  confirms the published page renders them

#### Scenario: Rendering is not claimed by the guard

- **WHEN** the image guard passes
- **THEN** it has proved host, shape and local existence only, and the documentation says so, so
  no reader mistakes it for proof that a published page displayed anything
