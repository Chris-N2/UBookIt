## ADDED Requirements

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
