## Why

`17.2.2` shipped on `main` on 2026-09-26 (`release-17-2-2`, archived on `main`). `dev/v18` has none
of it. A fix that reaches one line only has happened three times on this project, so the 18 line
gets the same patch in the same sitting.

It matters more on this line in two ways. The Umbraco Marketplace lists the **18.x** `UBookIt`
package, so **the v18 README is the Marketplace page**, and the README rewrite only reaches the
Marketplace through this release. It is also the first 18.x release through `publish.yml`, and the
manual API-key fallback can be removed only once both lines have published through the workflow.

The scope is `release-17-2-2`'s task 8.4, which lists what this change must carry.

## What Changes

1. **The setting length limit, identical to 17.2.2.** A settings value any of whose stored rows is
   longer than the store holds (2048) is refused with a 400 that states the limit. A recipient list
   is measured per address. The limit is `SettingRow.MaxValueLength`, which the schema shares. The
   same tests come across: 6 unit tests in `SettingsControllerTests`, the ceiling theory, and 3
   integration cases. The code, the tests and both affected specs are **byte-identical between the
   lines at their bases** (`d6f0199` and `14cf33f`), so `main`'s diff applies as it stands. This
   is ruled patch-eligible, as it was for 17.2.2 (Chris, 2026-09-26).
2. **The bound guard's ceiling message**, identical to 17.2.2: below, above, prerelease, spelled
   differently, and unparseable.
3. **The README rewrite**, ported from `main`'s final README. There are exactly four deliberate
   differences, and nothing else:
   - "Bookings for **Umbraco 18**" in the opening;
   - "Umbraco **18.2.0** or later" in *Get started*;
   - the v18 **Requirements** row, kept word for word from `dev/v18`;
   - every pin at `18.1.2`.

   `main`'s `README-inventory.md` accounting (archived in `release-17-2-2`) applies unchanged. It
   is not copied, because each line carries only its own archives.
4. **`docs/publishing.md`**, with `main`'s 17.2.2 edits ported:
   - the Marketplace paragraph as observed;
   - the Status bullet about the Marketplace, removed;
   - the step-3 table rows;
   - "every push".
   After publication, the workflow Status bullet becomes true for **both** lines (task 8.2).
5. **`docs/configuration.md`**: the limit sentence. **`CHANGELOG.md`**: the header anchor fix,
   `main`'s dated `17.2.2` entry copied unedited, and a new `18.1.2` entry.
6. **The version: `18.1.1` → `18.1.2`.**
7. **Release through the workflow**, as 17.2.2 did. `publish.yml` is identical on both lines under
   the parity check. The summary table and the pending-symbols 409 wording are recorded, if it
   occurs.

A **patch**: no signature, schema or migration change. The one behavioural narrowing is (1),
identical to 17.2.2's.

## Non-goals

- **Removing the manual API-key fallback.** That becomes possible once this release publishes
  through the workflow. It is its own change, on both lines.
- Anything `release-17-2-2` did not do. That includes the obligations it recorded: the `tree/` link
  blind spot, the client suite's skip detection, and the unobserved 409 wording.
- **Rewording the README for 18** beyond the four differences above. The two READMEs must read the
  same, so that a difference means something.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `site-settings`: the same ADDED requirement as `release-17-2-2`, *A value longer than the store
  holds is refused, not attempted*.
- `privacy-notice`: the same MODIFIED requirement, *A usable policy link is defined once, and means
  the same on every host*. **v18's main spec is byte-identical to `main`'s at the base**, so the
  guarantee diff from `release-17-2-2` task 1.4 holds here. It is restated in task 1.3:
  - six scenarios carried word for word;
  - one SHALL, and its scenario, deliberately narrowed to "within the store's capacity", because an
    over-long link is refused for length;
  - the "known gap" note superseded.

## Impact

- The same code and test files as `release-17-2-2`.
- `README.md`, `docs/publishing.md`, `docs/configuration.md`, `CHANGELOG.md`,
  `Directory.Build.props`.
- Five packages at `18.1.2` on nuget.org, frozen at push. **The `UBookIt` 18.1.2 readme becomes the
  Marketplace page** after the Marketplace rescans.
