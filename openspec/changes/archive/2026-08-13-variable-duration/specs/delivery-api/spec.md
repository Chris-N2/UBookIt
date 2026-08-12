## ADDED Requirements

### Requirement: Bookable-start read
The delivery API SHALL expose a bookable-start query for a resource over an inclusive `[from, to]` date range, delegating to the Core availability query service. Each returned entry SHALL carry the start instant as ISO-8601 UTC together with the shortest and longest bookable length from that start, expressed as whole minutes. The response SHALL carry the site time-zone id once at the top level, consistent with the existing free-time and slot responses.

The query SHALL NOT require a requested duration — answering "how long can I book from here" is its purpose. The existing slot query SHALL remain unchanged and SHALL continue to require a duration; the two SHALL be separate endpoints rather than one endpoint whose response shape varies with the presence of a parameter.

A range wider than the configured maximum SHALL yield the `date-range-too-large` failure; a `from` after `to` SHALL yield `date-range-invalid`; an unknown resource SHALL yield `resource-not-found`. Failures SHALL be rendered as problem details on the same terms as every other delivery endpoint. The endpoint SHALL be anonymous, consistent with the delivery API's auth stance.

#### Scenario: Bookable-start read
- **WHEN** bookable starts are requested for a resource over a valid date range
- **THEN** the response carries an ordered list of entries, each with an ISO-8601 UTC start instant and its minimum and maximum bookable lengths in whole minutes, plus the site zone id

#### Scenario: Any length is answerable from one response
- **WHEN** a client filters a bookable-start response to entries whose minimum is at most 90 minutes and whose maximum is at least 90 minutes
- **THEN** the resulting start instants are exactly those the slot endpoint returns for a 90-minute duration over the same resource and range

#### Scenario: Over-wide range is rejected
- **WHEN** a bookable-start query requests a range wider than the configured maximum
- **THEN** the response is 400 problem details carrying the `date-range-too-large` code

#### Scenario: Inverted range is rejected
- **WHEN** a bookable-start query is requested with `from` after `to`
- **THEN** the response is 400 problem details carrying the `date-range-invalid` code

#### Scenario: Unknown resource is rejected
- **WHEN** a bookable-start query names a resource id that does not exist
- **THEN** the response is 404 problem details carrying the `resource-not-found` code

#### Scenario: Anonymous access is permitted
- **WHEN** a bookable-start query is made with no credentials
- **THEN** the request succeeds, consistent with the rest of the delivery API
