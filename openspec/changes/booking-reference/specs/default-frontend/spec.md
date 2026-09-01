## MODIFIED Requirements

### Requirement: Post-Redirect-Get confirmation
A successful placement SHALL respond with a redirect (HTTP 303) to a confirmation page, not by
rendering the POST result directly. The confirmation SHALL show the booking's **reference** —
the identifier a person can quote, not its machine identifier — and the booked resource, time,
and booker contact details. Reloading or refreshing the confirmation page SHALL NOT create
another booking.

**The value shown SHALL be usable by the person reading it.** A confirmation that labels a
field "Reference" and prints something nobody can read aloud, write down or type back has
labelled it accurately and filled it uselessly. This requirement previously said the reference
*was* the booking's id; that is what changed.

#### Scenario: Success redirects to confirmation
- **WHEN** a booking is successfully placed
- **THEN** the response is a 303 redirect to a confirmation page showing the booking's quotable reference and details

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful booking
- **THEN** no further booking is created

#### Scenario: The confirmation shows a reference a person can use
- **WHEN** a visitor reads the reference on their confirmation
- **THEN** it is the booking's quotable reference, which they could dictate over a telephone or type into a search, rather than the identifier machines use
