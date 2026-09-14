# default-frontend — delta for release-readiness-fixes

## ADDED Requirements

### Requirement: The GET forms preserve configured host-page query parameters

uBookIt is a component inside somebody else's page, and that page's query string is not
uBookIt's to discard. Each shipped GET form (the catalogue and the date-and-length step)
SHALL carry forward, as hidden inputs, the current request's query parameters whose
names appear in a site-configured allow-list (`UBookIt:Frontend:PreservedQueryParameters`)
— matched case-insensitively, every value of a multi-valued parameter kept in order, and
values encoded on render. The allow-list SHALL default to empty, so an unconfigured site
renders exactly what it renders today. uBookIt's own query parameters SHALL never be
preserved by this mechanism, whether listed or not: a hidden input duplicating a live
control's name would submit two values and leave the winner to model binding. Parameters
not on the list SHALL be dropped — preservation of unlisted parameters is declined
because every preserved value is visitor-controlled input reflected into the markup, and
the configured bound is what makes the reflection acceptable.

The redirect that follows a booking submission SHALL carry the same preserved
parameters, so both pages a submission can land on — the confirmation and a failed
submission's redraw — keep them (decided 2026-09-14: a site's parameters may matter
after the redirect, so preservation covers the whole flow, not the GET steps alone).
The same allow-list bounds what reaches the redirect's Location header, and the values
are re-serialised through the flow's one query-building function, never echoed as raw
text.

#### Scenario: A listed parameter survives form submission

- **GIVEN** a site configures `utm_source` in `UBookIt:Frontend:PreservedQueryParameters`
- **AND** the booking page is requested with `?utm_source=newsletter`
- **WHEN** either GET form is rendered
- **THEN** it contains a hidden input named `utm_source` with value `newsletter`, so
  submitting the form keeps the parameter in the resulting URL

#### Scenario: An unconfigured site is unchanged

- **WHEN** the allow-list is absent or empty
- **THEN** neither GET form renders any preservation input, whatever the request's query
  string carries

#### Scenario: An unlisted parameter is dropped

- **GIVEN** a site configures only `utm_source`
- **AND** the request also carries `?session_hint=abc`
- **WHEN** either GET form is rendered
- **THEN** no hidden input named `session_hint` is rendered

#### Scenario: uBookIt's own keys cannot be preserved

- **GIVEN** a site lists a uBookIt query parameter (for example `ubDate`) in the
  allow-list
- **WHEN** either GET form is rendered
- **THEN** no preservation input with that name is rendered, and the exclusion covers
  every uBookIt query key by derivation rather than by a hand-kept list

#### Scenario: A multi-valued parameter round-trips whole

- **GIVEN** `tag` is listed and the request carries `?tag=a&tag=b`
- **WHEN** either GET form is rendered
- **THEN** two hidden inputs named `tag` are rendered with values `a` and `b` in that
  order

#### Scenario: Preserved parameters survive the submission redirect

- **GIVEN** a site lists `utm_source` and the booking page is reached with
  `?utm_source=newsletter`
- **WHEN** the booking form is submitted — whether the submission succeeds or is
  refused
- **THEN** the URL the visitor is redirected to still carries
  `utm_source=newsletter`, alongside the flow's own parameters

#### Scenario: A component-named flow's redirect gains only the preserved parameters

- **GIVEN** a site author named the resource on the component (no flow state in the
  URL) and a listed parameter is present on the request
- **WHEN** the form is submitted
- **THEN** the redirect carries the preserved parameter, and with no listed parameter
  present the redirect is byte-for-byte what it was before this requirement existed

#### Scenario: Preserved values are encoded

- **GIVEN** a listed parameter whose value contains markup-significant characters
- **WHEN** the form is rendered
- **THEN** the value appears HTML-encoded in the hidden input and never as markup
