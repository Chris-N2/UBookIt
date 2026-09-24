## ADDED Requirements

### Requirement: A usable policy link is defined once, and means the same on every host

A configured or stored privacy policy link SHALL be usable when, after surrounding whitespace is
removed, it is non-blank, contains no control character and no backslash, and is **either**:

- a **site-relative path**: it begins with exactly one `/` (not `//`); **or**
- an **absolute `http` or `https` URL**.

Every other value SHALL be unusable, and so treated as absent as the requirement *The package
states only what it can keep true, and the site links its own policy* already provides.

**Whether a value is usable SHALL NOT depend on the operating system hosting the site.** A value
beginning with `/` SHALL be judged as a site-relative path and SHALL NOT be interpreted as an
absolute URI of any scheme, so that a platform which reads such a value as a local file path
cannot change the answer.

**The settings screen SHALL accept a value for this setting exactly when the site would use it**,
for any value within the settings store's capacity. Validation on write and resolution remain
separate checks, as `site-settings` requires; they SHALL apply the same definition of usable, so
that the screen neither refuses a value the site would use nor stores one the site would treat as
absent. The screen's refusal SHALL name both accepted forms.

*A value longer than the settings store can hold — currently 2048 characters, the
`uBookItSetting.Value` column — is outside this requirement: the site can use such a value from
configuration, but the screen cannot store it, and today the write fails at the database rather
than being refused. That limit applies to every setting and predates this requirement; it is a
known gap, not yet addressed, rather than something settled here.*

#### Scenario: A site-relative link is used on a Linux host
- **WHEN** the site runs on Linux and the policy link is configured as `/privacy`
- **THEN** the notice links to `/privacy` and nothing is reported as unusable

#### Scenario: A site-relative link is used on a Windows host
- **WHEN** the site runs on Windows and the policy link is configured as `/privacy`
- **THEN** the notice links to `/privacy` and nothing is reported as unusable

#### Scenario: An absolute http or https link is used
- **WHEN** the policy link is `https://example.com/privacy` or `http://example.com/privacy`
- **THEN** the notice links to it

#### Scenario: The refusals are unchanged
- **WHEN** the policy link is blank, a protocol-relative `//host/path`, a path without a leading
  slash, a value containing a control character or a backslash, or a URL of any scheme other than
  `http` or `https`
- **THEN** it is unusable, on every host

#### Scenario: The settings screen accepts a site-relative link
- **WHEN** an editor stores `/privacy` as the policy link through the settings screen
- **THEN** the value is accepted and stored

#### Scenario: The screen and the site agree on every value
- **WHEN** any value within the settings store's capacity is submitted to the settings screen for
  the policy link
- **THEN** the screen accepts it if and only if the site, resolving the same value, would use it as
  the link

#### Scenario: The screen's refusal names both forms
- **WHEN** the settings screen refuses a policy link
- **THEN** the reason given names both an absolute http or https address and a site-relative path
  beginning with a single `/`
