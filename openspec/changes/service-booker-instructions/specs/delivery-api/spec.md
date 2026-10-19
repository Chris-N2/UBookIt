## ADDED Requirements

### Requirement: The public service read model never carries the instructions for the booker

The delivery API's service read model, from `GET /services` and `GET /services/{id}`, SHALL NOT
carry a service's instructions for the booker, under any name or in any nested member.

The instructions are written for people who have a confirmed booking, and may include a standing
link to join a meeting. Publishing them on an anonymous endpoint would give them to anyone who
asked. The check SHALL be made against the serialised response, not only against the model type.
A member added to the read model later under another name is the failure this rules out.

#### Scenario: A service with instructions is read publicly
- **WHEN** a service with instructions is read through `GET /services/{id}` and through `GET /services`
- **THEN** neither response contains the instructions' text anywhere in its body

#### Scenario: The read model publishes what it did before
- **WHEN** a service with instructions is read through the delivery API
- **THEN** the response carries the same members, with the same values, as for the same service without instructions
