## Why

`docs-truth-and-screenshots` is merged and archived, and **none of it has reached a consumer.**
The readme packed into all five published packages still lists **two** features under *What it
does not do yet* that `17.1.0` shipped: taking a booking on somebody's behalf, and looking one up
by reference. A packed readme is frozen per version, so the correction cannot be made in place —
it costs a version number to deliver, and that is the entire reason this change exists.

**Two, not three, and the distinction is the kind this project keeps getting wrong.** The
archived change counted three denials *across two documents*; the third — *"It does not find a
person across bookings"* — was in `docs/backoffice.md`, which **is not packed**, and it had been
false since `0.3.0` added the email-address search rather than since `17.1.0`. The packed readme
did not deny that search at all; it **affirmed** it, in the same bullet that denied reference
search. An earlier draft of this proposal and of the changelog entry collapsed the archived
change's headline into a precise-sounding claim that all three were denied by the packed readme,
and QA caught it before publication — which is the only reason it is not now frozen into five
packages.

It is a **patch**: no behavioural change, no API change, no schema change. `17.1.1` carries
documentation, screenshots and test-suite guards, which is exactly what the README's own
versioning table promises a patch contains.

**There is also a live cost to waiting.** The repository README on `github.com/Chris-N2/UBookIt`
shows four broken images, and the reason is worth stating exactly, because it decides when the
window closes: `main`'s readme points its screenshots at **`17.1.0`**, and no `17.1.0` tag
exists — none can be created, since the images landed after that release. The bump to `17.1.1`
lives on this branch, so **the window closes when this change is MERGED and the tag pushed**, not
when the tag alone is pushed. An earlier draft of this proposal said the refs already pointed at
`17.1.1`; they do not until the merge.

## What Changes

**The version, in six places across four files** — the list `docs/publishing.md` step 3 now
carries, which is the authority rather than this proposal. (Five of the six are enumerated below;
the sixth is the `CHANGELOG.md` entry, which the table also counts and which is described in its
own paragraph. The count is spelled out because this project has now miscounted this exact list
four times.)

- `Directory.Build.props` — `<Version>`, the only place it is *declared*.
- `README.md` — the sentence naming the version uBookIt is at.
- `README.md` — **all four screenshot URLs**, whose ref must equal the declared version or
  `The_readme_images_render_and_show_what_their_release_shipped` fails.
- `docs/publishing.md` — the *"uBookIt is at"* sentence under *What nuget.org will not let you
  undo*.
- `docs/publishing.md` — the four version literals in the *Tag the release* worked examples.
  **Nothing checks this one**; it is illustrative text no guard reads.

**A `CHANGELOG.md` entry for `17.1.1`**, its heading left **undated** until the feed confirms
publication. It leads with what a site has to do — nothing — and says what changed.

**The tag.** `17.1.1` is created and pushed **before** the package. Until it exists every
screenshot on the package page is a broken image, and no automated check can see that.

## Non-goals

- **No behavioural change, and no code change of any kind.** Nothing under `UBookIt.Core`,
  `.Persistence`, `.Backoffice` or `.Web` is touched. A patch that changed behaviour would
  falsify the versioning table the package publishes about itself.
- **Not the remaining tidy-up items.** The duplicated pin in `docs/notifications.md`, the
  `dotnet pack` test collision and the ordering-test landmine **ship nothing** — `docs/` is not
  packed and the other two are test-only — so they need no version number and are not held up by
  this release. `CancellationSecrets` housekeeping does change the package and belongs to a
  later one.
- **Not a re-review of `docs-truth-and-screenshots`.** That change is QA-approved, synced and
  archived. This release publishes it; it does not reopen it.
- **No unlisting of `17.1.0`.** It is install-compatible and remains the version some sites are
  on. `17.1.1`'s page simply becomes the default landing page.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None — `skip_specs: true`.

**This is a deliberate opt-out, and it differs from the last two releases.** `17.0.1` and
`17.1.0` each carried a spec delta because each *added a guarantee*: the version-anchor rules,
and then the changelog requirement. `17.1.1` adds none. Every obligation it must satisfy already
exists in `packaging` and was strengthened by the change it publishes — the version a reader is
told, the packed readme's links, the packed readme's images and their ref, a release naming its
contract changes, and what the pin asks of a publish. A release that invented a requirement in
order to have a delta would be writing a spec to satisfy a validator.

## Impact

| | |
|---|---|
| `Directory.Build.props` | `<Version>` 17.1.0 → 17.1.1 |
| `README.md` | Version sentence and four screenshot refs |
| `docs/publishing.md` | The *"uBookIt is at"* sentence, and the *Tag the release* examples |
| `CHANGELOG.md` | New `17.1.1` entry, undated until the feed confirms |
| Code, schema, API surface | **Untouched.** |
| nuget.org | Five packages at `17.1.1`. **A pushed version cannot be reused or edited.** |
| `github.com/Chris-N2/UBookIt` | The **merge plus** the `17.1.1` tag closes the broken-image window on the landing page — the merge moves the refs, the tag makes them resolve |

No breaking change. No new configuration. No migration.
