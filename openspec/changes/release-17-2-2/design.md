## Context

`17.2.1` was the first release made through a PR with CI. `trusted-publishing` then added
`publish.yml`. With that workflow, a pushed tag checks the commit, packs it from a clean clone,
verifies the packages, and publishes after approval in `release`, using a one-hour key from
nuget.org's token exchange. Neither line has published through it yet. See `docs/publishing.md`,
*Publishing a release*.

## Goals / Non-Goals

**Goals:** close the three tidy obligations; rewrite the README for the person deciding whether to
install; publish `17.2.2` through the workflow and record what only a real run shows.

**Non-Goals:** changing `publish.yml` or its scripts. If the run exposes a defect in them, the
defect is recorded, the fallback publishes, and the fix gets its own change. Also out: widening the
nuget.org policy.

## Decisions

### D1. The length limit has one definition

The literal `2048` in `UBookItDbContext` becomes an `internal const` on the settings entity (or
beside it) in `UBookIt.Persistence`. `HasMaxLength` and `SettingValidation` both read it, and
`UBookIt.Backoffice` already sees Persistence's internals. It is **internal, not public**, so the
patch adds no public surface. `SiteClosure.MaxLabelLength` is public, but that pattern is not
copied here.

*Alternative rejected:* restating `2048` in the validator. That is a second copy of a rule, which
is the drift `SettingValidation`'s `Url` case already records having suffered.

**What is measured is every row the value would be stored as, not the submitted text.** This was
found during apply, and the first plan was wrong about it. `SettingsController.PutSetting` stores
an `EmailList` as one row per address, through `SettingText.RowsFor`. A check on the whole
submitted string would therefore refuse a long recipient list that works today, with every
address well inside 2048 characters. That would be a regression delivered in a patch. So the check
**calls** `SettingText.RowsFor(descriptor, value, <empty configuration>)` and measures each row's
value. The same principle as the `Url` case applies: a call can't drift from the storage shape,
and a mirror of it can. The overflow blanks `RowsFor` adds for a configured list are empty, and an
empty configuration adds none, so they can't affect the result.

The check runs **before** the kind switch and after the blank check. That way every kind gets it,
and the blank message still wins for an empty value. `string.Length`
counts UTF-16 code units, and `nvarchar(n)` capacity is also measured in UTF-16 code units, so the
two agree for surrogate pairs too. The integration test exercises that with a value at capacity
made of non-BMP characters, so the agreement is measured rather than asserted.

Message: *"Must be no longer than 2048 characters."* The number is interpolated from the constant.

### D2. The seam is the constant, so each side is pinned to it where that side really runs

*Revised during apply.* The first plan drove `SettingsController` against SQL Server from the
integration project. That project references only Core and Persistence. Adding `UBookIt.Backoffice`
would bring the client's `npm run build` into the integration build, which is the known
parallel-build race (`MSB3073`). The seam doesn't need it either. Validation and storage meet at
two points: **`SettingRow.MaxValueLength`** and **`SettingText.RowsFor`**. Each side is tested
where it actually runs:

- **Integration (real SQL Server, schema built by the real EF migrations, which are the production
  column):** the store's actual capacity **equals** `SettingRow.MaxValueLength`.
  - A value of exactly that many UTF-16 code units round-trips identical. So does one made of
    non-BMP characters (surrogate pairs), measured.
  - A value one unit longer **fails at the store.** That is the defect as it stands without
    validation, so it is shown to be real rather than inferred from the schema.

  If a future migration changed the column, the constant would disagree with the database and this
  test would fail.
- **Unit (the real `SettingsController`, wired as the container wires it, over the fake store):**
  - An editable setting at limit + 1, otherwise valid, returns 400 `SettingValueInvalid`, and
    **nothing is written**. The refusal happens before the store.
  - At the limit, the rows written are exactly `RowsFor`'s rows, each at most the constant.
  - A recipient list over the limit in total is written in full: every address as its own row.
    This is the regression guard.
- **Unit (`SettingValidation`):**
  - accept at the limit and refuse at limit + 1 for `Url` (a valid `https://` link padded to
    length), and for the `Text` kind through a constructed descriptor. No catalogue setting is
    `Text` today; the kind exists, and the spec's "every type" means it;
  - `EmailList`: one over-long address is refused, and a long list whose addresses all fit is
    accepted;
  - a blank value still gets the blank message.

  **Mutation check:** remove the check, and the controller's limit + 1 case must fail. The
  integration test shows that the value it would then write fails at SQL Server.

### D3. The ceiling message is chosen by comparing versions, detection untouched

`high != expectedCeiling` stays the detection. The message branches on how `high` compares with
the expected ceiling:
- **lower:** the existing text, which is true for this case;
- **higher:** "A ceiling above Umbraco {major} admits Umbraco {major+1} or later, which this
  line is not built against."
- **unparseable:** says so.

*Revised during apply and in QA round 1.* Two more branches:
- **the right bound spelled differently** (`18.0`, `18.0.0.0`). Detection flags it, and "lower"
  or "higher" would be false of it;
- **a prerelease of the ceiling** (`18.0.0-rc`). It sorts below `18.0.0`, but it refuses no
  Umbraco 17 release. It admits the next major's earlier prereleases, so "lower" would be false of
  it too.

The comparison uses all four numeric parts, with missing parts as zero, as NuGet does, so
`18.0.0.1` is "higher" and not "spelled differently". It is **hand-rolled rather than
`NuGetVersion`**, because the test project doesn't reference `NuGet.Versioning`, and a dependency
for a failure message's wording isn't worth taking.

The message is built in a small static helper so each branch can be fired directly. It is proved
by one test case per branch. The guard itself can't be driven into the high branch without
packing a wrong package. **Each case asserts the text of its own branch and the absence of the
others' text.** A test that only checks "contains 'ceiling'" would pass on the bug being fixed.

### D4. Release through the workflow, with the pre-approval checks the runbook keeps

The order follows `docs/publishing.md`, *Publishing a release*:
1. PR into `main` with a merge commit. CI must be green on the PR, parity included.
2. Fetch. The merge commit's own `ci` push run must be green.
3. Chris pushes tag `17.2.2` on the merge commit. The check job waits for `ci` if necessary.
4. **Before approving:**
   - `curl` the tagged `booking-flow.png` and expect 200;
   - download the run's `packages` artifact and read **every** `.nuspec`:
     - version, publisher, licence, readme and icon;
     - `Umbraco.Cms.*` bounded `[17.6.2, 18.0.0)`;
     - every `UBookIt.*` sibling at `17.2.2`;
     - `umbraco-marketplace` on `UBookIt` alone;
     - the description naming Umbraco 17.

     The run checks version, repository, commit and SourceLink, but it doesn't check that the
     values are right.
   - Extract the README from a `.nupkg` and fetch every address in it.
5. Approve. Then record the run summary table verbatim, and the CLI text of any 409.

*Why read every nuspec when the runbook says one:* this is the first workflow release, and the
artifact is what gets pushed. It is five files.

### D5. If the workflow fails

- **Before the publish job:** a defect in the commit follows `17.2.1`'s recovery, 6.6: delete the
  tag, fix through a PR, re-tag.
- **A failure in the publish job itself** (a token exchange `403`, an unrecognised push answer):
  - capture the run log;
  - check the policy per *Trusted Publishing setup*;
  - re-run the job once only if the cause was outside the repository, like the policy's package
    selection;
  - otherwise **reject/cancel**, and publish with the fallback key from **the run's own
    verified packages**, not a local pack. The runbook's fallback command globs
    `src/**/bin/Release`, so it would not see them, and a local pack would repeat the
    stale-artifact and SourceLink work the workflow exists to remove. The exact commands, settled
    before tagging (QA round 1):

    ```powershell
    gh run download <publish-run-id> -R Chris-N2/UBookIt -n packages -D <scratchpad>\packages-17.2.2
    $key = Read-Host -AsSecureString; $plain = [System.Net.NetworkCredential]::new('', $key).Password
    Get-ChildItem <scratchpad>\packages-17.2.2\UBookIt.*.nupkg | Where-Object Name -ne 'UBookIt.17.2.2.nupkg' | ForEach-Object { dotnet nuget push $_.FullName --api-key $plain --source https://api.nuget.org/v3/index.json --skip-duplicate }
    dotnet nuget push <scratchpad>\packages-17.2.2\UBookIt.17.2.2.nupkg --api-key $plain --source https://api.nuget.org/v3/index.json --skip-duplicate
    ```

    The libraries go first, then the meta-package, as the workflow orders them. Each push takes
    its `.snupkg` alongside. The runbook's `--skip-duplicate` caveat applies: an existing `.nupkg`
    skips its symbols, so on a partial push, check the symbols on the package page. Chris runs
    these commands, because the key is his.
- **A partial push:** a re-run completes it, by design.

Any of these is recorded, and it becomes an obligation against `trusted-publishing`, not a fix in
this change.

### D6. Post-publication edits go straight to `main`

As in `17.2.1`'s D3: stamp the date, then the *Status* bullet from proposal item 7, then archive.
The unit suite runs locally before each push.

### D7. The README rewrite: diff the reader's guarantees, not the prose

The README isn't a spec, but CLAUDE.md's rule for a wholesale rewrite applies to it in the same
way. A rewrite that drops a sentence can delete something a reader relied on, and nothing in the
diff looks like a deletion. So the rewrite is done against an inventory, not from memory.

1. **Inventory before writing.** List every claim the current README makes that a reader could act
   on, in `README-inventory.md` inside this change's directory. Examples are a default, a
   requirement, a grant, an exclusion, a limitation, or a failure mode such as "installing
   Backoffice without Web fails silently". Mark each one **kept in the README**, **moved to a
   docs page** (named, and verified to already say it), or **dropped, with a reason**. The
   inventory goes to QA with the rewrite.
2. **Must stay in the README**, because a reader needs them before installing, not after:
   - SQL Server only, not SQLite;
   - the Umbraco and .NET versions;
   - the section grant;
   - the Sensitive-data group, and the fact that new admins aren't in it;
   - the delivery API being off;
   - emails being off;
   - the Settings grant being given to nobody;
   - install `UBookIt`, not the parts;
   - self-service cancellation's link being the credential;
   - the accessibility boundary.

   Each of these can be shorter. None can be removed.
3. **Pins.** Before editing, run the suite and list every `Says`/`SaysOnce`/`DoesNotSay`, version
   anchor, link and image guard that reads `README.md`. The grep counts in the proposal are a
   sample, and `NotificationDocumentationTests`' sweep covers every shipped markdown file. Each
   pinned sentence is either kept word for word or re-judged: its guard changes, with a reason in
   the test, in the same commit. A pin must never be deleted just to make the suite pass. The
   `RetiredClaims` needles apply as usual, and the rewrite must not bring any of them back.
4. **"How it's built" claims only what can be shown.** It names the mechanisms and links to them,
   pinned to the release tag like every other repository link: the specs, the CI workflow and the
   publishing runbook. It says the review is done by an agent that did not write the change. It
   **does not** make claims about history ("every change since …"), counts, or the model used.
   Those go stale or can't be checked from the page. It says plainly that the package is built
   with AI assistance under this workflow, because that is the point of the section (Chris,
   2026-09-26).
5. **Tone.** Plain, confident, second person. No threat-model asides in the pitch. The caveats
   live in the linked docs, and the README links to them where a reader would look.
6. **The link guards constrain new links.** Every repository link has to be an absolute `https`
   URL pinned to the version. A directory link (`tree/<ver>/openspec/specs`) may not match the
   guard's `blob/<ver>/` form. Check what the guard accepts and link to a file if it has to,
   rather than widening the guard.

**Both lines.** `main` gets the rewrite in this change. `release-18-1-2` ports it with that line's
version, the "Umbraco 18" title line and its Requirements row, and diffs the two READMEs so that
those are the only differences. The v18 README is the one the Marketplace shows, so it is the one
to read last, as a stranger would.

## Risks / Trade-offs

- **[The token exchange fails and the key has expired]** → The key expires around mid-October, and
  this release is before then. D5's fallback depends on it. **Check the key's expiry before
  tagging.** If it has expired, generate one before tagging rather than after a failure.
- **[The 409 wording differs]** → The step fails closed, which is the intended behaviour. The run
  shows *unrecognised*, the packages that were pushed are live, and the text is recorded. The
  classifier change is separate. The run's own verdict stands, even though the packages are live.
- **[The Marketplace sentence is pinned or counted]** → The rewrite in `publishing.md` may trip
  `VersionTruthTests` or a `DocumentationAssert` pin. Run both. A red is worked through the way
  the runbook says, by judging each sentence, never by relaxing the guard.
- **[The narrowing surprises someone]** → No stored value can exceed the column, so nothing stored
  is affected. The entry says so.
- **[The rewrite drops a guarantee silently]** → D7's inventory, reviewed by QA against the old
  README side by side.
- **[The rewrite adds a claim that isn't true]** → Every new sentence, especially in "How it's
  built", is checked against the repository before QA, as the changelog entries are.
- **[One line only]** → The close-out lists what `release-18-1-2` must carry, file by file.
