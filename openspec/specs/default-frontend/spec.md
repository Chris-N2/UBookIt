# default-frontend Specification

## Purpose

Defines the shipped, dependency-free default front-end: server-rendered, no-JavaScript booking flows exposed as Umbraco ViewComponents. It renders a single-resource flow, a service flow, and a catalogue of the bookable things a site offers — services and directly-bookable resources together — dispatched by a `BookingFlow` view component from whichever entry point a site author chooses, while the original `Booking` component continues to render the single-resource flow when invoked directly with a resource id. It renders availability by calling the Core resource, service and availability ports in-process (never the anonymous delivery API over HTTP), places bookings through the Core booking services behind an anti-forgery-protected same-origin POST, and confirms via Post-Redirect-Get. It ships a stylesheet as a static web asset, emitted by one documented partial and overridable through a published design-token and class contract. Accessibility is a first-class requirement, and the claim names its own boundary: every flow meets every WCAG 2.2 AA criterion determined by markup, uses semantic HTML, and preserves input while reporting failures accessibly; the criteria determined by CSS are met by the shipped stylesheet's defaults and become the consuming site's on override; and those determined by the host page were never the package's to claim, since a component cannot conform on its page's behalf. This is the default rendering that any alternative UI (a separate-repo DevExpress front-end, a SPA, a mobile client) may replace by consuming the delivery API instead.

## Requirements

### Requirement: No-JavaScript single-resource booking flow
The default front-end SHALL provide a complete booking flow for a single resource that functions with JavaScript disabled, using only server-rendered pages and standard form submissions. A visitor SHALL be able to choose a date and a booking length, see the available start times for that date and length, select one, provide contact details, submit, and receive a confirmation. The flow SHALL be rendered by an Umbraco ViewComponent invoked with a resource id, so a site author can place it in a template.

#### Scenario: Booking completes without JavaScript
- **WHEN** a visitor with JavaScript disabled chooses an open date, selects an available time, enters a valid name and email, and submits
- **THEN** the booking is placed and a confirmation is shown

#### Scenario: Choosing a date reveals that date's times
- **WHEN** a visitor chooses a date on which the resource is open and has free time
- **THEN** the page shows the available start times for that date at the selected length

#### Scenario: A date with no availability says so explicitly
- **WHEN** a visitor chooses a date on which the resource has no free time
- **THEN** the page shows an explicit "no times available" message rather than an empty list

### Requirement: Visitor-chosen booking length
The default front-end SHALL let a visitor choose the booking length rather than fixing it at the resource's minimum duration. The length control SHALL be presented alongside the date control in the same date-selection step, so that the start times subsequently listed are only those that admit the chosen length and no invalid start-and-length combination can be submitted.

The control SHALL offer only lengths the resource actually permits — the granularity multiples between its minimum and maximum duration — and SHALL default to the resource's minimum duration, so a visitor who does not touch it experiences the previous behaviour. The control SHALL be a labelled native form control operable with the keyboard and without JavaScript.

The chosen length SHALL be submitted with the booking and SHALL be validated server-side irrespective of what the control offered; the control is an affordance, not a trust boundary.

#### Scenario: Choosing a longer length filters the offered times
- **WHEN** a visitor chooses a length of 2 hours on a date whose free time is 09:00–12:00 with 1-hour granularity
- **THEN** the offered start times are only those from which 2 hours can be booked

#### Scenario: The default length preserves previous behaviour
- **WHEN** a visitor chooses a date and submits without altering the length control
- **THEN** the booking is placed for the resource's minimum duration

#### Scenario: The chosen length is booked
- **WHEN** a visitor chooses a 90-minute length, selects an available start, provides valid details, and submits
- **THEN** the placed booking's interval is 90 minutes long

#### Scenario: An unpermitted length is rejected server-side
- **WHEN** a submission carries a length the resource does not permit, regardless of what the form offered
- **THEN** placement is rejected and the form is redrawn with the failure reported, rather than a booking being placed

#### Scenario: The length control works without JavaScript
- **WHEN** a visitor with JavaScript disabled changes the length and submits the date-selection step
- **THEN** the page reloads showing the start times available for that length

### Requirement: An unavailable length explains the longest that is available
When a visitor chooses a length for which no start time is available on the selected date, the page SHALL state explicitly that no times are available for that length and SHALL report the longest length that is available on that date. When no length at all is available on that date, the page SHALL fall back to the existing explicit "no times available" message.

This message SHALL be composed in the front-end's view model from availability data rather than produced by Core, so no presentation string enters the domain.

#### Scenario: A too-long request explains itself
- **WHEN** a visitor chooses a 3-hour length on a date whose longest available run is 90 minutes
- **THEN** the page states that no 3-hour times are available and that the longest available that day is 90 minutes

#### Scenario: A fully unavailable date keeps the existing message
- **WHEN** a visitor chooses a date on which the resource has no free time at all
- **THEN** the page shows the explicit "no times available" message rather than a longest-available figure

### Requirement: Availability rendered from Core in-process
The front-end SHALL obtain the resource and its available start times by calling the Core resource and availability ports directly (in-process), and SHALL NOT call the delivery API over HTTP to render itself. Start times SHALL be displayed as wall-clock times in the site time zone. The value submitted for a selected time SHALL identify the exact instant, so placement does not depend on re-deriving the time from a display string.

#### Scenario: Times display in the site zone
- **WHEN** the site zone is `Europe/London` and a resource is free from 09:00 local
- **THEN** the earliest offered start time is shown as 09:00

#### Scenario: The selected time is submitted as an exact instant
- **WHEN** a visitor selects a start time and submits
- **THEN** the submitted request carries the exact UTC instant of that start, not only its displayed local text

### Requirement: Anti-forgery-protected submission
Booking submission SHALL be a same-origin POST carrying a valid anti-forgery token. A submission with a missing or invalid token SHALL be rejected and SHALL NOT create a booking. Placement SHALL run the Core booking service in-process; the anonymous delivery API SHALL NOT be used for the default form submission.

#### Scenario: Submission without a valid token is rejected
- **WHEN** a booking POST arrives without a valid anti-forgery token
- **THEN** it is rejected and no booking is created

#### Scenario: Valid submission places via Core
- **WHEN** a booking POST arrives with a valid token and a valid, available selection
- **THEN** the booking is placed through the Core booking service

### Requirement: Post-Redirect-Get confirmation
A successful placement SHALL respond with a redirect (HTTP 303) to a confirmation page, not by rendering the POST result directly. The confirmation SHALL show the booking reference (its id) and the booked resource, time, and booker contact details. Reloading or refreshing the confirmation page SHALL NOT create another booking.

#### Scenario: Success redirects to confirmation
- **WHEN** a placement succeeds
- **THEN** the response is a 303 redirect to a confirmation page showing the booking reference and details

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful booking
- **THEN** no additional booking is created

### Requirement: Accessible, semantic markup (WCAG 2.2 AA)
Every flow the default front-end renders — the single-resource flow, the service
flow, and the catalogue — SHALL meet **every WCAG 2.2 AA criterion determined by
markup**, and SHALL use semantic HTML. Every form control SHALL have a
programmatically associated label. The selectable start times SHALL be a grouped set
of radio controls within a `fieldset` carrying a `legend` that names the group. Hints
and error text SHALL be associated with their controls (for example via
`aria-describedby`). Required inputs SHALL be indicated in text, not by colour or
placeholder alone. Each flow SHALL be fully operable by keyboard. Each page SHALL
remain usable, with a logical reading and focus order, when no author stylesheet is
applied.

Criteria determined by **CSS rather than markup** — 1.4.3 (text contrast), 2.4.11 and
2.4.13 (focus appearance), and 2.5.8 (target size) — SHALL be met by the shipped
stylesheet at its default token values, and SHALL become the consuming site's
responsibility for any token that site overrides. The package SHALL NOT claim those
criteria for a rendering it did not style.

**The shipped stylesheet SHALL NOT alter the contrast the host page established.**
Every declaration of a property able to change a rendered colour — including `color`,
`background`, `background-color`, `background-image`, `opacity`, `filter`,
`backdrop-filter`, `mix-blend-mode`, `text-shadow` and `-webkit-text-fill-color` —
SHALL resolve, at its default, to a value that leaves the host's own pair exactly as it
was: `currentColor`, `inherit`, `transparent`, `none`, or a token whose fallback is one
of those. `color-mix()` MAY be used for **border** colours, which are decoration here,
and SHALL NOT be used for text.

This clause is what makes 1.4.3 true of the shipped default rather than merely argued,
and **its scope is deliberately the guarantee rather than any particular declaration**,
because two narrower versions were shipped and both were defeated. The first forbade a
*literal* colour; a hint derived as `color-mix(in srgb, currentColor 75%, transparent)`
satisfied it and rendered at 2.9:1 on a host whose body text was exactly AA-conformant,
because compositing text toward transparency reduces contrast. The second forbade a
derived **text** colour; `opacity: 0.75` on the same element satisfies that and produces
a byte-identical result, as does a `background` shorthand altering the other half of the
pair. A rule naming the mechanism a violation is expected to use will be met by the next
mechanism; a rule naming the property being guaranteed will not.

**1.4.11 (non-text contrast) is NOT claimed for the package's decorative borders, and
that exemption SHALL be stated rather than implied.** The rule beside a notice and the
border around the confirmation panel default below 3:1. Both are decoration: neither
carries information, every message they mark is stated in full in adjacent text, and
neither is a boundary a visitor must perceive to operate a control. Where a border
*does* bound a control or convey meaning, it SHALL meet 1.4.11.

This narrowing states a bar that was never held rather than lowering one that was.
The package shipped no stylesheet at all until this change, so every CSS-determined
criterion was already decided entirely by the consuming site, whatever the requirement
said. Nothing a visitor experiences becomes less accessible; what changes is that the
claim now names its own boundary.

**The no-author-stylesheet clause above is what the narrowing rests on, and SHALL NOT
be read as superseded by it.** Because the markup is operable, labelled and correctly
ordered with no CSS applied at all, no stylesheet a site ships can make a flow
*inoperable* — only unreadable. That is what makes the split defensible rather than
an excuse, and it is why the clause is load-bearing precisely where it looks like the
clause a CSS-scoped narrowing would drop.

The package SHALL publish which criteria fall where: met by the shipped default, met
by the shipped default but the site's once a token is overridden, or determined by
the host page (among them 1.4.10 reflow, 1.4.12 text spacing, 2.4.1 bypass blocks,
2.4.2 page titled, 3.1.1 language of page, and the document's heading outline, into
which the flow's own headings are placed). A component placed in someone else's page
cannot conform on that page's behalf, and saying so plainly is worth more than a
claim that cannot be kept.

This SHALL be **one bar, stated once**, rather than a bar per flow. Accessibility is
a differentiator for this package rather than a checkbox, and per-flow restatements
drift: the flow written second gets the attention, and the guarantee quietly becomes
"whichever flow was reviewed most recently". The same hazard applies to the
markup/CSS split — it is one split, stated once, not a caveat repeated per surface.

Where a choice is offered as a set of related controls — the start times, and the
catalogue when it is rendered as a chooser — it SHALL be a grouped set with a
`legend` naming what is being chosen, on the same terms as the start times.

#### Scenario: Every control is labelled
- **WHEN** a booking form is rendered, in either flow
- **THEN** each input and select has a programmatically associated `label`

#### Scenario: Start times are a labelled radio group
- **WHEN** available start times are rendered, in either flow
- **THEN** they are radio inputs inside a `fieldset` whose `legend` names the group

#### Scenario: Usable without an author stylesheet
- **WHEN** any flow is rendered with no author CSS applied
- **THEN** the content order is logical and every control remains operable and labelled

#### Scenario: The catalogue meets the same bar
- **WHEN** the catalogue is rendered as a set of choices
- **THEN** it is a grouped set of labelled controls with a `legend` naming what is being chosen, operable by keyboard

#### Scenario: The service flow meets the bar the resource flow meets
- **WHEN** the service flow is rendered
- **THEN** it satisfies every clause of this requirement, with no clause holding only for the resource flow

#### Scenario: The CSS-determined criteria are met by the shipped defaults
- **WHEN** a flow is rendered with the shipped stylesheet applied and no token overridden
- **THEN** text contrast, focus appearance and target size are met, and text contrast cannot fail because every text colour resolves to what the host already chose

#### Scenario: A declaration that alters the host's contrast fails
- **WHEN** the stylesheet declares any colour-affecting property whose default resolves to anything other than `currentColor`, `inherit`, `transparent`, `none` or a token falling back to one of those — whether it names a colour, derives one, or composites without naming one at all
- **THEN** the check fails, naming the property and value

#### Scenario: The check covers routes that name no colour
- **WHEN** the stylesheet reduces rendered contrast by `opacity`, by a `background` shorthand, or by any other compositing property rather than by a `color` declaration
- **THEN** the check still fails — the rule is scoped to the guarantee, not to the declaration a previous defect happened to use

#### Scenario: A decorative border is exempt from 1.4.11 and says so
- **WHEN** the package's default border colours are below 3:1
- **THEN** the borders they draw carry no information and bound no control, and the exemption is stated in the published accessibility statement rather than left implied

#### Scenario: The published statement names its own boundary
- **WHEN** the accessibility statement is published
- **THEN** it names, criterion by criterion, which are met by the shipped default, which pass to the site on a token override, and which are determined by the host page

#### Scenario: The narrowing does not reach the markup clauses
- **WHEN** any clause of this requirement other than the CSS-determined criteria is evaluated
- **THEN** it holds exactly as it did before this change, for all three flows

### Requirement: Accessible failure handling with input preservation
When a submission fails validation or placement, the form SHALL be redrawn with an
error summary that lists each problem in text and is associated with the offending
fields (qualified below, where the field is no longer on the page), and the
visitor's entered contact details SHALL be preserved. Stable domain
failure codes SHALL be mapped to user-facing messages. A time that became
unavailable between rendering and submission (a `conflict`) SHALL produce a clear
"no longer available" message with refreshed availability, not a raw error.

A problem SHALL be associated with its field **where that field is on the page**,
and SHALL still be listed in text where it is not. A redraw does not always render
every control the previous submission carried — in the shipped front end, for
example, the booker fields appear only where there are times and the choice control
only where a choice is still offered — so a failure can outlive the control it
names. Which controls a given front end drops is its own business; that some may be
dropped is what this requirement has to account for.

An association pointing at a control that was not rendered SHALL NOT be emitted. It
reads as a route to the problem and is none: a summary that says "please enter your
name" and takes the visitor nowhere is worse than one that says it plainly, because
it spends their trust on a link that cannot work. This is a narrowing of the
association guarantee and not of the reporting guarantee — every problem is still
stated in text, which is what the visitor needs in order to know what went wrong.

Because that reporting guarantee is what the narrowing rests on, it SHALL be
checked and not assumed: every problem the redraw carries SHALL appear in the
rendered document, linked or not. And where a problem is linked, **the link's text
SHALL be the problem's own message**. A link's text is its accessible name, so a
summary of "click here" beside the messages somewhere else states every problem and
names none of them — it satisfies the letter of both guarantees while destroying
what they are for.

Where a control is replaced by settled text rather than removed — a fixed length, a
choice that is no longer offered — the replacement SHALL carry the control's
identity, so the association survives. Removing the control is not a licence to drop
the link when something can still stand in its place.

The standing requirement that hints and error text are associated with their
controls is unaffected and unmodified: it governs text belonging to a control that
is rendered. Where the control is absent, the problem is stated as text in its own
right and there is nothing to bind it to — which is a case that requirement does not
reach rather than one it forbids.

#### Scenario: Validation failure redraws accessibly and preserves input
- **WHEN** a visitor submits with a missing email
- **THEN** the form is redrawn with an error summary identifying the email field and the previously entered name is preserved

#### Scenario: A now-unavailable time is reported clearly
- **WHEN** the selected time was taken by another booking between page load and submission
- **THEN** the visitor sees a clear "no longer available" message and refreshed availability, and no booking is created

#### Scenario: A problem about a control that is no longer rendered is still stated
- **WHEN** a redraw carries a failure about a control the page no longer renders — a booker field on a date with no times, or a choice the service no longer offers
- **THEN** the summary states the problem in text and does not link to the absent control

#### Scenario: A problem missing from the page fails
- **WHEN** a redraw carries a failure whose message appears nowhere in the rendered document
- **THEN** the check fails, naming the view and the message

#### Scenario: A link that does not say what the problem is fails
- **WHEN** a problem is linked to its control but the link's text is something other than the problem's message
- **THEN** the check fails, naming the view and the control

#### Scenario: Settled text keeps the association
- **WHEN** a control is replaced by settled text rather than removed, and a failure names it
- **THEN** the replacement carries the control's identity and the summary still links to it

### Requirement: Rendered markup resolves its own references
Every view the default front end renders SHALL, in every state its model can
express, produce a document whose internal references resolve: each `label`
carrying a `for` names an element that exists in that document; each
`aria-describedby` and `aria-labelledby` names, for every id it lists, an element
that exists in that document; every in-page link — an `href` naming a fragment of
the same document — names an element that exists in it; no id is emitted more than
once; and every form control carries an accessible name, whether from an associated
`label`, an `aria-label`, or an `aria-labelledby` that resolves.

**A reference SHALL resolve to something that says something.** Every element an
`aria-describedby` or `aria-labelledby` names SHALL carry non-whitespace text, an
accessible name SHALL be non-empty, and a `fieldset`'s `legend` SHALL be present and
non-empty. An association pointing at an empty element resolves perfectly and
describes nothing: the screen reader announces the relationship and then has nothing
to read, and for a group the boundary is announced with no name at all. That the
same text may also appear in the error summary does not repair it — the summary
serves the visitor reading the list of problems, and this serves the one standing on
the field.

This clause is the difference between a reference existing and a reference working,
and it is the second half of what "carries an accessible name" already meant: a
check for a labelling *element* rather than for a *name* passes an empty
`<label for="…"></label>`, which leaves the control unnamed. Ten mutations of this
shape — emptied labels, emptied legends, emptied described targets — were silent
under the earlier rules.

The in-page link is not a lesser member of that list. It is the reference the error
summary is built on, and the reason a control replaced by settled text still carries
its identity — the summary links to it, and a link with no target is worse than the
control it replaced. It is also the clause whose absence hid two real faults.

An in-page link's target SHALL be able to receive focus — natively, or by carrying
a `tabindex`. Resolving is not arriving: following a link into a plain `div` moves
the viewport and leaves focus where it was, which for a keyboard or screen-reader
user is the failure the link exists to prevent. This bites precisely where a
control has been replaced by settled text, because what carries the identity is
then a wrapper rather than a control. A negative `tabindex` satisfies it: such a
wrapper should be reachable by the link and absent from the sequential tab order.

This SHALL be checked against the **rendered document**, not against view source.
The distinction is the whole requirement. A path or an attribute appears in a
`.cshtml` file whether the construct around it renders or is emitted as literal
text, so a source scan cannot tell a working association from a dangling one, nor
a rendered partial from a tag helper printed onto the page. Both faults have
shipped into a fully green suite, and the second was introduced *by* the test
written to catch the first.

The rendered output SHALL contain no tag-helper residue — no literal `<partial`
element and no `asp-` attribute. `UBookIt.Web` registers no tag helpers and has no
`_ViewImports.cshtml`, so any tag helper degrades to visible text on the page
rather than failing the build. The existing source-level ban stays; this is the
same rule asserted where the consequence actually appears.

This requirement SHALL NOT be read as replacing the WCAG 2.2 AA bar, and
satisfying it is not sufficient for meeting that bar: a document can resolve every
reference it makes and still be inaccessible. Every clause here is one the
accessibility requirement already states and that nothing has ever checked.

#### Scenario: A dangling aria reference fails
- **WHEN** a view renders an `aria-describedby` naming an id that no element in the document carries
- **THEN** the check fails, naming the view

#### Scenario: A label pointing at nothing fails
- **WHEN** a view renders a `label` whose `for` names no control in the document
- **THEN** the check fails, naming the view

#### Scenario: A link into the page that lands nowhere fails
- **WHEN** a view renders an `href` naming a fragment that no element in the document carries
- **THEN** the check fails, naming the view

#### Scenario: A link into the page whose target cannot take focus fails
- **WHEN** a view renders an `href` naming an element that is neither natively focusable nor carries a `tabindex`
- **THEN** the check fails, naming the view

#### Scenario: A duplicated id fails
- **WHEN** a view renders the same id on two elements
- **THEN** the check fails, naming the view

#### Scenario: An unnamed control fails
- **WHEN** a view renders an input or select with no associated label, `aria-label`, or resolving `aria-labelledby`
- **THEN** the check fails, naming the view

#### Scenario: A control whose label is empty fails
- **WHEN** a view renders a control whose only labelling element carries no text
- **THEN** the check fails, naming the view — a labelling element is not a name

#### Scenario: A description that resolves to an empty element fails
- **WHEN** a view renders an `aria-describedby` or `aria-labelledby` naming an element that carries no text
- **THEN** the check fails, naming the view and the reference

#### Scenario: A group with an empty or missing legend fails
- **WHEN** a view renders a `fieldset` whose `legend` is absent or carries no text
- **THEN** the check fails, naming the view

#### Scenario: A tag helper reaching the page fails
- **WHEN** a view renders a `<partial>` element or an `asp-` attribute as literal output
- **THEN** the check fails, naming the view

#### Scenario: The shipped views satisfy it in every exercised state
- **WHEN** every view in scope is rendered across the model states its own properties can express
- **THEN** every rendered document resolves its references and its in-page links, carries no duplicate id, names every control and every group non-emptily, describes with elements that carry text, and shows no tag-helper residue

### Requirement: A view renders every state its model can express
For each model property a view references, varying that property SHALL change the
view's rendered output. A property a view names but cannot make any difference to
is either dead, or sits behind a branch that cannot be reached — and the second is
a message placed where it can never appear.

**A property whose job is to say that something is shown SHALL be held to the
stronger form**: wherever the model sets such a flag, the page SHALL render
something it would not render with the flag unset. "Changes the output in *some*
state" is not sufficient for these and SHALL NOT be accepted as satisfying this
requirement. A flag live wherever one condition holds and dead where it does not
passes the weaker form and is exactly the defect this requirement exists to catch:
⑩-1's notice was reachable for two of its three causes.

The stronger form SHALL apply wherever the model can **set** the flag — in every
such state, whichever value it currently holds. It is restricted only to states
that can express the flag at all, because a property may be settable on one model
implementing a shared contract and computed on another, and a state that cannot
express it cannot be evidence about it.

A state where a flag is legitimately silent — because a higher-priority message
about the same thing takes precedence — SHALL be an explicit reasoned exemption
rather than excluded by a broader rule. Excluding the class rather than the case
would switch the guarantee off far beyond the case it was meant to accommodate.

The set of properties this applies to SHALL be **every property the view actually
refers to**, and SHALL NOT be a list maintained separately from the view: a
separate list fails the way the defect it guards against fails, by being silent
about what it omits.

A property SHALL be exempt only by an explicit, reasoned entry — never by being
absent from a list, and never by a rule that skips a whole class of states — so
that exempting one is a decision someone made rather than something that happened.

This is a guarantee about the front end and not merely a testing technique. The
flow decides what a visitor is told; the view decides whether they are told it. A
model that reports a choice was reset, rendered by a view that cannot say so, is
the quiet substitution the reset exists to prevent, and it has happened.

#### Scenario: An unreachable branch fails
- **WHEN** a view references a model property whose value the rendered output never reflects, in any state exercised
- **THEN** the check fails, naming the view and the property

#### Scenario: A property behind an unreachable cause fails
- **WHEN** a model reports a state the view can only render under a condition that state cannot satisfy
- **THEN** the check fails, because varying the property changes nothing

#### Scenario: The derivation sees every reference form
- **WHEN** a view refers to its model through the null-conditional operator
- **THEN** that property is among those checked, rather than the view being treated as referencing none

#### Scenario: An exemption is explicit
- **WHEN** a property is excluded from the check
- **THEN** the exclusion is recorded with its reason, and a property that is merely unlisted is not excluded

#### Scenario: A flag live in one state and dead in another fails
- **WHEN** a model can set a flag whose job is to show a message, and the view renders nothing different for it in some state that can express it
- **THEN** the check fails, even though other states render it, unless that state is an explicit reasoned exemption

#### Scenario: The shipped views satisfy it
- **WHEN** every view in scope is checked against the properties its source references
- **THEN** each of those properties changes what the view renders, and each flag does so wherever the model sets it

### Requirement: Every branch a view carries can be taken
A view SHALL NOT carry markup that no state its model can express will render.
Markup a view can emit but never does is a branch that cannot be reached, and a
message in it is a message no visitor will ever see.

This is a guarantee distinct from the one above and neither implies the other. A
property can be perfectly live while a branch it selects is dead: forcing a
condition always-true leaves both the flag and its underlying collection changing
the output — the two states still differ — while the alternative branch becomes
unreachable.

A branch that nothing renders SHALL be treated as unproven rather than accepted: it
is either dead markup or untested behaviour, and the second is a gap in what the
flows are known to do rather than a licence to stop asking.

Where reachability cannot be established for a branch, that SHALL be recorded
explicitly rather than left implicit, so the limits of the guarantee are known
rather than discovered.

#### Scenario: An unreachable branch fails
- **WHEN** a view carries markup that no state its model can express will render
- **THEN** the check fails, naming the view and the markup

#### Scenario: A live property does not excuse a dead branch
- **WHEN** a condition is forced always-true, leaving its alternative unreachable while every model property still changes the output
- **THEN** the branch check fails even though the property check passes

#### Scenario: An unreached branch is covered rather than excused
- **WHEN** a branch is reached by no exercised state
- **THEN** a state reaching it is added, rather than the branch being exempted

### Requirement: Every view the package ships is exercised
The set of views the rendering rules are asked of SHALL be **every view the package
ships**. A view that ships and is not exercised is a view about which every rule
reports green while asserting nothing, which is the same shape as the defects those
rules exist to catch.

That set SHALL be derived from what the package ships rather than maintained as a
list beside it. A list decays silently: a view added later sits outside the suite
with nothing anywhere saying so, and the omission is invisible precisely because
omission has no representation.

A view SHALL be exempt only by an explicit, reasoned entry — never by being absent,
and never by a phrase like "in scope" that names a set nobody defines. Where a view
cannot be exercised, the record SHALL state **why** and **what it would take**, so
that the limit of the guarantee is known rather than discovered. This is the clause
that previously did the deferring by implication: three views sat outside the suite
behind an undefined phrase, and the two flow views a visitor actually books through
were the ones outside it.

This requirement SHALL carry a guard that fails when the exercised set is empty,
when it shrinks, or when it is smaller than the shipped set. A completeness rule
that is satisfied by checking nothing is the vacuous case of the fault it exists to
prevent, and this project has shipped that fault before.

#### Scenario: A shipped view outside the suite fails
- **WHEN** the package ships a view that no exercised state renders
- **THEN** the check fails, naming the view

#### Scenario: An exemption is explicit or it is not an exemption
- **WHEN** a view is excluded from the exercised set
- **THEN** the exclusion is recorded with its reason and with what would lift it, and a view that is merely unlisted is not excluded

#### Scenario: A completeness check that checks nothing fails
- **WHEN** the exercised set is empty, or smaller than the set the package ships
- **THEN** the check fails, rather than passing because it found no counter-example

#### Scenario: The views that call into Umbraco are exercised like any other
- **WHEN** a view obtains its form from `Html.BeginUmbracoForm`
- **THEN** it is exercised by every rendering rule on the same terms as a view that does not, with no standing exemption for depending on Umbraco

#### Scenario: Every shipped view is exercised today
- **WHEN** the shipped view set is compared against the exercised set
- **THEN** the two are equal, and no view is deferred

### Requirement: A composed document is the composition a flow view renders
Where a rendering rule asks a question about a **document** — id uniqueness,
reference resolution, in-page link targets — the document it is asked of SHALL be
one a shipped view actually renders. A document assembled by the test suite to
resemble a page is evidence about the assembly, not about the page, and the rules
that read it report on something the site never serves.

Such an assembly SHALL NOT be accepted as satisfying a document-level rule, even
where it is believed to match. The belief is the problem: it is unfalsifiable by
the suite that depends on it, and it has already been wrong. A hand-built
composition that included the booker fields unconditionally made an error summary
resolve against controls the real page does not render, which is the exact defect
class the document-level rule exists to catch — so the fixture was concealing the
rule's own target.

A source-level check that a flow view **names** each partial SHALL NOT be accepted
as discharging this. Naming is not composition: a source scan cannot see the order
partials are rendered in, the conditions they are rendered under, or that one is
rendered twice. Each of those changes the document, and the second is where the
known drift occurred.

Where a flow view renders a partial conditionally, the composed document SHALL be
subject to that same condition, because the page is. A document that always
contains a conditionally-rendered part is a page the flow cannot produce, and it
fails in the concealing direction — it resolves references that the real page
leaves dangling.

#### Scenario: A hand-assembled document is not evidence
- **WHEN** a document-level rule is satisfied by a document the suite assembled rather than one a view rendered
- **THEN** the rule is not treated as satisfied for that document

#### Scenario: A composition that ignores the flow's condition fails
- **WHEN** a composed document contains a part the flow view renders only under a condition that the composition does not apply
- **THEN** the composition is wrong, whether or not the rules pass on it

#### Scenario: Naming a partial does not prove composing it
- **WHEN** a flow view's source names every partial the composed document contains
- **THEN** that agreement does not discharge this requirement, since it is silent about order, condition and repetition

#### Scenario: A drift between flow view and fixture is visible
- **WHEN** a flow view changes which partials it renders, or under what condition
- **THEN** the documents the rules are asked of change with it, without any fixture being edited

#### Scenario: The shipped flows satisfy it
- **WHEN** the document-level rules are asked of the booking flows
- **THEN** each document is the rendered output of a shipped flow view, and no document-level rule is answered from an assembled substitute

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

### Requirement: No-JavaScript service booking flow
The default front-end SHALL provide a complete booking flow for a **service** that
functions with JavaScript disabled, using only server-rendered pages and standard
form submissions. A visitor SHALL be able to choose a date and a booking length, see
the start times available for that service at that length, select one, provide
contact details, submit, and receive a confirmation.

The flow SHALL obtain the service, its available starts, and its placement from the
Core service-booking and availability ports in-process, and SHALL NOT call the
anonymous delivery API over HTTP to render or submit itself — the same rule the
resource flow follows, for the same reason.

The flow SHALL be renderable from a template given a service id, so a site author can
place it on a page.

A service booking claims **several** resources. The flow SHALL be written for that
case rather than for the single-resource case with extras: nothing in it may assume
exactly one resource, including a single-role service, whose resolved set is a
collection of one.

#### Scenario: A service booking completes without JavaScript
- **WHEN** a visitor with JavaScript disabled chooses an open date, selects an available time, enters a valid name and email, and submits
- **THEN** the service booking is placed and a confirmation is shown

#### Scenario: Choosing a date reveals that service's times
- **WHEN** a visitor chooses a date on which the service can be fulfilled
- **THEN** the page shows the start times available for that service on that date at the selected length

#### Scenario: The times offered are the service's, not one resource's
- **WHEN** a service requires a room and a therapist, and rooms are free at a start where no therapist is
- **THEN** that start is not offered

#### Scenario: A date with no availability says so explicitly
- **WHEN** a visitor chooses a date on which the service has no available starts
- **THEN** the page shows an explicit "no times available" message rather than an empty list

### Requirement: The confirmation reports every resource a service resolved to
The confirmation for a placed service booking SHALL report the booking reference, the
booked interval, the booker's contact details, and **every** resource the service
resolved to, not one of them and not a count.

A visitor who booked a room and a therapist was given both, and a confirmation naming
one of them describes a different booking from the one that exists. For a single-role
service the report SHALL still be the resolved set, which has one member — the flow
does not special-case it.

#### Scenario: A multi-role confirmation names every resource
- **WHEN** a service requiring a room and a therapist is booked
- **THEN** the confirmation names both resolved resources

#### Scenario: A single-role confirmation names its one resource
- **WHEN** a single-role service is booked
- **THEN** the confirmation names that one resolved resource

#### Scenario: Refreshing the confirmation does not re-submit
- **WHEN** a visitor refreshes the confirmation page after a successful service booking
- **THEN** no additional booking is created

### Requirement: A service that cannot be booked says which kind of cannot
When a service cannot be booked, the flow SHALL distinguish a **transient** refusal —
the times are taken, and another date or time may work — from a **deterministic** one,
where the service as configured can never be fulfilled. Rendering them alike discards
a distinction the domain went to trouble to make.

The two SHALL be told apart by **which question was asked**, and not by the failure
code alone. A placement refusal — whatever its code — is an answer about **one
instant**: `service-unavailable` is raised identically for a structurally impossible
service and for one whose resources merely happen to be busy, so it cannot support a
permanent claim. Only the **configuration-time** check, asked over the resolved
candidate pools rather than over a moment, may establish that a service can never be
fulfilled.

The permanent claim SHALL therefore be made in exactly one place: the flow's
configuration-time refusal, rendered **before any form is offered**. No message
derived from a placement failure may assert it.

A transient refusal SHALL invite the visitor to try another time. A deterministic
refusal SHALL NOT, because it would be inviting them to fail again. A refusal SHALL
NOT be rendered in a shape that contradicts its own wording — a page stating that a
service cannot be booked while offering bookable times for it is the same conflation
arriving from the other direction.

This is the same failure the withholding-resource requirement exists to prevent,
arriving through a third door: a correctly configured thing that yields nothing and
says nothing about why. "No times available" sends a visitor back tomorrow when
tomorrow cannot help, and tells the site owner their opening hours are wrong when
they are not.

#### Scenario: A busy service invites a retry
- **WHEN** every resource able to fulfil a service is booked at the chosen time, but the service is fulfillable in general
- **THEN** the page reports that the time is unavailable and invites the visitor to choose another

#### Scenario: An unfulfillable service does not invite a retry
- **WHEN** a service can never be fulfilled as configured — a role with no eligible resource at all
- **THEN** the page reports that the service is not currently available for booking, and does not invite the visitor to try another time

#### Scenario: The two refusals are distinguishable on the page
- **WHEN** a transient refusal and a deterministic refusal are each rendered
- **THEN** their messages differ, and the difference is carried in text rather than by styling alone

#### Scenario: A bookable service refused at one instant still invites another
- **WHEN** a service that can be fulfilled is refused for the instant submitted, whatever the failure code
- **THEN** the visitor is invited to choose another time, and is not told the service is unavailable for booking

#### Scenario: A refusal is never rendered above the times it denies
- **WHEN** a refusal message is shown on a page that also lists bookable start times for that service
- **THEN** that message invites the visitor to choose one of them, rather than stating that the service cannot be booked

### Requirement: A visitor may choose who fulfils a service
Where a service has a visitor-selectable role, the service flow SHALL offer a control
for choosing which resource fills it, and SHALL offer **no such control** for a
service that has none. The control SHALL default to expressing no choice — "any" —
and choosing nothing SHALL leave the flow behaving exactly as it does today.

The control SHALL sit in the same step as the date and the length, and the start times
shown SHALL be those at which the chosen resource can actually be assigned. Choosing
who before when SHALL NOT add a step: the choice joins an existing submission rather
than introducing a page between the times and the contact details.

The choices SHALL be the resources resolved for that role — every resource whose type,
capabilities and duration range let it fulfil the service — listed by display name in
a stable order. The list SHALL NOT be filtered by the chosen date. A resource with no
free time on the selected date SHALL still be offered, and the start list SHALL then
report that there are no times, because filtering the people by date would make the
control's contents change under the visitor as they change the date, and would answer
with the control a question the times already answer.

Where the selectable role has a count greater than one, the control SHALL state that
the visitor is choosing **one** of that many resources and that the remainder are
assigned. A control that implies a choice and then books resources the visitor never
chose is a substitution, however friendly its wording.

The chosen resource SHALL be carried through to placement as the pinned resource, so
the booking that is placed is the booking that was offered. It SHALL NOT be treated as
a preference: no other resource SHALL be substituted for it.

#### Scenario: A service offering a choice shows the control
- **WHEN** a visitor opens the flow for a service whose therapist role is visitor-selectable
- **THEN** a labelled control offers each eligible therapist by name, defaulting to no particular choice

#### Scenario: A service offering no choice shows no control
- **WHEN** a visitor opens the flow for a service with no visitor-selectable role
- **THEN** no such control is rendered and the flow is exactly as it was

#### Scenario: Choosing a person narrows the times
- **WHEN** a visitor chooses a therapist who is booked all morning
- **THEN** the start times shown are only those at which that therapist can be assigned

#### Scenario: Choosing nobody offers the service's own times
- **WHEN** a visitor leaves the choice at "any"
- **THEN** the start times shown are the service's, as they are for a service offering no choice

#### Scenario: The choice is honoured at placement
- **WHEN** a visitor chooses a therapist, selects an offered time, and submits valid details
- **THEN** the booking placed claims that therapist, and the confirmation reports them

#### Scenario: A person with no times that day is still offered
- **WHEN** a visitor selects a date on which one eligible therapist is fully booked
- **THEN** that therapist still appears in the control, and choosing them shows the no-times message

#### Scenario: Choosing one of several says so
- **WHEN** a service's selectable role has a count of 2
- **THEN** the control states that the visitor chooses one of the two and the other is assigned

#### Scenario: No control can pin a resource for a non-selectable role
- **WHEN** the rendered flow for a service with no visitor-selectable role is inspected
- **THEN** it contains no control, hidden field, or other means by which a pinned resource could be submitted

### Requirement: A refused choice is distinguished from no availability
Where a visitor's chosen resource cannot fulfil the booking at the time they chose,
the flow SHALL say **that**, and SHALL NOT report it as the service having no
availability. The two are different facts with different next steps: one is answered
by choosing another time or another person, the other by choosing another date.

The message SHALL name the resource the visitor chose, because they chose it, and
SHALL offer the way forward — the times that choice can be honoured, or the option of
letting the service assign anyone.

Where the chosen resource is no longer able to fulfil the service at all — deleted,
no longer eligible, or its role no longer selectable — a request carrying it SHALL
NOT silently fall back to assigning someone else. Before anything is submitted the
flow SHALL reset the choice to "any" **and say that it has**, so a stale link does
not quietly become a different booking; on submission the placement failure for an
ineligible resource SHALL be reported rather than absorbed.

#### Scenario: A busy choice is reported as such
- **WHEN** a visitor's chosen therapist is claimed at the submitted time while other therapists are free
- **THEN** the page says that person is not available at that time, names them, and does not say the service has no availability

#### Scenario: A refused choice is not silently substituted
- **WHEN** a submission pinning a therapist cannot be honoured
- **THEN** no booking is placed with a different therapist

#### Scenario: A stale choice in a link resets and says so
- **WHEN** a visitor opens a link whose chosen resource no longer fulfils the service
- **THEN** the form is shown with the choice reset to "any" and a message saying the previous choice is no longer offered

#### Scenario: An ineligible choice at submission is reported
- **WHEN** a submission carries a resource that cannot fulfil the service
- **THEN** the failure is reported to the visitor rather than absorbed into a booking

### Requirement: A visitor-facing refusal discloses no configuration detail
A refusal rendered to a visitor SHALL state what it means for them and SHALL NOT
carry the service's configuration: it SHALL NOT name a role, a resource type, a
required capability, a count, or how many eligible resources exist.

The pool-sufficiency diagnostic built for the backoffice answers "which role is short
of what" for the person who can fix it. A visitor cannot fix it, is not owed the
site's staffing, and would be told by any such message how many therapists the
business employs.

This SHALL hold for both the transient and the deterministic refusal, and SHALL NOT
be achieved by suppressing the diagnostic at its source — the backoffice report is
unchanged.

A refusal concerning a resource the visitor **themselves chose** MAY name that
resource, and nothing more. Naming back a choice the visitor made discloses none of
the five facts above: not which role it fills, not what the role requires, not how
many others exist. This is a narrow permission and SHALL NOT be read as licence to
name resources a visitor did not choose — a deterministic refusal names none, whether
or not the service offers a choice.

Where a site has turned on a visitor-selectable role, the pool of that role is
disclosed by the picker as the site owner's deliberate configuration. That SHALL NOT
change what a **refusal** may say: the picker publishing a list is a choice the site
made, and a refusal remains bound by this requirement whether or not a picker is
rendered.

#### Scenario: A deterministic refusal names no role
- **WHEN** a service cannot be fulfilled because a role has no eligible resource
- **THEN** the visitor-facing message names neither the role, the resource type, nor the capability it required

#### Scenario: The backoffice diagnostic is unaffected
- **WHEN** the same misconfigured service is inspected in the backoffice
- **THEN** the shortfall report still names the role and its requirements, exactly as before

#### Scenario: A refused choice names only what the visitor chose
- **WHEN** a visitor's chosen resource cannot be assigned at the submitted time
- **THEN** the message names that resource and discloses no role, type, capability, count, or pool size

#### Scenario: A deterministic refusal names nothing even where a choice was offered
- **WHEN** a service with a visitor-selectable role becomes permanently unfulfillable
- **THEN** the refusal names no resource, including any the visitor had chosen, and no configuration detail

### Requirement: A catalogue of bookable things
The default front-end SHALL be able to render a catalogue from which a visitor
chooses what to book, listing the site's services and the resources that permit
direct booking **together**, as one set of choices.

The two kinds SHALL be presented alike. Whether a bookable thing is a service or a
resource is an implementation distinction; a visitor books "a massage" or "meeting
room A" and is not helped by being told which is which. Choosing an entry SHALL lead
to the flow appropriate to its kind.

A resource that withholds permission to be booked on its own SHALL NOT appear.
Listing it would offer a choice that leads only to the statement that it is not
offered on its own.

The catalogue SHALL be composed from the same resource and service reads the rest of
the front end uses, and SHALL NOT introduce a second notion of what is bookable.

#### Scenario: Services and directly-bookable resources are listed together
- **WHEN** a site has two services and one directly-bookable resource
- **THEN** the catalogue offers all three as choices

#### Scenario: A service-only resource is not offered
- **WHEN** a resource withholds permission to be booked on its own
- **THEN** it does not appear in the catalogue

#### Scenario: Choosing an entry reaches the right flow
- **WHEN** a visitor chooses a service from the catalogue and then a resource
- **THEN** each leads to the booking flow for that kind

### Requirement: A site may enter the flow at any point
The front-end SHALL let a site author start a visitor at the catalogue, at a
particular service, or at a particular resource, and SHALL NOT require the catalogue
to be traversed first.

A site offering one service should be able to render its booking flow directly. Making
a visitor choose that service from a list of one is a step that exists only because
the software has more than one shape, which is not the visitor's problem.

Each entry point SHALL be independently renderable and SHALL produce the same flow
from that point on as it would have if reached from the catalogue.

#### Scenario: A site starts at one service
- **WHEN** a site author renders the flow for a particular service
- **THEN** the visitor sees that service's date and length step, with no catalogue

#### Scenario: A site starts at the catalogue
- **WHEN** a site author renders the catalogue
- **THEN** a visitor may choose any bookable thing and continue into its flow

#### Scenario: The entry point does not change what follows
- **WHEN** a service flow is reached directly and by way of the catalogue
- **THEN** the steps, controls and outcomes from that point are the same

### Requirement: Flow state is carried in the URL
Each step of a flow SHALL carry its accumulated choices — what is being booked, the
chosen date, the chosen length, and the chosen resource where one was offered — in the
request URL, so that a step is linkable, bookmarkable, and reachable by the browser's
back button without resubmitting anything.

State SHALL NOT be held in a way that makes going back produce a step inconsistent
with the one shown, which is the failure a server-side wizard state would introduce.

Contact details SHALL NOT appear in a URL. They are entered at the final step and
submitted by POST, and the existing anti-forgery and Post-Redirect-Get rules govern
them unchanged.

A resource identifier in the URL discloses nothing a public read does not already
carry, and a link naming a resource that no longer fulfils the service SHALL be
handled as a stale choice rather than as an error.

#### Scenario: A step is linkable
- **WHEN** a visitor copies the URL of a date-and-length step and opens it again
- **THEN** the same service, date and length are shown

#### Scenario: A chosen resource is linkable
- **WHEN** a visitor chooses a therapist, copies the URL, and opens it again
- **THEN** the same service, date, length and therapist are shown

#### Scenario: Going back does not resubmit
- **WHEN** a visitor uses the browser's back button from a later step
- **THEN** the earlier step is shown as it was, and nothing is resubmitted

#### Scenario: Contact details are not in the URL
- **WHEN** a visitor submits their name and email
- **THEN** those values are carried by the POST body and do not appear in any URL

### Requirement: A default stylesheet ships, and makes no colour decision
The package SHALL ship a stylesheet as a static web asset served at
`_content/UBookIt.Web/ubookit.css`. It SHALL style layout, spacing and arrangement,
and SHALL NOT declare a literal colour value for any property. **Colour SHALL inherit
from the host page**, so that the flow takes on the surrounding site's appearance with
no configuration at all.

Typography SHALL inherit **except for two named floors**, which are stated as
exceptions rather than left to be discovered in the file: a `line-height` of 1.5, and
`font-weight` where it distinguishes a field's label or an error from body text. The
line-height floor overrides a host value below it *and above it*, so a site wanting
pure inheritance sets `--ubookit-line-height: inherit`. Both floors are legibility
minima that cost the host nothing to concede; neither is an appearance choice, and
nothing else in the file overrides inherited type.

Emphasis that would conventionally be carried by colour — an error, a notice — SHALL
be carried by properties that cannot clash with an unknown host: `currentColor`, border
weight and font weight. A colour **derived** from `currentColor` MAY carry decoration,
and SHALL NOT carry text. This is not austerity for its own sake: a colour the package
picks sits on a background the package has never seen, so its contrast is not computable
and any claim about it would be unfounded — and a derivation is a choice too, since
compositing toward transparency reduces contrast rather than preserving it.

Native form controls — `input[type=date]`, `select`, `button` — SHALL NOT be restyled.
Platform-rendered controls are accessible by construction, respect the user's own
preferences, and behave correctly in forced-colors mode; restyling them is the most
common way a booking UI loses its accessibility. The sole exception SHALL be a
minimum target size, which is a floor rather than an appearance.

The stylesheet SHALL be emitted by **one documented partial**, so that a consuming
site opts in with a single line in its layout and the package controls `<head>`
placement and cascade order. That partial SHALL be the only route by which the
package emits its own styling, so that a future theme can replace what it emits
without the site changing anything.

A site that adds nothing SHALL render exactly as it did before this change.

#### Scenario: The asset is served
- **WHEN** a site with the package installed requests `_content/UBookIt.Web/ubookit.css`
- **THEN** the stylesheet is returned with a CSS content type

#### Scenario: No colour is decided by the package
- **WHEN** the shipped stylesheet is inspected
- **THEN** it declares no literal colour value; every text colour resolves to what the host already chose; and the only derived colours are border colours the site may override

#### Scenario: Native controls keep their platform appearance
- **WHEN** the shipped stylesheet is inspected
- **THEN** it applies no appearance property to a date input, a select or a button, other than a minimum target size

#### Scenario: Opting in is one line
- **WHEN** a site adds the documented partial to its layout
- **THEN** the stylesheet is linked in `<head>`, before any stylesheet the site links afterwards

#### Scenario: Opting out changes nothing
- **WHEN** a site does not add the partial
- **THEN** every flow renders exactly the markup it rendered before this change, with no styling and no broken reference

#### Scenario: There is one emission route
- **WHEN** the package's views and stylesheet are inspected
- **THEN** no view emits a `link` or `style` element for package styling other than through that one partial

### Requirement: A site can override any token from anywhere in the cascade
The package SHALL publish a named set of design tokens as its appearance-override
contract, covering layout and spacing, typography, and the derived colours described
by the stylesheet requirement. Each token's default SHALL be expressed **as a
fallback at the point of use** — `var(--ubookit-x, <default>)` — and the package
SHALL NOT declare a token's default value on any element it renders.

This is a mechanism requirement rather than a style preference, and it is the whole
of what "a site's tokens drop over our defaults cleanly" means. Custom properties
inherit, so a default declared on a package wrapper is a declaration on an element
closer to the control than the site's `:root`, and it therefore **wins**. A site
setting the documented token would then see no effect, with nothing anywhere
reporting a problem — the contract would be false at the mechanism level while every
individual declaration looked correct.

A token SHALL be overridable from `:root`, since that is where a consuming site will
write it, and overriding it SHALL require no selector that names a package class and
no `!important`.

#### Scenario: A token set at the document root takes effect
- **WHEN** a site declares a documented token on `:root` and a flow is rendered with the shipped stylesheet
- **THEN** the rendered appearance uses the site's value

#### Scenario: A default declared on a package element fails the check
- **WHEN** the stylesheet declares a documented token's default on any element the package renders, rather than as a use-site fallback
- **THEN** the check fails, naming the token — because such a declaration silently defeats every override a site can write

#### Scenario: Overriding needs no package selector
- **WHEN** a site overrides every documented token
- **THEN** it does so without naming a package class and without `!important`

#### Scenario: Every documented token is used
- **WHEN** the published token list is compared against the stylesheet
- **THEN** every documented token is read by at least one declaration, and no token is documented that nothing reads

### Requirement: The styling contract is a stable class vocabulary
The classes the views render SHALL be a published, stable contract, because they are
the surface a consuming site writes CSS against. They SHALL follow one naming rule:
`ubookit-<block>` for a block, `ubookit-<block>-<part>` for a part of one, and
`ubookit-<block>--<variant>` for a variant of one. A variant SHALL be expressed with
the variant separator rather than as a differently-named block, so that a reader can
tell a variant from a sibling.

Every region a site would reasonably style SHALL carry a class hook. In particular a
field — a label, its control, and that control's messages — SHALL carry one, since it
is the unit both the package's own layout and a site's overrides operate on; and every
submit control SHALL carry one.

**Ids SHALL NOT be used as styling hooks and SHALL NOT be renamed by this change.**
The ids the views render are the accessibility contract — `aria-describedby` and
`aria-labelledby` targets, and the targets the error summary's in-page links resolve
to. They are governed by the requirements about resolving references and accessible
failure handling, and they are not appearance. Conflating the two would put a
guarantee about screen-reader behaviour at the mercy of a restyle.

#### Scenario: The vocabulary follows the rule
- **WHEN** the classes rendered by the views are inspected
- **THEN** each is a block, a part of a named block, or a variant expressed with the variant separator

#### Scenario: A variant is not a sibling block
- **WHEN** a flow that is a variant of the booking block is rendered
- **THEN** it carries the block's class and the variant's class, and not a separately-named block

#### Scenario: Every field and every submit control has a hook
- **WHEN** any flow is rendered
- **THEN** each label-and-control group carries the field class and each submit control carries the submit class

#### Scenario: No id is repurposed or renamed
- **WHEN** the ids rendered by the views are compared against those rendered before this change
- **THEN** they are unchanged, and no stylesheet declaration selects on any of them
