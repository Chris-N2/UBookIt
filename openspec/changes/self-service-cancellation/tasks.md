# Tasks — self-service-cancellation (㊵)

Specs: `specs/self-service-cancellation/` (new), plus deltas for `booking-emails`,
`email-templates`, `site-settings`, `booker-erasure`, `persistence`. Design decisions referenced
as D1–D7.

**Read before starting:** this change introduces the package's **first authentication primitive**.
㊴ shipped five guards placed where they could not fire, every one of them a *fix*. For every guard
written here, ask **"can it fire?"** before "is it right?" — grep the new symbol for a production
caller and a reachable write — then mutate it. See `a-guard-must-be-able-to-fire` in the session
memory and `ubookit-guard-correctness`.

## 1. Baseline

- [x] 1.1 Confirm `main` is in sync with origin and the tree is clean; record the starting commit
- [x] 1.2 Record the four baseline test counts (unit / integration / rendering / client) from a green run, so every later delta is measured rather than claimed
- [x] 1.3 Branch `change/self-service-cancellation` from `main` and push with `-u` (push from **Bash** — the PowerShell tool opens the GitHub account chooser and hangs)

### Guarantee diffs — one per MODIFIED requirement

A `## MODIFIED Requirements` entry replaces its requirement **wholesale**. For each, list every
SHALL and every scenario in the version on `main`, decide *carried forward / deliberately dropped /
superseded*, and record the count both ways. A drop with no entry here is a deletion nothing in the
diff looks like.

- [x] 1.4 `booking-emails` — *What a message tells the booker*: diff guarantees; confirm the seven existing scenarios survive verbatim and the three added ones are additions, not replacements
- [x] 1.5 `email-templates` — *What a view receives is published, typed, and fit to be frozen*: diff guarantees; confirm the move message's previous-interval member and all five existing scenarios survive
- [x] 1.6 `site-settings` — *Settings are tiered, and the tier is enforced by the server*: diff guarantees; confirm both deliberate omissions (`PreservedQueryParameters`, the theme), the no-destructive-editable rule and all four existing scenarios survive
- [x] 1.7 `booker-erasure` — *What erasure does not reach is documented*: diff guarantees; confirm the four existing bullets and four existing scenarios survive, and that the closing "left unstated" sentence still enumerates every bullet
- [x] 1.8 Record each diff's result in this file (lines removed, scenarios before → after), so QA can re-run rather than re-derive

**Baseline (1.2), from a green run on `main` at `168aee7`:** unit **1718**, integration **158**,
rendering **1086**, client **287**; 0 warnings in a clean Release build; `openspec validate --all
--strict` 21/21 (22 with this change present).

**Guarantee diffs (1.4–1.7), scripted rather than eyeballed.** The instrument normalises before
comparing — emphasis stripped, lines unwrapped — because a wrapped line and a bold word have each
defeated this check before (㉛, ㉙). It compares scenario titles and every SHALL-bearing sentence.
Re-runnable: `scratchpad/gdiff.py`.

| Requirement | Scenarios | SHALL sentences | Dropped |
|---|---|---|---|
| `booking-emails` — What a message tells the booker | 7 → 10 | 7 → 12 | **none** (one superseded, below) |
| `email-templates` — What a view receives… | 5 → 7 | 9 → 11 | **none** |
| `site-settings` — Settings are tiered… | 4 → 6 | 4 → 6 | **none** |
| `booker-erasure` — What erasure does not reach… | 4 → 5 | 6 → 7 | **none** |

**The one sentence the instrument flagged, and its triage.** In `booking-emails`, the narrowing
sentence changed:

- was: *"…SHALL NOT claim that supplied content states the booking's state correctly, presents the
  reference in the quotable form, **or expresses times in any particular zone.**"*
- now: *"…presents the reference in the quotable form, expresses times in any particular zone,
  **or carries the cancellation link.**"*

**Superseded by a stronger claim**, not dropped: the package disclaims one more thing than before,
and the three original disclaimers are intact. Recorded rather than waved through, because a
flagged sentence that turns out to be fine is exactly the case this discipline exists to make
somebody look at.

## 2. The secret, in Core

- [x] 2.1 A value type for the cancellation secret: generated from a cryptographically secure source, with its one-way hash as a separate operation; verify by test that two generations differ and that the hash is stable for a given secret
- [x] 2.2 A Core port for storing and reading secrets — issue, find-by-hash, mark redeemed; verify the port's contract is stated in XML docs and that nothing on it accepts or returns the plaintext secret except at issue
- [x] 2.3 Expiry derived from the booking's start (D2), computed where the secret is issued rather than stored as policy; verify by test that a booking moved later does **not** extend an already-issued secret's life, and record whichever answer is chosen as deliberate

**§2 notes.** `CancellationSecret` is 256 bits, base64url, hashed to lower-case hex; **no member of
`ICancellationSecretStore` accepts or returns the plaintext** — even `IssueAsync` takes the hash — so
an implementation cannot persist what it is never given. `TryParse` is deliberately **intolerant**
where `BookingReference.TryParse` is tolerant: nothing types a secret, so every tolerance would only
widen what counts as a match.

Three guards mutation-tested, each killed: `ToString` leaking the value, `Hash` returning the value,
and `TryParse` gaining a `Trim()`.

**A method note worth keeping.** The first mutation run reported all three mutants "Passed" — a
false green in the instrument built to catch false greens. Python on Windows does not resolve Git
Bash's `/tmp`, so the backup never existed, the anchor assertion threw, and **no mutant was ever
applied**. The lesson is the change's own: *a guard that cannot fire reports success*. Verify the
harness mutated the file before believing the result.

## 3. The visitor's cancellation entry point

- [x] 3.1 Add the visitor-terms cancellation entry point to `IBookingService` as a **sibling** of `CancelAsync` (D3), with an XML doc carrying the **BREAKING** note and the reason it lands in a minor
- [x] 3.2 Implement it so a booking whose start has passed is refused, while `CancelAsync` remains able to reach one; verify with a test that cancels the *same* booking through both entry points and asserts they diverge
- [x] 3.3 Prove the waiver is structural: a test asserting no parameter on the operator entry point can produce visitor terms, and none on the visitor entry point can produce operator terms
- [x] 3.4 Confirm a successful self-service cancellation produces the identical booking state and the identical observer call as the operator route; verify through the production entry point, not by comparing two halves

**§3 notes.** `CancelAsVisitorAsync` is a sibling of `CancelAsync`, not a parameter on it, and the
only difference is the refusal — everything after the time check *is* `CancelAsync`, so there is no
second implementation to drift. The failure code is **`booking-already-started`**, naming the fact
rather than a policy: "too late to cancel" would be a statement about a site's rules, and the
package has none, so a cancellation-window feature can arrive later without making this code a lie.

**The break is real and was visible immediately:** 12 test doubles implementing `IBookingService`
stopped compiling. Each gained the member, matching whatever its local `CancelAsync` does (delegate,
`throw Unexpected()`, or `NotSupportedException`). Inserted **after the complete member** rather than
before the declaration, and swept afterwards: **0 doc blocks stranded** across the six files — ㊴
stranded two by anchoring on a member line alone.

Mutation-tested, both killed: removing the time check (3 tests fail), and loosening `<=` to `<` so a
booking could be cancelled exactly at its start (1 test fails — the boundary theory, which is what
it is for). §2.3's question is answered here too: the expiry is computed from the booking's start at
issue time and **stored**, so a later move of the booking does not extend a link already in an inbox.

Unit suite **1740** (baseline 1718; +22).

## 4. Persistence

- [x] 4.1 Entity and additive migration for the secrets table; verify against a database from the previous version that nothing is altered or dropped
- [x] 4.2 Store implementation; verify the integration test suite covers issue, find, redeem, expired, and redeem-twice
- [x] 4.3 A guard that the stored row is **not** a credential: present everything the table holds to the flow and assert no booking is cancelled
- [x] 4.4 A guard that the plaintext secret appears in no table, no log and no other store; make it a scan that can fail rather than a claim in a comment

**§4 notes.** `uBookItCancellationSecret`, migration `20260918132340_AddCancellationSecrets` —
`CreateTable` and `CreateIndex` only, nothing altered or dropped. **The hash is the primary key**,
so "one row per secret" is a property of the schema rather than of the store's code, and a
redemption is a seek on the key (which matters: it runs on an anonymous request).

**Redemption is a compare-and-swap in one statement**, the expiry inside the `WHERE` rather than
checked around it. The mutation that matters: replacing it with read-then-write left 8 of 9 tests
green and **7 of 8 concurrent redemptions succeeded** — a single-use credential usable seven times,
with exactly one test standing between the two implementations.

**Two structural guards**, both proven able to fire: the port deals only in hashes (type identity),
and **the persistence assembly cannot see `CancellationSecret` at all** — mutated by making it
reference the type, which failed the scan. The first version of the identity guard was too loose
(substring matching flagged `CancellationSecretRecord`) and reported a violation that was not one;
tightened to compare types, with the episode recorded in the test.

Integration **167** (baseline 158; +9).

## 5. Issuing the link

- [ ] 5.1 Issue a secret at the point a booker message is due, only where the feature is on; verify that a site with the feature off writes no row
- [ ] 5.2 Add the cancellation URL to the booker's email model, absent where no link exists (D1 in `email-templates`); verify a view written before the member existed renders unchanged
- [ ] 5.3 Build the absolute URL on the shape `BackofficeBookingLink` uses; verify the built URL resolves against the site's configured address rather than a request's host
- [ ] 5.4 The shipped booker templates carry the link and say what it is for; verify the rendering suite asserts both the link and the sentence
- [ ] 5.5 A later message about the same booking does **not** restate the link; verify with a test that places, then confirms, and asserts only one message carries a secret

## 6. The landing page

- [ ] 6.1 A package route serving the cancellation page; verify it is served only while the feature is on, and is absent — not refused — while it is off
- [ ] 6.2 `GET` is safe: verify by test that retrieving the page neither cancels the booking nor marks the secret redeemed
- [ ] 6.3 The page states reference, what was booked and when, in the booking's own zone, and **cannot** express booker contact details; verify structurally (the model has no member for them), not by asserting absence from rendered text
- [ ] 6.4 Anti-forgery-protected `POST` that redeems and cancels; verify a submission without valid protection is refused and the booking is unaffected
- [ ] 6.5 One indistinguishable response for expired / redeemed / uncancellable / never-issued; verify with a single test that drives all four and asserts the responses are equal, rather than four tests asserting four sentences
- [ ] 6.6 `Referrer-Policy: no-referrer` on the page's response; verify by asserting the header
- [ ] 6.7 The new views satisfy `default-frontend`'s universals — every state the model expresses, every branch reachable, every view exercised, markup resolving its own references; verify the existing view-coverage guards pick the new views up **without amendment**, and if they do not, find out why before changing them

## 7. The flag

- [ ] 7.1 `UBookIt:SelfServiceCancellation:Enabled`, bound at startup, default off; verify an unconfigured site issues nothing and serves no route
- [ ] 7.2 Declare it in the read-only tier and as restart-bound; verify a write through the settings endpoint is refused **by the server**, not merely hidden by the client
- [ ] 7.3 State the `SendBookerEmails` dependency on the settings screen when it is unmet; verify the screen says the feature cannot run and why, rather than showing it as on
- [ ] 7.4 A test that the feature does nothing at all where booker emails are off — no row, no link, and the route still absent

## 8. Documentation

- [ ] 8.1 `docs/` — the flag, its dependency, and that turning it off strands outstanding links; verify the documentation tests pick up the new section
- [ ] 8.2 `booker-erasure`'s new boundary documented **where an operator performing an erasure will meet it** (D6), not only where sending is described
- [ ] 8.3 The cancellation URL joins the published theming/template contract; verify it is documented alongside the other model members

## 9. Verification

- [ ] 9.1 `openspec validate --all --strict` passes
- [ ] 9.2 Release build `--no-incremental`, zero warnings. **Build the client first, then `dotnet build` — never interleave**: a client rebuild renames content-hashed chunks and leaves the static-web-asset manifest naming the old ones, which surfaces as `StaticWebAssets.targets(706,5)` "1 Error(s)" and self-heals on the next build
- [ ] 9.3 Full suite green from a clean build; record all four counts and the deltas from 1.2
- [ ] 9.4 **Live**: place a booking on the TestSite with the feature on and booker emails on; follow the link from the message, confirm the page names the right booking and shows no contact details, cancel, and confirm the booking is cancelled and the cancellation message sent
- [ ] 9.5 **Live**: retrieve the link a second time and confirm the uniform refusal; confirm a fetch without submitting changes nothing
- [ ] 9.6 **Live**: with the feature on and booker emails off, confirm the settings screen states why it cannot run
- [ ] 9.7 Stop the TestSite and confirm port 44348 is free

## 10. QA handover

- [ ] 10.1 Write the QA handover: what was built, what is claimed, build and test state, and the instruction to **verify rather than trust**
- [ ] 10.2 Name for the reviewer where a defect is most likely, and say plainly that this is the package's first authentication primitive
