## Context

`docs/publishing.md` was written as a manual procedure: pack locally, verify by hand, push with
an API key. `trusted-publishing` (㊿①) moved releases into `publish.yml`. It kept the manual
procedure whole as *Fallback: pushing by hand*, and the three sections it depends on stayed in
place: *The order that makes SourceLink correct*, *Verify, do not assume* and *Publish only from
a commit that is already on the public repository*. They still read as instructions.

Two `packaging` requirements are met only by those sections and by the 403 lesson inside the
fallback:

- *The package points a consumer at a public home* requires the docs to state that source links
  follow the remote, to give the order that makes both correct, to require produced artifacts to
  be inspected, and to require the packed commit to be on the public repository.
- *The publishing runbook records what a publish actually cost* requires the 403 lesson (check
  the key's **owner**), that ownership can be transferred later, and the flat-container endpoint.

`VersionTruthTests.The_publishing_runbook_states_what_cannot_be_undone` pins those sentences with
`SaysOnce`. `AcceptedPublicationMentions` counts `nuget.org` in this file exactly (23 today).

The fallback text is identical on both lines. Only version literals differ in this file.
`VersionTruthTests.cs` differs between lines by about 290 lines, so a cherry-pick of the test edit
is not expected to apply cleanly.

## Goals / Non-Goals

**Goals:**
- No procedure for a hand push remains in the runbook, on either line, and the runbook says so.
- Both `packaging` requirements stay satisfied with no edit to `packaging`.
- Every pinned sentence the edit touches keeps its guarantee, under the same wording or a pin on
  the new wording. The one exception is the dropped fallback, which is recorded.

**Non-Goals:**
- Any workflow, script or policy change. Any other tidy item. Any release.

## Decisions

### D1. Reframe the manual sections rather than keep or delete them (option C)

| Option | Outcome |
|---|---|
| A. Delete only the push section | The SourceLink, Verify and SHA sections still tell a maintainer to `git remote set-url`, `dotnet clean`, `dotnet pack`. That is a hand procedure in all but the push, so the docs would still imply a route that no longer exists. |
| B. Delete them too | `packaging`'s SHALLs lose their only home. Satisfying them again needs a MODIFIED `packaging` requirement, a wholesale replacement for content that is still true. |
| **C. Reframe** | Each hazard stays, as the reason for a check the workflow makes. `packaging` stays satisfied as written. |

Concretely:

- **The order that makes SourceLink correct.** Keep the three-surface table, which shows where
  each URL comes from, and the explanation that a pack made before the remote moved carries stale
  links. Replace the numbered command block with what removes the hazard: the workflow packs
  from a fresh clone of the public repository. That covers the order (the properties are
  committed, the clone's remote is the public one, and the build is fresh) and the stale-artifact
  hazard (a fresh clone has no earlier `.nupkg` for `GenerateNuspec` to skip over). The
  `GenerateNuspec` explanation stays as the reason a clean clone matters, not as "Step 3".
- **Verify, do not assume.** The workflow verifies version, repository, commit, symbols and
  SourceLink for every package. The person still reads a `.nuspec` for authors, copyright,
  licence and description, which nothing checks. Keep that as the pre-approval check. Point it at
  the run's `packages` artifact, not at `src/**/bin/Release`, and keep the note that a `.nupkg`
  is a zip.
- **Publish only from a commit already on the public repository.** Keep the SHA explanation.
  Replace *"Pack from the merge commit on main, after it is pushed"* and its `git branch -r
  --contains` block with what enforces it: the check job refuses a tag whose commit no published
  line contains. That wording is true on both lines, which retires the `dev/v18` "on main" debt.

### D2. "No hand route" is stated, not left implicit

Deleting the fallback silently would leave a reader to wonder whether it was lost or never
written, and would leave the old *"If the workflow cannot publish"* question unanswered. The
runbook says: there is no hand route. If the workflow cannot publish, the release waits, or the
workflow is fixed in its own change and the release then goes through it. The reason is a hand
push skips the tag check, the clean pack, the package verification, the approval and the push
order.

Rejected: an "emergency only" sentence explaining how to create a key. It would be the fallback
again under another name, and a key can be created on nuget.org without help from this document.
Git history and the archived `trusted-publishing` change keep the old procedure for anyone who
decides otherwise deliberately.

### D3. The 403 lesson moves under *Trusted Publishing setup*

The workflow's `dotnet nuget push` uses the key the token exchange returns. A policy whose owner
cannot publish therefore produces the same 403 text, which names only the key. The section keeps
its checks, reordered for the policy and, after QA round 1, grown to four:

1. Can the policy's owner publish? An organization's email must be confirmed separately.
2. The scope. The policy's *new versions of existing packages only* cannot create a package ID.
3. The package selection. A glob that does not match the ID, or an empty selection.
4. Is the ID owned by someone else?

*QA round 1 corrections.* (a) The order now matches the spec (owner, scope, selection). (b)
According to Microsoft Learn (*Trusted Publishing on nuget.org*, Policy Ownership), an ownership
problem marks the policy **inactive** with a warning in the UI. So it may surface as a refused
token exchange rather than a push 403, and the runbook says so, noting that neither has been seen.
(c) The isolation step adapted from the key lesson ("try a personally owned policy") was wrong,
because a policy applies only to its owner's packages. It is replaced by "open the policy on the
Trusted Publishing page", plus checking the organization's address directly. That page shows the
inactive state.

It also keeps the ownership-is-transferable paragraph. The existing *"The same `403` on the
workflow route"* paragraph and the policy-notes bullet about the narrow scope are merged into it,
so the lesson is stated once.

`packaging`'s scenario (*"a 403 naming the API key → check whether the key's owner can
publish"*) stays true as written. The key is the exchanged one, and its owner is the policy's
owner. The re-homed text should say that in one clause, so the connection is explicit rather
than inferred.

### D4. The spec delta is REMOVED + ADDED, not MODIFIED

OpenSpec 1.13 refuses a MODIFIED block that drops a scenario, which is the right behaviour.
Dropping *The manual route remains available* is the point of the change, so it is recorded as a
REMOVED requirement with its reason. An ADDED requirement restates every other guarantee:

| Old guarantee | Fate |
|---|---|
| Tag push through the workflow is the route | Carried, as *the only* route |
| One-time setup stated, with exact values | Carried, and scenario kept |
| Policy belongs to the owning account; transfer needs a new policy | Carried, and **gains** a scenario (it had none) |
| Status section does not list Trusted Publishing as outstanding | Carried, as SHALL NOT |
| Manual procedure SHALL remain documented, labelled fallback | **Dropped**, replaced by "no hand route" |
| Scenario: maintainer releasing follows the workflow | Carried verbatim |
| Scenario: setup can be reproduced | Carried verbatim |
| Scenario: the manual route remains available | **Dropped**; replaced by *The workflow cannot publish* |

New in the ADDED requirement: the explained-not-prescribed rule (D1) and the 403-to-policy
direction (D3), each with a scenario.

### D5. Pins: same guarantee, possibly new words

Every `SaysOnce` pin in the touched sections is checked one at a time:

| Pin | Disposition |
|---|---|
| `check the produced \`.nuspec\`, not the source, before you push` | Untouched (line 22) |
| `This order is load-bearing, not tidiness` | Kept, introducing the table |
| `a package built before the remote moved carries the old SourceLink URLs` | Kept |
| `DELETE the old artifacts`, `Step 3 is not housekeeping` | The step is gone. The guarantee, that the stale-artifact hazard is documented, is re-pinned on the new sentence saying a fresh clone has no earlier `.nupkg` to survive |
| `Each package carries its own metadata, so check all five` | Kept if the sentence survives the Verify reframe, otherwise re-pinned on its replacement |
| `SourceLink embeds the commit SHA` | Kept |
| `Pack from the merge commit on main, after it is pushed` | Replaced by a pin on the sentence saying the check job refuses a commit no published line contains |
| `an organization has its own email address that must be confirmed` | Kept, in the re-homed 403 section |
| `push new packages and package versions` | Describes a **key** scope. Re-pinned on the policy-scope sentence (D3, check 2) |
| `transferred to an organization` | Kept |
| `the flat-container endpoint` | Kept (*After the push*) |

A new pin covers the "no hand route" statement, because the guarantee that replaced the fallback
must not be the one sentence nothing watches. `AcceptedPublicationMentions` for this file is
recounted after the edit. Each removed or added mention is judged in the reason text, following
the `22 → 23` entry before it, not by adjusting the number to fit.

### D6. Both lines, with an archive on each

The change is applied on `main` (branch `remove-publish-fallback`), and then on `dev/v18` (branch
`remove-publish-fallback-v18`). The docs and README hunks carry over with version literals kept
per line. The test edit is redone by hand on the v18 file, because the files differ. QA reviews
both branches. Each line archives the change into its own `openspec/`, as `trusted-publishing`
did, so each line's `release-publishing` spec matches its runbook.

## Risks / Trade-offs

- **A reframed section still reads as instructions.** The likely survivor is a command block
  left behind. Mitigation: no `dotnet pack`, `git remote set-url` or `dotnet nuget push` command
  remains in the runbook, checked by grep at apply and stated in the handover for QA to verify.
- **A pin disappears with its sentence and nothing notices.** `SaysOnce` fails on absence, so
  deletion is loud. The quiet risk is weakening a guarantee by re-pinning on a vaguer sentence.
  Mitigation: D5's table is the checklist, and QA compares each new pin's sentence with the old.
- **The README sentence ships late.** It reaches nuget.org only with each line's next release.
  The readmes already published keep describing a fallback that existed when they shipped, and
  that is accurate for them.
- **No hand route in a real emergency.** Accepted (D2). The cost is a release delayed until the
  workflow works. The alternative is a release that skips every check, which this repository has
  shown to be the worse trade.
