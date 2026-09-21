## Why

`docs-truth-and-screenshots` is merged and archived, and **none of it has reached a consumer.**
The readme packed into all five published packages still tells a prospective user that uBookIt
cannot take a booking on someone's behalf, cannot look one up by reference, and cannot find a
person across bookings. All three have shipped since `17.1.0`. A packed readme is frozen per
version, so the correction cannot be made in place — it costs a version number to deliver, and
that is the entire reason this change exists.

It is a **patch**: no behavioural change, no API change, no schema change. `17.1.1` carries
documentation, screenshots and test-suite guards, which is exactly what the README's own
versioning table promises a patch contains.

**There is also a live cost to waiting.** The repository README on `github.com/Chris-N2/UBookIt`
points its four screenshots at a `17.1.1` tag that does not exist yet, so the project's landing
page currently shows four broken images. That was accepted as a time-bounded cost on the
condition that this release closes it.

## What Changes

**The version, in five places across four files** — the list `docs/publishing.md` step 3 now
carries, which is the authority rather than this proposal:

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
| `github.com/Chris-N2/UBookIt` | The `17.1.1` tag closes the broken-image window on the landing page |

No breaking change. No new configuration. No migration.
