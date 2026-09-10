## Context

The package has three tiers of customisation and none of them is *change these words*:

```
  look     ── stylesheet + tokens ── a site editor can do this
  markup   ── a theme (an RCL)    ── a site needs a developer
  words    ── nothing             ── does not exist
```

That gap is what makes this change interesting, because a privacy notice is the first thing a
site owner's **lawyer** will want to reword. And the constraint is hard, not a preference:
**precompiled views cannot be replaced.** Measured during the theming work — a site cannot drop
its own `_PrivacyNotice.cshtml` into `Views/Shared/UBookIt/` and have it win, in either
compilation mode. The only tier that can replace our markup is a theme.

So the design cannot be "ship default prose and let sites edit it". It has to answer the wording
question some other way.

Two things already built do most of the work, and neither needs extending:

- **`IBookingFormView` is a published compatibility contract**, and `theming` requires that a
  theme receive the same strongly-typed view models the shipped views receive. Anything put there
  reaches every theme automatically.
- **`_YourDetails.cshtml` is a published building block** a theme may call. A theme that reuses
  it gets whatever it renders; a theme that replaces it owns the omission, under the narrowing
  `theming` already states ("a theme that renders no form renders no booking").

## Goals / Non-Goals

**Goals:**

- A notice that cannot state a retention period the code does not keep.
- A notice that says something true on a default install, where retention is off.
- No new customisation tier, no new theming machinery, no new accessibility claim.
- A headless consumer able to state the retention period accurately.

**Non-Goals:**

- Localisation, a tickbox, marketing consent, or hosting the site's policy. See the proposal.
- Enforcing that a themed site renders the notice. See D5.

## Decisions

### D1. The package writes only sentences it can keep true; the site supplies its policy by link

The notice is composed of two parts with different owners:

| Part | Who owns it | Why |
|:--|:--|:--|
| What is collected, why, how long, who can see it | **The package** | It is the only party that knows. All four are facts about code that already exists. |
| Everything else a privacy policy contains | **The site**, by link | Jurisdiction, controller identity, complaints, other processing — none of it is uBookIt's to know or to guess. |

`UBookIt:PrivacyPolicyUrl` is optional and holds a URL. One value, trivially authorable, no HTML
in JSON, and nothing for a site to keep in step with the code.

*Alternative considered — one config key holding the whole notice.* Rejected: it defeats the
reason this change follows retention. A site could then publish "we keep your details 30 days"
while `RetentionDays` is 90, and the package would render the lie faithfully. The whole point of
sequencing ④ after ③ was that the notice reads the period.

*Alternative considered — a content node picked by GUID.* Rejected for now: native authoring and
rich text are genuinely better, but it wants a picker, and a picker wants the admin settings
screen that is roadmap 0.9.0. A GUID in `appsettings` is worse than a URL until then. Revisit
with the settings screen.

### D2. The retention sentence is derived, and says something true when there is no period

`SiteBookingSettings.RetentionDays` is `int?` — and it is `int?` rather than `0`-means-off
*precisely so this change could tell the two apart*, which change ③ recorded at the time.

```
  RetentionDays = 90   →  "…kept for 90 days after your booking, then permanently removed."
  RetentionDays = null →  "…kept until we remove them. No automatic removal period is set."
```

**The null case is stated, not omitted.** Retention is off by default, so the omission would be
the *usual* rendering — a notice missing one of the elements it exists to provide, on most
installs, invisibly. Saying it plainly is true, and it nudges a site toward configuring a period
without the package refusing to work.

**It is derived at render time from settings, never authored and never cached.** A site that
changes the period and restarts gets a notice that changed with it, because there is no second
copy to fall out of step.

### D3. Structured data on the view model, not a pre-rendered string

`IBookingFormView` gains a `PrivacyNotice` whose members are the *facts* — the retention period,
whether one is set, the policy URL — rather than a sentence or a block of HTML.

A pre-rendered string would be easier and is wrong twice: a theme could then only print our
English or discard it wholesale, and the front-end contract invariant says what we publish is
data, never markup. Structured members let a theme render the same facts in its own words, its
own markup and eventually its own language.

The shipped partial turns those facts into sentences. That is a rendering concern and it lives in
the view, where every other English string the package ships already lives.

### D4. The delivery API publishes the number, not the notice

`GET {route}/privacy` returning the retention period, under the existing versioned route.

A headless consumer builds its own page and writes its own privacy wording — it does not want our
sentences, and shipping English prose into a data contract would make that prose a compatibility
promise we would have to localise later. But such a consumer **cannot know how long uBookIt keeps
the data**, and that is the one fact it cannot obtain any other way. Publishing the number closes
exactly that gap and nothing more.

**A site-wide fact gets its own endpoint rather than riding on the resource or service read
models.** Retention is not a property of a resource; putting it on `ResourceReadModel` would
duplicate one site-wide value across every row and invite a reader to think it varies.

*It is anonymous, like every delivery endpoint, and it discloses nothing sensitive:* a retention
period is a policy a site publishes to visitors on purpose. It is the same value the Razor front
end already prints on a public page.

### D5. A theme that drops the notice owns that, and the docs say so

`theming` already narrows every markup-determined guarantee to the views the package ships, with
the reasoning that the package does not take responsibility for code it did not write, and it
already extends that to *behaviour a view's markup determines* — "a theme that renders no form
renders no booking".

A theme that renders no notice shows no notice. That follows from the existing rule; restating it
as a new obligation is exactly the per-surface repetition `theming` forbids.

**But it gets a documentation sentence it would not otherwise get**, because the consequence is
different in kind. A theme that drops the time-picker produces a site that visibly does not work.
A theme that drops the privacy notice produces a site that works perfectly and is quietly less
compliant than its owner believes. Nobody files a bug for that.

*Alternative considered — detect it and warn at boot.* Rejected as undeliverable: themed views
are precompiled into another assembly, so the only way to know whether a theme rendered the notice
is to inspect rendered output, and a boot-time check cannot. A guard that cannot observe what it
claims is this project's most-repeated defect; better to state the limit than to fake the check.

### D6. Where it renders, and how it is announced

Inside the details fieldset, in reading order **before the submit button** — so the person reads
what happens to their data before they hand it over, in the DOM order a screen reader follows.

It is informative prose, not a control: no label, no validation, nothing to interact with except
the policy link when configured. The fieldset already groups the fields it describes, so the
notice is associated with them by containment and reading order rather than by wiring `aria`
relationships a plain paragraph does not need.

**Both states of the model must render** — with a policy URL and without, with a period and
without. `default-frontend` already requires that a view renders every state its model can
express, and that requirement now covers four combinations rather than none.

## Risks / Trade-offs

**The package asserts a legal-ish fact on a site's public page, in the package's words.** → The
sentences are confined to what the code does, and every one of them is checkable against code in
this repository. Anything requiring judgement — lawful basis, controller identity, jurisdiction —
is the site's policy, reached by link. The notice describes behaviour; it does not give advice.

**A site owner reads the notice as legal advice.** → Documented as what it is: a factual
statement about what the package does with the data it collects, not a privacy policy and not a
substitute for one. The docs say so in those words.

**English-only prose on a public page.** → Consistent with every other string the package renders,
and the structured view model means a theme can already replace it with any language. Called out
as a package-wide gap rather than pretended away.

**Adding a member to `IBookingFormView`.** → Declared as breaking, per `theming`'s own
compatibility promise. Low real impact: themes consume the model rather than implement it.

**The notice and the retention job disagree.** → They cannot: both read
`SiteBookingSettings.RetentionDays`, which is resolved once at startup from one key. There is no
second copy, and the test for this asserts they agree rather than asserting each separately.

## Migration Plan

None. No schema change, no migration, no data movement. A site that configures nothing gets the
notice with the no-period sentence and no policy link.

## Open Questions

None blocking. Two recorded:

- **A content-node source for the notice**, revisited when the admin settings screen lands at
  0.9.0 and there is somewhere to put a picker.
- **Package-wide localisation.** The notice makes the gap more visible than the field labels do,
  because a privacy statement in the wrong language is worse than a button in the wrong language.
  Not this change.
