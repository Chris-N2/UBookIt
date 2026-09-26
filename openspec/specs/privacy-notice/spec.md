# privacy-notice

## Purpose

What the booking form tells a person about the personal data it is collecting: that a notice
appears at the point of collection in every flow the package ships, and states what is taken,
why, how long it is kept and who can see it; that the retention statement is **derived from the
configured period** rather than authored, so it cannot claim something the code does not do, and
says something true when no period is set; that the package writes only sentences that are facts
about its own behaviour — never a lawful basis, a controller identity, a jurisdiction, or a
message it does not send — while the site supplies everything else by linking its own policy;
that the notice is carried as data on the view model so a theme receives the facts rather than
the package's English; and that it is a statement rather than a consent mechanism, gating
nothing.

Stated as its own capability rather than inside `default-frontend` because it is a promise about
**what is said**, not about how a view renders. The markup rules already govern the rendering;
what needed writing down is that the words are true, that they stay true when configuration
changes, and where the boundary of the package's knowledge falls.

## Requirements

### Requirement: The booking form states what happens to the details it collects

Every booking flow the package ships **for a person to complete about themselves** SHALL present
a privacy notice **at the point where contact details are collected**, and SHALL state all four
of:

- **what** personal data is collected — the booker's name, email address and, where given, phone
  number;
- **why** it is collected — to hold and identify the booking, and so that the site is able to contact the booker about it;
- **how long** it is kept, per the requirement below;
- **who can see it** — that a booker's contact details are shown only to backoffice users
  Umbraco permits to see sensitive data, per the `sensitive-data` capability.

**At the point of collection, not on some other page.** A notice a visitor must go looking for is
not a notice. It SHALL appear in the same form that asks for the details, ahead of the control
that submits them, so that it is read before the data is given rather than after.

**A screen on which an OPERATOR records somebody else's details is not such a flow, and SHALL
NOT present this notice.** The requirement is addressed to the person whose data it is, at the
moment they hand it over. On an operator's screen that person is not present — they spoke on the
telephone or stood at a desk — so the notice would be rendered to a member of staff, read by
somebody it was not written for, and would inform nobody who needed informing. Showing it there
would make this capability's guarantee *look* kept while the data subject learned nothing, which
is worse than the gap it papers over.

**This narrowing does NOT discharge the obligation to tell that person; it locates it.** What a
site must tell somebody whose details were taken by telephone is a real question, and the package
does not answer it today: the booker's own message is the first thing that actually reaches them,
and it carries no such statement. Recorded here as an open question rather than left as an
implication of the wording, because a requirement narrowed in silence reads afterwards as a
requirement that never applied.

**The notice SHALL NOT gate submission.** It is a statement, not a consent mechanism: there is
nothing to tick, nothing to agree to, and a booking SHALL complete exactly as it did before. The
lawful basis for holding a booker's details is performance of the booking, and an unrefusable
tickbox would misrepresent that as consent — which would also imply a right to withdraw it and
make the site's own records revocable.

#### Scenario: The notice appears where the details are asked for
- **WHEN** a visitor reaches a step that asks for their own contact details, in any flow the package ships for a person to complete about themselves
- **THEN** the privacy notice is present in that same form, ahead of the control that submits it

#### Scenario: All four statements are made
- **WHEN** the notice renders
- **THEN** it states what is collected, why, how long it is kept, and who can see it

#### Scenario: Booking is unaffected by the notice
- **WHEN** a visitor completes a booking
- **THEN** it succeeds without the visitor having agreed to, ticked, or dismissed anything, and the notice offers no such control

#### Scenario: An operator's screen presents no visitor notice
- **WHEN** an operator records a booking on somebody's behalf and enters that person's name, email address and telephone number
- **THEN** the package presents no privacy notice on that screen, because the person it addresses is not the one reading it

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

### Requirement: A usable policy link is defined once, and means the same on every host

A configured or stored privacy policy link SHALL be usable when, after surrounding whitespace is
removed, it is non-blank, contains no control character and no backslash, and is **either**:

- a **site-relative path**: it begins with exactly one `/` (not `//`); **or**
- an **absolute `http` or `https` URL**.

Every other value SHALL be unusable, and so treated as absent as the requirement *The package
states only what it can keep true, and the site links its own policy* already provides.

**Whether a value is usable SHALL NOT depend on the operating system hosting the site.** A value
beginning with `/` SHALL be judged as a site-relative path and SHALL NOT be interpreted as an
absolute URI of any scheme, so that a platform which reads such a value as a local file path
cannot change the answer.

**The settings screen SHALL accept a value for this setting exactly when the site would use it**,
for any value within the settings store's capacity. Validation on write and resolution remain
separate checks, as `site-settings` requires; they SHALL apply the same definition of usable, so
that the screen neither refuses a value the site would use nor stores one the site would treat as
absent. When the screen refuses a value within the store's capacity, the refusal SHALL name both
accepted forms.

*A value longer than the settings store can hold (2048 characters) is outside this requirement.
The site can use such a value from configuration, but the screen cannot store it. The screen
refuses it for its length and states the limit, as the `site-settings` requirement "A value longer
than the store holds is refused, not attempted" provides. That refusal is about length, not form,
so it does not name the two forms.*

#### Scenario: A site-relative link is used on a Linux host
- **WHEN** the site runs on Linux and the policy link is configured as `/privacy`
- **THEN** the notice links to `/privacy` and nothing is reported as unusable

#### Scenario: A site-relative link is used on a Windows host
- **WHEN** the site runs on Windows and the policy link is configured as `/privacy`
- **THEN** the notice links to `/privacy` and nothing is reported as unusable

#### Scenario: An absolute http or https link is used
- **WHEN** the policy link is `https://example.com/privacy` or `http://example.com/privacy`
- **THEN** the notice links to it

#### Scenario: The refusals are unchanged
- **WHEN** the policy link is blank, a protocol-relative `//host/path`, a path without a leading
  slash, a value containing a control character or a backslash, or a URL of any scheme other than
  `http` or `https`
- **THEN** it is unusable, on every host

#### Scenario: The settings screen accepts a site-relative link
- **WHEN** an editor stores `/privacy` as the policy link through the settings screen
- **THEN** the value is accepted and stored

#### Scenario: The screen and the site agree on every value
- **WHEN** any value within the settings store's capacity is submitted to the settings screen for
  the policy link
- **THEN** the screen accepts it if and only if the site, resolving the same value, would use it as
  the link

#### Scenario: The screen's refusal names both forms
- **WHEN** the settings screen refuses a policy link within the settings store's capacity
- **THEN** the reason given names both an absolute http or https address and a site-relative path
  beginning with a single `/`
