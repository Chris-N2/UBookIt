## Purpose

How a site's own code supplies public holiday dates to uBookIt, and how an operator turns them
into site closures: a published port the host implements, and an operator-triggered preview that
selects which of the returned dates the organisation is actually closed on.

## ADDED Requirements

### Requirement: A site supplies its own holidays through a published port

The package SHALL define a port a site's own code may implement to supply public holidays, taking
an inclusive date window and returning, for each holiday, **a date and a name**.

**The name SHALL be required of the source**, because a closure's label is required and a date-only
port would oblige the package to invent one for every row — the bare-date problem the label exists
to prevent.

The package SHALL ship **no holiday data for any country**, and SHALL make no assumption about
where a source gets its dates: an HTTP API, a file, a database table and a hard-coded list are all
equally valid implementations.

**The port SHALL NOT take a region, country or locale.** Which jurisdiction's holidays a site wants
is a property of the implementation the site registered, not a parameter the package can
meaningfully validate or default.

A source SHALL be free to return fewer holidays than the window asked for, including none.

#### Scenario: A source returns holidays for the window
- **WHEN** the package asks a registered source for a window
- **THEN** it receives zero or more holidays, each carrying a date and a name

#### Scenario: The package ships no holiday data
- **WHEN** the package's own code and content are inspected
- **THEN** no public holiday date for any country appears in it

#### Scenario: A source narrower than the window is not an error
- **WHEN** a source returns holidays covering only part of the requested window
- **THEN** the result is used as returned, and no failure is reported

### Requirement: With no source registered, the feature is absent

Where a site has registered no source, the package SHALL present **no import control and no
explanation of one**. The capability SHALL be absent rather than disabled, refused, or rendered
with a message.

**This follows the delivery API's reasoning about a disabled direction**: a control that appears and
then explains it cannot work is a control that looks live and is not. A developer learns from the
documentation that a source must be registered; an operator on a site without one is not shown a
feature their site does not have.

**It is the reasoning that carries over, not the rule.** The delivery API's rule is stronger: an
anonymous caller receives no status, header, body member or timing signal separating "disabled"
from "never existed". This capability does not meet that bar and SHALL NOT be read as claiming it,
because the client must be told which case it is in so it can render nothing — the probe answers
exactly that, in a body member. What makes the weaker rule sufficient here is the audience: the
probe sits behind backoffice authentication and the same verb as the import, so what it
distinguishes is distinguished only for operators who could register a source themselves. An
anonymous caller learns nothing either way, which is the guarantee the delivery API's rule exists
to give.

Registration SHALL be optional in the ordinary sense — the package SHALL function completely
without one, and SHALL NOT fail to start, warn, or log about its absence.

#### Scenario: No source, no control
- **WHEN** the closures screen is presented on a site with no registered source
- **THEN** no import control appears, and nothing refers to importing

#### Scenario: No source, no failure
- **WHEN** the package starts on a site with no registered source
- **THEN** it starts normally, and nothing is logged or warned about the absence

#### Scenario: The endpoints are absent too, not merely hidden
- **WHEN** an import endpoint is called directly on a site with no registered source
- **THEN** the request is refused, so the absence is not something only the client observes

### Requirement: Import is operator-triggered and never automatic

The package SHALL import holidays **only** in response to an operator's action. It SHALL NOT
import on a schedule, at startup, on a timer, or as a side effect of any other operation.

**An automatic import would make a deleted closure impossible to delete.** A closure an operator
removed would return at the next run, and preventing that would require remembering every deletion
— invisible state that must then expire on some rule nobody can see. Operator-triggered means a
closure that reappears is one a person asked for.

#### Scenario: Nothing imports on its own
- **WHEN** the package runs with a registered source and no operator acts
- **THEN** no holiday is fetched and no closure is created

#### Scenario: A deleted closure stays deleted
- **WHEN** an operator deletes a closure that a previous import created, and no further import is run
- **THEN** the closure does not return

### Requirement: The preview is a selection, not a confirmation

Before creating anything, the package SHALL present every holiday the source returned for the
requested window, and SHALL let the operator **choose which of them to create**. Confirming SHALL
create exactly the chosen rows and no others.

Each row SHALL carry one of three states:

- **New** — no closure exists for that date. Selectable, and selected by default.
- **Already closed** — a closure already exists for that date, whoever created it. Not selectable,
  because at most one closure may exist per date and the existing one is not this import's to
  replace.
- **Cannot import** — the holiday would be rejected by the domain, such as a name longer than a
  label may be. Not selectable, and SHALL be shown **with the reason** rather than omitted.

**Deselection SHALL NOT be remembered.** A later import over a window containing the same date
SHALL offer it again. A remembered refusal is invisible state that diverges from what the source
says, and the operator would have no way to see or revise it.

The preview SHALL create nothing. Asking for it SHALL be safe to repeat.

#### Scenario: Only the selected rows are created
- **WHEN** a preview offers four new holidays, the operator unticks one, and confirms
- **THEN** three closures exist, and the unticked date has none

#### Scenario: An existing closure is reported, not replaced
- **WHEN** the source returns a holiday for a date that already carries a closure
- **THEN** the row is reported as already closed, it cannot be selected, and the existing closure's date and label are unchanged after confirming

#### Scenario: A rejected holiday is shown with its reason
- **WHEN** a source returns a holiday whose name is longer than a closure label may be
- **THEN** the row is reported as unimportable with the reason, and confirming creates nothing for it

#### Scenario: A deselection is not remembered
- **WHEN** an operator unticks a date, confirms, and later previews a window containing that date again
- **THEN** the date is offered again as new

#### Scenario: Previewing changes nothing
- **WHEN** a preview is requested twice without confirming
- **THEN** no closure is created by either request, and the second reports the same states as the first

### Requirement: What the import creates is an ordinary closure

A closure created by an import SHALL be **indistinguishable from one an operator typed**: same
storage, same validation, same editing, same deletion, same per-resource opt-out, and the same
place in the availability ladder.

The package SHALL NOT record that a closure came from an import, and SHALL NOT treat one
differently anywhere. **Nothing would read such a record**: import is operator-driven, deselection
is not remembered, and reporting a date as already closed needs no origin.

#### Scenario: An imported closure behaves as any other
- **WHEN** a closure created by an import is renamed, moved, opted out of by a resource, and deleted
- **THEN** each behaves exactly as it does for a closure an operator created by hand

#### Scenario: An imported closure closes resources on the same terms
- **WHEN** availability is projected for a resource on a date an import closed
- **THEN** the date offers no free time, on the same terms as any other closure

### Requirement: A source's failures and oddities are the package's to absorb

The source is code the site wrote, frequently reaching a network. The package SHALL absorb what it
returns without failing in a way the operator cannot act on.

- **A source that throws or does not answer** SHALL produce a reported failure on the preview,
  naming that the source failed rather than presenting an empty list — an empty list and a broken
  source are different facts and SHALL NOT be conflated.
- **Two holidays on one date** — which a source merging regions can legitimately produce — SHALL
  collapse to a single row, retaining the first name, and the preview SHALL report that a duplicate
  was collapsed rather than silently discarding it.
- **A holiday outside the requested window** SHALL be ignored, since the window is what the
  operator asked to see.
- **The operator's request SHALL be cancellable**, and the package SHALL pass cancellation to the
  source so a slow one can be abandoned rather than holding the screen.

#### Scenario: A failing source is reported as failing
- **WHEN** the registered source throws while a preview is requested
- **THEN** the preview reports that the source failed, and does not present an empty holiday list

#### Scenario: Two names for one date collapse and are reported
- **WHEN** a source returns two holidays on the same date
- **THEN** one row is offered for that date carrying the first name, and the preview reports the collapse

#### Scenario: A holiday outside the window is ignored
- **WHEN** a source returns a holiday dated outside the requested window
- **THEN** it is absent from the preview

#### Scenario: Cancellation reaches the source
- **WHEN** a preview request is cancelled
- **THEN** the cancellation is passed to the source

### Requirement: Importing is reached by the verb that changes closures

Both previewing and importing SHALL require `UBookIt.Settings` — the verb that already governs
changing the site closure list — and SHALL be enforced by the server.

**Previewing is gated identically to importing** although it creates nothing: it makes the site's
own code reach out on an operator's behalf, and it is the first half of an act whose second half
writes closures. Splitting the two would grant a reader the outbound call without the decision it
exists to serve.

`UBookIt.Configure` SHALL NOT reach either. Reading the closure list is a resource-level act that
verb holds; deciding the site's closures is not.

**The probe reporting whether a source is registered SHALL require the same verb**, so that all
three of this capability's endpoints classify under `UBookIt.Settings` and none is left
unclassified. It is gated not because the answer is sensitive but because it is of no use below the
verb that acts on it: a client that may not import has nothing to do with knowing a source exists,
and an endpoint answering a question its caller cannot act on is a gap in the classification rather
than a convenience.

#### Scenario: The settings verb reaches both halves
- **WHEN** a user whose groups hold `UBookIt.Settings` previews and then imports
- **THEN** both are served

#### Scenario: Configure alone reaches neither
- **WHEN** a user whose groups hold only `UBookIt.Configure` requests a preview or an import
- **THEN** both are refused, and reading the closure list remains available to them
