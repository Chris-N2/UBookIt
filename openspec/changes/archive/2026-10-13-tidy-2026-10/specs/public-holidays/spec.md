## ADDED Requirements

### Requirement: A refused request is reported as a refusal

When the site refuses a preview or an import because of who is asking, the cause is not the
holiday source. The session may have ended, or the operator's permission may have been withdrawn
while the screen was open. The screen SHALL then say that the request was refused, and SHALL NOT
say either of the following:

- **that the source failed**, because that sends somebody to debug a feed that is working;
- **that the site has no source**, because that withdraws a feature the site still has.

The three outcomes stay distinct for the same reason that an empty window and a broken source are
distinct: each one tells the operator to do something different. The refusal message SHALL name
both causes the screen cannot tell apart, an ended session and missing permission, rather than
guess between them.

#### Scenario: A refused preview does not blame the source
- **WHEN** an operator requests a preview and the server refuses it as unauthenticated or forbidden
- **THEN** the screen says the request was refused, and does not say the holiday source failed or could not be reached

#### Scenario: A refused preview does not withdraw the feature
- **WHEN** a preview is refused as unauthenticated or forbidden
- **THEN** the import panel stays on screen, as it does for a source failure, and is not withdrawn as it is when the site has no source

#### Scenario: A refused import is reported as a refusal
- **WHEN** an operator confirms an import and the server refuses it as unauthenticated or forbidden
- **THEN** the screen says the request was refused, rather than only that the closures could not be created, and creates nothing

#### Scenario: A source failure is still reported as one
- **WHEN** a preview fails for any reason other than a refusal or the source's absence
- **THEN** the screen reports that the holiday source failed, exactly as before
