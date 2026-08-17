## MODIFIED Requirements

### Requirement: Direct-resource booking is unaffected
Introducing booking via a service SHALL NOT change how a resource is booked directly. The existing per-resource availability queries, the existing placement pipeline, and the existing placement endpoint SHALL behave identically whether or not any service exists, and whether or not a resource happens to be in some service's candidate pool. A booking placed through a service SHALL be an ordinary booking, indistinguishable in shape from a directly placed one, and SHALL block the resolved resource for other bookings on the same terms.

What a resource **permits** is a separate question from what services do to it. Direct
booking is now offered only for a resource whose editor has said it may be booked on
its own, and that gate is unrelated to services: it applies identically whether the
resource belongs to a service's pool or to none, and it is the editor's answer rather
than any consequence of a service existing. The guarantee above is therefore unchanged
in substance — services still change nothing about direct booking — and the scenarios
below are stated for a resource that permits it, because a resource that does not is
answering a different question.

Membership of a service's candidate pool SHALL remain independent of the permission in
both directions: a resource that withholds it is still resolved, still contributes to
composite availability, and is still claimed by a service booking.

#### Scenario: Direct placement is unchanged by services
- **WHEN** a resource that permits direct booking, and that belongs to a service's candidate pool, is booked directly at a valid interval
- **THEN** placement succeeds exactly as it would if no service existed

#### Scenario: A service booking blocks direct booking
- **WHEN** a service booking resolves to a resource that permits direct booking, and a direct booking is then attempted for an overlapping interval on that resource
- **THEN** the direct booking fails with `conflict`

#### Scenario: A direct booking removes a candidate
- **WHEN** a resource in a candidate pool is booked directly and service availability is then queried over that interval
- **THEN** that resource contributes no runs for the affected starts

#### Scenario: Withholding direct booking does not remove a candidate
- **WHEN** a resource that withholds direct booking is in a service's candidate pool
- **THEN** it is resolved, contributes availability, and can be claimed by a service booking exactly as a permitting resource would
