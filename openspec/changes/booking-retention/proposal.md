## Why

A site can now erase a booker on request — one booking at a time (`booker-erasure`), found from
an email address (`find-by-booker`). Both are *reactive*: personal data leaves the database only
when somebody asks. Nothing expires. A site that has taken bookings for three years still holds
the name, email address and phone number of everyone who ever booked, indefinitely, with no way
to say otherwise.

Data-protection practice — and every privacy notice a site will want to publish — asks for the
opposite default: personal data is kept for a stated period and then goes, without anybody
having to remember. This is change ③ of roadmap 0.3.0, and change ④ (the privacy notice) depends
on it: the notice has to read the configured period, or it will be a claim the code does not
keep.

## What Changes

- **A site-wide retention period**, configured as `UBookIt:RetentionDays`, alongside the existing
  flat `UBookIt:*` keys. **Absent by default — retention is OFF unless a site turns it on.**
- **A background job** that erases the booker of every booking whose interval **ended** more than
  that many days ago, and that has not been erased already. It reuses
  `IBookingService.EraseBookerAsync` — the verb `booker-erasure` defined — rather than
  introducing a second way to erase.
- **A store read that selects bookings due for erasure by time alone**, returning booking
  **identifiers only**. It never reads, returns or logs a contact detail. That is a security
  property, not tidiness — see the `booker-erasure` change below.
- **A filtered index** supporting that read, so the job does not scan a table that grows without
  limit.
- **Documentation** of the setting, what it does, that it cannot be undone, and — stated plainly
  — that turning it on erases historical bookings on the *first* run, not gradually.
- The retention period is expressed in **days after the booking's end**, for every booking
  whatever its status. A booking cancelled long before its date is erased on the same clock as
  one that went ahead; an operator wanting it gone sooner erases it by hand, which is what
  changes ② and ③ of 0.3.0 exist for. *Decided with sign-off; the alternative needed a
  cancellation timestamp the schema does not have.*

### Not a breaking change, and no destructive schema change

Nothing is removed from the public API. `SiteBookingSettings` gains an optional property, the
booking store gains a read, and the migration adds an index — additive on every count. No column
is dropped, no data is rewritten by the migration itself.

**But the feature is destructive by design, and once, at the moment it is switched on.** Erasure
is irreversible. A site setting `RetentionDays` to 90 on a database holding three years of
bookings erases nearly all of them on the job's first run. That is correct behaviour and it is
the whole point, but it must be documented as a one-way door rather than discovered.

## Capabilities

### New Capabilities

- `booking-retention`: the retention policy itself — that a site may configure a period after
  which booking personal data is erased automatically; that the period is measured from the
  booking's end and applies whatever its status; that the feature is off unless configured and
  that an unreadable configuration means off rather than a guessed period; that the sweep is
  idempotent, resumable, and safe to run concurrently with anything else; that it erases through
  the package's one erasure verb; and that what it does and cannot undo is documented.

### Modified Capabilities

- `booker-erasure`: the requirement *"Only a caller permitted to read contact details may erase
  them"* is written entirely in terms of a **caller**, and retention has none — the job runs
  unattended with no user. The requirement is reopened using the same method `find-by-booker`
  used on `sensitive-data`: promote the guarantee to the operative sentence, replace the
  mechanism with obligations the unattended path must meet, and keep the tripwire. Also, the
  requirement *"What erasure does not reach is documented"* describes erasure as something an
  operator performs on one booking; it needs to cover erasure that nobody performed.
- `persistence`: the composition requirement enumerates what the composer registers and how a
  missing setting behaves; the retention job and the retention setting join that list, and an
  unreadable retention value has to behave *unlike* the existing settings — it may not fall back
  to a working default. The schema requirement states which columns are indexed and why; the new
  filtered index belongs there.

## Impact

**Code**

- `UBookIt.Core`: `SiteBookingSettings` gains `RetentionDays` (`int?`). `IBookingStore` gains a
  read returning the ids of bookings due for erasure.
- `UBookIt.Persistence`: the SQL implementation of that read; the retention job; its registration
  and the setting's resolution in `UBookItPersistenceComposer`; one additive EF Core migration
  adding a filtered index on the booking table.
- `docs/`: the setting, the one-way-door warning, and the sentence change ④ will read.

**Dependencies**

Umbraco's `IDistributedBackgroundJob` (`Umbraco.Cms.Infrastructure`), already available
transitively and present in the pinned 17.6.2 — verified, not assumed. No new package reference.

**Systems**

The job takes a database-backed lease from Umbraco's own distributed scheduler, so a
load-balanced site runs it on exactly one server without uBookIt owning a lock.

## Non-goals

- **Deleting booking rows.** Retention erases the *person*; the booking, its reference, its
  interval and its status survive forever. Settled by the roadmap and by `booker-erasure`, and
  restated here because "retention" ordinarily implies deletion and this one does not.
- **Recording *why* a booking was erased.** A booking erased by the timer is indistinguishable
  from one erased on request, deliberately: the row is anonymised identically and the answer to
  "why is this gone?" is the same. *Decided with sign-off.* An audit facility is not something
  0.3.0 has anywhere else, and inventing one here would need a place on the wire and in the
  backoffice to be worth storing.
- **Per-resource or per-service retention periods.** Site-wide only, per the roadmap.
- **A backoffice screen for the setting.** Config-file only — a settings screen wants
  admin-only access, which is roadmap 0.9.0. Same reasoning already applied to 0.5.0's email
  configuration.
- **Reporting on what retention erased.** Beyond a count in the log. A durable retention report
  is explicitly named in `booker-erasure` as the kind of feature that would give contact details
  a second home; it is not being built, and the job's logging carries counts and identifiers
  only.
- **Erasing cancelled bookings sooner than others.** Considered and declined above; it needs a
  cancellation timestamp the schema does not record.
- **Retention for anything other than bookings.** Resources and services hold no personal data.
