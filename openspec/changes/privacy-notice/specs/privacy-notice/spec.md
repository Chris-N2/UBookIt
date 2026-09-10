## ADDED Requirements

### Requirement: The booking form states what happens to the details it collects

Every booking flow the package ships SHALL present a privacy notice **at the point where contact
details are collected**, and SHALL state all four of:

- **what** personal data is collected — the booker's name, email address and, where given, phone
  number;
- **why** it is collected — to hold, identify and confirm the booking;
- **how long** it is kept, per the requirement below;
- **who can see it** — that a booker's contact details are shown only to backoffice users
  Umbraco permits to see sensitive data, per the `sensitive-data` capability.

**At the point of collection, not on some other page.** A notice a visitor must go looking for is
not a notice. It SHALL appear in the same form that asks for the details, ahead of the control
that submits them, so that it is read before the data is given rather than after.

**The notice SHALL NOT gate submission.** It is a statement, not a consent mechanism: there is
nothing to tick, nothing to agree to, and a booking SHALL complete exactly as it did before. The
lawful basis for holding a booker's details is performance of the booking, and an unrefusable
tickbox would misrepresent that as consent — which would also imply a right to withdraw it and
make the site's own records revocable.

#### Scenario: The notice appears where the details are asked for
- **WHEN** a visitor reaches a step that asks for contact details, in any flow the package ships
- **THEN** the privacy notice is present in that same form, ahead of the control that submits it

#### Scenario: All four statements are made
- **WHEN** the notice renders
- **THEN** it states what is collected, why, how long it is kept, and who can see it

#### Scenario: Booking is unaffected by the notice
- **WHEN** a visitor completes a booking
- **THEN** it succeeds without the visitor having agreed to, ticked, or dismissed anything, and the notice offers no such control

### Requirement: The retention statement is derived from the configured period

What the notice says about how long data is kept SHALL be **derived from the site's configured
retention period** and SHALL NOT be authored, stored or cached separately.

**There SHALL be exactly one source.** The notice and the retention sweep SHALL read the same
configured value, so that the two cannot disagree. A notice that could be written independently
of the code could claim a period the code does not keep, which is the failure this change exists
to prevent and the reason it follows retention rather than preceding it.

**Where no retention period is configured, the notice SHALL say so plainly** rather than omitting
the statement. Retention is off by default, so an omission would be the *usual* rendering: most
installs would publish a notice silently missing one of the four things it exists to say. What is
stated SHALL be true of that configuration — that the details are kept until removed and that no
automatic removal period is set — rather than implying either a period or a promise to delete.

#### Scenario: A configured period is what the notice states
- **WHEN** a retention period is configured and the notice renders
- **THEN** the period it states is the configured one

#### Scenario: Changing the period changes the notice
- **WHEN** the configured retention period is changed and the notice renders again
- **THEN** it states the new period, with no separately authored value left saying the old one

#### Scenario: No configured period is stated as such
- **WHEN** no retention period is configured and the notice renders
- **THEN** it states that the details are kept until removed and that no automatic removal period is set, and states no period and no promise to delete

#### Scenario: The notice and the sweep cannot disagree
- **WHEN** the value the notice states and the value the retention sweep acts on are compared
- **THEN** they are the same value from the same source

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

### Requirement: The notice is data on the view model, not markup alone

The notice SHALL be carried on the booking form's **view model** as structured values — what is
collected, the retention period or its absence, and the policy link — and SHALL NOT be exposed
only as a rendered string or a block of markup.

**Because a theme receives view models, not markup.** The `theming` capability makes the view
models a published contract precisely so an alternative rendering is handed the same information
the shipped views get. A pre-rendered sentence would let a theme print the package's English or
discard it, and nothing else; structured values let a theme state the same facts in its own
markup, its own wording and its own language.

This is the front-end contract applied to prose: what the package publishes is **data**, and
turning data into sentences is a rendering concern belonging to the view.

#### Scenario: A theme receives the notice
- **WHEN** a theme's view for a step that collects contact details renders
- **THEN** it is passed the same notice values the package's own view receives

#### Scenario: The model carries values rather than a sentence
- **WHEN** the notice on the view model is inspected
- **THEN** it exposes the retention period or its absence and the policy link as values, rather than only a pre-composed sentence or markup

### Requirement: What the notice does not reach is documented

The documentation SHALL state what the notice says, where it appears, and **the two boundaries of
what it achieves**:

- **It is not a privacy policy.** It states what this package does with what this form collects.
  A site still needs its own policy, and configuring the link is how the notice points at it.
- **A theme that replaces the view that collects contact details decides whether the notice
  renders at all.** The package renders no theme and claims nothing about a theme's markup, per
  `theming`; a theme is handed the notice on the view model and may render it, reword it, or not
  render it.

The second SHALL be stated for theme authors where themes are documented, because its consequence
is unlike other omissions: a theme that drops the time-picker produces a site that visibly does
not work, and a theme that drops the notice produces a site that works perfectly while telling its
visitors nothing. Nobody reports that as a fault.

#### Scenario: The limits of the notice are documented
- **WHEN** a reader consults the documentation
- **THEN** it states that the notice is not a privacy policy, that a site needs its own, and how to configure the link to it

#### Scenario: Theme authors are told the notice is theirs to render
- **WHEN** a theme author consults the theming documentation
- **THEN** it states that a theme replacing the contact-details view decides whether the notice renders, and that the package makes no claim either way
