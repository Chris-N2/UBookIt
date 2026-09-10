## ADDED Requirements

### Requirement: Retention period read

The delivery API SHALL expose the site's configured **retention period** over a read under its
versioned public route. The response SHALL carry the period in whole days where one is configured,
and SHALL distinguish *no retention period is configured* from any numeric value — it SHALL NOT
report the absence of a period as `0` or as any other number.

**It publishes the number, and no prose.** A consumer building its own booking UI writes its own
privacy wording in its own language; the package's English sentences would be of no use to it and
would make untranslatable prose part of a published contract. What such a consumer **cannot**
obtain by any other means is how long uBookIt keeps the data it is about to collect — and without
that it cannot state the period accurately, which is the whole reason the notice was sequenced
after retention. Publishing the number closes that gap and nothing else.

**It is a site-wide fact and SHALL be read as one**, rather than carried on the resource or
service read models. Retention is not a property of a resource; repeating one site-wide value on
every row of a paged read would invite a consumer to believe it varies by resource, and would
duplicate a value with one source.

**Anonymous, on the same terms as every other delivery endpoint, and that is not a disclosure.**
A retention period is a policy a site publishes to its visitors deliberately — the shipped Razor
front end already prints it on a public page. It says nothing about any person and identifies no
booking.

#### Scenario: A configured period is published
- **WHEN** a retention period is configured and a consumer reads it from the delivery API
- **THEN** the response carries that period in whole days

#### Scenario: No configured period is distinguishable from a period
- **WHEN** no retention period is configured and a consumer reads it
- **THEN** the response reports that none is set, in a form that cannot be read as a numeric period

#### Scenario: The read carries no prose
- **WHEN** the response is inspected
- **THEN** it carries the period and no rendered notice, sentence or markup

#### Scenario: The read is anonymous
- **WHEN** an unauthenticated consumer reads the retention period
- **THEN** the request is served without an authentication challenge

#### Scenario: Retention does not appear on the resource or service reads
- **WHEN** the resource and service read models are inspected
- **THEN** neither carries a retention period
