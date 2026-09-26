## ADDED Requirements

### Requirement: A value longer than the store holds is refused, not attempted

The package SHALL refuse a submitted setting value if **any value it would store** is longer than
the settings store can hold. This applies to **every** setting, whatever its type, and the refusal
SHALL happen before any write to the store is attempted.

What is measured is what would be stored, not the submitted text:

- a setting stored as one value is measured whole;
- a setting stored as a list, one stored value per entry, is measured **per entry**. A list is
  never refused for its total length when every entry fits.

The refusal SHALL be reported as an invalid value against the setting it concerns, in the same
form as any other validation failure, and SHALL state the limit.

The limit SHALL be the store's own capacity, taken from the same definition the store uses, so the
two cannot disagree. A value exactly at the limit SHALL be accepted and stored intact.

This requirement narrows what validation accepts, and nothing else: it refuses only what the store
could not have held. A value already in the store is not re-examined by it.

#### Scenario: An over-long value is refused before the store is reached
- **WHEN** a value one character longer than the store's capacity is submitted for a setting stored
  as one value, and the setting's type would otherwise accept it
- **THEN** the write is refused as an invalid value, the failure names the setting and states the
  limit, and no value is stored

#### Scenario: A value at capacity is stored intact
- **WHEN** a value exactly as long as the store's capacity, and otherwise valid, is submitted for a
  setting stored as one value
- **THEN** it is stored, and reading the setting back returns the same value, character for
  character

#### Scenario: The limit applies to every setting type
- **WHEN** an over-long value is submitted for a free-text setting, which has no other constraint
- **THEN** it is refused exactly as in the first scenario

#### Scenario: A list is measured per entry, not in total
- **WHEN** a list is submitted whose combined length exceeds the store's capacity, but whose every
  entry fits
- **THEN** it is accepted and stored, exactly as it would have been before this requirement

#### Scenario: A list with one over-long entry is refused
- **WHEN** a list is submitted in which one entry is longer than the store's capacity
- **THEN** the write is refused as in the first scenario, and none of the list is stored
