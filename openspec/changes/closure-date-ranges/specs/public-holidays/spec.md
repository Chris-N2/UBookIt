## MODIFIED Requirements

### Requirement: The preview is a selection, not a confirmation

Before creating anything, the package SHALL present every holiday the source returned for the
requested window, and SHALL let the operator **choose which of them to create**. Confirming SHALL
create exactly the chosen rows and no others. **Each row confirmed SHALL create a closure of that
one day.**

Each row SHALL carry one of three states:

- **New** — no closure covers that date. Selectable, and selected by default.
- **Already closed** — a closure already covers that date, whoever created it and whether it is a
  single day or a range. Not selectable, because no date may be covered by two closures and the
  existing one is not this import's to replace.
- **Cannot import** — the holiday would be rejected by the domain, such as a name longer than a
  label may be. Not selectable, and SHALL be shown **with the reason** rather than omitted.

**Deselection SHALL NOT be remembered.** A later import over a window containing the same date
SHALL offer it again. A remembered refusal is invisible state that diverges from what the source
says, and the operator would have no way to see or revise it.

The preview SHALL create nothing. Asking for it SHALL be safe to repeat.

#### Scenario: Only the selected rows are created
- **WHEN** a preview offers four new holidays and the operator deselects one before confirming
- **THEN** exactly three closures are created, each of one day, and the deselected date has none

#### Scenario: An existing closure is reported, not replaced
- **WHEN** the source returns a holiday for a date that already carries a closure
- **THEN** the row is reported as already closed, it cannot be selected, and the existing closure's dates and label are unchanged after confirming

#### Scenario: A date inside a range is already closed
- **WHEN** the source returns a holiday for a date inside an existing closure's range
- **THEN** the row is reported as already closed, it cannot be selected, and confirming changes nothing about that closure

#### Scenario: A rejected holiday is shown with its reason
- **WHEN** a source returns a holiday whose name is longer than a closure label may be
- **THEN** the row is reported as unimportable with the reason, and confirming creates nothing for it

#### Scenario: A deselection is not remembered
- **WHEN** an operator unticks a date, confirms, and later previews a window containing that date again
- **THEN** the date is offered again as new

#### Scenario: Previewing changes nothing
- **WHEN** a preview is requested twice without confirming
- **THEN** no closure is created by either request, and the second reports the same states as the first
