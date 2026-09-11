# Delta for email-templates

Found by the sync-time outward sweep (task 7.1): the phrase "a configured recipient
list" named the internal audience before responsibility widened it. One term changes;
every guarantee is byte-identical otherwise.

## MODIFIED Requirements

### Requirement: The model for the site's own recipients cannot express a booker's contact details
The model passed to a view for the **site's own recipients** SHALL have **no member carrying the
booker's name, email address or telephone number**.

**This makes an existing guarantee structural instead of advisory.** The package already promises
that a message to the site's own recipients carries no booker contact details, because such a
list is not the population the **Sensitive data** control governs. If one model served both
audiences with the booker merely absent or null, that promise would silently weaken from *what
the package does* to *what the package does unless a site writes a view* — and nothing would
report the difference.

A site that genuinely wants contact details in its internal mail SHALL still be able to send such
a message **itself**, through the booking notifications. What it SHALL NOT be able to do is have
the package send one.

#### Scenario: The internal model offers nothing to leak
- **WHEN** the model for the site's own recipients is inspected
- **THEN** it exposes no booker name, no email address and no telephone number

#### Scenario: A view for the site's own recipients cannot render contact details
- **WHEN** an author attempts to render a booker's contact details in a view for the site's own recipients
- **THEN** there is no member to render, and the attempt fails before the view can be used rather than at a recipient's inbox

#### Scenario: The booker's own message is unaffected
- **WHEN** a view for the booker renders
- **THEN** the booker's own contact details are available to it, as they are in the package's own content
