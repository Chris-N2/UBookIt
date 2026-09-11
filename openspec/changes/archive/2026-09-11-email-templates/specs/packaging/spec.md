# packaging — delta for email-templates

## ADDED Requirements

### Requirement: How a site supplies its own message content is documented
The package's documentation SHALL state that a site can supply the content of the messages the
package sends, and SHALL give an author everything needed to do it: **the complete set of message
names**, **where a view goes**, **what each view receives**, and **what a view may state about the
message**.

**An extension point nobody is told about is not one** — the same reasoning that already requires
the notifications to be documented. This one is more acute, because the alternative to finding it
is worse than going without: a site that wants different wording and cannot find this will take
over sending instead, and thereby inherit the sending conjunction, the erased-booker rule and the
personal-data rules that the package is tested for and their own handler will not be.

**The documentation SHALL state the limit of the medium**: that a message carries either HTML or
plain text and never both, so supplying HTML content means sending no plain-text alternative, and
that this follows from the mail abstraction the package sends through rather than from a choice
this package made.

**It SHALL state which of the package's promises stop applying to supplied content and which do
not.** An author needs to know that the words become theirs — including whether the message
correctly describes the booking's state — while the gating, the audiences, the erased-booker rule
and the exclusion of booker contact details from messages to a site's own recipients continue to
hold regardless of what they write.

#### Scenario: An author can supply content from the documentation alone
- **WHEN** an author reads the documentation
- **THEN** they can find every message name, where to put a view, what the view receives, and how to state a subject and a content type, without reading the package's source

#### Scenario: The single-body limit is stated
- **WHEN** an author reads how to supply HTML content
- **THEN** the documentation states that no plain-text alternative is sent with it, and why

#### Scenario: What narrows and what does not is stated
- **WHEN** an author reads what supplying content makes them responsible for
- **THEN** the documentation distinguishes the wording, which becomes theirs, from the gating, audiences, erased-booker rule and internal-message exclusion, which do not
