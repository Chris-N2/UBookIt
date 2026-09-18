## Why

A booking, once placed, is unreachable to the person who made it. The delivery API places
bookings and reads availability; there is no read of a booking and no way to cancel one, so every
cancellation is a phone call or an email to the site. This is the last feature of the 17.1.0 set,
and the roadmap records it as the riskiest of the five despite looking the smallest: **the feature
is authentication**, and the package has no authentication primitive today — no token store, no
signed-link machinery, nothing.

The obvious design — "type your reference to cancel" — makes the reference a bearer token over an
alphabet of 27⁸ and turns every refusal into an enumeration oracle. `find-booking` has just spent
three QA rounds making that refusal *honest* for an operator (*"No booking has the reference
ZZZZ-2222"*); pointed at an anonymous visitor, the same sentence is the oracle. So the reference
must stop being the credential.

## What Changes

**The credential is possession of the booker's mailbox, not knowledge of the reference.** The
reference identifies; the mailbox authenticates.

- **A cancellation link rides the confirmation email the package already sends.** There is no
  "email me a link" request step in this change, which means there is **no enumeration oracle and
  no way to make the package send mail to a stranger** — both risks are designed out rather than
  mitigated. The link is minted when a booker message is due, so it exists exactly where a message
  already goes.
- **A stored, hashed, single-use token.** A random value goes into the email; only its SHA-256 is
  stored, against the booking and an expiry. The plaintext exists in the message and nowhere else.
- **The token expires when the booking starts**, derived rather than configured. A cancellation
  link is useless after the thing it cancels has begun.
- **A package-routed landing page.** The `GET` is safe and changes nothing — link prefetchers
  (Outlook Safe Links, corporate scanners) issue a `GET` on every URL in an email, and a `GET` that
  cancelled would let robots cancel bookings. It shows the reference, what was booked and when —
  the package's existing non-PII description of a booking, never the booker's name, address or
  telephone number — and an anti-forgery-protected `POST` performs the cancellation.
- **One sentence for every unusable token** — expired, already used, or never issued — so the
  landing page does not become the oracle the design just closed.
- **A visitor may not cancel a booking that has started.** An operator may (no-show tidying), so
  this is a difference of *terms*, waived structurally by which entry point is called, exactly as
  `PlacementTerms.Visitor`/`Operator` already works for placement. It is not a flag.
- **One configuration flag**, in the read-only tier. `site-settings` already puts
  `DeliveryApi:EnableReads`/`EnablePlacement` there because the boundary exists "to keep
  irreversible erasure and the package's anonymous exposure out of an operator's reach". An
  anonymous route that cancels bookings is precisely that.
- **BREAKING (declared, lands in this minor):** `IBookingService` gains a cancellation entry point
  carrying visitor terms, a sibling of `CancelAsync` rather than a parameter on it — the same shape
  `PlaceOnBehalfAsync` took in ㊳. A new Core port for the token store is additive.
- The booker's email model gains the cancellation URL, **additively**, on the terms
  `email-templates` already set for the move message's previous interval: absent where there is no
  link, and its absence meaning exactly that.

**The feature cannot work where booker emails are off, and that is mechanical rather than a
policy choice.** The link rides the confirmation message; no message, no vehicle. The trap to
avoid is silence — an administrator who enables cancellation on a site that sends no booker mail
must be told why nothing happens.

## Non-goals

- **Shape (A), "email me a cancellation link" on demand.** Deferred deliberately, with its own
  flag when it arrives, because it is a different exposure: an unauthenticated trigger that sends
  mail on a stranger's say-so, which needs positions on rate limiting and mail-bombing that this
  change does not have to take.
- **A delivery-API endpoint for redemption.** The shipped front end renders from Core in-process
  rather than through the delivery API, so the flow is complete without one, and a package-served
  landing page works even for a site that placed the booking headlessly. A headless site that wants
  to own the cancellation screen needs the endpoint; that is a later, additive change.
- **Cancellation windows** ("no cancelling within 24 hours"), which the roadmap already holds for
  a later minor. This change ships one rule — not after it has started — and no configuration.

  *Considered and deferred during the proposal, with the reasoning kept because the idea is a good
  one:* deriving the cut-off from the booking's `LeadTime` — *"if you need that much notice to book
  it you should need at least that much to cancel it"* — is elegant, needs no new setting and varies
  correctly per service. It was set aside because it **is** a cancellation window rather than a
  coherence rule, and this change has no vocabulary for what happens below the line; because it
  would overload one value with two policies a site may want set differently; and because
  *preventing* a late cancellation converts it into a no-show, which is worse for the site than
  being told. **The shape the later feature should take is a separate setting that defaults to
  `LeadTime`** — some sites will want more notice to cancel than to book, and some less.
- **Rate limiting.** Without a request step there is nothing to flood: the token is unguessable and
  redemption discloses nothing. Shape (A) is where this becomes load-bearing.
- **Amending a booking**, self-service. Cancel only.
- **Styling and theming of the cancellation pages.** They are standalone documents the package
  serves itself — `Layout = null`, no stylesheet, not composed into a site's page — so **nothing a
  site writes can reach them**: no host layout, no cascade, no token override. Their class
  attributes are internal hooks, deliberately **not** added to the published class vocabulary, and
  they sit outside the theme-view set (`Views/Cancellation/`, not
  `Views/Shared/UBookIt/Themes/`), so a theme RCL cannot supply them either.

  *Recorded as a removal rather than left silent.* The classes were briefly published as a new
  block under the claim that "a site styling the flow can style these too", which QA established was
  false — the same shape as the `UnmetDependency` defect this change already caught itself on: a
  statement describing something the site does not have. Giving these pages a route to CSS is a
  real feature with a real design question behind it (whose stylesheet, and how does a package-served
  page reach it?), and it is not this change's.

  What does **not** narrow is the part that matters: the markup is semantic and operable **with no
  stylesheet at all**, which is the first bullet of invariant 5 and the clause the whole narrowing
  rests on.

## Capabilities

### New Capabilities
- `self-service-cancellation`: the token's lifecycle (mint, expiry, single use), what the landing
  page may show and must not, the safe-`GET`/acting-`POST` split, the one sentence every unusable
  token gets, the visitor's time rule, and what the flag turns off.

### Modified Capabilities
- `booking-emails`: the message to a booker carries a cancellation link where one exists — and,
  like the rest of that message's content, the obligation narrows where a site supplies its own.
- `email-templates`: the booker's published model gains the cancellation URL, additively, with
  absence meaning "no link for this message".
- `site-settings`: the tier enumeration gains the new setting, in the read-only tier, and shown as
  requiring a restart.
- `persistence`: cancellation tokens are stored in their own additive table — the shape
  responsibility assignments, the one-shot flag table and stored settings already follow.
- `booker-erasure`: what erasure does **not** reach gains the outstanding cancellation link, which
  carries no contact detail and survives for the same reason the booking does.
- `bookings`: the enumeration of Core's booking-service entry points gains the visitor-terms
  cancellation. Widened rather than dropped, for the third time — `move-booking` and
  `booking-on-behalf` each appended when they added one, and a sentence that has been the record of
  Core's surface for two changes stops being that record the moment one addition skips it.

## Impact

**Code.** `UBookIt.Core` — a visitor-terms cancellation entry point, a token port, the token value
and its rules. `UBookIt.Persistence` — one additive migration and table, and the store. `UBookIt.Web`
— the landing page route, its two views and the anti-forgery `POST`; the link builder, on the shape
`BackofficeBookingLink` already uses to resolve an absolute URL. The booker message model and the
shipped booker templates.

**Public API.** One declared BREAKING addition to `IBookingService`, carrying the reason and landing
in a minor per the versioning policy. The email model addition is additive and frozen-compatible.

**Data.** One new table. No destructive schema change. Nothing is written to it unless the feature
is on.

**Documentation.** `docs/` gains the flag, its dependency on booker emails, and the statement that
turning the flag off eventually strands anyone still holding a link — the fallback being the
pre-feature status quo of contacting the site.

**Not affected, stated rather than left silent.** `privacy-notice` does not change: the notice is
written against whether the site emails the booker at all, which is already true when a link can
exist, and a cancellation link is the same processing for the same purpose. `booking-retention` does
not change: its sweep is deliberately scoped to find bookings by time alone and never handle a
contact detail, and token housekeeping does not belong inside that guarantee. `default-frontend`
does not change: its obligations on shipped views — every state a model can express, every branch
reachable, every view exercised, markup resolving its own references — are written as universals
and bind the new views without amendment.
