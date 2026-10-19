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

## MODIFIED Requirements

### Requirement: A site can supply the content of a message without owning its delivery
The package SHALL allow a site to replace the **body** of any message it sends with content the
site supplies, while the package continues to decide **whether** to send, **to whom**, and
**under what configuration**.

**This is the gap the existing seam leaves.** A site can already intercept Umbraco's outgoing-mail
notification, but the message it receives there is an immutable copy and the only available action
is to take over sending entirely — so a site wanting different wording inherits delivery, retries,
the sending conjunction and the personal-data rules along with it. Content and delivery SHALL
therefore be separable.

**A site that supplies nothing SHALL be unaffected by this capability.** With no content supplied,
every message the package sends SHALL be exactly the package's own message for that event — the same
wording, the same recipients and the same conditions as if this capability did not exist. What the
package's own wording says is governed by the capabilities that define each message, and may grow
with them: a cancellation link, or a service's instructions for a confirmed booker, appears in the
package's own message because those capabilities say so, not because this one changed it.

#### Scenario: Content is replaced without delivery being replaced
- **WHEN** a site supplies content for a message and a booking event occurs that would send it
- **THEN** the message sent carries the site's content, and the package still decides whether to send it, to whom, and under the same configuration as before

#### Scenario: A site that supplies nothing is unchanged
- **WHEN** no content is supplied for any message
- **THEN** every message the package sends is identical to the package's own message for that event, as it would be if this capability did not exist
