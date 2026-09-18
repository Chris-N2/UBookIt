## Context

See `proposal.md` — Why. The constraints that shape the approach, all verified against the tree
rather than assumed:

- **There is no authentication primitive in the package.** No token store, no `IDataProtection` use,
  no one-time-link machinery anywhere in `src/`. This change introduces the first one.
- **The shipped front end renders from Core in-process**, not through the delivery API
  (`default-frontend`: *"Availability rendered from Core in-process"*). The delivery API is a
  parallel surface for headless consumers and is off by default. So a Razor flow needs no delivery
  endpoint to be complete.
- **`BackofficeBookingLink` already resolves an absolute URL** for an email, through Umbraco's
  hosting abstraction, and `BookingMessageModels` already carries a nullable `Uri` for it. The
  public-facing link has the same problem and a proven shape to copy.
- **`Booking.Cancel()` is a status transition** with no time rule: cancellation succeeds from
  `Requested` or `Confirmed` and refuses a second attempt. The operator's route relies on that, and
  this change must not alter it.
- **Erasure nulls the booker's contact** (`Booker.Erased`, `Contact is null`) while keeping the
  booking, and `booking-emails` already requires that an erased booker is never written to.

## Goals / Non-Goals

**Goals:**

- Make possession of the booker's mailbox the credential, and keep the reference an identifier.
- Introduce the token primitive in a way that depends on **nothing the package cannot verify** —
  no host configuration, no key material, no topology assumption.
- Leave every existing cancellation path byte-for-byte unchanged in behaviour.

**Non-Goals (design level, beyond the proposal's scope boundaries):**

- A general-purpose token framework. This token does one thing; a second use case can generalise it
  when it exists, and guessing its shape now would be the abstraction this project rejects at
  propose time.
- Reusing the token as a *read* credential for a booking ("view my booking"). It would work, and it
  is a different feature with a different disclosure question.

## Decisions

### D1 — A stored, hashed secret, not a signed stateless one

**Ratified by Chris 2026-09-18**, on the trade-off rather than on the absence of cost: *"it seems
to come with significant benefits and not too much downside; certainly the trade-off seems
positive."* The costs are real and named below — one additive migration and rows to age out.

**Chosen:** a random value, delivered in the email, stored only as a SHA-256 hash against the
booking with an expiry and a redeemed flag.

**Alternative: ASP.NET Data Protection**, signing `{bookingId, purpose, expiry}` into the URL. No
table, no migration, no cleanup.

**Rejected because it makes correctness depend on host configuration the package cannot see, and
fails intermittently when that configuration is wrong.** Data Protection derives from a key ring
that, by default, each node persists locally. A token minted on node A is undecryptable on node B.
The reply "but a load-balanced Umbraco already needs a shared key ring for backoffice auth" is
exactly what makes the failure mode dangerous:

| | Backoffice cookie | Cancellation link |
| --- | --- | --- |
| Issued | during a session | in an email |
| Redeemed | same session → same node | days later → any node |
| Sticky sessions | masks the problem | do not help |

So a site can have a working backoffice and cancellation links that fail (1 − 1/n) of the time,
intermittently, with nothing to diagnose. Ephemeral storage — containers, App Service without
persisted keys, a slot swap — kills every outstanding link silently on restart.

Three properties follow from storing rather than signing, and each answers a question the design
would otherwise have to argue around:

1. **Withdrawing the feature needs no second switch.** The row is the capability; stop minting and
   the outstanding ones age out on their own. A stateless token would have forced a second flag to
   stop honouring unexpired signatures — and two independent switches can express "we email links
   that do not work", which is a failure this project has already shipped once.
2. **Single use is real**, not merely harmless-because-`Cancel()`-refuses-twice. The landing page is
   also a read, and single use bounds that too.
3. **Redemption can be recorded before the cancellation is reported**, closing a double-submit.

**Hashing is not optional in this decision.** Storing the secret would make the table a credential
store, and every backup and every reader of it a potential booker.

### D2 — Expiry is derived from the booking, not configured

The secret dies at the booking's **start**. A cancellation link is useless once the thing it cancels
has begun, so the booking already carries the answer.

**Alternative: a configured lifetime** (24 hours, 30 days). Rejected — it is a second source of
truth able to disagree with the first, a setting a site must reason about for no benefit, and it
produces the absurd case of a link that expires while the booking is still months away.

### D3 — The visitor's rule is an entry point, not a parameter

`IBookingService` gains a cancellation entry point carrying visitor terms, a **sibling** of
`CancelAsync` rather than a flag on it. This is the shape `PlaceOnBehalfAsync` took in ㊳ and the
shape `PlacementTerms.Visitor`/`Operator` already expresses: **the waiver is structural — a caller
reaches operator terms only by calling the entry point that carries them, never by setting a
parameter.**

**This is a declared BREAKING addition** to a public interface, landing in a minor per the
versioning policy, exactly as `move-booking` and `booking-on-behalf` did.

**Alternative: a default interface method** delegating to `CancelAsync` after a time check —
rejected, because the check needs to load the booking and the store is not on the interface, so
the default would have nothing to read.

**Alternative: a separate `ISelfServiceCancellation` service** — avoids the break, but puts a
cancellation somewhere other than where cancellation lives, and invites a second opinion about the
status machine. The break is cheap and declared; the incoherence would not be.

### D4 — The landing page is a package-served Razor route over Core

The shipped front end already renders from Core in-process, so the flow is complete without a
delivery endpoint. The link points at a package route, which exists on every Umbraco site including
one whose visitor-facing front end is headless.

**GET is safe; POST acts.** The secret is marked redeemed by the POST. Anti-forgery on the form, per
the repository convention for booking submissions.

**Deferred deliberately:** a delivery-API endpoint so a headless site can own the cancellation
screen. Additive when it comes, and recorded in `proposal.md` as a non-goal rather than left silent.

### D5 — One flag, read-only tier, restart-bound

`UBookIt:SelfServiceCancellation:Enabled`, bound at startup beside the existing exposure settings,
default off. `site-settings` already places anonymous-exposure switches in the read-only tier
because the boundary exists *"to keep irreversible erasure and the package's anonymous exposure out
of an operator's reach"*.

**The dependency on `SendBookerEmails` is mechanical, not policy** — the link travels in the
booker's message, so no message means no vehicle. What the design must avoid is silence: an admin
enabling the feature on a non-sending site must be told why nothing happens, which is why the
dependency is stated on the settings screen rather than `&&`-ed away at runtime.

### D6 — Erasure does not revoke an outstanding link

**Ratified by Chris 2026-09-18.** Recorded here with its history because the recommendation
**reversed**: during the explore, "erasure can revoke the token" was offered as a point in favour of
storing it (D1). Reading `booker-erasure` properly inverted that conclusion. D1 stands on its other
three grounds, none of which depended on revocation.

Considered and rejected: deleting rows on erasure. The link carries no contact detail, discloses
none when followed, and cancelling is the one thing the booker was told they could do. Erasure
**keeps the booking** by design, so a booking that still exists is one that can still be cancelled.
Revoking would remove a promised ability while protecting nothing.

Recorded in `booker-erasure` under *what erasure does not reach*, because the rule there is that a
boundary is documented where an operator will meet it — and an operator answering a data-subject
request must not believe erasure did more than it did.

### D7 — Housekeeping is not correctness

An expired or redeemed row is refused **by the check at redemption**, never by its absence. Cleanup
is therefore free to be lazy, and `booking-retention` is deliberately **not** touched: its sweep is
scoped to find bookings by time alone and never handle a contact detail, and widening that guarantee
to sweep a second table would trade a carefully-made promise for tidiness.

## Risks / Trade-offs

- **This is the package's first authentication primitive, reviewed by people who have not reviewed
  one here before.** → Every guard in this change gets the question ㊴ paid for five times: *can it
  fire?* Before asking whether a check is correct, grep the symbol for a production caller and a
  reachable write, then mutate it.
- **A secret in a URL leaks more passively than it is acted on** — browser history, shoulder
  surfing, and `Referer` if a *theme* adds analytics to the page (the shipped views load nothing
  third-party). → `Referrer-Policy: no-referrer` on the response. The stronger pattern — redeem,
  drop into a short-lived cookie, redirect to a tokenless URL — is noted and not taken, because it
  adds machinery for a residual risk already bounded by single use and a short derived expiry.
- **The uniform refusal is easy to write and easy to break.** Four distinguishable causes converge
  on one sentence; any future branch that reports a cause re-opens the oracle. → The requirement
  names all four together, and the test asserts they are indistinguishable rather than asserting
  each separately.
- **A site turning the feature off strands outstanding links.** → Documented as a known consequence
  rather than mitigated; the fallback is the pre-feature status quo.
- **Two messages could carry the same single-use secret** if a later message re-rendered the model.
  → The spec forbids restating a link in a later message, and the model member is absent rather than
  stale for messages that did not mint one.
- **A theme that replaces the booker template drops the link**, leaving its bookers no route. →
  The theming narrowing already says the markup is the theme author's; the package's obligation is
  to put the link on the model so a theme *can* render it, and to say so in the shipped template.

## Open Questions

- **Where the cleanup of expired rows eventually lives.** Correctness does not depend on it (D7),
  so this can be answered after the flow exists — opportunistically on mint, a bounded delete on
  redemption, or its own job. None of the options changes a spec, the approach, or the task
  breakdown.
- **The secret's exact length and alphabet.** Bounded by "cryptographically secure and not
  practically guessable"; the specific choice is an implementation detail the spec deliberately does
  not fix.
