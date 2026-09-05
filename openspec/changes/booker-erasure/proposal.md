## Why

uBookIt stores a booker's name, email and optional phone number and offers no way to remove
them. A site that takes bookings from the public needs one: a person may ask for their data to
be erased, and a site must be able to honour that without destroying its own records of what
was booked.

Erasure here means **anonymise, not delete**. An erased booking still occupies its interval,
still blocks its resources, still counts, and still has to be discussable over the telephone —
which is what the quotable reference was built to outlive the person for. Deleting the row
would silently free time a site had sold.

The domain cannot express this today. `Booker.Create` refuses an empty name and requires a
well-formed email, so the fields cannot simply be blanked and `"[erased]"` is not an address.
This is therefore a model change, and it is the first of four (erasure → subject search →
retention → privacy notice); it goes first because every later one needs the verb it defines.

## What Changes

- **A booker's contact details become erasable in the domain.** `Booker` keeps its identity
  role and gains two states: it either carries contact details or records that they were
  erased, with the instant of erasure. `Booking.Booker` stays non-null — a booking still always
  has a booker — so `Booking.Rehydrate`'s contract is unchanged.
- **Erasure removes the member key too.** An Umbraco member key identifies a person as surely
  as their email; leaving it behind would make the erasure a gesture.
- **A management endpoint erases one booking's personal data**, gated on Umbraco's Sensitive
  data group — the same group that decides who may read those details in the first place.
  Erasure is irreversible and idempotent: erasing an erased booking succeeds and changes
  nothing, so a retry is always safe.
- **BREAKING — `UBookIt.Core`'s published public API changes.** `Booker.Name`, `Booker.Email`
  and `Booker.Phone` shipped in `0.1.0` and are **removed**; the values move into a nested
  `Booker.Contact`, which is null exactly when the booker is erased. `BookingSummary`'s
  positional signature changes as two `string` members become one `SummaryBooker`, and
  `IBookingService` gains `EraseBookerAsync`, which breaks any external implementer.
  **This is not qualified by "unpublished" — `0.1.0` is on NuGet and these members were in
  it.** The compatibility promise in CLAUDE.md requires the call-out; the change is
  nonetheless proposed rather than deferred, because the alternative is a domain that cannot
  express erasure at all, and because the compiler names every affected call site rather than
  letting one fail at runtime. Whether `0.1.0` consumers exist is a release decision, not this
  change's to make.
- **BREAKING (unpublished): the wire gains an explicit three-state booker.**
  `BookingModel.Booker` stops being nullable. It becomes a member that is always present and
  states which of *shown*, *withheld* or *erased* applies, carrying contact details only in the
  first case and the erasure instant only in the last. This **replaces** the nullable member
  added since `0.1.0` and never published.
- **Erasure is visible to every user with section access**, not only to the Sensitive data
  group. That a record was erased is a fact about the record, not about the person, and it
  tells an operator something they must act on differently: *"ask a colleague who can see it"*
  and *"this is gone and nobody can retrieve it"* call for different next steps.
- **The backoffice list renders erasure as its own stated absence**, distinct from the withheld
  one it already renders and from an empty cell.
- **The schema stores the erasure.** `uBookItBooking` gains a nullable erased timestamp, and
  booker name and email become nullable. Additive; no data is destroyed by the migration.

## Capabilities

### New Capabilities
- `booker-erasure`: what erasure means and does not mean, what it removes, the state a booker
  is in afterwards, irreversibility and idempotence, who may perform it, and the constraint
  that keeps the claim honest — that booker contact details have exactly one durable home.

### Modified Capabilities
- `bookings`: **Booker identity** currently requires contact details of every booker
  unconditionally. It gains an erased state in which they are absent by construction rather
  than blank.
- `booking-management`: the read port and the list endpoint currently carry the booker's name
  and email as non-null strings, and the HTTP contract expresses withholding as a null member
  whose only possible meaning is "withheld from you". Both change shape. The backoffice view
  requirement gains the third rendering.
- `persistence`: the `uBookItBooking` column list changes — booker name and email become
  nullable, an erased timestamp is added — and the round-trip requirement must cover a booking
  whose booker has been erased.
- `sensitive-data`: **A withheld value is absent, not blanked** spends the null on withholding
  and states that a member whose absence is already meaningful must not be overloaded. Erasure
  makes absence meaningful, so the requirement is restated in terms of an explicit state rather
  than a null — a narrowing of the mechanism that *strengthens* the guarantee, since the
  ambiguity it warned about becomes unrepresentable.

## Non-goals

- **Finding a subject's bookings by email.** An erasure request arrives as an email address and
  the management port deliberately offers no way to search for one; `booking-management`
  already records that this is "a different query with different indexing, and is not provided
  here". Change ② adds it, gated on the same group, exact-match only, with the index that makes
  it affordable. Until then an operator erases a booking they have already located.
- **Retention.** Erasing on a timer is change ③. It reuses this change's verb rather than
  defining a second one, so that the scheduled path and the manual path cannot drift apart.
- **A privacy notice.** Change ④, and it depends on ③'s configured retention period.
- **Erasing anything other than a booker.** Resources and services carry no personal data.
- **Un-erasing.** Reversible erasure is not erasure.
- **Erasing a person across bookings.** This change erases one booking. Doing it for every
  booking a person made requires finding them, which is change ②.
- **An erasure audit trail.** Worth having, and it must be designed so that it does not record
  the erased address — which would re-home the data this change removes. Deferred rather than
  done badly.

## Impact

- `UBookIt.Core`: `Booker` (state), `BookingSummary` (contact details become optional and
  gain the erasure instant), `IBookingService` or a sibling port for the erase verb.
  `Booking`, `Booking.Rehydrate` and `BookingReference` are untouched.
- `UBookIt.Persistence`: `BookingRow`, `UBookItDbContext` mapping, one additive migration,
  `SqlBookingStore` and `SqlBookingManagementStore` projections.
- `UBookIt.Backoffice`: `BookerModel`/`BookingModel`, `BookerVisibility`,
  `BookingModelMapper`, `BookingsController` (new endpoint), and the Lit bookings view.
- `UBookIt.Web`: **expected to be untouched.** The delivery API has no endpoint that reads a
  booking back — its only booker-carrying responses echo a placement in the same request — so
  no anonymous surface can disclose an erased booker. To be re-verified by enumeration during
  apply rather than assumed from this sentence.
- Tests: the sensitive-data membership-snapshot guard will fail by design until the new
  members are recorded, and its failure message is the intended prompt.
- Docs: `docs/backoffice.md` gains what erasure does, who may do it, and that it cannot be
  undone.
