## ADDED Requirements

### Requirement: A resource states whether it may be booked on its own
A resource SHALL carry whether it may be booked **on its own**, independently of
its type, its capabilities and its availability. The value SHALL default to
**withheld**: a resource that has not been given the permission does not have it.

This is a statement about what the business offers, not about what the system can
compute. A resource may be perfectly available, perfectly eligible and still
meaningless alone — a therapist with no room to work in — and no rule over type,
capability or calendar can distinguish that case from a room that is genuinely
lettable. Only the editor knows, so only the editor may say.

The permission SHALL constrain **direct** booking alone. A resource that withholds
it SHALL remain fully usable as part of a service: it resolves into candidate
pools, contributes to composite availability, and is claimed by a service booking
exactly as before. Withholding it makes a resource unbookable *by itself*, never
unbookable.

The permission SHALL NOT participate in eligibility. Candidate resolution is type,
then required capabilities, then a duration the service permits; adding a fourth
term would make a service's pool depend on whether its members happen to be
separately lettable, which is unrelated to whether they can fulfil the service.

The permission SHALL NOT be a validation rule. No configuration becomes invalid by
withholding it and none becomes valid by granting it, so nothing SHALL be rejected
on its account at save time.

#### Scenario: A new resource withholds the permission
- **WHEN** a resource is created without stating whether it may be booked on its own
- **THEN** it does not permit direct booking

#### Scenario: The permission is independent of availability and capability
- **WHEN** a resource that permits direct booking has its opening hours, capabilities or constraints changed
- **THEN** it still permits direct booking, because the permission describes what is offered rather than what is possible

#### Scenario: A withholding resource is still a service candidate
- **WHEN** a service role resolves over resources of a type, one of which withholds direct booking
- **THEN** that resource appears in the candidate pool exactly as it would have done, and the pool is unchanged by the permission

#### Scenario: Withholding is not a validation failure
- **WHEN** a resource withholding the permission is created or updated
- **THEN** the operation succeeds, because the permission is an offer rather than a rule
