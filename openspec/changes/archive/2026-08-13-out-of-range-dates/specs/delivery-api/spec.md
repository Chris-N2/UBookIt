## ADDED Requirements

### Requirement: Boundary inputs fail as validation, never as an exception
No delivery endpoint SHALL answer with an unhandled exception for any date, instant, or duration a caller can express in the request's own types. An input at or near the limit of what a date or instant can represent SHALL be rejected by the domain as a structured failure and rendered as problem details on the same terms as any other validation failure — `date-range-invalid` for a query range that cannot be walked, `interval-invalid` for a placement interval or open-hours window that cannot be represented. Both are already 400 under the failure mapping, so no new code and no new status is introduced.

This SHALL hold for every endpoint that accepts a date, an instant, or a duration, on both the direct-resource and via-service paths, and SHALL be verified against the running site rather than only in unit tests: the failure mode being corrected is an unhandled exception, which is exactly the class that in-process tests can miss and a live request cannot.

#### Scenario: Availability queries at the calendar's end
- **WHEN** free-time, slots, bookable-starts, or service bookable-starts are requested with `from` and `to` both set to the last representable date
- **THEN** each responds 400 problem details carrying `date-range-invalid`, and none responds 500

#### Scenario: Placement at the representable limits
- **WHEN** a booking is placed, on either the direct or the service route, with a start at the last representable date, a start at the first representable date, or a duration large and negative enough to underflow
- **THEN** each responds 400 problem details carrying `interval-invalid`, and none responds 500

#### Scenario: Ordinary requests are unchanged
- **WHEN** any availability query or placement is made with everyday dates and durations
- **THEN** its status, body, and failure codes are exactly as before this change
