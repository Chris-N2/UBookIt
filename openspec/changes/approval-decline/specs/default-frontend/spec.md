# default-frontend — delta for approval-decline

## ADDED Requirements

### Requirement: The confirmation page's wording follows the booking's status
The confirmation page each shipped flow renders after a successful placement SHALL derive
what it says about the booking's state from that state, and SHALL NOT be written on the
assumption that a placed booking is in any particular one. A booking placed under
`AutoConfirm` off is `Requested`, and a page telling that visitor "your booking is
confirmed" would be false at the moment it renders — the same falsehood the
`booking-emails` capability's message wording was built to make impossible, on the page
instead of in the inbox.

This SHALL govern every statement of state the page makes: the heading, the region's
accessible name, and the lead sentence SHALL all describe a requested booking as received
and awaiting the site's confirmation, and SHALL describe a confirmed booking as confirmed.
Everything else the page shows — the quotable reference, the interval, what was booked, the
booker's details — SHALL be identical in both states: the reference is *more* important to
a person whose booking is pending, not less, because it is what they will quote when they
chase it.

Both shipped flows SHALL behave this way — the single-resource confirmation and the service
confirmation — and their view models SHALL carry the placed booking's state so the views
can. The addition to those models SHALL be additive, since they are part of the published
theme contract; where a theme supplies a confirmation view, what it does with the state is
the theme author's, per this capability's existing narrowing, and the package claims nothing
about it in either direction.

#### Scenario: A confirmed placement reads as confirmed
- **WHEN** a visitor completes either shipped flow on a site whose `AutoConfirm` setting is on
- **THEN** the confirmation page's heading, accessible name and lead sentence describe the booking as confirmed

#### Scenario: A requested placement reads as received, not confirmed
- **WHEN** a visitor completes either shipped flow on a site whose `AutoConfirm` setting is off
- **THEN** the page's heading, accessible name and lead sentence describe the booking as received and awaiting confirmation, and no text on the page states the booking is confirmed

#### Scenario: The pending page still carries everything the visitor needs
- **WHEN** the confirmation page renders for a requested booking
- **THEN** it shows the quotable reference, the interval, what was booked and the booker's details, exactly as it would for a confirmed one

#### Scenario: The theme contract addition is additive
- **WHEN** the confirmation view models' public shape is compared with the shape before this change
- **THEN** every member that existed is unchanged in name and type, and the state is a new member beside them
