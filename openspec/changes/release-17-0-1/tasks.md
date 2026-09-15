## 1. The readme's links

- [x] 1.1 Rewrite all nine relative links in `README.md` as absolute URLs under
      `https://github.com/Chris-N2/UBookIt/blob/main/`, preserving the one anchor fragment. Change
      nothing else in the file.
- [x] 1.2 Add `The_readme_links_resolve_from_anywhere` to `VersionTruthTests`: derive the repository
      prefix from `RepositoryUrl` in `Directory.Build.props`, walk every markdown link and image in
      the packed readme, fail any target that is not absolute `https`, and fail any target under the
      derived `/blob/` prefix whose path (fragment stripped) does not exist in the working tree.
- [x] 1.3 State the guard's reach in its own remarks: shape and local target existence only, never
      network reachability — and that a link whose ref is stale still passes because only the path
      is resolved.
- [x] 1.4 Mutation-check 1.2 four ways, verifying each mutant actually differs from the original
      before trusting its verdict: a relative link; a relative image; a `/blob/` link to a file that
      does not exist; and a repository prefix changed in `Directory.Build.props`. A mutant that
      leaves the guard green has proved the guard does not read what its name claims.

## 2. The icon

- [x] 2.1 Commit `assets/icon.png` — the publisher's app-icon mark resampled to 128×128 RGBA PNG.
- [x] 2.2 Declare `PackageIcon` in `Directory.Build.props` and pack the file with a `None` item
      carrying the same `IsPackable` and `Exists()` conditions the readme item already uses.
- [x] 2.3 Add `The_package_carries_an_icon` to `VersionTruthTests`: the declared file exists, is a
      supported raster format, is square, and is within NuGet's one-megabyte limit.
- [x] 2.4 Extend the existing packaging test that inspects produced `.nupkg` files so it asserts
      every package both declares an icon and physically contains the file it declares.
- [x] 2.5 Mutation-check 2.3 and 2.4: a declaration naming a missing file, and a non-square image.

## 3. The version

- [x] 3.1 Set `<Version>` to `17.0.1` in `Directory.Build.props`.
- [x] 3.2 Run the suite and let `VersionTruthTests` enumerate every document that states the
      version; update each one it names. Do not hand-search for them first — the guard's list is
      the population, and a hand-search is a sample.
- [x] 3.3 Split the version-claim patterns in two, because the bump proves the existing guard
      conflates them: claims about the version uBookIt is **at** must equal the declared version;
      **anchors** (`the first release is`, `declared stable from`) record history and must not
      move. Judge each of the six claims the guard listed on that basis; do not edit a true
      sentence into a false one to make a guard pass.
- [x] 3.4 Add `The_documented_anchors_do_not_move`. **Pin the anchors to the version this
      repository's own immutable archive records as the first release** — the earliest
      `openspec/changes/archive/*-release-<major>-<minor>-<patch>` — deriving it rather than
      writing a literal into the test. Keep the agreement and not-later-than-declared checks as
      secondary: they give a more specific message when they fire first, but **agreement between
      anchors is not sufficient and must not be relied on**, because a repo-wide find-and-replace
      moves every anchor together and leaves them agreeing by construction.
- [x] 3.5 Mutation-check 3.3 and 3.4. Each mutant must be verified to differ from the original
      before its verdict is trusted, and **each assertion must be proved independently** — during
      round 1 the "anchor ahead of declared" mutant tripped the *disagreement* assertion first, so
      the check it was meant to prove was still unproven. Required mutants: one anchor moved
      alone (fails for disagreement); **every anchor moved together to a version that agrees and
      is not later than declared** (must fail on the archive pin — this is the one round 1 missed
      entirely); an anchor ahead of the declared version with all anchors agreeing; and a stale
      current-version claim.
- [x] 3.6 **MODIFIED requirement: "The version a reader is told is the version the package
      carries"** (`packaging`). It is replaced wholesale, so diff the guarantees rather than the
      prose. Every SHALL and every scenario of the requirement as it stands in `openspec/specs/`,
      accounted for — **re-run after the round-2 rewrite, because a stale diff is exactly how a
      guarantee goes missing**:

      | Guarantee as it stands upstream | Disposition |
      |---|---|
      | A stated version SHALL be the declared one | **Narrowed** — applies to claims about the version uBookIt is *currently at*. Anchors to history are carved out explicitly, because the requirement as written demands "the first release is `17.0.1`" |
      | SHALL be verified against the declaration, not restated | **Carried forward**, and extended to the anchors: they are checked against the archive, which is derived, so no version literal enters the test either |
      | Documentation SHALL state what the version means | **Carried forward** unchanged |
      | The major SHALL NOT read as a breaking-change signal; departure from SemVer stated | **Carried forward** unchanged |
      | The policy SHALL be stated where a consumer meets the version | **Carried forward** unchanged |
      | Scenario: The documented version tracks the declared version | **Carried forward, title verbatim**; the WHEN confines it to current-version claims. Retitling it was reverted — OpenSpec matches scenarios by title, so a rename is indistinguishable from a deletion at archive time |
      | Scenario: A bump that misses the documentation fails | **Carried forward**, worded for current-version claims |
      | Scenario: The major is not a breaking-change signal | **Carried forward verbatim** |
      | Scenario: A patch never breaks a site | **Carried forward verbatim** |

      **Added** by this change — one SHALL and five scenarios:

      | Added | |
      |---|---|
      | SHALL: an anchor equals the first release named by the immutable archive, **and agreement between anchors is explicitly declared insufficient** | the guarantee round 1 lacked entirely |
      | Scenario: A version the documentation anchors to does not move with the release | |
      | Scenario: **A bump that moves every anchor together fails** | added in round 2 |
      | Scenario: **The pinned version is derived, not restated** | added in round 2 |
      | Scenario: Anchors that disagree with each other fail | secondary check |
      | Scenario: An anchor later than the declared version fails | secondary check |

      Nothing upstream is deleted. The single narrowing is disclosed in the requirement body.

## 4. Recording that publication happened

- [x] 4.1 Rewrite the Status section of `docs/publishing.md` to state that `17.0.0` was published to
      nuget.org on 2026-09-15, that all five packages indexed, and what remains (no CI, no Trusted
      Publishing, Marketplace submission outstanding).
- [x] 4.2 Run `No_document_claims_the_package_has_reached_a_feed`, and work the list it produces one
      sentence at a time. For each: admit it to `AcceptedPublicationMentions` with a reason and an
      occurrence count if publication made it true, or rewrite it if it is still false or now
      misleading. Do not admit sentences in bulk and do not widen the vocabulary to make the failure
      go away.
- [x] 4.3 Check `openspec/config.yaml`'s publication-status line specifically — it sits in the guard's
      scan and has been the blind spot once already.
- [x] 4.4 Verify no *pre-release framing* guard has gone red. Those assert absence, and publishing
      does not bring the sentences back; if one is red, something else changed.
- [x] 4.5 The feed guard's premise expires at publication: its message asserted "nothing is
      pushed". Re-premise it as an accounting guard and rename it
      `Every_mention_of_the_feed_is_accounted_for`, moving the name, the failure message, the
      remarks and the runbook section that describes it together. Do not delete or relax it, and
      state plainly that the new guarantee is weaker than the one it replaces.
- [x] 4.6 Repair the pinned runbook sentence that publication made stale — it was written in the
      future tense about an event that has now happened. Keep the guarantee, move the pin to
      wording that is true before and after ANY publication.

## 5. What the first publish taught the runbook

- [x] 5.1 Rewrite the push command in `docs/publishing.md` as a single PowerShell line. The current
      trailing `\` continuation is bash syntax and breaks in the shell this project is driven from.
- [x] 5.2 Document the key-ownership failure: a correctly scoped key still returns 403 if its owner
      cannot publish — specifically a nuget.org organization whose email is unconfirmed because it
      was given an address already belonging to a member account. Note that the 403 text names only
      the key, and that a personal-account key is the fastest way to isolate it.
- [x] 5.3 Document the post-push states — accepted, validating, indexed — that the package presents
      as unlisted throughout, and that `https://api.nuget.org/v3-flatcontainer/<id>/index.json`
      returning the version is the signal that restore will work.
- [x] 5.4 Add the ownership-transfer note: a package can be moved to an organization after
      publication from Manage Owners, so ownership need not block a push.

## 6. Verification

- [x] 6.1 Clean `bin`/`obj` of the packable projects and delete every stale `.nupkg`/`.snupkg` —
      `dotnet pack` is incremental and a stale artifact survives a rebuild that looks clean.
- [x] 6.2 Full clean Release build, then the suite as a separate `dotnet test` step. Never accept a
      `--no-build` run as evidence; it can execute stale assemblies and report green.
- [x] 6.3 Pack, then verify the produced `.nuspec` of all five packages: version `17.0.1`, icon
      declared and present, readme present, publisher and URLs unchanged from `17.0.0`.
- [x] 6.4 Confirm the packed readme contains no relative link, by reading the copy inside a
      produced `.nupkg` rather than the copy in the working tree.
- [x] 6.5 Hand the change to a QA subagent. Report build state, test count and what was verified
      live, and ask it to verify rather than trust each claim.

## 7. QA round 1 — REJECT (3 MAJOR, 7 MINOR, 3 NIT)

- [x] 7.1 **MAJOR 1** — `The_documented_anchors_do_not_move` could not detect the edit it exists to
      prevent: QA moved all four anchors to `17.0.1` together and all 11 tests passed. Mutual
      agreement only catches the sloppy edit; a repo-wide find-and-replace moves them uniformly by
      construction. Worse, the remarks justified the weakness by asserting *"Nothing in this
      repository still records that independently"* — **false**, and it is the ladder this change
      documents: the guard's remarks claiming more than its mechanism. Pin the anchors to the
      earliest archived `release-<version>` change; correct the remark to say what it now does.
- [x] 7.2 **MAJOR 2** — `roadmap/version_roadmap.md:7-9` claimed its version line is machine-checked
      against `Directory.Build.props` and "tracks the build". The split made that false, and a
      maintainer believing it would edit the anchor and produce MAJOR 1's falsehood, green.
- [x] 7.3 **MAJOR 3** — `Directory.Build.props` still told whoever bumps the version that "the prose
      has to follow", which for anchors is an instruction to falsify them. The feed guard was
      re-premised across all four rungs; the version guard's surrounding document was not moved at
      all. Same fault, same change.
- [x] 7.4 MINOR 1/2 — the anti-vacuity message named `VersionClaimPatterns`, now a computed array
      that cannot be added to; the feed guard cited reasoning that had moved. Both corrected.
- [x] 7.5 MINOR 3/4 — runbook: `## Before the first push` was expired prose of exactly the kind
      task 4.6 existed for; `rm -rf` and an `unzip` verify block were bash in a PowerShell project,
      two lines after a task that fixed the push command for being bash. Instance fixed, class
      swept. The verify pattern now includes `icon` and `readme`, the two frozen elements this
      release exists to correct.
- [x] 7.6 MINOR 5 — proposal claimed "three verbatim"; two are. Corrected, along with "nothing is
      dropped" sitting beside a disclosed narrowing.
- [x] 7.7 MINOR 6 — the 403-ownership lesson, the transfer note and the post-push states had no
      pin and no spec, so the thing that cost the first publish could be deleted silently. Added
      as an ADDED requirement with four `DocumentationAssert.Says` pins.
- [x] 7.8 NIT — the icon guard now checks the IHDR tag literally rather than assuming offsets 16
      and 20 are dimensions.
- [x] 7.9 MINOR 7 — `AvailableDatesCostTests` failed once on a clean Release run (195.5× against a
      120× threshold) and passed on re-run. Pre-existing, unrelated to this change, but it makes
      the runbook's "green from a clean build" gate unreliable. **Record as a deferred obligation;
      do not fix here** — a flaky perf threshold is its own change.
- [x] 7.10 Re-run the full suite, mutation-check the archive pin specifically against the uniform
      move QA used, and return to the SAME reviewer.

## 8. QA round 2 — REJECT (2 MAJOR, 4 MINOR, 4 NIT)

- [x] 8.1 **MAJOR A** — the Status line, the sentence this whole mechanism is built around, was
      rewritten in round 1 as *"uBookIt `17.0.0` was published on 2026-09-15"* — which names no
      feed, matches nothing in the guard's vocabulary, and so became **the one sentence in the
      repository able to make any claim about a package feed with nothing watching**. QA falsified
      it to a version that does not exist, on a date years away, with the suite green. The change's
      own defect class, landed on its own centrepiece. Rephrase it so **two** guards see it: name
      `nuget.org` (the accounting guard counts it, and removing the name leaves an unconsumed
      allowance) and state the version in the *first release* phrasing (the archive pin catches a
      falsified version). Pin it with `DocumentationAssert.Says`. Correct the paragraph that
      described the mechanism in the present tense, and state plainly that the **date** is still
      not machine-checked. Prove both halves by mutation.
- [x] 8.2 **MAJOR B** — the guarantee-diff table was not re-run after round 2's second rewrite of
      the MODIFIED requirement, so it recorded three added scenarios where there are five, still
      described the superseded "checked against each other" mechanism, and omitted the archive-pin
      SHALL entirely; 3.4/3.5 still specified the round-1 guard. Re-diff against the current delta.
      **This is the fourth consecutive change whose count in that list was wrong** — the table is
      an artifact that must be regenerated whenever the requirement moves, not written once.
- [x] 8.3 MINOR — `**BREAKING (unpublished)**` survived in three live specs and, worse, in XML
      documentation on a **public type** (`src/UBookIt.Backoffice/Models/BookingModels.cs:86`),
      which ships in the package and tells a consumer's IntelliSense the package is unpublished on
      a package they just restored from nuget.org. The word is outside the feed guard's vocabulary,
      so "work the guard's list" structurally could not find it — which is why the artifacts'
      claim to have finished that sweep overstated. Reworded to "made before the first release
      reached a feed", which stays true.
- [x] 8.4 MINOR — `Directory.Build.props` "before the first push" (expired prose, ~30 lines from
      the comment rewritten in round 1: *the region you are already editing is the one you sweep
      least*); the verify snippet now names the stale-artifact cause instead of dying with a
      null-reference cascade when the glob matches more than one `.nupkg`; proposal's "three of
      six"/"three true sentences" corrected to four, and its stale guard name and "currently"
      annotated.
- [x] 8.5 NIT — `` -split "`n" `` instead of a literal newline, which survives reflow.
- [x] 8.6 Re-run the full suite and return to the same reviewer.

## 9. QA round 3 — REJECT (1 MAJOR, 2 MINOR)

- [x] 9.1 **MAJOR — DID NOT REPRODUCE, but the shape it named was real and is now removed.** QA
      reported `openspec validate release-17-0-1 --type change` failing because the MODIFIED block
      renamed an upstream scenario, and OpenSpec matches scenarios by title so a rename reads as a
      deletion. On the tree as handed over, `--type change`, `--strict` and `--all --strict` all
      pass (**21 passed, 0 failed, exit 0**).
      **SUPERSEDED IN ROUND 4 — the contamination theory recorded here was wrong.** QA reproduced
      its failure on an untouched tree and found the real variable: it ran
      `@fission-ai/openspec@1.13.0` via `npx …@latest`, this machine has **1.6.0** globally. Same
      tree, same command, opposite verdicts, no contamination. See 9.2 and 10.2 — and read the
      instruction below as historical, since 9.2 now records exactly the validator limitation this
      line told you not to record. **The diagnosis was still correct**: exactly one
      delta scenario title differed from upstream. Reverted to the upstream title verbatim, with
      the narrowing expressed in the WHEN — strictly safer at archive time whatever the validator
      currently accepts. Do not record this as a validator defect; record it as a title that should
      never have moved.
- [x] 9.2 **Add `openspec validate --all --strict` to the verification steps** — with its reach
      stated, because an unqualified "run the validator" would be the over-claim this change keeps
      finding. **The installed CLI is `@fission-ai/openspec` 1.6.0, and 1.6.0 does not enforce
      scenario preservation on a MODIFIED requirement at all.** That rule is the mechanical form of
      the one thing CLAUDE.md devotes a whole section to. `1.13.0` does enforce it — that is the
      entire explanation of the round-3 disagreement, and it is why the hand-diff in task 3.6
      remains the real check here, not a formality the tool duplicates. Run the validator for
      everything else it catches; do not let it stand in for the diff.
- [x] 9.3 MINOR — the "not machine-checked" disclaimer named only the date, implying everything
      else was covered. QA proved two more gaps on a clean tree: **polarity** (the pins are
      substring matches, so "uBookIt has NOT reached nuget.org" satisfies them exactly as well as
      the true sentence) and the **"all five packages are indexed and restorable"** clause. Both
      now named, with the note that only the feed itself can make the section true. *A disclaimer
      narrower than your own editing standard is a blind spot with a sentence in front of it.*
- [x] 9.4 MINOR — `proposal.md` Impact omitted four modified files, including a shipped source
      file's public XML documentation and three main specs edited **in place rather than through a
      delta**. Neither `ChangeDeltaIntegrityTests` nor `openspec validate` can see an in-place spec
      edit, so Impact is the only place that scope is approved. Listed, with the reason.
- [x] 9.5 Re-run: `openspec validate --all --strict`, clean Release build, full suite, and return
      to the same reviewer.
- [x] 9.6 The feed allowance moved 16 → 18: the round-3 disclaimer names `nuget.org` more often.
      Caught by the guard on the verification run, which is the mechanism behaving exactly as
      designed — an exact count means you cannot edit this document without re-reading whether
      each new mention is an instruction, a true record, or a claim nobody checked.

## 10. QA round 4 — REJECT (1 MAJOR, 1 MINOR)

- [x] 10.1 **MAJOR — the pin was defeated by the paragraph written to explain the pin.** Round 3's
      disclaimer quoted QA's falsification verbatim, and that quote reproduced the pinned substring
      `reached nuget.org on`. `DocumentationAssert.Says` is satisfied by ANY occurrence, so the pin
      silently moved off the Status line and onto the prose about itself: QA rewrote the Status line
      to *"arrived at nuget.org in September 2030"* — a phrasing the pin does not recognise, a date
      four years out — and all 11 guards passed. **The round-2 guarantee, partially reversed by the
      round-3 fix, in a new costume of this project's oldest fault.**
      Fixed at BOTH levels: the disclaimer now describes the proof instead of reproducing it, and
      `DocumentationAssert.SaysOnce` enforces the general rule — *a pinned string must not be quoted
      elsewhere in the document it pins* — across all 19 runbook pins, so the next one fails rather
      than being noticed.
- [x] 10.2 **MINOR (and the adjudication of the round-3 dispute).** Neither side was wrong: QA ran
      `@fission-ai/openspec@1.13.0` via `npx …@latest`, this machine has 1.6.0 globally. Same tree,
      same command, opposite verdicts. Recorded in 9.2 with the consequence that matters — at 1.6.0
      the validator cannot perform the check 9.2 was adopted for.
- [x] 10.3 Feed allowance 18 → 17 after the paraphrase. Third time this count has moved in three
      rounds; each move was a real edit to the runbook and the guard caught every one.
- [x] 10.4 Re-run validator, clean build, full suite; mutation-check QA's Status-line rewrite and
      the duplicate-pin case; return to the same reviewer.

## 11. QA round 5 — APPROVE (no CRITICAL, no MAJOR)

- [x] 11.1 MINOR — `SaysOnce`'s distinguishing branch (`occurrences > 1`) was exercised by nothing
      in the suite: all 19 pins sit at exactly 1, so only QA's mutations touched it. It worked, and
      nothing kept it working. Five cases added to `DocumentationAssertTests`, including a
      **wrapped** and a **decorated** duplicate — the normalisation trap that has bitten this
      project four times would land exactly there, on an instrument that matches across wrapping
      but counts without it.
- [x] 11.2 MINOR — task 9.1 still carried the contamination theory and an instruction ("do not
      record this as a validator defect") that 9.2 now disobeys. Annotated as superseded rather
      than rewritten, so the round-3 reasoning and its correction both stay readable.
- [x] 11.3 NIT — the `SaysOnce` pin comment claimed the Status line names the feed; the mechanism
      proves only that ONE sentence in the document does. Corrected, with the residue stated and
      attributed to the polarity exemption already disclosed.
- [x] 11.4 NIT — markdown `**bold**` inside an XML doc comment renders as literal asterisks;
      changed to `<b>`.
- [x] 11.5 Deferred, both recorded in memory with their measurements: the duplicated pin in
      `docs/notifications.md` (same defect class, found by QA applying this change's lesson
      outward — **and explicitly unmeasured beyond that one instance**), and the OpenSpec CLI
      upgrade owed before the next MODIFIED requirement.
- [x] 11.6 Final verification.
