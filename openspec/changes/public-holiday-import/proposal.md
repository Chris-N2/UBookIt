## Why

Site closures now exist, so a site can say "we are shut on Boxing Day" once. It still has to say
it **by hand, every year, for every date** — and public holidays are the case where the dates are
already published by somebody else, differ by country and region, and move around (a bank holiday
falling at a weekend is substituted to the next working day).

uBookIt cannot know where those dates come from. A UK site wants `gov.uk`; another wants a table
its HR system fills; another wants a file. So the package publishes the **seam** and imports what a
site's own code supplies, rather than shipping data it would have to maintain for every country
that uses it.

## What Changes

- **A published port, `IPublicHolidaySource`**, that a site's own code may implement: given a date
  window, return holidays as a **date and a name**. A name rather than a bare date, because a
  closure's label is required — a date-only seam would force the package to invent "Public holiday"
  for every row, which is the bare-date problem the label exists to prevent.
- **Registration is optional, and absence is the whole answer.** With no source registered the
  feature is *absent*: no import control, nothing to explain, nothing that looks live and is not.
  The port follows the `IBookingObserver` pattern — an optional dependency, defaulted to null.
- **An operator-triggered preview that is a selection, not a confirmation.** The operator asks for
  a window (defaulting to today through the end of next year), sees every holiday the source
  returned, and **unticks the ones the organisation is not closed on**. Confirming creates only the
  ticked rows.
- **Three row states in that preview**: *new* (selectable), *already closed* (a closure exists for
  that date, whoever made it), and *cannot import* (the domain would reject it — an over-long name —
  shown with its reason rather than silently dropped).
- **Imported closures are ordinary closures.** They are created through the same store and the same
  validation as a hand-typed one, so everything that already works — renaming, deleting, a
  resource opting out, the availability ladder — works on them unchanged, and no part of the
  package learns what an import is.
- **A worked implementation on the TestSite**, against the real `gov.uk` bank-holiday feed. Not
  shipped, and not part of the package: it is the seam's only proof that a real source fits it.

**No breaking change.** A new interface, new endpoints, new client code; no existing signature
changes, no member is added to any published interface, and no stored data is rewritten. A site
that registers no source behaves exactly as it does today. Lands in **`17.2.0`** alongside
site-wide closures, and on the 18 line as **`18.1.0`** — each line's minor counts its own releases.

**No schema change at all.** The import writes existing `uBookItSiteClosure` rows. There is no
migration in this change.

## Non-goals

- **No scheduled or background import.** An automatic import that recreated a closure an operator
  had deleted would make that closure impossible to remove without inventing tombstones — invisible
  state that must then expire somehow. Operator-triggered means "it came back" is always something
  a person did. The package acts on a timer in exactly one place, the retention sweep, deliberately.
- **No remembered deselections.** Unticking May Day this year does not untick it next year. A
  remembered "no" is invisible state that silently diverges from what the source says; unticking
  again is cheap and visible.
- **No provenance.** A closure does not record that it came from an import. Nothing would read it:
  import is operator-driven, deselection is not remembered, and "already closed" needs no origin.
  A column nothing reads is a column that can drift from the truth. Additive later if a use appears.
- **No holiday data in the package, for any country.** Shipping dates means maintaining them —
  every country, every substitution rule, forever — and being wrong silently when a government
  changes one.
- **No region parameter.** England versus Scotland is the host's query, not ours. A source that
  merges regions may return two names for one date; the import collapses and reports that rather
  than pretending it cannot happen.
- **No new permission verb, and no change to what the existing ones govern.** Importing creates
  closures, which `UBookIt.Settings` already governs.

## Capabilities

### New Capabilities
- `public-holidays`: the source port and its contract, the preview's three row states and its
  selection semantics, what confirming creates, and the absence of everything when no source is
  registered.

### Modified Capabilities

None — and that is a claim worth stating rather than an omission:

- **`site-closures` is untouched.** An imported closure is created through the same store and
  validation as any other, so no requirement about closures changes. If this change needed to
  modify that spec, the import would have grown a second kind of closure, which is the thing it is
  shaped to avoid.
- **`permissions` is untouched.** The `UBookIt.Settings` bullet already reads "reading the site
  closure list, and changing it"; importing changes it. The preview is a read in service of that
  change and is gated identically. No new power is conferred: the source is code a developer
  registered, not a URL an operator supplies, so there is no outbound target anyone can choose.
  Recorded as considered rather than left silent.

## Impact

**Code**

- `UBookIt.Core`: `IPublicHolidaySource` and a `PublicHoliday` record; the import's decision logic
  (classify each returned holiday into new / already closed / cannot import, collapse same-date
  duplicates) as a pure function over a source's results and the existing closures.
- `UBookIt.Backoffice`: preview and import endpoints behind `UBookIt.Settings`, contract models, the
  import panel on the Closures view, new localization terms, regenerated OpenAPI client.
- `UBookIt.TestSite`: a `gov.uk` implementation of the port, registered in the site's own composer.
  Not packed.
- `UBookIt.Tests`: the classification logic as pure-function tests; endpoint tests over a fake
  source, including one that throws and one that returns duplicates; client tests for the selection
  state.

**Nothing touches** the availability path, the delivery API, the front end, or the schema.

**Docs**

- The seam in `docs/backoffice.md` (what an operator does) and a developer-facing section on
  implementing and registering a source, with the TestSite implementation as the worked example.
