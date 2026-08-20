## ADDED Requirements

### Requirement: Rendered markup resolves its own references
Every view the default front end renders SHALL, in every state its model can
express, produce a document whose internal references resolve: each `label`
carrying a `for` names an element that exists in that document; each
`aria-describedby` and `aria-labelledby` names, for every id it lists, an element
that exists in that document; no id is emitted more than once; and every form
control carries an accessible name, whether from an associated `label`, an
`aria-label`, or an `aria-labelledby` that resolves.

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

#### Scenario: A duplicated id fails
- **WHEN** a view renders the same id on two elements
- **THEN** the check fails, naming the view

#### Scenario: An unnamed control fails
- **WHEN** a view renders an input or select with no associated label, `aria-label`, or resolving `aria-labelledby`
- **THEN** the check fails, naming the view

#### Scenario: A tag helper reaching the page fails
- **WHEN** a view renders a `<partial>` element or an `asp-` attribute as literal output
- **THEN** the check fails, naming the view

#### Scenario: The shipped views satisfy it in every exercised state
- **WHEN** every view in scope is rendered across the model states its own properties can express
- **THEN** every rendered document resolves its references, carries no duplicate id, names every control, and shows no tag-helper residue

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
