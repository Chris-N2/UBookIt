## Context

`17.2.0` was committed as a change directly on `main`. Since then CI runs on every push to either
line and on pull requests (`continuous-integration`), and feature changes merge by PR with a merge
commit (#1, #2). This is the first release in that setting.

## Decisions

### D1. The release commits go through a PR, and the pack is taken from the merge commit

The branch `release-17-2-1` carries the red fix, the version and the entry. It is merged by PR
with **"Create a merge commit"**, and CI must be green on the PR. The runbook's rule, *pack from
the merge commit on main, after it is pushed*, then applies literally:
- the tag `17.2.1` goes on that merge commit;
- `git branch -r --contains HEAD` must list `origin/main` before packing;
- the SourceLink SHA is the merge commit.

*Alternative rejected:* committing on `main` directly, as `17.2.0` did. It would skip the CI
verification this release is the first to have, and CI is what just caught a red `main` that the
local post-archive check missed.

### D2. The PR's parity step is expected to be green

Only `<Version>` differs between the lines in `Directory.Build.props`, and the parity check
exempts that line. **This is a prediction, and the PR run checks it.** If it goes red, the diff
has touched a parity file beyond `<Version>`, and that's a defect to find, not a known red to wave
through.

### D3. The post-publication bookkeeping goes straight to `main`

Stamping the entry's date and archiving are record-only commits. They go to `main` directly, as
today's `marketplace-listing` records did. **The suite runs locally after the archive, before the
push.** `marketplace-listing`'s archive turned `main` red because only `openspec validate` was run
after syncing. This change syncs no spec, but the rule is cheap and holds generally.

### D4. `18.1.1` follows in its own change, in the same sitting

The 18 line's release needs the same red fix, its own `<Version>`, readme pins and entry, and a
PR into `dev/v18`. It is proposed after `17.2.1` publishes, so that anything this release learns
reaches it. That includes the `publishing.md` wording QA flagged, "until 17.2.1/18.1.1", which
names a version that must exist on both lines before it is true.

## Risks / Trade-offs

- **[A tag on a merge commit that changes after tagging]** → The tag is pushed only after the
  merge is on `origin/main` and has been fetched. The commit the tag names is checked to equal
  `origin/main`'s tip before packing.
- **[Stale build output]** → The working copy has alternated between lines today. Before packing:
  delete `src/*/bin/Release`, `src/UBookIt.Backoffice/wwwroot/App_Plugins/UBookItBackoffice` and
  `src/UBookIt.Backoffice/obj/Release`. The expected unit total on this line is **1982**, and any
  other number means the wrong binaries.
- **[The narrowing (c) surprising an editor]** → The entry says a stored value stays, and what the
  screen now tells them on the next save of that setting.
