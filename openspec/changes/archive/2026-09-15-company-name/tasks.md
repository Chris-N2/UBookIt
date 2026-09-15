# Tasks — company-name

## 1. The name

- [x] 1.1 `Directory.Build.props`: `Authors`, `Company`, `Copyright` → `Norwood Design &amp;
      Development Ltd.` (XML-escaped ampersand — a bare `&` fails the build).
- [x] 1.2 `LICENSE`: the copyright line names the correct entity. Plain text: literal `&`.
- [x] 1.3 `README.md`: licence footer matches. Plain text: literal `&`.

## 1.4 Scope addition, mid-apply (Chris, 2026-09-15)

- [x] The README footer links the company site, `https://www.norwood-development.co.uk`.

**Where it goes, and why NuGet has no other slot.** A `.nuspec` has exactly one project link
(`PackageProjectUrl`), and it is correctly the GitHub repository — an OSS consumer clicking
"Project website" wants the source and the docs, not a company page. `owners` is not rendered
by nuget.org (the listing shows the account that pushed). **But `PackageReadmeFile` is set, so
`README.md` ships inside every package and nuget.org renders it as the listing body** — a link
in the footer is therefore on the NuGet page, which is what was actually being asked for.

**Deliberately not guarded.** The publisher NAME is guarded because the licence depends on it
and five copies had already drifted. A company link is attribution, not a claim a consumer acts
on, and a guard asserting a URL is present would be the over-guarding this project keeps having
to unwind. Recorded so the asymmetry is a decision rather than an oversight.

**One property of the timing worth stating**: the README is packed INTO the package, so
`17.0.0`'s copy points at that URL permanently. The site is a placeholder today and fills in
this week (Chris) — fine, because the link target changes without the package changing. It
would NOT have been fine to link a URL that might move.

## 2. The guard (design D1–D3)

- [x] 2.1 Derive the name from `<Company>`, XML-decode it, assert `LICENSE` and `README.md`
      state it. Anti-vacuity: fail if the declaration is missing or empty.
- [x] 2.2 Assert `Authors` and `Company` agree — distinct NuGet fields, nothing else stops the
      listing and the assemblies disagreeing about the publisher.
- [x] 2.3 Mutation-check against a COMMIT, each mutant verified to DIFFER first: change
      `<Company>` alone → fails naming the stale file; revert `LICENSE` alone → fails; make
      `Authors` disagree with `Company` → fails.

## 3. Verification

- [x] 3.1 Full suites green at Release, client green, `--no-incremental` 0 warnings. Recount at
      HEAD. Run `dotnet build` and `dotnet test --no-build` as SEPARATE steps — the recorded
      pack-contention flake only appears when one invocation does both.
- [x] 3.2 `openspec validate --all --strict` clean.
- [x] 3.3 **Pack and read the produced `.nuspec`** — `authors` and `copyright` must show the
      full legal name with a literal `&`. The props file is not the artifact; the artifact is.

## 4. QA

- [x] 4.1 QA round(s) — fresh subagent, no editing of the tree while it runs (㉟ round 1's
      CRITICAL came from exactly that).

## 5. Sync + archive

- [x] 5.1 Sync the ADDED requirement into `openspec/specs/packaging/spec.md`; falsified-sentence
      sweep BY PATTERN, wrap- and decoration-normalised, **including the lines edited by hand**.
- [x] 5.2 Archive; merge after QA approval.

## QA round 1 — REJECT (1 MAJOR, 1 MINOR, 3 NIT), and the fix (2026-09-15)

- [x] R1.1 [MAJOR] **The README named the publisher TWICE and only one spelling matched.** The
      link label added mid-apply read *"Norwood Design & Development"* — no `Ltd.` — and the
      guard's `Contains` was satisfied by the copyright line above it. QA proved it by renaming
      every declared location to a bogus name and watching the suite go green while the footer
      still carried the old one.
      **This is the original defect's exact shape — the correct name minus a component — and I
      reintroduced it in the line added to fix something else.**
      Fixed two ways, because the label alone would close the instance and not the class:
      (a) the label is now the declared name, so it falls under the guard for free;
      (b) **the guard checks EVERY occurrence of the name's first word**, not mere containment
      — each must begin the full declared name. Containment finds an instance; enumeration
      closes **the class of near-miss that begins with that word, matching case** — which is
      the one that shipped. A case variant or an abbreviation shares no marker and still
      passes (QA round 2 demonstrated both), and the guard's remarks say so rather than
      implying wider cover. Precondition, also recorded there: the first word must not occur
      in ordinary prose in those files, or the guard fails on a correct tree.
      *Chris's call outstanding, non-blocking*: if he prefers the trading name without `Ltd.`
      in a footer, the label changes and the decision gets recorded here — but the default is
      the guarded, consistent spelling.
- [x] R1.2 [MINOR] **`docs/publishing.md` omitted the very fields this change proved are
      frozen.** Its one-way-door list named "project URL, repository URL, licence, description,
      icon" and not authors/copyright, and its verify command grepped
      `projectUrl|repository|license` — so a maintainer following the runbook's own
      "check the produced `.nuspec`" instruction would never have seen the wrong company name.
      The document whose job is to enumerate one-way doors was missing the door this change
      walked through. Both fixed, with the reason named.
- [x] R1.3 [NIT ×3] Dates I invented (`2026-09-16`) corrected to the commit's actual date;
      task 2.3 ticked (the ledger disagreed with the handover); the mid-apply scope addition
      moved from after §5 into §1 where it belongs.

### R1 mutation evidence (at `a3c5ffb`, each verified to DIFFER first, tree restored)

| Mutant | Expected | Result |
|---|---|---|
| Footer label drops `Ltd.` — **QA's exact finding** | fail | **Fails** |
| Every declared location renamed consistently to a bogus name | **pass** (the stated limit) | **Passes** |

The second is the terminal-question check on the remarks' limit sentence — a guard that claims
not to verify legal correctness must PASS when the name is consistently wrong, and it does.

At HEAD: 2700 .NET (1485 + 130 + 1085), Release no-incremental 0 warnings, 21 items strict.

## QA round 2 — APPROVE (2 MINOR, 2 NIT), and the fix (2026-09-15)

QA closed the MAJOR by mutant (footer dropping `Ltd.` now fails naming file and offset) and
went further than I had: it injected a near-miss into the **`LICENSE`** body — a file it never
touched in round 1 — and confirmed that fails too, so the enumeration is load-bearing rather
than incidental. It also proved the new loop's boundary safety both ways (`Norwood` as the
literal last token fails cleanly with no `ArgumentOutOfRangeException`; a file ending in the
exact name passes).

- [x] R2.1 [MINOR] **"checking every occurrence closes the class" over-claimed**, and QA
      falsified it with two near-misses that PASS: an all-caps variant (the scan is Ordinal, so
      the marker is never found) and an abbreviation (`NDD`, which shares no first word). What
      the guard actually closes is *every mention beginning with the declared name's first
      word, matching case* — which contains the defect that shipped, and is not "the class".
      **The same fault this project keeps recording: a remark naming the mechanism's ambition
      rather than its guarantee.** Corrected in BOTH homes — the guard's remarks and this
      record — because the claim had been written twice.
- [x] R2.2 [MINOR] **The marker's precondition was undeclared.** QA built the case: a correct
      tree, consistently renamed to a company whose first word appears in prose, fails at a
      real offset with a message that read as an accusation. Now stated in the remarks, and the
      failure message distinguishes the two causes and says **"do not reword the prose to
      satisfy the test"** — the wrong fix being the obvious one.
- [x] R2.3 [NIT] `docs/publishing.md` pointed at "the `company-name` change", which task 5.2
      was about to archive — a stale pointer in the one document read before an irreversible
      action. Rewritten archive-stable.
- [x] R2.4 [NIT] "caught with hours to spare" was an unanchored flourish that dates badly; the
      checkable half (these fields have been wrong here before) is kept.

## 5.1 Sync

ADDED requirement landed **VERBATIM** in `openspec/specs/packaging/spec.md` (byte-identity
asserted); 21 items strict. **Sweep by pattern over every live mention of the name**, including
the lines edited by hand: six live mentions, all carrying `Ltd.`; the two in
`VersionTruthTests` quote the WRONG name deliberately, describing the defect, in a file the
guard does not walk and that ships to nobody. The only surviving "closes the class" phrase is
in `NotificationDocumentationTests`, about markdown discovery, where a scan genuinely does
close its class — not this over-claim.

At HEAD: 2700 .NET (1485 + 130 + 1085), Release no-incremental 0 warnings, 21 items strict.

**Outstanding for Chris, non-blocking:** the footer label uses the full legal name. If he
prefers the trading name without `Ltd.`, the label changes and this line records the decision.
