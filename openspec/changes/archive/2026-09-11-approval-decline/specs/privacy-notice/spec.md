# privacy-notice — delta for approval-decline

## MODIFIED Requirements

### Requirement: The package states only what it can keep true, and the site links its own policy

The package SHALL author only those statements that are **facts about what it does**, and SHALL
NOT author, host, render or validate a site's privacy policy.

A site SHALL be able to supply a link to its own privacy policy through configuration, and the
notice SHALL present that link when one is configured and SHALL render correctly without one.

**The division is by who can know the answer.** What is collected, why, how long and who sees it
are properties of code in this package; jurisdiction, the identity of the data controller, other
processing a site performs and how to complain are not, and a package that guessed at them would
be putting words a site never wrote onto its public pages.

**A configured link that cannot be used SHALL be treated as absent**, and reported, rather than
rendered as a broken link on a public page. The notice renders without a link in that case, on
the same terms as a site that configured none.

**What the notice says about being contacted SHALL be true of the site rendering it.** The
package now sends messages on sites that configure it to and sends nothing on sites that do not,
so neither silence nor a promise is correct everywhere:

- Where a message **will** be sent to the booker, the notice SHALL say so. Withholding it would
  understate the processing being performed on the very page collecting the address.
- Where a message **will not** be sent to the booker, the notice SHALL NOT state that one will,
  and SHALL state only that the site is **able** to make contact. A notice promising a message
  on a site that sends none would assert processing the package does not perform.

**What it promises SHALL be that messages about the booking are sent, and SHALL NOT name a
confirmation specifically.** A form's statement is made before placement, and what the booker
subsequently receives depends on the site's `AutoConfirm` setting and on the operator: a site
requiring approval sends *"we have received your booking"* at placement, and a booking that is
declined is never confirmed to anybody. A notice naming a confirmation is therefore a promise the
package cannot keep on every site that renders it — which is the same fault as promising one
where nothing is sent, arrived at by a different route.

*This is a narrowing of what the sentence asserts, not of when it appears: the notice still
speaks wherever a message will be sent, and still says nothing wherever none will. Only the noun
changes, from an outcome the package cannot guarantee to the processing it actually performs.*

**The condition governing the sentence SHALL be the condition governing the sending**, and not
merely the setting that requests it. A site that has asked for messages on a host that cannot send
mail sends nothing, and its notice SHALL say nothing about messages — so the sentence and the
behaviour cannot diverge, because they are decided by the same thing.

**That condition SHALL remain a single one.** Qualifying the sentence by `AutoConfirm` as well
would give it two conditions and therefore a second way to drift from the behaviour, and would
still not cover a declined booking. A statement true under every combination of the settings is
what keeps this requirement's guarantee cheap to hold.

**This binds every surface of the same form that mentions contact**, not the notice alone. The
email field's own hint states why the address is wanted, and it SHALL be governed by this
requirement on the same terms as the notice.

*Recorded as a requirement rather than left to the wording, because it is a mistake the package
had already made and would make again: the form's email field carried the hint "We'll send your
booking confirmation here" from long before anything could send one. That hint was untrue when it
was written. What has changed is that it is now untrue only on some sites, which is harder to
notice and no less wrong.*

*And it made the same mistake a third time, which is why the paragraph above it exists. The
`approval-decline` change made `AutoConfirm` reachable without revisiting either sentence, and its
own proposal asserted — wrongly — that "the form today promises nothing about confirmation
timing". Both surfaces promised a confirmation, on a site that might send a request
acknowledgement or a decline instead. Caught in QA, not by the change's own sweep.*

**The notice is not a privacy policy and the documentation SHALL say so** — it is a factual
statement about one package's handling of the data one form collects, and a site that needs a
policy still needs a policy.

#### Scenario: A configured policy link is presented
- **WHEN** a privacy policy link is configured and the notice renders
- **THEN** the notice presents it as a link to the site's own privacy policy

#### Scenario: The notice renders without a link
- **WHEN** no privacy policy link is configured and the notice renders
- **THEN** the notice renders its four statements and presents no link and no empty or placeholder link

#### Scenario: An unusable link is treated as absent
- **WHEN** a configured privacy policy link cannot be used as a link
- **THEN** the notice renders as though none were configured, and the unusable value is reported

#### Scenario: The package writes no policy of its own
- **WHEN** the notice's statements are inspected
- **THEN** each is a statement about what this package collects, why, for how long, or who can see it, and none asserts a lawful basis, a controller identity, a jurisdiction, or any processing the package does not perform

#### Scenario: No message is promised where none is sent
- **WHEN** the notice renders on a site where no message will be sent to the booker
- **THEN** it states no message that will be sent, and any surface of the same form that mentions contact states only that the site is able to make contact

#### Scenario: The message is stated where one is sent
- **WHEN** the notice renders on a site where a message will be sent to the booker
- **THEN** it states that messages about the booking will be sent to that address, and does not state that the booking will be confirmed

#### Scenario: The promise holds when the site requires approval
- **WHEN** the notice renders on a site where a message will be sent to the booker and `AutoConfirm` is off, so placement produces a requested booking
- **THEN** what the notice and the email field's hint say is unchanged from the same site with `AutoConfirm` on, and neither states that the booking will be confirmed

#### Scenario: Requesting messages the host cannot send promises nothing
- **WHEN** the notice renders on a site that has asked for messages to the booker while the host is unable to send mail
- **THEN** it states no message that will be sent, on the same terms as a site that asked for none

#### Scenario: Internal recipients do not change what the booker is told
- **WHEN** the notice renders on a site that notifies its own people but sends nothing to the booker
- **THEN** it states no message that will be sent to the booker
