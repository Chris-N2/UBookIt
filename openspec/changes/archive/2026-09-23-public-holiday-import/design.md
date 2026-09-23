## Context

See `proposal.md` — *Why*. What shapes this design is what shipped this morning and what it left
in place.

`SiteClosure` is a date, a required label and a stable id. `uBookItSiteClosure` has a **unique
index on the date**, so at most one closure exists per date and a second is refused with
`duplicate-closure-date` rather than merged. `ISiteClosureManagementStore` already does create,
update, delete and a filtered list. A resource opts out of a closure **by id**, and the whole
availability ladder — closure, then the resource's own exception, then the weekly pattern — is
decided in `AvailabilityConfiguration.EffectiveWindows`.

None of that needs to change. An import that produces ordinary `SiteClosure` rows inherits every
one of those behaviours for free, which is the property this design is built to keep.

One precedent decides registration: `BookingService` takes `IBookingObserver? observer = null`.
A host that supplies one gets observation; a host that does not gets silence, with no configuration
switch and nothing logged.

## Goals / Non-Goals

**Goals:**

- A port a site can implement against anything, with no uBookIt-shaped assumptions in it.
- An import whose every outcome is visible before anything is written.
- No new stored state: no provenance, no tombstones, no remembered refusals, no migration.
- The import's decisions testable without a network, a database, or a running site.

**Non-Goals:**

- Anything scheduled. See the proposal's non-goals — this is the decision the whole shape rests on.
- Regions, locales, or multi-source merging.
- Editing a closure through the import. The import creates; the closures screen edits.

## Decisions

### D1 — The port returns a name, not just a date

```csharp
public interface IPublicHolidaySource
{
    Task<IReadOnlyList<PublicHoliday>> GetAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record PublicHoliday(DateOnly Date, string Name);
```

A closure's label is required — decided during site-wide-closures, because an inherited closure is
offered for opt-out in a resource's editor and a bare date asks an operator to exempt something
they cannot identify. A date-only port would push that problem into the package: it would have to
invent "Public holiday" for every row, and every imported closure would read identically in the
opt-out list.

*Alternative considered: date only, with the operator naming each row in the preview.* It makes the
common case laborious — a dozen rows to type — to serve a source that could have supplied names.
Sources that have dates generally have names; `gov.uk` supplies `title`.

*Naming*: `IPublicHolidaySource` departs slightly from this codebase's agent-noun habit
(`IBookingObserver`, `IResponsibleRecipientResolver`, `IBookingTemplateRenderer`). Chosen because
the thing is where data comes from rather than something done to it, and because "provider" reads
as DI vocabulary. Cheap to rename before release; impossible after.

### D2 — Registration is an optional dependency, and absence is total

The port is resolved as `IPublicHolidaySource?`. No source means:

- the preview and import endpoints refuse, so the absence is not merely a client-side hide;
- the client renders no import control and no explanation;
- nothing is logged, warned, or configured.

*Alternative considered: a registered "null source" returning nothing.* It makes absence
indistinguishable from a source that legitimately returned no holidays — which is a real state, and
one the preview must be able to report honestly.

### D3 — The preview is a selection, and its classification is a pure function

The whole decision is one function over two inputs:

```
classify(holidays from source, existing closure dates) -> rows
```

Each row is `New` (selectable), `AlreadyClosed`, or `CannotImport(reason)`. That function needs no
database, no network and no HTTP, so every interesting case — duplicates, an over-long name, a date
outside the window, a date that already has a closure — is a unit test over data.

The endpoint is then thin: fetch from the source, read the existing closures, classify, respond.
Confirming takes the chosen dates and creates them through the existing management store.

*Why classification is not the store's job*: the store's business is rows. Deciding what an operator
should be offered is a domain rule, and putting it in `UBookIt.Core` keeps it testable and keeps the
storage layer free of a second opinion about closures — the same reasoning that kept closure
precedence out of SQL.

### D4 — Confirm re-validates rather than trusting the preview

Confirming sends dates and names, not a token referring to the earlier preview. The server
validates each again through `SiteClosure.Create` and the unique index.

**The preview's answer can be stale**, and cheaply: someone else can create a closure between the
two requests, and the same operator can leave the screen open. So a date that was *new* at preview
may be *already closed* at confirm. That is reported per row in the result, and the rest still
create — a batch that failed wholesale because one date was taken would be a worse answer than a
report.

*Alternative considered: a server-side preview token.* It would make the confirm authoritative
about a world that has since moved on, which is the same class of error as a count rendered beside
an action.

### D5 — Same-date duplicates collapse, and say so

A host merging England and Scotland can legitimately return two names for one date. The unique
index refuses the second, so the import must decide before writing: **first wins, and the preview
reports the collapse**. Silently discarding the second would leave an operator unable to tell why a
name they expected never appeared.

*Alternative considered: joining the names* ("New Year's Day / Hogmanay"). It invents a label
neither source row carried, and the label is what an operator sees against an opt-out.

### D6 — Failure is reported as failure, never as emptiness

A source that throws, times out, or is cancelled produces a reported failure. **An empty list and a
broken source are different facts**: a site with no holidays in the window is a real, correct
answer, and presenting a broken source as "no holidays" would invite an operator to conclude their
calendar is clear.

Cancellation is passed through to the source rather than abandoned locally, so a slow source can
actually stop.

*No package-level timeout.* A timeout is a policy that would need a setting, a default and a
justification; the request's own cancellation is the standard contract, and the documentation says
a source must respect it.

### D7 — No stored state is added by this change

No provenance column, no tombstones, no remembered deselections, **no migration**. Every one of
those would be state that nothing reads, or state that must expire on a rule nobody can see. The
import's entire memory is the closures it created, which are visible, editable and deletable like
any other.

This is why the change modifies no existing capability: with nothing recorded about origin, there
is nothing about a closure that this feature could contradict.

### D8 — The TestSite implementation is the seam's proof, and is not shipped

A `gov.uk` implementation lives in `UBookIt.TestSite`, registered in that site's own composer. It
is not packed and not part of the public surface.

**An interface nobody has implemented is a guess about what a real source needs.** The UK feed is
the case the seam was designed against — `title`, `date`, and a `notes` field carrying "Substitute
day" — and writing it is what proves the port can express a real jurisdiction's data, including
substituted bank holidays, without the package knowing what a substitution is.

Its live verification reaches the public `gov.uk` open-data endpoint: no authentication, no personal
data, a published open feed. Tests never touch it; they use a fake source.

## Risks / Trade-offs

- **A site that forgets to import gets no holidays** → accepted, and the direct consequence of
  never importing automatically. The failure is visible (an empty list on a screen someone opened)
  and not a safety failure: a missing closure means the site takes bookings it might not have
  wanted, which is the state every site is in today.
- **Unticking is repeated every import** → accepted over remembering it. The cost is a few clicks a
  year; the alternative is state that silently contradicts the source.
- **A source's names are a site's own text, rendered in the backoffice** → the label is already
  operator-supplied free text with the same handling; the import adds no new class of content. It
  is never rendered to anonymous callers, because closures are not disclosed publicly.
- **The preview can be stale by the time it is confirmed** → D4 re-validates and reports per row
  rather than pretending otherwise.
- **`gov.uk` could change its shape and break the TestSite implementation** → it is example code in
  a site that is not shipped; a break there is a demo to fix, not a package regression. The
  package's own tests never call it.

## Migration Plan

None. No schema change, no data change, no configuration change. A site that registers no source is
unaffected in every observable way; a site that registers one gains a control on the closures
screen.

Order of work follows `tasks.md`: the port and the classification rule first (both pure, both
testable without infrastructure), then the endpoints, then the client, then the TestSite
implementation, then docs — with the TestSite implementation before the live checks, since it is
what makes a live check possible.

Ships in `17.2.0` on `main` alongside site-wide closures, and on the 18 line as `18.1.0`; each
line's minor counts that line's own releases.
