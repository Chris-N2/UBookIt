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

**Every markup clause in this requirement is a claim about the views the package
ships.** Where a registered theme supplies the view that renders, that view's markup is
the theme author's: the package neither claims conformance for it nor requires any of
the theme. This is the same narrowing already made for the CSS-determined criteria
below, applied to the one category that was previously held unconditionally, on the
same reasoning — the package does not take responsibility for code it did not write.

**That narrowing reaches themed views and nothing else.** For a site with no theme
registered, and for every view a registered theme does not supply, every clause of this
requirement holds exactly as written, unchanged by the existence of theming. In
particular the no-author-stylesheet clause above is untouched and remains load-bearing:
it is what makes both narrowings defensible rather than escapes. The `theming`
capability states the boundary, its reasoning and the obligation on the published
accessibility statement in full.

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
It applies equally to the shipped/themed split added here.

Where a choice is offered as a set of related controls — the start times, and the
catalogue when it is rendered as a chooser — it SHALL be a grouped set with a
`legend` naming what is being chosen, on the same terms as the start times.

#### Scenario: Every control is labelled
- **WHEN** a booking form is rendered by the package's own views, in either flow
- **THEN** each input and select has a programmatically associated `label`

#### Scenario: Start times are a labelled radio group
- **WHEN** available start times are rendered by the package's own views, in either flow
- **THEN** they are radio inputs inside a `fieldset` whose `legend` names the group

#### Scenario: Usable without an author stylesheet
- **WHEN** any flow is rendered by the package's own views with no author CSS applied
- **THEN** the content order is logical and every control remains operable and labelled

#### Scenario: The catalogue meets the same bar
- **WHEN** the catalogue is rendered by the package's own view as a set of choices
- **THEN** it is a grouped set of labelled controls with a `legend` naming what is being chosen, operable by keyboard

#### Scenario: The service flow meets the bar the resource flow meets
- **WHEN** the service flow is rendered by the package's own views
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
- **WHEN** any clause of this requirement other than the CSS-determined criteria is evaluated against a rendering the package itself produced
- **THEN** it holds exactly as it did before the stylesheet change, for all three flows

#### Scenario: A themed view is outside this requirement, and only a themed view
- **WHEN** a registered theme supplies the view that renders
- **THEN** this requirement's markup clauses are not claimed for that view, and they continue to hold unchanged for every view the package itself renders

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
package emits its own styling, so that a theme can replace what it emits
without the site changing anything. **What that partial emits when a theme is active is
governed by the `theming` capability**, which is the mechanism this clause was written
in anticipation of; the single-route obligation is unchanged and applies equally then.

A site that adds nothing SHALL render exactly as it did before the stylesheet change.

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
- **WHEN** a site with no theme active adds the documented partial to its layout
- **THEN** the stylesheet is linked in `<head>`, before any stylesheet the site links afterwards

#### Scenario: Opting out changes nothing
- **WHEN** a site does not add the partial
- **THEN** every flow renders exactly the markup it rendered before the stylesheet change, with no styling and no broken reference

#### Scenario: There is one emission route
- **WHEN** the package's views and stylesheet are inspected, with or without a theme active
- **THEN** no view emits a `link` or `style` element for package styling other than through that one partial

### Requirement: The styling contract is a stable class vocabulary
The classes the package's own views render SHALL be a published, stable contract, because
they are the surface a consuming site writes CSS against. They SHALL follow one naming rule:
`ubookit-<block>` for a block, `ubookit-<block>-<part>` for a part of one, and
`ubookit-<block>--<variant>` for a variant of one. A variant SHALL be expressed with
the variant separator rather than as a differently-named block, so that a reader can
tell a variant from a sibling.

Every region a site would reasonably style SHALL carry a class hook. In particular a
field — a label, its control, and that control's messages — SHALL carry one, since it
is the unit both the package's own layout and a site's overrides operate on; and every
submit control SHALL carry one.

**This vocabulary is a contract over the markup the package renders.** A theme renders its
own markup and is under no obligation to reproduce it; a theme that calls the package's
shared partials as building blocks gets their classes with them. The contract is not
weakened by theming — it continues to describe, exactly and completely, whatever the package
itself renders.

**Ids SHALL NOT be used as styling hooks and SHALL NOT be renamed by the change that
introduced this requirement.** The ids the views render are the accessibility contract —
`aria-describedby` and `aria-labelledby` targets, and the targets the error summary's in-page
links resolve to. They are governed by the requirements about resolving references and
accessible failure handling, and they are not appearance. Conflating the two would put a
guarantee about screen-reader behaviour at the mercy of a restyle.

#### Scenario: The vocabulary follows the rule
- **WHEN** the classes rendered by the package's own views are inspected
- **THEN** each is a block, a part of a named block, or a variant expressed with the variant separator

#### Scenario: A variant is not a sibling block
- **WHEN** a flow that is a variant of the booking block is rendered by the package's own views
- **THEN** it carries the block's class and the variant's class, and not a separately-named block

#### Scenario: Every field and every submit control has a hook
- **WHEN** any flow is rendered by the package's own views
- **THEN** each label-and-control group carries the field class and each submit control carries the submit class

#### Scenario: No id is repurposed or renamed
- **WHEN** the ids rendered by the package's own views are compared against those rendered before the stylesheet change
- **THEN** they are unchanged, and no stylesheet declaration selects on any of them

#### Scenario: A theme is not held to the vocabulary
- **WHEN** a theme renders markup of its own
- **THEN** no rule requires it to carry the package's classes, and the vocabulary continues to hold for every view the package itself renders
