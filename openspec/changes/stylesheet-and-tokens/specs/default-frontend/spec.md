## MODIFIED Requirements

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

Criteria determined by **CSS rather than markup** — 1.4.3 and 1.4.11 (contrast),
2.4.11 and 2.4.13 (focus appearance), and 2.5.8 (target size) — SHALL be met by the
shipped stylesheet at its default token values, and SHALL become the consuming site's
responsibility for any token that site overrides. The package SHALL NOT claim those
criteria for a rendering it did not style.

This narrowing states a bar that was never held rather than lowering one that was.
The package shipped no stylesheet at all until this change, so those five criteria
were already determined entirely by the consuming site, whatever the requirement
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
- **THEN** contrast, focus appearance and target size are met, and no colour pair the package chose can fail because the package chooses none

#### Scenario: The published statement names its own boundary
- **WHEN** the accessibility statement is published
- **THEN** it names, criterion by criterion, which are met by the shipped default, which pass to the site on a token override, and which are determined by the host page

#### Scenario: The narrowing does not reach the markup clauses
- **WHEN** any clause of this requirement other than the CSS-determined criteria is evaluated
- **THEN** it holds exactly as it did before this change, for all three flows

## ADDED Requirements

### Requirement: A default stylesheet ships, and makes no colour decision
The package SHALL ship a stylesheet as a static web asset served at
`_content/UBookIt.Web/ubookit.css`. It SHALL style layout, spacing and arrangement,
and SHALL NOT declare a literal colour value for any property. Colour and typography
SHALL inherit from the host page, so that the flow takes on the surrounding site's
appearance with no configuration at all.

Emphasis that would conventionally be carried by colour — an error, a notice — SHALL
be carried by properties that cannot clash with an unknown host: `currentColor`,
border weight, font weight, and colours derived from `currentColor`. This is not
austerity for its own sake: a colour the package picks sits on a background the
package has never seen, so its contrast is not computable and any claim about it
would be unfounded.

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
- **THEN** it declares no literal colour value, and every colour it does express derives from `currentColor` or from a token the site may set

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
