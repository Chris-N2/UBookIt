## Context

This follows `release-17-2-1` (archived 2026-09-25) step for step. Read that change's `tasks.md`
for the procedure and for the four QA rounds on its changelog paragraph. This design records only
where the 18 line differs.

## Decisions

### D1. The same flow: a PR into `dev/v18`, packed from the merge commit

Branch `release-18-1-1` goes to a PR into `dev/v18` (CI runs on pull requests into either line),
merged with a merge commit. Pack only after **the merge commit's own push run** is green at every
step. The tag `18.1.1` goes on that commit. SourceLink embeds that SHA.

### D2. The registration is written by hand, and its evidence re-checked

A cherry-pick of `main`'s hunk would conflict or misapply, because this line's entry prose differs
from main's around the edited line. So the count changes `6 → 8` by hand, with a reason naming
the same two sentences. The feed evidence (`17.2.0`/`18.1.0` library nuspecs carry the tag) is
**re-fetched**, not copied, because a claim copied from another record is exactly what this
project's QA keeps refuting.

### D3. The `17.2.1` entry is copied byte-for-byte

It is history on nuget.org. Verify with a diff of the two entries between `main` and this branch,
which must be empty.

### D4. The `18.1.1` entry reuses `17.2.1`'s wording where the facts are the same, and differs where they aren't

The privacy-link paragraphs are the same code on both lines (`9cd14ef` is the cherry-pick of
`327783e`). **Check that per file** rather than assume it:
`git diff 327783e 9cd14ef -- <the three src files>` must show no content difference. The
Marketplace paragraph differs: on this line the description was **wrong** (it said 17), so the
entry says so, and says `18.1.1` says Umbraco 18.

## Risks / Trade-offs

- **[Stale build output after switching lines]** → Clear the outputs before every build here.
  The unit total on this line is **1994**, and any other number means the wrong binaries.
- **[A changelog sentence true on 17 but not on 18]** → Every sentence of the `18.1.1` entry is
  checked against `18.1.0` vs HEAD on this line, not against the 17 line's record.
- **[publishing.md shared prose]** → Lines 83/397 name both versions and become fully true once
  this publishes. Verified at 8.2, not edited.
