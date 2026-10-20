## ADDED Requirements

### Requirement: The public service read model does not carry the email template set

The delivery API's service read model, from `GET /services` and `GET /services/{id}`, SHALL NOT
carry a service's email template set, under any name or in any nested member.

The set is site configuration that chooses email content. It has no meaning to a visitor or to a
headless front end, and the read model is a public contract that every published member extends.
The check SHALL be made against the serialised response, not only against the model type.

#### Scenario: A service with a template set is read publicly
- **WHEN** a service whose template set is a distinctive name is read through `GET /services/{id}` and through `GET /services`
- **THEN** neither response contains that name anywhere in its body

#### Scenario: The read model publishes what it did before
- **WHEN** a service with a template set is read through the delivery API
- **THEN** the response carries the same members, with the same values, as for the same service without one
