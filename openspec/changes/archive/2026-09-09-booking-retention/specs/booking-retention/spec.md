## ADDED Requirements

### Requirement: A site may configure a retention period, and there is none unless it does

The package SHALL let a site configure a **retention period in whole days**, site-wide, through
the `UBookIt` configuration section. When a period is configured, the package SHALL erase the
personal data of bookings older than it, per the requirements below.

**There SHALL be no retention unless a site configures one.** A package that erased personal
data by default would destroy a site's records on upgrade, on a policy its owner never chose.

**A configured value that cannot be read as a positive whole number of days SHALL mean no
retention**, and SHALL NOT be replaced by a default period. Absent, blank, non-numeric, zero and
negative all mean the same thing: retention is off.

This is deliberately the opposite of how the package treats its other numeric setting. A
malformed query-range guardrail falls back to a working default because the cost of being wrong
is a rejected query. **The cost of being wrong here is irreversible destruction of personal
data**, so the failure direction is chosen to keep data rather than to keep the feature. Reading
`0` as "erase as soon as a booking ends" is refused on the same ground: it is far likelier to be
somebody writing "off" than somebody asking for immediate erasure, and only one of those
misreadings can be undone.

**A value too large to be subtracted from the present instant SHALL also mean no retention.**
The sweep computes its cutoff by subtracting the period from now, and a period beyond the
representable range of a date makes that computation fail rather than produce a cutoff — so a
pasted timestamp or a millisecond count would give a site a recurring error instead of a policy.
The package SHALL therefore refuse periods above a stated maximum of at least a century. Nothing
expressible is lost: a value above it is either a mistake or an attempt to say *never*, and
**omitting the setting already says never**.

**An unreadable configured value SHALL be reported at startup as an error**, distinguishably from
the setting being absent. Silence would leave a site believing retention is on while nothing
erases — and, once a privacy notice publishes the period, telling visitors something untrue.
Absence is a choice and needs no complaint; a value that was written and could not be understood
is a fault.

**Off SHALL be distinguishable from a period, in the package's own settings**, rather than
encoded as a period of zero or of some large number. A later feature that states the retention
period must be able to tell "we erase after N days" from "we do not erase", and must not be able
to accidentally publish the second as the first.

#### Scenario: No retention is configured
- **WHEN** the site configuration carries no retention period
- **THEN** no booking's personal data is erased by the package on its own initiative, however old

#### Scenario: A configured period is used
- **WHEN** the site configuration carries a retention period of a positive whole number of days
- **THEN** the package's settings report that period, and retention operates on it

#### Scenario: An unreadable period means off, not a default
- **WHEN** the site configuration carries a retention period that is blank, non-numeric, zero or negative
- **THEN** the package's settings report no retention period, no default period is substituted, and no booking's personal data is erased on the package's own initiative

#### Scenario: An unusably large period means off rather than a recurring failure
- **WHEN** the site configuration carries a retention period larger than the package's stated maximum
- **THEN** the package's settings report no retention period, an error is reported at startup, and the sweep neither erases anything nor fails when it runs

#### Scenario: An unreadable period is complained about
- **WHEN** the site configuration carries a retention period that was written but cannot be read as a positive whole number of days
- **THEN** an error is reported at startup identifying the setting

#### Scenario: An absent period is not complained about
- **WHEN** the site configuration carries no retention period at all
- **THEN** no error is reported for it

#### Scenario: Off and a period are different values
- **WHEN** the package's settings are inspected with retention off and with retention configured
- **THEN** the two are distinguishable without interpreting a numeric period as meaning off

### Requirement: Retention erases bookings whose interval ended longer ago than the period

When a retention period is configured, the package SHALL erase the personal data of every
booking whose interval **ended** more than that many days before the present instant, and whose
personal data has not been erased already.

**The clock runs from the booking's end**, not its start, its creation, or any later event. A
booking is not over until its interval is.

**Retention SHALL apply whatever the booking's status.** A booking that was cancelled, declined,
or never confirmed holds a real person's details exactly as firmly as one that went ahead. A
status filter would be a way for the sweep to under-erase, which is the failure that matters
here — the same reason a subject's search carries no status filter.

**A booking whose interval has not yet ended SHALL NOT be erased by retention**, whatever its
status and however long ago it was made. A booking cancelled well before its date therefore keeps
its booker's details until its would-be end plus the period; erasing it sooner is available to an
operator on request, and is not something the timer decides.

**Retention SHALL NOT delete any booking row**, on the same terms as every other erasure: the
booking, its reference, its interval, its status and its claims survive.

#### Scenario: A booking past the period is erased
- **WHEN** retention is configured and a booking's interval ended more days ago than the period
- **THEN** its booker's personal data is erased

#### Scenario: A booking inside the period is untouched
- **WHEN** retention is configured and a booking's interval ended fewer days ago than the period
- **THEN** its booker's personal data is unchanged

#### Scenario: A future booking is never erased by retention
- **WHEN** retention is configured and a booking's interval has not yet ended
- **THEN** its booker's personal data is unchanged, whatever the booking's status and however long ago it was created

#### Scenario: A cancelled booking is erased on the same clock as any other
- **WHEN** retention is configured and a cancelled booking's interval ended more days ago than the period
- **THEN** its booker's personal data is erased, on the same terms as a booking that went ahead

#### Scenario: Every status past the period is reached
- **WHEN** retention runs over bookings of differing statuses whose intervals all ended more days ago than the period
- **THEN** every one of them has its booker's personal data erased

#### Scenario: The booking survives retention
- **WHEN** retention erases a booking's personal data
- **THEN** the booking still exists with its reference, interval, status and resource claims unchanged

### Requirement: Retention erases through the package's single erasure operation

Retention SHALL erase by invoking the package's existing erasure operation, once per booking.
It SHALL NOT carry its own implementation of erasure, and SHALL NOT reach the stored columns by
any other route.

**One erasure path is the point.** `booker-erasure` guarantees that erasure is absorbing,
irreversible and safe to retry, and that anything erasure later grows — an audit record, a
notification — attaches in one place. A bulk erase written beside it would be a second place for
erasure to happen, and the next change to erasure would silently leave it behind: both paths
would still erase, so nothing would look broken.

**An erased booking SHALL be indistinguishable from one erased on request.** The stored state
records that erasure happened and when, and nothing more. Whether the timer or a person caused it
is not recorded.

#### Scenario: Retention uses the erasure operation
- **WHEN** the retention implementation is inspected
- **THEN** it erases by invoking the package's erasure operation and contains no other write to the booker's stored columns

#### Scenario: An automatically erased booking is in the ordinary erased state
- **WHEN** a booking is erased by retention and then read back
- **THEN** it reports an erased booker with the instant of erasure, on the same terms as a booking erased on request, and carries no record of which caused it

### Requirement: The sweep finds bookings by time alone and never handles a contact detail

The read that selects bookings for retention SHALL select on the booking's **end instant and
erasure state alone**, SHALL accept no contact detail as input, and SHALL return **booking
identifiers only** — no name, email address, phone number or member key.

This is what allows an erasure with no caller to exist without weakening the rule that only
somebody permitted to read contact details may destroy them: the unattended path never has one
to read. A sweep that selected rows carrying personal data would put that data in the hands of
the one code path with no user accountable for it.

**Retention SHALL NOT record, log or report any booker's personal data**, including in a count
that is broken down by it or in a diagnostic naming a booking's booker. What it reports about its
own work SHALL identify bookings and quantities, never people. `booker-erasure` requires that
contact details have exactly one durable home and names retention reporting as a feature that
would naturally create a second; this is that requirement honoured rather than restated.

#### Scenario: The due-read takes no contact detail and returns none
- **WHEN** the read that selects bookings for retention is inspected
- **THEN** its inputs are the cutoff instant and a batch size, and its result carries booking identifiers and no personal data

#### Scenario: Retention's diagnostics name no person
- **WHEN** retention runs and reports what it did
- **THEN** the report carries counts and booking identifiers, and no booker name, email address, phone number or member key

### Requirement: The sweep completes, repeats safely, and cannot silently do half its work

Retention SHALL erase **every** booking that is due at the time it runs, not a fixed-size sample
of them, and SHALL be safe to run repeatedly, concurrently with ordinary use of the site, and to
interrupt part-way.

**Where the sweep processes bookings in batches, each batch SHALL be drawn from the head of the
remaining due set.** It SHALL NOT page by offset. Erasing a booking removes it from the due set,
so an offset that advances past the batch just erased steps over exactly as many bookings as it
erased, leaving them un-erased while the sweep reports success. This is a silent, partial
failure: it looks identical to a completed run, and it is invisible to any test whose fixtures
fit in a single batch.

**A batch in which nothing was successfully erased SHALL end the run**, with the failure
reported. A booking that could not be erased still matches the due set and would otherwise be
selected again without end.

**A booking that cannot be erased SHALL NOT prevent the others being erased.** The sweep
continues past it and reports that it failed. One permanently unerasable row must not be able to
stop retention for every booking behind it.

**Interruption SHALL be safe.** A sweep stopped part-way — by a shutdown, a deployment, or a lost
lease — SHALL leave every booking it reached erased and every booking it did not reach due, so
that the next run completes the work with no double erasure and no gap. This follows from erasure
being absorbing rather than being separately arranged.

**In a load-balanced site, retention SHALL run on one server at a time**, and SHALL rely on the
CMS's own scheduling for that rather than on a lock of the package's own. Where two runs overlap
regardless, the outcome SHALL be the same as one run, because erasure absorbs.

#### Scenario: More bookings are due than fit in one batch
- **WHEN** retention runs with more bookings due for erasure than a single batch holds
- **THEN** every due booking has its personal data erased, not merely the first batch's worth

#### Scenario: The sweep does not page past what it erased
- **WHEN** the sweep's batching is inspected
- **THEN** each batch is drawn from the head of the remaining due set, and no offset advances past bookings the sweep has erased

#### Scenario: One unerasable booking does not stop the rest
- **WHEN** a booking in a batch cannot be erased and other due bookings remain
- **THEN** the remaining due bookings are erased and the failure is reported

#### Scenario: A wholly unsuccessful batch ends the run
- **WHEN** a batch is selected and no booking in it is successfully erased
- **THEN** the run ends and the failure is reported, rather than selecting the same bookings again

#### Scenario: An interrupted sweep resumes cleanly
- **WHEN** a sweep is interrupted after erasing some of the due bookings and retention runs again
- **THEN** the bookings it had already erased are unchanged, including their erasure instants, and the bookings it had not reached are erased

#### Scenario: Running twice over the same data changes nothing the second time
- **WHEN** retention runs to completion and then runs again with no new booking having become due
- **THEN** no booking's stored state changes, including every recorded erasure instant

### Requirement: Retention runs unattended and is not reachable from a request

Retention SHALL be performed by scheduled background work, and SHALL NOT be triggerable by any
HTTP request, management endpoint or delivery endpoint.

**No endpoint SHALL exist that erases many bookings at once.** Erasure through the API stays one
booking per request, with its authorization intact. An endpoint that ran the sweep would be a way
to destroy a site's personal data in one call, gated only by whatever policy somebody remembered
to put on it, and it would reintroduce the caller that this feature's design depends on not
having.

#### Scenario: No endpoint runs the sweep
- **WHEN** the package's management and delivery endpoints are enumerated
- **THEN** none of them performs retention or erases more than one booking

#### Scenario: Retention is scheduled work
- **WHEN** the retention implementation is inspected
- **THEN** it is registered as scheduled background work and its execution is not reachable from request handling

### Requirement: Retention's effect and its irreversibility are documented

The package's documentation SHALL state how to configure retention, that it is off unless
configured, what the period is measured from, and that what it does cannot be undone.

It SHALL state plainly that **turning retention on erases historical bookings on the first run,
not gradually.** A site enabling a ninety-day period on a database holding three years of
bookings erases almost all of them within minutes. That is the feature working correctly, and it
is the single thing a site owner most needs to know before setting the value — there is no
confirmation step to catch them, because there is nowhere to put one.

It SHALL state that retention applies to every booking whatever its status, that it never erases
a booking whose interval has not ended, and that erasing a booking sooner remains available on
request.

It SHALL state that the setting is read at startup, so a change to it takes effect when the site
restarts.

#### Scenario: A site owner can find out how to turn retention on
- **WHEN** a reader consults the documentation
- **THEN** it names the setting, states that retention is off unless configured, and states that the period is counted from the end of a booking

#### Scenario: The one-way door is documented
- **WHEN** a reader consults the documentation
- **THEN** it states that enabling retention erases every booking already past the period on the first run, and that erasure cannot be undone

#### Scenario: The boundaries are documented
- **WHEN** a reader consults the documentation
- **THEN** it states that retention applies whatever a booking's status, that it never erases a booking whose interval has not ended, and that the setting takes effect on restart
