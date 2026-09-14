# uBookIt version roadmap

How uBookIt got from `0.1.0` (the MVP) to its first full release. **Every row is now
delivered** — this is the record of that plan, not a list of outstanding work.

> **Read this whole file — the table included — as a record of the plan as it stood**, not as
> a description of the package today. The one exception is the version line directly above:
> that states uBookIt's current version and is machine-checked against
> `Directory.Build.props` (`VersionTruthTests`), so it tracks the build rather than the plan.
> Every row below was written *before* the work in it existed,
> and every section below argued for a version that has since shipped, sometimes shipping
> something better than the argument anticipated. Where a sentence describes behaviour a later
> change replaced, the correction is noted inline — but **the current truth is
> [the README](../README.md) and [the docs](../docs/), never this file.**

**The first release is `17.0.0`, not `1.0.0`** — the major tracks the Umbraco major, as Umbraco
packages conventionally do, so that the uBookIt for Umbraco 18 is 18.x and nobody has to
remember which uBookIt went with which CMS.

*Reviewed against the code on 2026-09-03, then reordered and re-scoped after a second pass.
Rows marked **already shipped** or **partly shipped** were checked, not assumed.*

**Ordering principle: everything that protects people's data comes before anything that sends
it anywhere.** The first draft emailed booker names and addresses to an internal list at 0.3.0
and only restricted who could see PII at 0.9.0 — six versions during which the package's largest
new flow of personal data existed without the control over it. Nothing had shipped publicly, so
the reorder cost nothing.

## The plan

| Version | Feature | Notes |
|:--------|:--------|:------|
| 0.2.0 | Use the Sensitive Data group to hide PII | Umbraco ships a built-in **Sensitive Data** user group. uBookIt reads it and redacts booker names and email addresses from anyone not in it. Small, cheap, and it puts the control in place before anything else moves personal data around. |
| 0.3.0 | Data protection: retention, erasure, privacy notice | **Model-affecting, so it belongs early** — erasure must mean *anonymise*, not delete, because an erased booking still occupies its slot and still has to be discussable. That is exactly why the booking reference was built to outlive the person (see the archived `booking-reference` change). Plus a retention policy, and a privacy notice on the booking form. **Not a consent tickbox** — see "On consent" below. |
| 0.4.0 | **Show which dates have availability** | **A front-end feature — the API is already built.** At the time the flow was `<input type="date">` with a min/max range: a visitor picked a date blind and only then learned whether anything was free. Instead, show the days that actually have availability over the next ~30 days. *(Shipped: the step now lists dates that have availability — `default-frontend`, "The step lists dates that have availability".)* A date picker cannot express this; it wants a list of dates. Brought forward because it depends on nothing else and is the most visible improvement on the list. |
| 0.5.0 | Confirmation / cancellation emails | Via `IEmailSender`, Umbraco's own mail abstraction (confirmed present in 17). To the booker and to an internal list. The notifications this hangs off already exist and already carry the whole `Booking`, reference included — nothing new is needed in the domain. **Configured in `appsettings` at this stage, deliberately**: a settings *screen* should be admin-only, which needed permissions that did not exist yet, so config-file settings kept this change small and removed the dependency. *(Those permissions arrived in 0.10.0; the settings screen itself is 17.1.0 work.)* |
| 0.6.0 | Approval and decline | An `AutoConfirm` option **defaulting to on**, so the default is exactly today's behaviour. When off, placement produces `Requested`; an operator confirms or declines from the backoffice, and the booker is told either way through 0.5.0's email path — which already derives its wording from `Booking.Status` in anticipation of exactly this. `Booking.Confirm()` and `Decline()` exist and are tested; the change builds the routes into them. **Slotted before templates deliberately**: approval adds new message types, and the template scheme should be designed against the complete catalogue rather than retrofitted. *(Decided 2026-09-11 — previously "Not yet slotted".)* |
| 0.7.0 | Email templates — **developer-facing** | A site supplies its own message content as **Razor partials at a convention path**, with an RCL able to supply them too, following Umbraco Forms' shape rather than core's (core has no email templating at all — see below). **Built into this package, not a separate one** (decided 2026-09-11). **Caveat: email HTML is not web HTML** (inline styles, tables, no external stylesheet), so this is a separate rendering path, not a reuse of the theming mechanism. **Editors editing wording in the backoffice is explicitly NOT in this row** — see "Not yet slotted". |
| 0.8.0 | Resource / service responsibility | Who is responsible for which resource or service, by backoffice user or group — or by groups a site defines itself, as Umbraco Workflow does. **Not permissions: this decides who gets emailed** about what, and supersedes 0.5.0's flat list. Pairs with 0.6.0: who gets emailed about a pending booking is the same question as who must act on it. |
| 0.9.0 | Control the delivery API's exposure | **Reframed from "throttling"** — see "On the public API" below. Chiefly: let a site turn the delivery API **off**, since it was then registered on every install whether used or not; document that it is anonymous and belongs behind the host's own rate limiting; and consider a per-caller cap on the expensive availability queries. **API keys are out of scope.** |
| 0.10.0 | Permissions model *(was gated on a spike)* | Who may create resources and services, who may view and cancel bookings — via Umbraco user groups. **Partly shipped at the time**: the whole section could be hidden by group; what was missing was granularity *within* it. **Do a spike first** against the Umbraco source in `ref/` to establish what the backoffice actually permits. If it proved impractical, this would wait until after 17.0.0, where it would be purely additive and so allowed by the release policy. The admin-only **settings screen** was to ride with this. *(Shipped: the spike came back feasible — see Open decision 2, CLOSED — and three verbs now refine the section grant (`permissions` spec). The settings screen did **not** ride with it; it is 17.1.0 work.)* |
| 17.0.0 | First full release ✅ | **Complete.** Every row above is delivered and the release is prepared and versioned; the compatibility promise applies from this version, and takes public effect when the package reaches a feed. **Policy re-settled 2026-09-14, superseding "adopt Umbraco's own schedule":** a package's changes are far less impactful than a whole CMS's and the install base is far smaller, so uBookIt keeps its own rule — the public interface stays as consistent as possible, and a breaking change, where genuinely required, lands in a **minor** (`17.x.0`) and never a patch, with sensible defaults or an upgrade path. Parallel API versions are deliberately avoided while the product is young. Stated in the README, which also names the departure from SemVer. |

## On consent

**My earlier wording reintroduced something we had already settled, and it was wrong to.**

The original discussion: an "I agree to my data being stored" tickbox on the booking form. That
is the wrong instrument, because the lawful basis for holding a booker's name and email is
**performance of a contract** — you cannot deliver a booking without them — and consent that
cannot be refused is not consent. Worse, if consent *were* the basis, withdrawing it would
oblige erasure, which would make the site's own booking records revocable on request.

Listing "consent" in 0.3.0 muddied that. What 0.3.0 actually needs is:

- **A privacy notice** on the booking form — what is collected, why, how long it is kept, who
  sees it. Required whatever the lawful basis, and it is not a tickbox.
- **Optional marketing consent**, *only* if a site wants to contact bookers about anything other
  than their booking. That is a genuinely free choice and so a genuine consent — and it is
  separable, so it need not be in 0.3.0 at all.

A configurable "require an explicit tickbox" option is worth supporting anyway, because some
site owners' legal advice will insist on one. Support it; do not make it the default.

## On the public API

The concern is *"something finds an unsecured API and hits it with millions of requests"*. Three
honest observations:

**A package cannot prevent a DDoS.** That is edge infrastructure — Cloudflare, Azure Front Door,
a WAF. In-process rate limiting still requires every request to reach the application and be
processed far enough to be rejected, so it protects the *database* rather than the *server*.
Anything we ship should be described as protecting our own expensive work, not as DDoS defence,
or we would be making a promise the code cannot keep.

**Our endpoints do amplify, though, and that part is ours.** The availability query walks the
requested range day by day, so a cheap request produces expensive work — far more so than a
static page. `MaxQueryRangeDays` (default **31**) already caps the worst case per request, which
is the main lever and it is already in place.

**The cheapest and largest lever is not throttling at all: it is that the delivery API was on by
default.** `UBookItDeliveryApiComposer` registered it unconditionally, so a site using only the
Razor front end still exposed anonymous availability and placement endpoints it never asked for
and gained nothing from. **An opt-out removes the surface entirely** for those sites, which no
amount of rate limiting can match. That, plus documenting the API as anonymous and expecting the
host's rate limiter in front of it, was the honest shape of 0.9.0.

> **What 0.9.0 actually shipped, since the paragraph above is the reasoning that led to it and
> not a description of the package today.** The API is now **off by default in both
> directions** (`UBookIt:DeliveryApi:EnableReads` / `EnablePlacement`), and a disabled
> direction is *absent* rather than refused — the host's own 404, indistinguishable from a
> route that never existed. See [the delivery API docs](../docs/delivery-api.md).

## What is already built

Checked against the code, so nobody rebuilds it.

- **The availability API 0.4.0 needs already exists.** `GET /services/{id}/bookable-starts?from=&to=`
  returns bookable start times across a date range, the zone they are expressed in, and — when
  the list is empty — a structural reason why. Equivalents exist for a single resource
  (`/resources/{id}/bookable-starts`, `/free-time`, `/slots`). **0.4.0 is the Razor front end
  consuming what is already there.**
- **Section-level access control (part of 0.10.0).** A user group is granted the **uBookIt
  Section**, and that single grant governs both whether the section appears *and* whether that
  user may call the management API. Access to another section does not grant uBookIt. Documented
  in `docs/backoffice.md`. What remains is finer grain — e.g. view bookings but not configure
  resources.

## Two mechanisms worth knowing before starting

**0.2.0's group is membership, not a flag.** `IUser.HasAccessToSensitiveData()` tests membership
of a built-in group with a fixed key. We cannot create, rename or reconfigure it — but we do not
need to: the instruction to a site owner is one sentence, *"add anyone who should see booker
contact details to the Sensitive Data group"*, rather than ticking something on every user
record. Since Umbraco provides it out of the box, using it is the right call. Our work is
reading it, redacting **server-side**, and documenting that sentence.

**0.4.0 must work without JavaScript.** The shipped front end is server-rendered and operable
with JS disabled, and that is a stated invariant. Fortunately a list of available dates is
*easier* to do accessibly than a date picker — a set of links or radios, one per day, no widget.

Two costs to design around: a 30-day availability query walks the range day by day, which is
exactly the expense `MaxQueryRangeDays` was added to bound — so the intended window fits inside
the existing guardrail with a day to spare, but it is real work per render and may want caching.
And it is a *second* query on a page that, at the time, only listed times.

## Not yet slotted

Wanted, and currently on no version. Recorded so they are decisions rather than omissions.

*(Approval/decline was here until 2026-09-11; it is now slotted as **0.6.0** — see the table.
The working notes that lived in this section, kept because the change will need them:
`Booking.Confirm()` and `Booking.Decline()` exist, are covered by `StatusMachineTests`, and both
transition **from `Requested`** — but nothing produces `Requested`, because `BookingService`
hard-codes `Confirmed` at placement. The status machine is done; every route into it is missing.
What the change actually needs: the setting, placement branching on it, service operations,
management API endpoints, a backoffice affordance, and the messages. 0.5.0 already anticipates
it: the email wording is derived from `Booking.Status` rather than hard-coded, precisely so that
adding `Requested` as a reachable state cannot silently turn "your booking is confirmed" into a
lie in a customer's inbox. Decline — previously its own bullet here — rides with it: confirm and
cancel both get an email, and decline is the third thing a customer needs telling about.)*

- **Editors editing email wording in the backoffice.** Deliberately **after 17.0.0**, decided
  with the 0.7.0 scope on 2026-09-11. Two reasons: it needs the admin-only settings screen and
  the permissions work that is itself gated on a spike (0.10.0); and **styling is not catered
  for by the editor route at all** — email HTML wants inline styles and table layout, which an
  RTE cannot produce sanely, so the developer route is the primary one rather than the fallback.
  Most people installing this are developers.

  **One constraint it places on 0.7.0, though.** 17.0.0 starts the compatibility promise, and
  the developer-facing feature publishes a model type, a discovery convention and a registration
  API that the promise then freezes. So 0.7.0 should design its **model as a token vocabulary a
  later editor UI could expose**, rather than as whatever the shipped Razor views happen to
  need — and should not hard-code "a template is the only possible source of content", so that a
  stored body could later take precedence the way a theme takes precedence over a shipped view.
  That is a design constraint on 0.7.0, not scope added to it.

- **Self-service cancellation by booking reference** (raised by Chris, 2026-09-11). A booker
  cancels their own booking without an operator, identified by the reference they were given.
  **The reference was built for exactly this** — short, unambiguous read aloud, stable for the
  life of the booking, and it already survives erasure.

  *The prompt was consumer law requiring cancellation to be as easy as sign-up. Read honestly:
  in the UK that is the Digital Markets, Competition and Consumers Act 2024, and it is aimed at
  **subscription contracts** rather than one-off bookings, so this is probably "not obliged,
  plausibly still wanted" rather than a compliance obligation. Do not let the roadmap imply
  otherwise — a package claiming to deliver compliance it has not verified is the kind of
  statement this project has twice had to retract.*

  **The design question is authentication, and it is the whole feature**: a reference alone is a
  bearer token, and the addressable space is 27⁸. Guessing one and cancelling a stranger's
  booking must be infeasible — so this needs a rate-limited lookup at minimum, and more likely a
  link mailed to the address on the booking, which makes it depend on emails being configured.
  That dependency is the reason it is not trivially cheap.

  Two things it would make better rather than worse: `InternalCancelled` becomes genuinely
  informative, since the cancellation would no longer be something the site just did; and it
  pairs with 0.9.0's delivery-API exposure work, because it would be a new anonymous endpoint.

- **Move / amend a booking's time.** Currently a cancellation and a new booking, which loses the
  reference the customer is holding.
- **Descriptions in the UI.** `Resource` has a `Description` and the delivery API returns it;
  **`Service` has no description at all, and no shipped view renders either** — verified.
- **Show a resource's capacity** so a booker can pick something big enough. Display only — not
  the same as letting one resource hold several bookings at once, which was considered and
  deliberately rejected.

## Open decisions

1. ~~**Does the email work ship as a separate package?**~~ **CLOSED, 2026-09-11: no. It ships
   in this package.** The DevExpress precedent was cited for this and was wrong — DevExpress is
   separate because its licence forbids redistribution, a legal constraint rather than an
   architectural principle, and no licence constrains email HTML.

   What settled it, researched rather than assumed: **Umbraco Forms ships both its email
   templates and its themes inside the Forms package**, and it is the closest comparable in the
   ecosystem. The umbrella `UBookIt` package already references `UBookIt.Web`, so a renderer
   living there is present on every ordinary install. And "install one thing and it works" is a
   real adoption feature for an open-source package.

   **The architectural decision underneath it matters more and is settled too**: `UBookIt.Persistence`
   references Core only and must not gain Razor, since the email path lives there so a headless
   consumer gets emails without the front end. So templating goes behind a **Core-defined port with
   an optional substitute registered elsewhere**, exactly as `IBookingObserver` does — which means
   the renderer is a separately-registered implementation either way, and packaging was never the
   load-bearing question. Full research, including what core actually does and why
   `SendEmailNotification` cannot change wording, is in the agent memory
   `ubookit-email-templates-research`.
2. ~~**Does the permissions spike come back feasible?**~~ **CLOSED, 2026-09-12: yes — granularity
   within the existing section, no section split needed.** Run against the Umbraco 17 source in
   `ref/` and the public docs. The shape, verified end to end:

   - **UI**: an `entityUserPermission` manifest (our existing package manifest, no new UI) with
     uBookIt entity-type and verb strings renders toggles in *Users → User Groups → Default
     permissions*. The CMS's own example targets the dictionary entity, so non-document types
     are supported, and a Skrift article (Warren Buckley) documents the exact pattern.
   - **Storage**: verbs land in `IUserGroup.Permissions` (`ISet<string>`, free strings);
     `UserGroupPresentationFactory` maps `FallbackPermissions` verbatim in both directions —
     no whitelist, so custom verbs round-trip through the group editor.
   - **Server**: our management controllers check current user → groups → union of
     `Permissions` → verb, as an authorization handler layered on the existing section policy.
     The section grant remains the gate; verbs refine within it.
   - **Client**: `ContentPermissionService.FilterFallbackPermissionsAsync` is a pass-through by
     default, so the current-user presentation carries custom verbs and our section UI can
     condition on them.

   The section-split fallback below is therefore NOT needed — and the admin-only settings
   screen rides on a verb (e.g. `UBookIt.Settings`) rather than a second section. **The 0.10.0
   proposal's central open decision is upgrade semantics**: a group holding the section grant
   then had no uBookIt verbs, and existing installs must not lose access on upgrade — so
   either a migration seeds the verbs for section-granted groups, or absence of every uBookIt
   verb reads as legacy full access. Decide there, not here.

   **There is a fallback, so the spike cannot return "impossible" — only "which shape".**
   (Chris, 2026-09-11.) If granularity *within* a section proves impractical, split the section:
   a separate **uBookIt Admin** section with its own dashboard, carrying configuration of
   resources and services, while the existing section keeps day-to-day booking management. The
   grant we already have — section access governing both the menu and the management API — then
   does the work unchanged, at the granularity Umbraco actually supports rather than the one we
   wanted. **Umbraco Workflow used exactly this model in v13**, including an option to inherit
   Umbraco user groups, which we probably do not need. Not verified against v17; check when the
   spike runs.

   This reframes the spike from *feasibility* to *choice of mechanism*, and it means the admin-only
   settings screen riding with 0.10.0 has a home either way — which matters, because the
   editor-facing email work deferred past 17.0.0 depends on that screen.
