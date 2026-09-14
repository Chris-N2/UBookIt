# Tasks — release-17-0-0

## 1. The version itself

- [x] 1.1 `Directory.Build.props`: `<Version>0.1.0</Version>` → `17.0.0`.
- [x] 1.2 Verify the stamped manifest follows: build and confirm
      `wwwroot/App_Plugins/UBookItBackoffice/umbraco-package.json` carries `17.0.0`
      (the csproj replaces the `0.0.0` placeholder from `$(Version)` and fails the build
      if the placeholder is gone). `PackageCompositionTests` asserts it in the packed
      output — run it rather than assuming.

## 2. Version truth in the prose (design D1–D3)

- [x] 2.1 `README.md`: rewrite the `0.1.0` note for 17.0.0 — what the major means (tracks
      the Umbraco major, NOT a breaking-change signal), the departure from SemVer stated
      plainly, the breaking-change policy (minor, never patch; defaults or an upgrade
      path), and that leaving `0.x` is the stability promise. The dead "`1.0.0`" must be
      gone, not reworded around.
- [x] 2.2 `docs/mvp.md`: correct the `0.1.0` sentence; mark the document as the
      historical scope record it now is (design D3 declines folding it into the README —
      record the decline, it is instructed by the document itself).
- [x] 2.3 `CLAUDE.md` Conventions: the compatibility promise gains the minor-version
      rule, so the repository's own instructions are not the stalest account of the
      policy.
- [x] 2.4 `roadmap/version_roadmap.md`: mark the 17.0.0 row done.

## 3. Guards (design D1)

- [x] 3.1 A version-truth guard: parse `<Version>` from `Directory.Build.props` and
      assert every document that states a uBookIt version states THAT one. Derived, never
      hard-coded — a hard-coded `17.0.0` moves the drift rather than closing it.
      Anti-vacuity: the guard must fail if it finds no version claim at all.
- [x] 3.2 `DocumentationAssert` guards for the policy sentences a consumer acts on: the
      major is not a breaking-change signal, the SemVer departure, minor-not-patch, and
      the upgrade-path promise.
- [x] 3.3 Mutation-check both, against a COMMIT: bump the props version alone → 3.1
      fails naming the stale document; delete a policy sentence → 3.2 fails. Verify each
      mutant DIFFERS before trusting its verdict.

## 4. The deferred URLs (design D4) — NOT fixed here

- [x] 4.1 Record, and report to Chris, that `Directory.Build.props`'
      `PackageProjectUrl` / `RepositoryUrl` still name Azure DevOps and MUST be corrected
      before the package is published, once the GitHub URL exists. A blocker on
      publication, not on merge.

## 5. Verification

- [x] 5.1 Full suites green at Release, client suite green, Release `--no-incremental`
      build 0 warnings. Recount totals at HEAD for the QA handover — never reuse a
      previous count, and never accept a `--no-build` run as evidence (㉞'s lesson).
- [x] 5.2 `openspec validate --all --strict` clean.
- [x] 5.3 Pack and inspect: `dotnet pack -c Release` and confirm the produced packages
      carry `17.0.0` and the README, per the packaging spec's existing requirements.

## 6. QA

- [x] 6.1 QA round(s) — fresh subagent, reused across rounds; report claims for it to
      verify rather than trust; treat each round's fixes as new code.

## 7. Sync + archive (after QA approval)

- [x] 7.1 Sync the ADDED delta into `openspec/specs/packaging/spec.md`, then the
      falsified-sentence sweep wrap-normalised BY PATTERN over the whole tree — candidates
      to expect: anything saying uBookIt is pre-release, "0.1.0", "1.0.0", "not yet
      published", "the API may still move", or describing the roadmap as unfinished.
- [x] 7.2 Update memory: the versioning note's "open question" (majors for non-LTS
      Umbraco) and the Azure DevOps note, which the GitHub move will stale.
- [x] 7.3 Archive; merge after QA approval.

## Records

### 3.3 mutation evidence (at commit `074c126`, tree restored clean after each)

| Mutant | Result |
|---|---|
| `<Version>` → `17.1.0`, prose untouched | **Caught** — names README.md and both numbers |
| README's version claim reworded away (`uBookIt is at ...` removed) | **Caught** — anti-vacuity message says the claim is gone or reworded |
| "not a breaking-change signal" → "not a release signal" | **Caught** — policy guard names the missing sentence |
| "documented upgrade path" → "documented upgrade note" | **Caught** — policy guard names the missing sentence |

**One mutant was a no-op and its "pass" proved nothing** — a multi-line PowerShell
`Replace` against a CRLF file matched nothing, and the test passed because the file was
unchanged. Caught by printing whether the mutant differed, which is why that check is in
the loop. ㉞ recorded this lesson; it recurred within one change of being written down.

### 4.1 BLOCKER ON PUBLICATION, not on merge — the repository URLs

`Directory.Build.props` still carries:

- `PackageProjectUrl` → `https://dev.azure.com/NorwoodDesignDev/uBookIt`
- `RepositoryUrl` → `https://dev.azure.com/NorwoodDesignDev/uBookIt/_git/uBookIt`

Both are **private Azure DevOps URLs**, and the repository is moving to GitHub (Chris,
2026-09-14). They are deliberately NOT guessed here. **A package pushed to NuGet with
these uncorrected sends every consumer — and the Umbraco Marketplace listing — to a
repository they cannot open**, and NuGet.org does not allow a pushed version to be
edited: correcting it later costs a version number. The file's own comment names this as
the one place they change.

**Reported to Chris. Must be corrected in the same commit as, or before, the first push.**

## QA round 1 — REJECT (4 MAJOR, 3 MINOR, 3 NIT), and the fix (2026-09-15)

QA's closing instruction was the important one: MAJORs 1 and 2 are ONE fault — a sentence
stating something about the release that nothing checks — so the fix swept the class
rather than patching the four named lines.

- [x] R1.1 [MAJOR 1] **The change asserted the package is published; nuget.org 404s on
      it.** QA checked the feed. Three documents claimed it (`CLAUDE.md`, `docs/mvp.md`,
      `roadmap/version_roadmap.md`) — the exact defect class this change exists to close,
      reintroduced by the change, in the repository's own governing instructions.
      **Swept by pattern** (publication-claim family, emphasis-stripped — my FIRST sweep
      missed `it **is** published` because it stripped comment markers but not markdown
      emphasis, which is the wrapped-sentence trap in a new costume). Class is four:
      the three above, fixed; plus `openspec/specs/bookings/spec.md` ("`UBookIt.Core` is
      published and its surface is a compatibility promise") — **pre-existing, NOT fixed
      here**: it becomes true at publication and correcting it means replacing a
      requirement this change does not own, per the standing rule. Recorded for whichever
      change next touches `bookings`.
      **The repair shape is the keeper**: prefer sentences that stay true ACROSS the
      publication event ("the surface is declared stable from `17.0.0`", "the release is
      prepared and versioned") over sentences that need a second edit at it. A guard,
      `No_document_claims_the_package_is_already_published`, holds the line and is
      explicitly the thing to delete in the change that publishes.
- [x] R1.2 [MAJOR 2] **The version guard's file list was a sample** — QA set
      `roadmap/version_roadmap.md`'s identically-shaped claim to `16.4.2` and all three
      tests passed green. The population is now found **by phrase across every live
      document** (README, CLAUDE.md, docs/**, roadmap/**, openspec/specs/**; archive
      excluded as history), with `ClaimsAreFoundWhereTheyAreKnownToLive` pinning the four
      documents known to claim so the scan cannot pass over nothing. The residual limit —
      a NEW phrasing in a NEW document is not covered — is stated in the test's own XML
      doc and in `Directory.Build.props`, rather than left for the next reviewer to find.
- [x] R1.3 [MAJOR 3] The props comment claimed "every document" (two were checked) and
      "fails the build" (it fails the test suite). Both corrected; it now also tells
      whoever bumps the version that a new phrasing must be registered.
- [x] R1.4 [MAJOR 4] The publication blocker now lives in `Directory.Build.props` beside
      the URLs themselves — where somebody about to push will read it — not only in this
      file, which task 7.3 archives. It states the nuget.org immutability consequence and
      **why no test guards it**: there is no in-repo signal for "about to publish", and a
      test asserting the URLs are not the Azure ones would fail today, when they correctly
      are.
- [x] R1.5 [MINOR] README no longer promises "the release notes" (no CHANGELOG, no
      GitHub releases, and `PackageProjectUrl` is a private URL — a reader sent there
      arrives nowhere). Now: "called out explicitly rather than left to be discovered".
- [x] R1.6 [MINOR] `roadmap/version_roadmap.md:3` ("between `0.1.0` and its first full
      release") rewritten — it was falsified by line 31 of its own file.
- [x] R1.7 [MINOR] The README's major row said least of the three; it now says the API
      may change across an Umbraco major, since the CMS it targets did.
- [x] R1.8 [NIT] Both version patterns now tolerate wrapping symmetrically; `DeclaredVersion`
      asserts EXACTLY ONE `<Version>` rather than reading the first of several.
- [x] R1.9 [NIT, deferred to 7.1 as QA agreed] the "BREAKING (**unpublished**)" phrasings
      in `BookingModels.cs` and three specs — accurate as history, but that XML comment is
      what a 17.0.0 consumer meets in IntelliSense.

### R1 mutation evidence (at commit `12cd7e4`, each mutant verified to DIFFER first, tree restored)

| Mutant | Result |
|---|---|
| **QA's own demonstration**: roadmap claim → `16.4.2` | **Caught**, names `roadmap/version_roadmap.md` |
| `CLAUDE.md`'s claim → `9.9.9` (newly covered) | **Caught**, names `CLAUDE.md` |
| Re-add "and it is published, from `17.0.0`" | **Caught** by the publication guard |
| `docs/mvp.md` drops its claim entirely | **Caught** by the anti-vacuity pin |

At HEAD: 2697 .NET (1482 + 130 + 1085), Release 0 warnings.

## QA round 2 — REJECT (1 MAJOR), and the fix (2026-09-15)

- [x] R2.1 [MAJOR] **The publication guard was a denylist under a name that quantified
      over everything.** QA answered my own question with two live counterexamples: it
      added "uBookIt 17.0.0 is now live on nuget.org today" to `docs/mvp.md` — a scanned
      document — and all five tests passed; and `openspec/specs/bookings/spec.md`, in a
      walked directory, says "`UBookIt.Core` is published" while the guard named
      "No_document_claims_the_package_is_already_published" was green. The reasoning
      against it was already written eighty lines above in the same file, in
      `VersionClaimPatterns`' doc: *a denylist fences only what somebody thought of.*
      Applied to one guard and not its neighbour.
      **Took QA's option 2 (the real guard) over option 1 (rename to a regression pin)**,
      against its mild preference, because option 1 leaves the class unguarded and the
      class is precisely what round 1 showed I get wrong. Measured first: the publication
      vocabulary produces FOUR hits repo-wide, so an allow-list is tractable. Now:
      vocabulary × every live document, every hit classified or failing. **An allow-list
      fails closed.**
- [x] R2.2 [MINOR, taken] `LiveDocuments()` now walks `src/**/*.cs` and the two project
      READMEs. QA's argument is the project's own: `DocumentationAssert` strips `///`
      *"because these guards are asked about source files as well as markdown"*, and a
      claim in a public type's `<remarks>` is what a site author meets first, in
      IntelliSense. A scan walking only markdown was blind where the helper was built to
      see.
- [x] R2.3 [MINOR, taken] The biconditional guard: while the URLs are the `dev.azure.com`
      ones, the `CORRECT THESE BEFORE THE FIRST PUSH` block must be present — and when
      they are fixed, the stale warning must go. Fails on the only rot that can happen.
- [x] R2.4 [NIT, taken] The per-document anti-vacuity pin is now documented as deliberate.
- [x] R2.5 [NIT, taken] `docs/mvp.md:19` rewrapped.
- [x] R2.6 [QA must-fix 2] The declined `bookings/spec.md` item now has **two homes the
      archive cannot eat**: the deferred-obligations memory, and
      `AcceptedPublicationMentions` — where it is the one classified exception, with its
      reason, read by anyone who opens the guard.

### TWO FAULTS OF MY OWN, found by mutation, worth more than the fix

1. **The guard was green for the wrong reason, and only a mutant that FAILED TO FAIL
   showed it.** Removing the allow-list entry for `bookings/spec.md` left the test
   passing. Cause: the scan read raw text, and the document says `` `UBookIt.Core` ``
   **backticked** — as this repository quotes every identifier — so the pattern never
   matched, the allow-list entry was dead code, and QA's counterexample 2 was still live
   inside the fix for it. Markdown decoration defeating a raw pattern: the **third**
   costume of that trap in this one change (the first sweep missed `it **is** published`;
   `DocumentationAssert` exists because of the second). Fixed by stripping decoration
   before matching — and note the version scan deliberately does NOT, because it needs the
   backticks to delimit the number it captures.
   **The transferable rule: a mutant that leaves a guard green has not proven the guard
   wrong — it has proven you do not yet know what the guard reads.**
2. **A `git checkout` of a mutation ate the uncommitted fix** — the trap recorded in
   memory, hit again, in the same session that re-recorded it. The decoration fix was
   reverted silently and the suite went green against the BUGGY committed version. Caught
   by grepping for the fix after restoring. It is now its own commit (`6d7049f`) made
   BEFORE any further mutation.

### R2 mutation evidence (at commits `634a289` / `6d7049f`, mutants verified to differ, tree restored and the fix confirmed present after)

| Mutant | Result |
|---|---|
| **QA's counterexample 1**: "now live on nuget.org today" into `docs/mvp.md` | **Caught** twice (`nuget.org`, `is now live on`) |
| **QA's counterexample 2**: allow-list entry pointed elsewhere | **Caught** — names `bookings/spec.md: "UBookIt.Core is published"` (and **failed to fail** before the decoration fix) |
| Warning block tidied away while the private URLs remain | **Caught** by the biconditional |

At HEAD: 2698 .NET (1483 + 130 + 1085), 167 client, Release no-incremental 0 warnings,
21 items strict.

## QA round 3 — REJECT (1 MAJOR: one sentence), and the fix (2026-09-15)

QA's ladder observation is the finding, not the sentence: **round 1 the prose claimed more
than the package did; round 2 a guard's NAME claimed more than its body; round 3 a guard's
REMARKS claim more than its vocabulary.** It climbs one layer each time the check is
applied to the layer below the one being written. Its terminal question — *for every
sentence this documentation adds, is there a mutant that would falsify it?* — is the
discipline this round was done under, and every claim below has one.

- [x] R3.1 [MAJOR] "An allow-list fails closed — a new claim, in a phrasing nobody
      anticipated, fails until a human classifies it" was FALSE: QA changed one word
      (`is` → `was`) and it passed. The paragraph now states what it reaches and what it
      does not, **worded the same way `VersionClaimPatterns`' neighbour states the
      identical residual**, so the two guards in one file no longer disagree about their
      own reach.
- [x] R3.2 [free, taken] Tense alternation widened to `is|was|has been|have been`;
      `released to` added beside `published to`. QA's demonstration 2 is now caught.
- [x] R3.3 [NIT, taken] The allow-list accepts by **occurrence count**, consuming one
      allowance per hit — accepting an instance rather than a class, which is round 1's
      shape. **And an allowance nobody consumes now FAILS as dead code** — which is
      round 2's bug made impossible rather than merely fixed: that entry was dead for a
      whole round and nothing said so.
- [x] R3.4 [NIT, taken] `EnumeratePruned` prunes by directory NAME anywhere beneath the
      root; stated, with the verification that nothing tracked lives under a pruned name.
- [x] R3.5 [NIT, taken] `*.cshtml` and `*.ts` now walked — the last consumer-facing
      surfaces. Closes the category rather than declaring it latent.

### Every sentence, with the mutant that falsifies it (at commit `176e264`, tree restored)

| Claim in the documentation | Mutant | Result |
|---|---|---|
| "fails closed on every occurrence of the vocabulary it knows" | unclassified `nuget.org` into `docs/mvp.md` | **Fails** ✓ |
| (tense widening is real) | "uBookIt **was** published in September 2026" — QA's demo 2 | **Fails** ✓ |
| "does not see a claim phrased outside that vocabulary — `uBookIt was released to the public gallery` matches nothing and **passes**" | that exact sentence | **Passes** ✓ — the limit is true as written |
| "an allowance nobody consumes fails as dead" | correct the `bookings` sentence away | **Fails**, "1 unused" ✓ |
| "one allowance CONSUMED per occurrence" | second copy of the accepted sentence | **Fails** ✓ |

### The wrapping trap, FOURTH costume

Mutant D was a **no-op** on its first run and reported "Passed" — the spec sentence wraps
mid-clause, so a multi-line replacement matched nothing. Caught by printing whether the
mutant differed. In this one change that trap has now appeared as: a sweep missing
`it **is** published` (emphasis); a scan missing `` `UBookIt.Core` `` (backticks); and
twice as a mutation that changed nothing (CRLF, then wrapping). **The standing rule is
not "beware wrapping" — it is that every text-matching instrument, guard and mutation
tool alike, must be normalised before it is trusted.**

At HEAD: 2698 .NET (1483 + 130 + 1085), 167 client, Release no-incremental 0 warnings,
21 items strict.

## QA round 4 — APPROVE (2026-09-15), the three take-or-declines, and the sync

QA's verdict on the ladder: it **ended** this round, and its evidence is the right test —
the documentation now names a case where the guard does nothing and the mutant confirming
it expects *Passes*. A guard's documentation that predicts its own failure to fire, and is
right, is the opposite of the fault that had been climbing.

- [x] R4.1 [MINOR, taken] The dead-allowance check was unreachable behind the unclassified
      assertion — so a dead entry could hide behind an unrelated failure while the remarks
      called it "impossible to miss". Both lists are now collected and asserted once. The
      same sentence-stronger-than-mechanism shape, at NIT scale, closed rather than argued
      with.
- [x] R4.2 [MINOR, taken] Duplicate `(Document, Accepted)` keys are asserted with a message
      instead of thrown by `ToDictionary` — irrelevant at one entry, relevant exactly when
      the list grows at publication.
- [x] R4.3 [NIT, taken] The generated-TypeScript coupling is stated beside the `*.ts` entry,
      with the instruction to fix the generator input or exclude the file explicitly if it
      ever fires — **never by widening the vocabulary**.

## 7.1 Sync — the delta, and the sweep

- The ADDED requirement landed **VERBATIM** in `openspec/specs/packaging/spec.md`
  (scripted copy, byte-identity asserted). 21 items validate strictly.
- **Sweep run BY PATTERN, wrap- AND decoration-normalised** — my own fourth-costume rule
  applied to the sweep's own tooling, since the instrument is as defeatable as the guard.
  Two passes: the release-framing family, then a second pass aimed at what THIS change
  actually changed (mvp.md's status, the `0.x` framing, the roadmap prose).

**THE SWEEP'S REAL FINDING, and it is not this change's defect:**
`roadmap/version_roadmap.md` stated in the **present tense** that the delivery API "is on
by default" and that `UBookItDeliveryApiComposer` "registers it unconditionally" (:30,
:71-72). **0.9.0 flipped exactly that** — off by default in both directions, a disabled
direction absent rather than refused. So the sentence has been false since 0.9.0 merged,
and **0.9.0's own sync sweep did not catch it**. Fixed here because it is prose in a
roadmap, not a requirement — no delta, no wholesale replacement, none of the objection
that correctly stopped the `bookings/spec.md` fix. Two more of the same shape found and
corrected: "a page that currently only lists times" (falsified by 0.4.0) and "a group
holding the section grant today has no uBookIt verbs" (falsified by 0.10.0).

**And the structural fix, because three instances means the next one exists too:** the
roadmap now says in its header that the prose below the table is reasoning from the time
of writing, that corrections are noted inline where behaviour changed, and that **the
current truth is the README and the docs, never that file.** A document that describes
past reasoning cannot be kept true sentence by sentence forever; it can be kept honest
about what it is.

**Checked and NOT falsified**, with reasons: "frozen by the compatibility promise at the
first full release" (×5, specs + `.cs`) — timelessly true, and **adding `17.0.0` there
would create an unguarded version claim in files no version pattern reads, which is the
opposite of this change's purpose**; `BREAKING (unpublished)` (×4, R1.9) — true history
and still true, since nothing is published; it goes stale at publication, not now, so it
belongs to the change that publishes; `docs/mvp.md`'s "(It read `0.1.0` while this
document was still live)" — deliberate history; `docs/delivery-api.md`'s "flipped before
the first full release" — true; the `may still` / `is published` families — unrelated
domain usage.

- [x] 7.2 Memory updated: the `bookings/spec.md` publication claim recorded in
      deferred-obligations (and in `AcceptedPublicationMentions`, which the archive cannot
      eat).

At HEAD: 2698 .NET (1483 + 130 + 1085), 167 client, Release no-incremental 0 warnings,
21 items strict.

## QA round 5 (sync) — REJECT (1 MAJOR), and the fix (2026-09-15)

**The finding landed exactly where this project's other standing lesson says it would.** I
swept the whole tree, found and fixed a falsified present-tense claim in the roadmap's
PROSE — and never looked four lines up into the TABLE of the same file, where the identical
fact sat in the identical tense. QA found three survivors there (`:31` 0.4.0, `:32` 0.5.0's
permissions remark, `:37` 0.10.0), the first of which is the table twin of the prose
sentence I had just hand-corrected.

**A hand-edit is the strongest available signal that the class lives right there, and it is
the one region a sweep skips** — because a hand-fixed instance *feels* like the class has
been handled. ⑰ and ㉝ taught this; it is now at the top of
[[a-finding-enumerates-a-sample]] rather than buried in a change's tasks file.

- [x] R5.1 [MAJOR] **Both** of QA's offered fixes taken, because either alone leaves half the
      problem:
      (a) **The boundary.** The header said "read **the prose below the table**…", excluding
      the table — while I was correcting table cells. A disclaimer narrower than my own
      editing standard is not a disclaimer, it is a blind spot with a sentence in front of
      it. It now covers the whole file, on the honest grounds that *every row was written
      before the work in it existed*.
      (b) **The three survivors**, corrected the way row 0.9.0 was: past tense, plus an
      inline note where the outcome differed from the plan (`:31` now cites the shipped
      requirement; `:32` records that the permissions arrived in 0.10.0 and the settings
      screen is 17.1.0; `:37` records that the spike came back feasible and that the settings
      screen did NOT ride with it, correcting a plan the row still asserted).
- [x] R5.2 Re-swept **the table itself** by pattern afterwards — the region QA proved I had
      skipped. Clean: no present-tense marker survives any row. I also read all eleven rows
      by hand; row 0.6.0's "the default is exactly today's behaviour" is time-relative but
      not false (AutoConfirm does default on, verified at `SiteBookingSettings`), so it
      stands. **Line numbers are deliberately absent here: the header edit moved every one
      of them, and a stale citation inside a record about stale sentences would be the exact
      joke this change does not need.** The table has TEN data rows.
- [x] R5.3 Memory updated with the two durable rules QA asked be carried out of the tasks
      file: the sweep-your-own-edits rule and the disclaimer-scope corollary in
      [[a-finding-enumerates-a-sample]]; the green-mutant rule and the
      normalise-your-instrument rule in [[ubookit-guard-correctness]].

At HEAD: 2698 .NET, 167 client, Release 0 warnings, 21 items strict. The sync touched no
test and no source — three table cells, one header, and memory.

## QA round 6 — APPROVE (2026-09-15), three NITs taken

- [x] R6.1 [NIT] `:38`'s "the settings screen **rides** with this" was the one present-tense
      verb left in a cell whose neighbours were all converted. Now "was to ride with this",
      beside its own correction.
- [x] R6.2 [NIT] **The header vs the guard.** The widened disclaimer sat above the one
      sentence in the file that MUST be currently true — the version line, which
      `VersionClaimPatterns` reads and `DocumentsKnownToClaim` pins. Telling a reader to take
      the whole file as a record of the plan invited them to think that line need not track
      the build, while a test requires it to. The header now names the exception explicitly.
      **Taken rather than waved through because "a document disagreeing with what a guard
      enforces" is these six rounds in miniature** — it is the same fault as round 2's name
      and round 3's remarks, one more layer out, in the document instead of the test.
- [x] R6.3 [NIT] The R5.2 record's line citations were pre-edit and are removed rather than
      renumbered; the row count corrected to ten.
