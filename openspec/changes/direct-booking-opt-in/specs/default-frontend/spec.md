## ADDED Requirements

### Requirement: A resource not offered on its own is reported as such
The no-JavaScript booking flow, rendered for a resource that withholds permission
to be booked on its own, SHALL state that the resource is not offered for booking
by itself, and SHALL NOT report that it has no available times.

The two are different facts with different remedies, and they are indistinguishable
to a visitor once rendered the same way. "No times available" invites someone to
come back tomorrow, which will not help, and tells the site owner their opening
hours are wrong, which they are not. This is the failure mode the start-grid and
pool-sufficiency reports exist to prevent, arriving through a new door: a correctly
configured thing that yields nothing and says nothing about why.

The flow SHALL NOT offer a submission for such a resource. Rendering a form that
placement will always refuse would invite a visitor to fill it in and lose their
input to a failure that was knowable before they started.

The statement SHALL meet the same accessibility baseline as the rest of the flow —
semantic markup, no meaning carried by colour alone, and announced as the page's
other outcome messages are.

The flow for a resource that does permit direct booking SHALL be entirely unchanged,
including its length choice, its anti-forgery protection, its Post-Redirect-Get
confirmation, and its failure handling with input preservation.

#### Scenario: A withholding resource explains itself
- **WHEN** the booking flow is rendered for a resource that withholds direct booking
- **THEN** it states that the resource is not offered for booking on its own, and does not state that no times are available

#### Scenario: No form is offered
- **WHEN** the booking flow is rendered for a resource that withholds direct booking
- **THEN** no booking submission is presented

#### Scenario: A genuinely empty calendar still says so
- **WHEN** the booking flow is rendered for a resource that permits direct booking but has no bookable times in range
- **THEN** it reports that there are no available times, as it did before

#### Scenario: The permitted flow is unchanged
- **WHEN** the booking flow is rendered and submitted for a resource that permits direct booking
- **THEN** it behaves exactly as it did before this change, through to the redirect-and-confirm

#### Scenario: The statement is accessible
- **WHEN** the statement is rendered
- **THEN** it is semantic text, not conveyed by colour alone, and announced on the same terms as the flow's other outcome messages
