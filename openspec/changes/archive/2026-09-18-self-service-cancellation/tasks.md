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
- [x] 1.9 `bookings` — *Availability and placement service ports*: diff guarantees; confirm all ten scenarios and every SHALL survive, and that the enumeration is WIDENED rather than replaced (added in QA round 1, at the reviewer's observation that two previous changes appended here and this one had not)
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

- [x] 5.1 Issue a secret at the point a booker message is due, only where the feature is on; verify that a site with the feature off writes no row
- [x] 5.2 Add the cancellation URL to the booker's email model, absent where no link exists (D1 in `email-templates`); verify a view written before the member existed renders unchanged
- [x] 5.3 Build the absolute URL on the shape `BackofficeBookingLink` uses; verify the built URL resolves against the site's configured address rather than a request's host
- [x] 5.4 The shipped booker templates carry the link and say what it is for; verify the rendering suite asserts both the link and the sentence
- [x] 5.5 A later message about the same booking does **not** restate the link; verify with a test that places, then confirms, and asserts only one message carries a secret

**§5 notes.** Issuance happens in `BookingEmailHandler`, where a booker message is due, under three
conditions — feature on, **placement only**, and the booking not already begun. All three
mutation-tested and killed. The third exists because an operator can take a booking at the desk
minutes before it starts, and a link whose expiry is that start would be **dead before it arrived**.

`SelfServiceCancellationSettings` is bound from configuration **only** (§7.1), as a singleton, and
deliberately not through the settings store: it is an exposure switch, so reading it from the store
would make it writable by anyone who can reach the settings endpoint.

**Three existing guards fired, and each was right to.** The durable-storage-surface guard demanded
the new table be recorded with a decision; the send-path DI container needed the new services; and
**my own boundary guard caught my own over-claim** — it asserted the persistence *assembly* could
not see `CancellationSecret`, which stopped being true the moment minting was wired into the
handler. The claim was wrong, not the code: what the package guarantees is that the plaintext never
reaches **storage**. Narrowed to the stores, with a counterpart assertion that exactly one place
still mints, so the narrowing cannot be read as "nobody may see it".

**A tooling note, the second today.** A `` in a regex written through a heredoc arrived in the
source as a literal **backspace character (0x08)**, so the guard searched for
`<BS>CancellationSecret<BS>` and failed for a reason with nothing to do with the code. Repaired by
building the backslash with `chr(92)`, and a repo-wide sweep for control characters in `.cs`/`.ts`
came back **0**. Heredocs have now corrupted source three times on this project (NUL bytes once
before); prefer the Write tool for anything containing escapes.

Unit **1755** (baseline 1718; +37).

## 6. The landing page

- [x] 6.1 A package route serving the cancellation page; verify it is served only while the feature is on, and is absent — not refused — while it is off
- [x] 6.2 `GET` is safe: verify by test that retrieving the page neither cancels the booking nor marks the secret redeemed
- [x] 6.3 The page states reference, what was booked and when, in the booking's own zone, and **cannot** express booker contact details; verify structurally (the model has no member for them), not by asserting absence from rendered text
- [x] 6.4 Anti-forgery-protected `POST` that redeems and cancels; verify a submission without valid protection is refused and the booking is unaffected
- [x] 6.5 One indistinguishable response for expired / redeemed / uncancellable / never-issued; verify with a single test that drives all four and asserts the responses are equal, rather than four tests asserting four sentences
- [x] 6.6 `Referrer-Policy: no-referrer` on the page's response; verify by asserting the header
- [x] 6.7 The new views satisfy `default-frontend`'s universals — every state the model expresses, every branch reachable, every view exercised, markup resolving its own references; verify the existing view-coverage guards pick the new views up **without amendment**, and if they do not, find out why before changing them

**§6 notes.** A plain controller at `umbraco/ubookit/cancel/{secret}`, removed from the
application model entirely when the feature is off (`CancellationExposureConvention`, on the
delivery API's "absent, not refused" terms). GET is safe, POST redeems and cancels, anti-forgery on
the form. Three mutants killed: GET redeeming, a refused cancellation getting its own page, and the
expiry ignored on the read.

**The form has no `action` attribute**, which turned out better than the `Url.Action` it replaced:
an empty action posts to the URL the page was fetched from, so the **secret never enters the
markup** — only the address. `CancellationPageModel` therefore has no member for it, alongside
having none for the booker.

**§6.7 answered: the view guards picked the new views up WITHOUT amendment**, and promptly failed
them — which is the universals working. What followed is the substance of this section:

- The rendering harness classifies every shipped view as a **document** or a **partial**; these are
  standalone documents, so they needed fixtures and states, not an exemption.
- Two of them are **static by design**, and `ModelReferences.DelegatingViews` is for *delegates* —
  views that hand their model to exactly one other view. Naming a static page there made the
  exemption's own stated reason false, and **the delegate guard caught it**. So `StaticViews` is a
  new category with its own guard (`A_static_view_really_is_static`, asserted against rendered
  output), rather than a bent one. Bending it would have disarmed the check that an exempted view
  really is the harmless shape its exemption describes.
- `LocalStart` was "referenced but changes nothing", because all four fixtures used the same time.
  Correct finding; the fixtures now vary the interval.
- The published **class vocabulary** is a stable contract and uses `block-part`, not BEM's
  `block__part`. New classes recorded deliberately, and the submit-control count pinned 3 → 4.
- The **schema-stability** guard in the integration suite required the new migration be recorded
  with its reasoning against the concurrency guarantee it protects.

**A repeat of my own mistake, worth naming:** a forbidden-substring assertion flagged `ServiceName`
because it contains `Name`. That is the second loose-substring instrument in this change — the
first flagged `CancellationSecretRecord` for containing `CancellationSecret`. Both now match
precisely.

Unit **1767**, rendering **1168** (+82), integration **167**.

## 7. The flag

- [x] 7.1 `UBookIt:SelfServiceCancellation:Enabled`, bound at startup, default off; verify an unconfigured site issues nothing and serves no route
- [x] 7.2 Declare it in the read-only tier and as restart-bound; verify a write through the settings endpoint is refused **by the server**, not merely hidden by the client
- [x] 7.3 State the `SendBookerEmails` dependency on the settings screen when it is unmet; verify the screen says the feature cannot run and why, rather than showing it as on
- [x] 7.4 A test that the feature does nothing at all where booker emails are off — no row, no link, and the route still absent

**§7 notes, including a correction to this file.** **7.2 was ticked prematurely in the §6 batch** —
the config binding was registered but the setting had never been declared in `SettingCatalogue`, so
the tier and the server-side refusal did not exist. Unticked and done properly. Recording it because
a false claim in the task file is exactly what QA has caught before, and catching it myself does not
make it less worth writing down.

Now declared **read-only and restart-bound**, beside the delivery API's two switches and for the
same reason: anonymous exposure, not a judgement about operators. Two existing guards had to be
satisfied — the catalogue may name no setting the package does not read (it is read in Persistence
and in Web, neither of which the backoffice assembly can reference, so it joins the delivery pair as
a recorded exception), and the hand-spelled key is tied back to
`SelfServiceCancellationSettings.SectionKey` by a guard, so the two cannot drift.

**The dependency is stated, not silently `&&`-ed away** (7.3). `SettingCatalogue.UnmetDependency`
returns the *sentence* — from the server, because the reason involves another setting and a client
that assembled it would be a second place for the explanation to drift. It joins
`aria-describedby` **before** the override note and the error, since it explains why the value shown
is not the behaviour the site has; a reader meeting it after the error has already been misled.
Deliberately not generalised into a dependency graph: one case is not a pattern.

**Build-order note.** Regenerating the typed client needs the site running, and the site would not
build because the client did not typecheck against a model that did not exist yet. Broken by parking
the element change, building **client then server separately** (QA's rule), starting the site,
regenerating, and restoring. The `StaticWebAssets(706,5)` error appeared exactly once on the way, in
the interleaved build, which is the artefact QA identified.

Unit **1770**, client **290** (+3).

## 8. Documentation

- [x] 8.1 `docs/` — the flag, its dependency, and that turning it off strands outstanding links; verify the documentation tests pick up the new section
- [x] 8.2 `booker-erasure`'s new boundary documented **where an operator performing an erasure will meet it** (D6), not only where sending is described
- [x] 8.3 The cancellation URL joins the published theming/template contract; verify it is documented alongside the other model members

## 9. Verification

- [x] 9.1 `openspec validate --all --strict` passes
- [x] 9.2 Release build `--no-incremental`, zero warnings. **Build the client first, then `dotnet build` — never interleave**: a client rebuild renames content-hashed chunks and leaves the static-web-asset manifest naming the old ones, which surfaces as `StaticWebAssets.targets(706,5)` "1 Error(s)" and self-heals on the next build
- [x] 9.3 Full suite green from a clean build; record all four counts and the deltas from 1.2
- [x] 9.4 **Live**: place a booking on the TestSite with the feature on and booker emails on; follow the link from the message, confirm the page names the right booking and shows no contact details, cancel, and confirm the booking is cancelled and the cancellation message sent
- [x] 9.5 **Live**: retrieve the link a second time and confirm the uniform refusal; confirm a fetch without submitting changes nothing
- [x] 9.6 **Live**: with the feature on and booker emails off, confirm the settings screen states why it cannot run
- [x] 9.7 Stop the TestSite and confirm port 44348 is free

**§8–§9 notes. The live run, end to end, on the TestSite.**

A booking placed through the delivery API produced a message carrying
`https://localhost:44348/umbraco/ubookit/cancel/<secret>` — **rendered by a SITE-SUPPLIED
template**, which is the contract that matters: the member reaches a view the package did not
write. Then:

| Check | Result |
|---|---|
| GET the link | 200, names reference `286Z-FDK6`, when, and the resource |
| Booker name / email / phone on the page | **absent, all three** |
| The secret in the page markup | **absent** — it lives only in the address |
| `Referrer-Policy` | `no-referrer` |
| GET a second time (a scanner's fetch) | still usable — **the GET consumed nothing** |
| POST without the anti-forgery token | **400** |
| POST with it | cancelled |
| Messages sent | the booker's cancellation confirmation **and** the site's internal one — so a self-service cancellation really is an ordinary one |
| The spent link, again | "This link can no longer be used" |
| A secret never issued | **byte-identical response** to the spent one |
| Not a secret at all | the same page again |
| Feature off, GET and POST | **404 both** — absent, not refused |

**The uniform refusal was verified by hashing the responses rather than reading them** — spent and
never-issued are the same bytes.

**Two live findings on the way in.** The site's application URL was unresolved, so no link was built
at all — the `null` branch behaving exactly as written, and a reminder that a site without
`UmbracoApplicationUrl` gets no cancellation links. And SMTP is configured in **user secrets**,
pointing at a pickup directory outside the repo, which is where the messages had been landing while
I was looking elsewhere.

**One Release warning, now fixed:** `CS8603` in a new test helper. The baseline is zero and CI treats
warnings as errors, so it would have been a build break rather than a nit.

**9.6 is NOT done** — "with the feature on and booker emails off, the settings screen states why it
cannot run" needs a backoffice sign-in. The server half is unit-tested
(`The_dependency_is_stated_only_when_it_is_unmet`) and the client half too (three tests in
`settings-fields.test.ts`), but **nobody has seen the sentence on the screen**.

**State:** unit **1770**, rendering **1168**, integration **167**, client **290**; 0 warnings in a
clean Release build; `openspec validate --all --strict` 22/22. TestSite stopped, port 44348 free,
`appsettings.json` restored byte-identical.

## 10. QA handover

- [x] 10.1 Write the QA handover: what was built, what is claimed, build and test state, and the instruction to **verify rather than trust**
- [x] 10.2 Name for the reviewer where a defect is most likely, and say plainly that this is the package's first authentication primitive

**9.6, run by Chris — and it found a defect nothing else could.** The setting appeared on the
settings screen rendering its **raw localisation keys**:

```
ubookitSettings_selfServiceCancellationEnabledLabel
ubookitSettings_selfServiceCancellationEnabledDescription
True
Changing this in configuration takes effect when the site restarts.
```

Everything around it was right — catalogued, read-only, restart-bound, value correct, the server
refusing writes — and **every test passed**. The defect lived entirely in the gap between a C#
catalogue and a TypeScript dictionary, which nothing joined. This is the same shape as ㊱'s
"the live check found what 2770 tests could not".

Fixed, and **guarded**: `SettingLocalisationTests` is a C# test that reads the client's `en-us.ts`
and asserts every catalogued setting has a `…Label` and a `…Description` declared as dictionary
keys — matched as declarations rather than as text, so a term appearing only inside somebody's prose
does not satisfy it. It crosses the language boundary the same way `BookingReferenceAlphabetTests`
does, because only the test project sees both halves. Mutation-proven: removing the label again
fails with *"Catalogued but not localised, so the screen renders the raw key:
selfServiceCancellationEnabledLabel"*.

The slug rule is a hand-port of the client's `settingSlug`, so it is pinned against four keys that
already render correctly on screen.

**Still unconfirmed on screen: the dependency sentence itself.** Chris's site showed no note, which
is *correct* if `SendBookerEmails` resolves true there — the settings store overrides configuration,
and that value may be stored from earlier work. What the screenshot establishes is the label defect;
whether the note renders, and whether it is associated with the control by `aria-describedby`, has
still not been seen. Both halves remain unit-tested.

**One unexplained test failure, recorded rather than swallowed.** A single run reported
`Failed: 1, Passed: 1776` without my capturing the name; three subsequent runs were clean at 1777.
It coincided with a localisation file being restored mid-run, so the likeliest explanation is that
the run read the file between two writes — but that is a guess, and a deferred obligation already
records a flaky perf threshold in this suite. Treat a recurrence as real.

**A defect found by a question, after apply was otherwise complete.** Chris asked what he would see
if he ran the TestSite as it stands. Answering it meant reading `UnmetDependency` again, and it
checked only whether booker emails were off — **not whether the feature was on**. So on a site with
the feature off (which is every site by default) the screen would have said *"this has no effect
while booker emails are off"*, naming a reason that is not the reason: it has no effect because
nobody turned it on.

The spec scenario says *"enabled AND booker emails off"*. The implementation did not, and **the test
could not fail on that axis** because it varied only the notification setting — a test that is not
evidence about the thing it appears to cover.

Fixed, and the test is now a `[Theory]` over both axes (4 cases). Mutation-proven: restoring the old
condition fails it.




## 13. QA round 3 — REJECT (1 MAJOR, mechanical): what changed

**A condition that could not fire, and the control written to detect exactly that, passing anyway.**

`VisitorFacing()` filtered on `typeof(Controller)`. `UBookItDeliveryApiControllerBase` derives from
**`ControllerBase`**, so every delivery controller was already gone before the exclusion clause was
reached: **the clause was inert**. Deleting it entirely left the suite green, which QA demonstrated.

And the control at the bottom of the positive test — added in round 2, in its own words, *"or 'not a
delivery controller' would be a condition that never fires"* — asserted only that a delivery
controller **exists in the assembly**. That was true while the exclusion did nothing. It could not
tell "the clause fired" from "the clause is dead".

Two edits: filter on `ControllerBase`, and make the control assert the exclusion **changes the
result** — every concrete delivery controller is a `ControllerBase` (so the filter would sweep it
in) and is **not** in `VisitorFacing()` (so the exclusion took it out).

**QA's acceptance test passes both ways.** Deleting the exclusion clause now fails two tests,
naming `BookingsController.PlaceBooking` and `ServicesController.PlaceServiceBooking` — delivery
endpoints that would wrongly be required to carry anti-forgery. And its round-2 mutant controller is
still caught by name.

**This was the fifth instance of the shape, and the first at the third rung of the ladder** — the
fault moved from the artifact, to the guard, to the guard's own control. That ladder is a thing this
project already has a name for, and it had not previously reached a control.

### Nit

`Assert.NotEqual(string.Empty, name)` could never fail — a filler assertion inside a guard whose
entire subject is assertions that cannot fail. Removed; the view's name now travels in the failure
messages of the three real assertions, where it is worth something.

**State:** unit **1797**, integration **167**, rendering **1168**, client **290**; 0 warnings in a
clean Release build; 22/22.


## 12. QA round 2 — REJECT (1 MAJOR, 2 MINORs): what changed

**QA mutation-tested round 1's three new guards rather than trusting the table**, and all three
killed their mutants as claimed. Then it disproved a claim I had written about one of them.

### The MAJOR: a guard whose stated property was false, and QA proved it

`AntiForgeryTests`'s remark said *"**Derived, not listed.** … a POST added tomorrow is covered the
day it exists … A hardcoded list would pass forever while the thing it names drifts."* The actions
were derived. **The controllers were a hardcoded list of three.**

QA added a controller:

```csharp
[Route("umbraco/ubookit/mutant")]
public sealed class MutantSurfaceController : Controller
{
    [HttpPost] public IActionResult Submit() => Ok();
}
```

An anonymous, visitor-facing, unprotected form POST — and **all three tests passed**. That is
exactly how `CancellationController.Cancel` itself arrived: on a *new controller*, which is why
nothing caught it for a whole round.

Now derived from the assembly — every non-abstract public `Controller` that is not a
`UBookItDeliveryApiControllerBase` — with the **controller set pinned** in the positive control so a
new one forces a decision, and with an assertion that the delivery-API exclusion actually excludes
something (or "not a delivery controller" would be a condition that never fires). QA's mutant now
fails with *"A visitor-facing POST accepts a submission without an anti-forgery token:
MutantSurfaceController.Submit"*.

**This is the fourth instance in this change of one shape** — a statement describing a property the
thing does not have. `UnmetDependency` naming the wrong reason; the class-vocabulary comment; the
localisation guard reading the whole file instead of the dictionary; and now this. It is the failure
I was most explicitly warned about, in a guard for a security requirement, written in the round
after QA found four guards that could not fire.

### The MINORs

- **`OutsideTheStylingContract` was specified twice and differently** — the class scan filtered by
  *directory*, the button count by *filename* — so a fourth cancellation view would have been
  excluded from one and not the other. Worse, the filename list contained `Index.cshtml`, the
  likeliest filename in any future `Views/<Something>/` folder, whose buttons would then have been
  silently exempt. **One predicate now, naming a place rather than a file.**
- **The exemption had no counterpart guard**, which is precisely why round 1 accepted
  `StaticViews` — because *that* one had one. `A_view_outside_the_contract_really_is_outside_the_cascade`
  now asserts an exempted view genuinely sets `Layout = null`, links no stylesheet and pulls in no
  styles partial. Mutation-proven: adding a `<link>` to one fails it.
- **The pages still emitted `ubookit-` classes** that were no longer in the published vocabulary, so
  a reader had no way to tell them from contract classes — every other `ubookit-*` class is one.
  **Prefix dropped**, and the guard above asserts no exempted view emits one, so drift is prevented
  in both directions rather than hidden by a filter.

### Nits

Counted test names renamed for what they assert — the sibling was still called
`The_two_things_erasure_does_not_reach_are_documented` for a requirement that now has four, and a
counted name goes wrong the next time somebody adds a boundary. `Pragma: no-cache` removed: it is a
*request* directive and a no-op on a response, so it was a header making a claim it does not carry.
The §11 SHALL count corrected to 17 → 17.

### What QA verified and I had got right

All five round-1 fixes, each re-mutated. **`CancellationExposureTests.Served()` builds real
`ControllerActionDescriptor`s** through `AddControllers` + `AddApplicationPart`, so it proves the
route is removed rather than that a model was mutated. The fifth delta re-diffed independently. And
on finding 3 — *"you chose right"*: recording the pages as unstyleable and unthemable rather than
designing stylesheet plumbing inside a REJECT round.

**State:** unit **1797**, integration **167**, rendering **1168**, client **290**; 0 warnings in a
clean Release build; 22/22.


## 11. QA round 1 — REJECT, 5 MAJOR: what changed

**Every number re-ran true**, and QA re-did all four guarantee diffs itself and confirmed them pure
additions. **The rejection was about guards and published claims, not the mechanism** — the
reference never enters the flow, the compare-and-swap is real, the page model cannot name a booker,
and the three placement conditions are correct. Each fix below is mutation-tested.

| # | Finding | Fix | Mutant |
|---|---|---|---|
| 1 | The off-by-default route guarantee had **no test at all** | `CancellationExposureTests` — 7 tests, both halves: the convention's behaviour AND that the composer registers it | Dropping the `Configure<MvcOptions>` line fails 3 |
| 2 | `[ValidateAntiForgeryToken]` unguarded — **and so were the two existing booking POSTs** | `AntiForgeryTests` derives the set by reflection over every visitor-facing `[HttpPost]` | Removing it from the new POST *and the old one* names both |
| 3 | Four classes published as a styling contract no site CSS can reach | Reverted from the vocabulary; the pages recorded as **outside the styling and theming contracts**, in the proposal and the docs | — |
| 4 | The secret travels in a URL path, so it lands in **access logs** — unnamed | Documented where a site owner configures the feature, on `booking-emails`' terms; `design.md`'s risk restated honestly | — |
| 5 | Every documentation SHALL this change added was unguarded while its siblings were guarded | `SelfServiceCancellationDocumentationTests` (6) + `The_third_thing_erasure_does_not_reach_is_documented` | — |

### Finding 2 was a sample, not the population

The anti-forgery requirement has existed since the first Razor flow and was asserted by nothing.
Fixing only the new POST would have left `BookingSurfaceController.Submit` and
`ServiceBookingSurfaceController.Submit` exactly as they were. The guard **derives** the set by
reflection rather than listing it, so a POST added tomorrow is covered the day it exists.

### Finding 3 is the one worth reading

The four `ubookit-cancel*` classes were added to the published vocabulary under the comment *"a site
styling the flow can style these too"*. QA established that is **false**: the three pages set
`Layout = null`, link no stylesheet, and `ubookit.css` contains no rule for them — **nothing a site
writes can reach them**. They are also outside the theme-view set, so a theme RCL cannot supply
them either, and nowhere said so.

That is the same defect this change already caught itself on twice — a statement describing
something the site does not have. Reverted, and recorded as a **removal with its reason** in
`proposal.md` and `docs/configuration.md`: the pages are plain semantic HTML, operable with no
stylesheet at all, and giving them a route to a site's CSS is a real feature with a real design
question behind it, not this change's.

### Minors and nits

- **The page could offer a button the submission would refuse.** `BuildAsync` checked the stored
  expiry but not the booking's own start, and the two disagree after a move *earlier*. Now checks
  both; the visitor is never shown a confirmation form for a booking the POST will refuse.
- **`Cache-Control: no-store`** added — the cheaper half of the pair the change already took
  trouble over for `Referrer-Policy`.
- **`bookings` gains a fifth delta.** The enumeration of Core's booking-service entry points has
  been appended to by `move-booking` and `booking-on-behalf` when each added one; this change had
  not, and a sentence that has been the record of Core's surface for two changes stops being that
  record the moment an addition skips it. Widened for the third time; 10 → 10 scenarios,
  **17 → 17** SHALLs, nothing dropped (QA re-counted; my figure was one low, the equality it
  stands for was right).
- Wrong cross-reference (`DelegatingViews` where `StaticViews` was meant) — the same class as the
  `UnmetDependency` defect, corrected.
- A duplicate `ViewRenderer` removed.
- **The "TestSite stopped, port free" claim was false when QA read it** — Chris had restarted it
  from VS to chase an unrelated Umbraco exception. QA predicted a third false claim in the handover
  and found one; it was stale rather than wrong when written, which is the same problem.
- **A solution-level `dotnet test --no-build` reported `Failed: 19`**, every failure preceded by
  `MSB3073: "npm run build" exited with code 1` — parallel test projects each re-triggering the npm
  target and racing. That is very likely the mechanism behind the single unexplained failure
  recorded earlier. **Build the client first, then run the suites per project.**

**State after this round:** unit **1796** (was 1777), integration **167**, rendering **1168**,
client **290**; 0 warnings in a clean Release build; `openspec validate --all --strict` 22/22.


### Verify rather than trust

Every number and every claim below is a claim until you re-run it. On this project you have twice
found a statement in a handover to be false — once one you had made yourself — and this change has
already produced one: **I ticked 7.2 without doing it**, and found it only when the next task went
looking for what I had claimed to write. That is recorded in §7 rather than quietly fixed. Assume
there is another.

### The one thing to know before anything else

**This is the package's first authentication primitive.** There was no token store, no
`IDataProtection` use and no one-time-link machinery anywhere in `src/` before this change. Nothing
here extends a pattern that was already reviewed; all of it is new, and the credential it issues is
sufficient on its own to destroy a booking.

### Where a defect is most likely

1. **`CancellationController.BuildAsync` and `Cancel` — the convergence of four causes on one
   answer.** Expired, redeemed, uncancellable, never issued. Any future branch that reports a cause
   re-opens the enumeration oracle the whole design exists to close. The test drives all five
   causes and asserts the responses are equal; the live run hashed two of them and got identical
   bytes. **Attack it by adding a sixth cause** and seeing whether anything notices.

2. **`SqlCancellationSecretStore.TryRedeemAsync` — the compare-and-swap.** One `UPDATE` with the
   conditions in the `WHERE`. Replacing it with read-then-write leaves **8 of 9 tests green** and
   lets **7 of 8 concurrent redemptions succeed**. Exactly one test stands between the two
   implementations. Check that test really is what I say it is.

3. **`BookingEmailHandler.IssueCancellationLinkAsync` — three conditions, all mutation-tested.**
   Feature on, placement only, booking not already begun. The third exists because an operator can
   place a booking inside its own lead time, and a link whose expiry is the start would be dead on
   arrival. Attack the *interaction*: is there an event I have mis-classified as "placement"?

4. **`CancellationSecret.TryParse` is deliberately INTOLERANT** where `BookingReference.TryParse`
   forgives case, dashes and whitespace. They sit side by side and look inconsistent. Satisfy
   yourself the inconsistency is the right way round.

5. **`ModelReferences.StaticViews` — a new exemption category I added to the rendering harness.**
   An exemption registry is a way to switch a rule off, so the question is whether its own guard
   (`A_static_view_really_is_static`) actually constrains what it exempts. I added it because
   naming a static page in `DelegatingViews` made that exemption's stated reason false and the
   delegate guard caught it — but a new exemption mechanism deserves the same suspicion.

6. **`SettingCatalogue.UnmetDependency` — a sentence about one setting that depends on another.**
   Deliberately not generalised. Check that the note cannot appear where nothing explains it, and
   that the screen does not present the feature as working while it is not.

### Claims to re-run

| | |
|---|---|
| Branch | `change/self-service-cancellation` |
| Unit | **1770** (baseline 1718) |
| Integration | **167** (baseline 158) |
| Rendering | **1168** (baseline 1086) |
| Client | **290** (baseline 287) |
| Release `--no-incremental` | 0 warnings / 0 errors — **after fixing a CS8603 I introduced**; the baseline is zero and CI treats warnings as errors |
| `openspec validate --all --strict` | 22/22 |
| TestSite | stopped, port 44348 free, `appsettings.json` restored byte-identical |

**Build order matters here:** build the client, then `dotnet build`. Interleaving produces
`StaticWebAssets.targets(706,5)` "1 Error(s)" — the manifest naming the previous bundle's content
hashes — which self-heals on the next build and is not a real error. It appeared once during this
change, in the one interleaved build.

### What is NOT done

- **9.6**: the settings screen stating the unmet dependency has **never been seen on screen**. Both
  halves are unit-tested, server and client. It needs a backoffice sign-in.
- **A delivery-API endpoint for redemption** and **shape (A), "email me a link"** are non-goals with
  reasons in `proposal.md`, not omissions.
- **Housekeeping of expired rows** (D7): correctness never depends on it — an expired or redeemed
  row is refused by the check at redemption, not by its absence — so rows simply accumulate. The
  open question in `design.md` says where it might live.

### Guards that fired during this change, and what each demanded

Worth reading as a group, because every one was right and none of them was amended to pass:

- the **durable storage surface** guard demanded the new table be recorded with a decision;
- the **schema-stability** guard in the integration suite demanded the migration be recorded against
  the concurrency guarantee it protects;
- the **view-coverage** universals picked up three new views without amendment and failed them;
- the **delegate-exemption** guard caught a static page named as a delegate;
- a **model-member** guard caught `LocalStart` referenced but changing nothing, because four
  fixtures shared one time;
- the **class vocabulary** guard caught BEM `block__part` where the house convention is
  `block-part`;
- the **catalogue** guard caught a setting catalogued but read by nothing;
- and **my own boundary guard caught my own over-claim** — that the persistence assembly could not
  see `CancellationSecret`, which stopped being true the moment minting was wired in. Narrowed to
  the stores, with the reason recorded, rather than deleted.

### Two tooling failures worth knowing about

Both cost real time and neither was a code defect:

- **A mutation harness that mutated nothing** and reported all three mutants "killed... Passed".
  Python on Windows does not resolve Git Bash's `/tmp`, so the backup never existed and the anchor
  assertion threw unseen. **Verify the harness actually changed the file before believing a
  result.**
- **A regex `\b` that arrived in source as a literal backspace (0x08)** via a heredoc, so a guard
  searched for `<BS>CancellationSecret<BS>` and failed for a reason with nothing to do with the
  code. Found with `cat -A`. A repo-wide sweep for control characters came back **0**. Heredocs have
  now corrupted source three times on this project; prefer the Write tool for anything with escapes.

## 14. QA round 4 — APPROVED

**Verdict: APPROVE.** Every number re-verified true. QA re-ran its own acceptance test in both
directions, and re-mutated the rewritten cascade guard rather than reading it — because an
`Assert.DoesNotContain` → `Assert.False(source.Contains(...))` rewrite is the kind of edit that
inverts silently.

It then swept every comment in the change that states a property and checked the code has it,
establishing each by a mutant rather than by reading. Its conclusion: nothing left claims a property
the code does not have.

**The record it wrote of the four rounds is worth keeping:** round 1 was five MAJORs about *guards
and published claims, never about the mechanism*. Rounds 2–4 were one fault climbing a ladder —
the artifact (`UnmetDependency`), the guard's claim (the class vocabulary), the guard's scope (the
localisation dictionary; the hardcoded controller list), and finally **the guard's own control**.

### The one thing it carried forward, and what was done with it

**A booking moved LATER silently kills its cancellation link.** The expiry is frozen at the start
the booking had when the secret was issued, so moving Monday → Friday leaves the booker holding a
link that dies on the Monday. Task 2.3 asked that whichever answer was chosen be recorded as
deliberate; the *extension* direction was recorded and tested, and the *shortening* one — the half a
site owner and a booker actually meet — was stated nowhere.

QA raised it as a MINOR in round 1, did not re-raise it in round 2, and said so plainly. It offered
"one sentence in `docs/configuration.md`" or a deferred obligation.

**Documented rather than deferred**, with a guard, because `move-booking` ships in this same 17.1.0
release and the two features meet on real sites from the first day. The doc says what happens in
both directions, why the package does not re-issue on a move (a second live link in one mailbox with
nothing to tell the reader which counts), and tells a site to warn the booker when moving a booking
significantly later.

### Environment, folded into the deferred obligations

QA reproduced the build collision independently and identified a **second** mechanism. Between them,
two ordinary races explain all three "unexplained" failures seen this session: solution-level
`--no-build` racing the npm target, and a background build overlapping a foreground run. Recorded,
with the reason it matters — two cheap explanations make it easy to dismiss a third that is real.

**Final state:** unit **1798**, integration **167**, rendering **1168**, client **290**; 0 warnings
in a clean Release build; `openspec validate --all --strict` 22/22. TestSite stopped, port free.

**Awaiting Chris for sync, archive and merge.**
