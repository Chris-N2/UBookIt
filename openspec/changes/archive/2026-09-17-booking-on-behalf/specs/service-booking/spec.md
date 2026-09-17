## ADDED Requirements

### Requirement: Placing a booking on a booker's behalf is the service booking service's entry point
The service booking service SHALL expose a placement **on a booker's behalf** that is the
operator's single entry point for placing any booking, on exactly the terms its move already
is. Given a service, a start, a requested length and the booker's details it SHALL resolve the
service's roles to an assignment and place a booking for it; given a resource rather than a
service it SHALL delegate to the booking service's operator placement unchanged.

**The requested length SHALL be validated against the service's rules exactly as a visitor's
service placement validates one** — the intersection of the service's duration specification
with each candidate resource's own range, with the same stable codes `duration-too-short` and
`duration-too-long`, and with the same refusal to substitute a permitted length for the one
asked for. A service's length rules are a property of what the business offers, not of who is
asking for it, and an operator who could book a 30-minute appointment for a service sold in
45-minute units would be recording something the business does not sell.

**This SHALL be the reason the entry point exists.** `IBookingService` knows resources and
knows nothing of services, so an operator placement routed through it alone would apply the
resources' bounds and silently not the service's. The same split, and the same remedy, as
*Moving a booking placed for a service applies the service's length rules*.

**Role resolution, eligibility, assignment and the pinning rule SHALL be unchanged.** An
operator's placement resolves candidates by resource type and capability subset exactly as a
visitor's does, refuses a pinned resource that appears in no pool on the same terms, and
reports an unfulfillable service with the same classification of failures. Who is placing
changes the placement terms and nothing about what the service *is*.

**Operator terms SHALL reach every part of the placement that evaluates a rule — not only the
attempt.** Lead time and horizon are evaluated by the booking service's pipeline, which this
entry point places through; nothing in resolution, candidate shortlisting or the advisory
free-claims read applies either rule.

**They SHALL also reach the refusal.** When an attempt fails, service placement asks each
candidate's own rules which candidates to condemn and what to report; a rule the operator is not
subject to SHALL NOT take part in that answer. Otherwise a service whose only candidate is
merely **busy** is refused as though the service could not be booked at that time at all — the
operator is told not to retry, on the strength of a waived rule, when another time would work.

**A booking placed this way SHALL record the service that produced it**, on the same terms as
any service placement: the attribution is the snapshot taken when the booking was placed.

**BREAKING — published port, declared.** `IServiceBookingService` gains the on-behalf placement
member. A host implementing it must add one; it lands in a minor release, as its move member
and the store and observer additions do.

#### Scenario: An operator places a service booking for a booker
- **WHEN** an operator places a booking on a booker's behalf for a service, at a start and length its candidates admit
- **THEN** a booking is placed, claiming one resource per role, recording the service it was placed for

#### Scenario: A service's length rules bind an operator
- **WHEN** an operator places a booking on a booker's behalf for a service permitting 45–120 minutes, asking for 30 minutes
- **THEN** it fails with `duration-too-short`, and nothing is persisted

#### Scenario: A claimed resource's ceiling still binds
- **WHEN** an operator places a booking on a booker's behalf for a service permitting up to 240 minutes whose only candidate has a maximum of 90, asking for 120 minutes
- **THEN** it fails with `duration-too-long`, and nothing is persisted

#### Scenario: A direct resource is delegated untouched
- **WHEN** an operator places a booking on a booker's behalf naming a resource rather than a service
- **THEN** the outcome is exactly the booking service's operator placement, for the same arguments

#### Scenario: Lead time is waived for a service placement too
- **WHEN** an operator places a booking on a booker's behalf for a service whose resources require 24 hours' notice, one hour from now
- **THEN** placement succeeds, because the pipeline evaluates lead time as zero for an operator

#### Scenario: A busy candidate is reported as a conflict, not as an unbookable service
- **WHEN** an operator places a booking on a booker's behalf for a service whose only candidate is already booked at that interval, at a time inside that resource's configured lead time
- **THEN** the refusal is `conflict` — the resource is taken — and not `service-unavailable`, because the lead time does not bind an operator and so cannot contribute to the explanation

#### Scenario: An unfulfillable service is reported as it always was
- **WHEN** an operator places a booking on a booker's behalf for a service no resource can currently fulfil
- **THEN** the failure is the one a visitor's placement reports for the same service, rather than a new code

#### Scenario: A visitor's service placement is unchanged
- **WHEN** a visitor places a booking for a service on an installation where operator placement is available
- **THEN** every rule, code and assignment behaviour is exactly what it was before, including lead time and horizon
