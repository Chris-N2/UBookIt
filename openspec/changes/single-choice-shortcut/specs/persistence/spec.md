## ADDED Requirements

### Requirement: The single-choice skip setting resolves to on unless a readable value turns it off

**`UBookIt:SkipSingleChoiceCatalogue` SHALL resolve to on unless a readable value turns it off**,
from whichever source is in effect, stored or configured. An absent value SHALL resolve to on,
silently: it is the default and an ordinary choice. A value that was written and cannot be read as
a boolean SHALL resolve to on **and** be logged as an error at startup identifying the setting, on
`UBookIt:AutoConfirm`'s precedent — the site wrote something and is not getting what it wrote, so
it is told. A value readable as false SHALL be honoured.

The fallback direction is the default, and neither direction is harmful: both misreadings are
visible on the booking page to anyone who looks at it. What makes the error worth logging is that a
site which wrote "off" and still sees its catalogue skipped has no other way to learn why. See the
`default-frontend` capability, which owns the setting's meaning.

#### Scenario: An absent skip setting is on and silent
- **WHEN** neither source carries a `UBookIt:SkipSingleChoiceCatalogue` value
- **THEN** the settings report the skip on and nothing is logged about it

#### Scenario: A malformed skip setting resolves to on and is reported
- **WHEN** a `UBookIt:SkipSingleChoiceCatalogue` value that cannot be read as a boolean is in effect from either source
- **THEN** the settings report the skip on and an error is logged identifying the setting

#### Scenario: An explicit off for the skip is honoured
- **WHEN** a `UBookIt:SkipSingleChoiceCatalogue` value readable as false is in effect from either source
- **THEN** the settings report the skip off
