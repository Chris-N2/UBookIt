# packaging — delta for company-name

## ADDED Requirements

### Requirement: The package names one publisher, declared once

A package states who published it, and a licence states who grants it. Those names SHALL be
the same name, and SHALL be declared in one place so the others can be checked against it
rather than maintained in parallel.

The publisher name the package carries in its metadata, the name in the licence file, and the
name in the readme SHALL all match the single declaration. Where a file's format requires the
name to be escaped — an ampersand in XML, for example — the comparison SHALL be made against
the decoded value, so an escaping difference is not reported as a disagreement and, more
importantly, is not mistaken for agreement.

This SHALL be verified by a check that derives the expected name from the declaration rather
than restating it, because a restated name is another copy that can drift.

#### Scenario: The licence and readme name the declared publisher

- **WHEN** the publisher name is declared in the build properties
- **THEN** the licence file and the readme state that same name

#### Scenario: A change to the declaration that misses a file fails

- **WHEN** the declared publisher name changes and the licence or readme still states the
  previous one
- **THEN** the check fails and names the file that was missed

#### Scenario: Escaped and literal spellings are the same name

- **WHEN** the name contains a character the metadata format must escape
- **THEN** the escaped spelling and the plain-text spelling are treated as the same name
