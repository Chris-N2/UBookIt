## ADDED Requirements

### Requirement: Backoffice wording resolves in every backoffice language

Every term the uBookIt backoffice asks for SHALL resolve to wording for a backoffice user of
**any** language, and SHALL NEVER render as its raw term key. Where the user's language has no
uBookIt translation, the package's English wording SHALL be shown. This covers every uBookIt
screen, modal and permission label, including the terms the package adds to Umbraco's own
sections.

The English wording SHALL be registered as Umbraco's fallback, not as one regional variant of
English. A dictionary registered only for one region is loaded only for users of that exact
region, so it leaves every other English variant (for example British English) showing keys as well as
every other language.

The fallback SHALL NOT pre-empt a translation. If a site registers uBookIt wording for a
language or region, a user of that language or region SHALL see that wording rather than the
package's English.

#### Scenario: A user whose language has no uBookIt translation sees English

- **WHEN** a backoffice user whose language is Spanish opens any uBookIt view, modal,
  or the uBookIt permission group in the user-group editor
- **THEN** every label, heading, message and button shows the package's English wording
- **AND** no element shows a term key

#### Scenario: A British English user sees English

- **WHEN** a backoffice user whose language is English (United Kingdom) opens any uBookIt view
- **THEN** every string shows the package's English wording, never a term key

#### Scenario: A US English user is unaffected

- **WHEN** a backoffice user whose language is `en-US` opens any uBookIt view
- **THEN** every string shows the same English wording it showed before this requirement

#### Scenario: A site's own translation takes precedence

- **WHEN** a site registers its own uBookIt wording for `es`
- **AND** a backoffice user whose language is Spanish opens a uBookIt view
- **THEN** the site's Spanish wording is shown for every term it supplies
- **AND** the package's English wording is shown for every term it does not supply

#### Scenario: The registration cannot drift from Umbraco's fallback unnoticed

- **WHEN** the culture the package registers its wording under differs from the fallback
  culture of the Umbraco backoffice the package builds against
- **THEN** the test suite fails, naming both cultures
