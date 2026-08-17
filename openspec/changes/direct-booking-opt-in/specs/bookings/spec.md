## ADDED Requirements

### Requirement: Booking a single resource requires that resource to permit it
Placement of a booking that claims **one resource, named by the caller** SHALL be
refused when that resource withholds permission to be booked on its own, with the
stable code `resource-not-directly-bookable`.

The refusal SHALL be evaluated in `UBookIt.Core`, on the single-resource placement
entry point, **before** the request is composed into a claim set. Placing it in a
transport layer would leave every other caller open — the no-JavaScript booking
flow reaches placement in process without crossing an HTTP boundary at all — and
the rule is a property of booking, not of any one way of asking for one.

Placement that claims resources **derived from a service's roles** SHALL NOT be
subject to this rule, whatever those resources permit. The distinction SHALL be
structural rather than a flag on the request: the single-resource entry point *is*
the direct path, and service placement composes its own claim set without passing
through it. A parameter asserting "this is a direct booking" would restate what the
call site already means, in a form a caller can get wrong.

A service of one role and a count of one therefore books a resource that withholds
the permission, and SHALL succeed. That is not a loophole: the business has offered
that service, and the resource is being booked as part of it.

The refusal SHALL be reported as **its own** cause, distinct from unavailability.
It does not vary with the instant asked for, the length, the calendar or how busy
the resource is, so reporting it as no-availability would send a booker to try
another time that cannot help, and an editor to inspect opening hours that are not
wrong.

The refusal SHALL NOT be treated as a deterministic per-candidate refusal in
service placement's all-fail classification, because service placement never
reaches it.

#### Scenario: A resource withholding the permission cannot be booked alone
- **WHEN** a booking is placed naming a single resource that withholds direct booking, at a time it is otherwise free and open
- **THEN** placement fails with the stable code `resource-not-directly-bookable`, and no claim is persisted

#### Scenario: The same resource is bookable as part of a service
- **WHEN** the same resource, at the same instant, is claimed by a service booking whose role resolves to it
- **THEN** placement succeeds

#### Scenario: A single-role service of count one is still a service
- **WHEN** a service with one role of count 1 resolves to a resource that withholds direct booking, and is placed
- **THEN** placement succeeds, because the resource is being booked as part of a service rather than on its own

#### Scenario: A resource granting the permission is unaffected
- **WHEN** a booking is placed naming a single resource that permits direct booking
- **THEN** placement proceeds through the existing validation pipeline exactly as before

#### Scenario: The refusal precedes the rule pipeline
- **WHEN** a booking is placed naming a resource that withholds direct booking, for an interval that would also fail on open hours
- **THEN** the result carries `resource-not-directly-bookable` rather than `outside-open-hours`, because the request was never one this resource accepts

#### Scenario: The refusal is not a conflict and invites no retry
- **WHEN** a booking naming a withholding resource is refused
- **THEN** the code is `resource-not-directly-bookable`, never `conflict`, and the same request will be refused identically however many times it is retried
