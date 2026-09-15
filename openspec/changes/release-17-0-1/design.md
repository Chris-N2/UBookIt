## Context

`17.0.0` is on nuget.org. The packed readme and the absent icon are both frozen for that version,
so the fix is a new version rather than an edit. The repository's own publication status is still
written as pre-publication, and the guard that exists to notice that is now due to fail.

Three constraints shape the approach:

- **`Directory.Build.props` already rejects a second readme**, in a comment with its reasoning:
  *"two versions of the truth are a defect waiting to happen."* Any solution that produces a
  nuget-flavoured readme alongside the repository one is arguing against a decision this project
  has already made and recorded.
- **nuget.org resolves a relative link against the package page.** There is no configuration that
  changes this, and no rendering hook the package controls.
- **The readme is immutable once pushed.** A guard that runs before the push is the only kind that
  can help; review has already failed at this once.

## Goals / Non-Goals

**Goals:**

- Every readme link resolves from a host that is not the repository.
- A future relative link, or a link to a document that has been renamed away, fails the suite
  rather than reaching a frozen package.
- The package family is visually identifiable on nuget.org and in the Package Manager.
- The repository stops claiming the package is unpublished, sentence by sentence rather than by
  disabling the guard that found them.

**Non-Goals:**

- Rewriting readme prose. Only link targets change.
- Proving over the network that a published page's links resolve. That stays a human check.
- Any change to shipped behaviour, public API, schema, or migrations.

## Decisions

### Absolute URLs in the one readme, not a second readme and not pack-time rewriting

Three options existed:

| | |
|---|---|
| **Absolute URLs in the single readme** | Chosen. One document, no machinery, works identically on GitHub and nuget.org. |
| A separate `README.nuget.md` | Rejected — it is exactly the two-documents-that-drift failure `Directory.Build.props` already refuses, and the drift would be silent. |
| Rewrite relative → absolute at pack time | Rejected — it keeps relative links pretty on GitHub, but the packed readme then differs from the reviewed one, so what a consumer reads is a document nobody looked at. It also adds an MSBuild target whose failure mode is a silently wrong readme. |

Absolute URLs are marginally less pleasant to read in the raw markdown. That is the whole cost.

### Links point at `blob/main`, and the branch plan is a known liability

A link must name a git ref. `main` means "the documentation as it stands", which is right for a
reader who has just installed the package. The alternative — pinning each release's readme to its
own tag — would make an old package's links describe that old package exactly, but it requires
generating the readme per version, which is the pack-time rewriting rejected above.

**This is recorded as a liability rather than solved:** the plan on record is for this branch to
become `dev/v17` when Umbraco 18 work begins, and `main` to become the v18 line. On that day every
`blob/main` link in the readme starts resolving to v18's documentation. The change that renames the
branch owns that problem; this design names it so it is inherited deliberately rather than
discovered.

### The guard derives the repository, and only checks paths it can check

The guard reads `RepositoryUrl` from `Directory.Build.props` and derives the expected
`https://github.com/<owner>/<repo>/blob/<ref>/` prefix from it. Nothing restates the repository —
the project has been bitten by restatement twice, in the publisher name and in the version.

Link classification is deliberately narrow:

- A target that is not absolute `https` → **fail**.
- An absolute URL matching the derived repository prefix → strip any `#fragment`, resolve the
  remainder against the working tree, **fail if no such file**.
- Any other absolute URL (the company website, an external reference) → **accepted unchecked**, and
  the requirement says so. Checking those means network access from a unit test, which makes the
  suite fail for reasons unrelated to the code.

### The icon follows the readme's existing packing pattern exactly

`PackageIcon` plus a `None … Pack="true"` item in `Directory.Build.props`, guarded on `IsPackable`
and `Exists()` — the same two conditions the readme item already carries, for the same two reasons
(do not attach it to test projects; do not fail a partial checkout with a packaging error).

The file is the publisher's existing app-icon mark, resampled once to 128×128 and committed at
`assets/icon.png` (2.7 KB). It is committed rather than referenced from outside the repository
because a package build must not depend on a path on one machine.

### Publication is recorded by working the guard's list, not by widening the guard

`No_document_claims_the_package_has_reached_a_feed` will fail. Each sentence it names is judged
individually: if publication made it true, it is admitted to `AcceptedPublicationMentions` with a
reason and an occurrence count; if it is still false or now misleading, the sentence is rewritten.
The guard is not relaxed, and no sentence is admitted in bulk.

### Decided during apply: the feed guard is re-premised, not deleted and not left lying

`No_document_claims_the_package_has_reached_a_feed` asserted an absence that was true until
2026-09-15 and false afterwards. Its failure message said *"It has not — nothing is pushed"*, so
from the moment of the push the guard was itself asserting the falsehood it existed to prevent.

Three options: delete it (forbidden by the runbook, and it would drop the only thing watching
these sentences); leave the name and message and just extend the allow-list (the guard then lies
in its own failure output, which is the exact fault this project has climbed a ladder over —
artifact, then guard name, then guard remarks, then the document around it); or re-premise it.

Re-premised, renamed `Every_mention_of_the_feed_is_accounted_for`, with the message, the remarks
and the runbook section describing it all moved together — all four rungs, deliberately. **The
new guarantee is weaker and the design says so outright:** "no document claims X" is decidable
offline, "every document claiming X is right" is not, because that is a fact about the feed. What
survives is the accounting — exact counts mean editing a document that mentions the feed forces a
fresh look at whether the new text is true.

### Decided during apply: pinned prose expires at the event it describes

A second guard went red that nothing predicted. `The_publishing_runbook_states_what_cannot_be_undone`
pinned the sentence *"When you publish, that guard will start failing. Do not delete it."* — future
tense, written when there had been no publication. Publication made it read as a prediction about
the past, so the guard was holding the repository to expired wording.

The guarantee (a red guard at publication is not an obstacle; do not delete it) was kept, and the
pin was moved to a sentence true **before and after any** publication: *"A guard that goes red at
publication is doing its job. Do not delete it."* There will be more releases; a pin written in the
future tense about a recurring event expires at the first one.

## Risks / Trade-offs

- **The existence check false-fails on a link to a path that legitimately is not a file** (a
  directory, a GitHub-rendered anchor on a tree) → the guard only resolves URLs under the `/blob/`
  prefix it derives, and a directory link would not carry that prefix.
- **The derived prefix goes stale if the branch is renamed** → named above as an inherited
  liability, and the guard fails loudly rather than passing on stale links, because the paths it
  resolves are checked against the working tree regardless of the ref in the URL.

  *This is worth stating precisely: the guard checks the **path**, not the **ref**. A readme
  pointing at `blob/main/docs/theming.md` after `main` has become the v18 line still passes, because
  `docs/theming.md` still exists here. The guard cannot see that liability and is not claimed to.*
- **The icon is the publisher's mark, not the product's** → acceptable and common for a package
  family; a product mark can replace the file later without touching the declaration.
- **`17.0.0` keeps its broken links forever** → accepted in the proposal's non-goals. `17.0.1`
  becomes the default landing page.
- **A patch release that changes only metadata still costs a version number** → the alternative is a
  launch page whose every documentation link is dead, for as long as 17.1.0 takes.

## Migration Plan

No migration. No schema, no API, no behaviour changes; `17.0.1` is install-compatible with `17.0.0`
in both directions. The publish itself follows `docs/publishing.md`, which this change also corrects.

## Open Questions

None outstanding. Unlisting `17.0.0` was considered and declined with reasons in the proposal.
