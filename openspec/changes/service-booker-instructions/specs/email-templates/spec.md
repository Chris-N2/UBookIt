## ADDED Requirements

### Requirement: The booker's model carries the service's instructions on the same terms as the message

The booker's model SHALL gain the service's instructions for the booker, as text, on exactly the
terms the `booking-emails` capability sets for the package's own wording. The model SHALL carry
the instructions where the booking is confirmed when the message is composed and its service has
them, and SHALL NOT carry them otherwise.

**Frozen means additive.** The member SHALL be absent wherever the message does not carry the
instructions, and its absence SHALL mean exactly that. Every existing member SHALL keep its name,
type and meaning, and a view written before the member existed SHALL render unchanged.

The instructions SHALL be supplied as the text the site wrote, already normalised, with line breaks
preserved. They SHALL NOT be pre-formatted for a medium. Presenting them is the view's, and Razor
encodes them in an HTML view like any other value.

The model for the site's own recipients SHALL NOT gain the instructions.

#### Scenario: A view is given the instructions for a confirmed booking
- **WHEN** a view renders the booker's message for a confirmed booking whose service has instructions
- **THEN** the instructions are available on the model as the text the site wrote, line breaks included

#### Scenario: The member is absent where the message carries none
- **WHEN** a view renders the booker's message for a booking awaiting approval, a declined or cancelled booking, or a booking whose service has no instructions
- **THEN** the instructions member is absent, and a view written before it existed renders exactly as before

#### Scenario: The internal model does not gain them
- **WHEN** the model for the site's own recipients is inspected
- **THEN** it has no member for the instructions
