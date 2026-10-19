## Context

- **`Service`** (`src/UBookIt.Core/Services/Service.cs`) is a sealed class with get-only `Id`,
  `Name`, `Duration` and `Roles`, built by the public factory
  `Service.Create(string? name, ServiceDuration? duration, IEnumerable<ServiceRole> roles, Guid? id = null)`.
  It has no free-text field. `Resource.Description` exists but is published by the delivery API,
  so it cannot be reused.
- **The composer** (`src/UBookIt.Persistence/Notifications/BookingMessageComposer.cs`):
  - builds uBookIt's own wording first and keeps it as the fallback;
  - then builds `BookerMessageModel` and asks `IBookingTemplateRenderer` for the site's content;
  - `DescribeAsync` gives the service name from the booking's `ServiceAttribution` (recorded at
    placement) and reads each claimed resource by id. That read is wrapped in a catch that
    excludes cancellation, so a store failure costs the name and never the message.
- **`BookingService.MoveAsync`** moves from `Requested` or `Confirmed`. So "the move message"
  does not imply a confirmed booking, and the rule has to read the status.
- **`PublishedCallShapeTests`** guards the call shapes of published methods. It exists because an
  overload broke source compatibility on 2026-10-11.
- The last migration is `20261011182653_ClosureDateRanges`. Migrations are additive by
  convention and by spec.

## Goals / Non-Goals

**Goals:**
- A site can write per-service text that reaches a confirmed booker in uBookIt's own email, with
  no template.
- The text never reaches an unconfirmed booker, the site's own recipients, or the anonymous
  delivery API.
- No published call shape changes.

**Non-Goals:** resource-level instructions; the confirmation page; template selection per service;
per-booking meetings. See `proposal.md`.

## Decisions

### 1. The instructions are added to `Service` without touching `Create`

Adding a parameter to `Service.Create`, even an optional one, changes a published method's
signature. Compiled callers break (`MissingMethodException`), and the 2026-10-11 lesson shows an
overload can break source compatibility too.

So `Create` is unchanged and the aggregate gains:
- `public string? BookerInstructions { get; }`;
- an instance method `DomainResult<Service> WithBookerInstructions(string? instructions)`. It
  normalises (trim; empty or whitespace becomes `null`), validates the 2000-character limit
  (`service-booker-instructions-too-long`, field `bookerInstructions`), and returns a new
  `Service` equal in everything else. `Service` stays immutable.

Callers that know about instructions chain the two calls: the Management API create and update
paths, and the store mapping. Everything else is untouched and gets `null`.
`PublishedCallShapeTests` gains the new member, and keeps `Create`'s shape pinned.

*Rejected:* a settable property. `Service` is immutable, and a setter would let validation be
bypassed.

*Rejected:* a separate `ServiceBookerInstructions` table keyed by service id. It is cleaner for
erasure, but the text is site configuration, not personal data, and a column is the honest
representation.

**API validation must report every failure at once.** The service request validator collects
domain failures into one 400. A blank name and over-long instructions must both be reported, so
the controller calls `WithBookerInstructions` on the candidate even when `Create` failed. It does
that by validating the instructions independently through a static helper that the instance
method also uses. Then a request failing both rules yields both codes.

### 2. The confirmation rule lives in the composer and reads the status

`ForBookerAsync` reads the service only when `booking.Status == BookingStatus.Confirmed` and the
booking has a `ServiceAttribution`. The status rule settles every case, so the message kind is
never consulted:
- **Placed:** `Confirmed` under auto-confirm, `Requested` otherwise.
- **Confirmed:** always `Confirmed`.
- **Moved:** whatever the booking was.
- **Declined and Cancelled:** never `Confirmed`.

Reading the service only when it is needed also keeps the extra read off every other message.

The read is wrapped exactly like the resource read in `DescribeAsync`:
- `OperationCanceledException` is excluded;
- any other exception is logged with the booking id only and costs the instructions alone.

A deleted service (`null`) and a service with no instructions both yield none.

`ForSiteAsync` is not touched. `InternalMessageModel` has no member for the instructions, so the
compiler enforces the internal exclusion, the same way it enforces the absence of contact details.

### 3. Where they sit in uBookIt's own wording

```
<subject line>

<details: what, when (zone)>

<closing line for the state>

Instructions for your booking:
<the text, as written>

If you need to cancel this booking, use this link — …
<link>
```

Line endings in the text are normalised to the body's own `AppendLine` convention, so a CRLF from
the textarea does not double-space the message. The section is omitted entirely when there are no
instructions. No heading is left over a blank.

### 4. The model member

`BookerMessageModel` gains `public string? BookerInstructions { get; init; }`, set from the same
value the wording used. One read feeds both, so the template and the fallback cannot disagree.
`null` means "this message does not carry instructions". The XML docs say so, in the style of
`PreviousLocalStart` and `CancellationUrl`.

### 5. Persistence

- **Migration `AddServiceBookerInstructions`:** adds `bookerInstructions nvarchar(2000) NULL` to
  the service table, following the table's existing column naming. Apply verifies the actual
  casing against the snapshot.
- **The row:** gains a nullable string. `ServiceRowMapper` maps it through
  `WithBookerInstructions` on read, so the domain's normalisation applies to stored data too.
- **Guard:** an integration test asserts the migration's `Up` contains exactly one `AddColumn`
  and nothing else, read from the migration's operations, not by scanning its text.

### 6. The delivery API exclusion is checked on the wire

The delivery `ServiceReadModel` is not changed. The guard does not assert that a property is
absent from the type, because that misses a renamed or nested member. It serialises the real
`GET /services/{id}` and `GET /services` responses for a service whose instructions contain a
distinctive marker string, and asserts the marker appears nowhere in either body. The guard can
fire: a mutation that copies the instructions into any read-model member turns it red.

### 7. The backoffice editor

- **The field:** a `uui-textarea` in the Details group, labelled "Instructions for the booker",
  with a hint linked through `aria-describedby`. The hint says the text is sent to the booker once
  their booking is confirmed, and is not shown on the public site.
- **Errors:** the failure for `bookerInstructions` is placed against it, through the existing
  field-error mapping.
- **Localization:** the new keys are added to the English file. The fallback from
  `backoffice-localization-fallback` covers other cultures.
- **Clients:** the generated Management API client is regenerated on each line, because the
  generators differ between 17 and 18.

### 8. Both lines

Applied on `main`, then ported to `dev/v18` in the same sitting, from a branch off `dev/v18`. The
v18 port re-runs the client generator and the migration scaffolding against Umbraco 18. The
migration has to be equivalent on both lines, not identical by file: a site that upgrades from
17.x to 18.x must not find the column twice or not at all.

## Risks / Trade-offs

- **Send-time reads mean the text can change between messages.** A booker's confirmation and a
  later move message can carry different instructions. That is intended, because a changed link
  must reach them, and the spec states it. The cost is that the messages are not a fixed record
  of what was said at booking. The booking reference and time still are.
- **A standing join link in email is only as private as the inbox.** That is the nature of the
  feature, and it is no worse than the cancellation link, which already travels the same way.
  The docs say plainly that the field is not for secrets.
- **The editor hint makes a promise about the public site.** "Not shown on the public site" is
  true of the package's own front end and delivery API. A site's own template could still render
  the instructions publicly. The hint describes the package's behaviour, and the docs carry the
  narrowing.
- **The migration on two lines** is the place a cross-line defect would hide. Task 8 runs the
  upgrade path (17.x database, then the 18.x package) against a real SQL Server.
