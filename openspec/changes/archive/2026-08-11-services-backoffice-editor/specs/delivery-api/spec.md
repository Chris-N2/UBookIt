## ADDED Requirements

### Requirement: Delivery problem-details responses carry a type member
Problem-details responses from the delivery API SHALL populate the RFC 7807 `type` member alongside `title`, `status`, and the `errors` extension, for both domain failures and model-binding failures. This keeps the delivery envelope identical in shape to the management one, which requires the member because the backoffice client discards bodies without it. The addition is additive — the member was previously absent — and changes no status code, no failure code, and no existing member.

#### Scenario: A delivery failure response is typed
- **WHEN** the delivery API rejects a request with a validation, not-found, or conflict failure
- **THEN** the response carries a non-empty `type` member, and its status, `title`, and `errors` entries are exactly as before

#### Scenario: Transport failures are typed too
- **WHEN** a request fails model binding and is projected into the shared problem envelope
- **THEN** that response also carries a non-empty `type` member alongside its `invalid-request` error entries
