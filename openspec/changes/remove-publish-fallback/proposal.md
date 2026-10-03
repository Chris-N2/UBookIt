## Why

Both lines now release through `publish.yml`. `17.2.2` and `18.1.2` (2026-09-26) were the first,
and token exchange, policy and approval each worked first time. The runbook still documents a
second route, *Fallback: pushing by hand*: create an API key and run `dotnet nuget push` from a
local pack. That route skips every check the workflow makes, including the tag check, clean-clone
pack, symbol and SourceLink verification, approval gate and push order. The maintainer's last key
expires in mid-October 2026, so after that the documented fallback would not work either. Both
runbooks already name this removal as the next change (`docs/publishing.md`, *Status*).

## What Changes

- **Remove** *Fallback: pushing by hand* from `docs/publishing.md`, with every cross-reference to
  it:
  - the *Status* outstanding bullet announcing this change;
  - the ownership bullet's pointer to the fallback;
  - *"If the workflow cannot publish … use Fallback"* under *Publishing a release*;
  - the *"On the fallback route the order is the same … reject"* paragraph under *Tag the release*;
  - *"Either route"* and the wildcard-order paragraph under *After the push*.
- **State plainly that there is no hand route.** If the workflow cannot publish, the release
  waits, or the workflow is fixed in a patch and the release then goes through it. The reason is
  stated with it: a hand push skips every check above.
- **Reframe, not delete,** *The order that makes SourceLink correct*, *Verify, do not assume* and
  *Publish only from a commit that is already on the public repository*. They stop being
  instructions for a hand pack and become the reasoning behind what the workflow checks. This
  also retires the 18 line's *"Pack from the merge commit on main"*, which is wrong on `dev/v18`.
  That debt is logged under `release-18-1-2`.
- **Re-home the 403 lesson** under *Trusted Publishing setup*. The workflow pushes with a
  temporary key from the token exchange, so a policy whose owner cannot publish fails with the
  same 403 text, which names only the key. The lesson is about the owner, as before; the owner
  is now the policy's owner.
- **`README.md`, *Gated releases*:** drop "A manual route is documented as the fallback".
- Guards: `VersionTruthTests`' pins and its `nuget.org` count for `docs/publishing.md` follow
  the edited text. Each removed or reworded pinned sentence keeps its guarantee under new wording,
  or is dropped as listed below.
- **Both lines.** The fallback text is identical on `main` and `dev/v18` apart from version
  literals; the port is part of this change.

- **Scope addition, found at apply, Chris's call (2026-10-03): fix a test time bomb on both
  lines.** `ClosuresControllerTests.The_list_omits_past_closures_by_default` (from ㊺) dated its
  closures from `DateTime.UtcNow` while the controller under test runs on the fixed clock
  `TestData.Now` (2026-09-01). From 2026-10-02 the "past" closure counts as upcoming and the test
  fails every time, on both lines. That turns CI red for any push, this change's PRs included. The
  fix is test-only: date the closures from the controller's clock. A search of `tests/` found no
  other test that reads the machine clock.

**Deliberately removed** (a removal, not an omission): the `release-publishing` guarantee that
*"the manual API-key procedure SHALL remain documented, labelled as the fallback"*, and its
scenario *"The manual route remains available"*. It is replaced by the opposite guarantee: the
runbook says there is no hand route and what to do instead.

Nothing here is a public API or schema change. No package content changes except the readme
sentence, which reaches nuget.org with each line's next release. **No release is cut for this
change.**

## Non-goals

- Changing `publish.yml`, the scripts under `scripts/ci/`, the Trusted Publishing policy or the
  `release` environment.
- Modifying the `packaging` requirements *The package points a consumer at a public home* and
  *The publishing runbook records what a publish actually cost*. Both must stay satisfied as
  written. The reframed sections and the re-homed 403 lesson are how they do.
- Any other item from the tidy list (bound-guard message, `tree/` link guard, vitest skips, …).
  They are a separate change.
- Rewriting the packed readmes of versions already published. Their links are pinned to their
  tags and describe what those releases shipped.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `release-publishing`: the requirement *The runbook describes the workflow as the route and the
  key as the fallback* is **REMOVED**, with its reason, and replaced by an **ADDED** *The runbook
  describes the workflow as the only route*. It is a removal rather than a MODIFIED entry because
  one scenario (*The manual route remains available*) is dropped on purpose. The validator rightly
  refuses to let a MODIFIED block lose a scenario silently, and a REMOVED entry is where the drop
  is recorded. Every other guarantee is restated in the new requirement. The new requirement also
  adds the "no hand route" statement, the explained-not-prescribed rule for the reframed sections,
  and the 403-to-policy direction.

## Impact

- `docs/publishing.md` and `README.md` on both lines.
- `tests/UBookIt.Tests/VersionTruthTests.cs` on both lines: the pins in
  `The_publishing_runbook_states_what_cannot_be_undone` and the `AcceptedPublicationMentions`
  count and reason for `docs/publishing.md`.
- `openspec/specs/release-publishing/spec.md` at sync, on both lines.
- **Outside the repository, the maintainer:** delete the remaining nuget.org API key rather than
  let it expire, and confirm the repository holds no nuget.org key in its Actions secrets. The
  spec already requires that, and nothing here can check it.
