## 0. Settled by Chris, 2026-08-31

- [x] 0.1 **Licence: MIT.** Chris chose it against the recommendation as offered. Goes in the
  repository, every nuspec and the readme, and relicensing later needs every contributor's
  agreement — so it is settled before anything is written, not after.
- [x] 0.2 **First version: `0.1.0`.** CLAUDE.md's compatibility promise starts at publication,
  and the public API has taken three deliberate breaking changes in the last three changes.
  `0.1.0` says *usable, not frozen* — so the readme must not claim a stable API.

  Chris also raised, and deferred, **planning 0.2.0 → 1.0.0** so that 1.0 is "a fully
  functional product". Out of scope here; recorded as a deferred obligation rather than
  answered in passing.

## 1. Make the libraries packable

- [x] 1.1 Shared metadata in `Directory.Build.props`, guarded on `IsPackable`: authors,
  product, project and repository URLs, licence expression, tags, readme, and **one version
  property** (design D2).
- [x] 1.2 Per-project `PackageId` and a **real `Description`** — what that assembly is for,
  not a repetition of its name.
- [x] 1.3 `IsPackable=false` on `UBookIt.TestSite` and every test project, so nothing
  accidental is produced.
- [x] 1.4 **Investigate `UBookIt.Persistence.runtimeconfig.json` in the package's `lib/`.** A
  library has no business shipping one. Find what produces it before suppressing it — a
  suppression that hides a real misconfiguration is worse than the file.

## 2. The meta-package

- [x] 2.1 A `UBookIt` project with no code, depending on `UBookIt.Backoffice` and
  `UBookIt.Web` (design D1). This is the one a site installs.
- [x] 2.2 **`UBookIt.Web` is the reason this exists** — nothing references it, so it is the
  assembly that has been silently absent all along. Its arrival is what the verification must
  actually confirm.
- [x] 2.3 ~~The full readme on the meta; a short pointer on the libraries~~ — **the same readme
  on all five.** The design guessed at two documents; rejected on apply because two documents
  saying overlapping things drift, and the drift is silent. Four identical copies are noise;
  two versions of the truth are a defect waiting to happen, and this repository has been bitten
  by exactly that on the last three changes. The readme names the packages and says which one
  to install, which is what a library's NuGet page needed the pointer for anyway.

  Known limitation: the doc links in the readme are repo-relative, so they resolve on the
  repository front page and **not** on nuget.org, which only rewrites relative links for hosts
  it recognises and does not recognise Azure DevOps. Left as-is because nothing is published
  yet and the repository is where the readme is read today; it becomes real work the day
  publishing does.

## 3. Licence and readme

- [x] 3.1 `LICENSE` at the repository root, per 0.1.
- [x] 3.2 `README.md` serving both the repository front page and the NuGet readme: what
  uBookIt is, what v1 does and explicitly does **not** do (borrow from `docs/mvp.md`),
  requirements (**Umbraco 17 LTS, .NET 10, SQL Server** — SQLite is unsupported), how to
  install, and where the documentation is.
- [x] 3.3 **The SQL Server requirement finally gets its home.** `persistence`'s spec has
  required it be stated in package documentation since ②, and it never has been — recorded in
  deferred obligations as unmet-and-unhomeable because there was no README. There is one now.

## 4. The manifest version

- [x] 4.1 `umbraco-package.json` stops saying `0.0.0`; substituted at build from the version
  property rather than kept in step by hand (design D3).

## 5. Prove it by installing

- [x] 5.1 Script the whole path so it is repeatable: pack all five → local feed → `dotnet new
  umbraco` in a temp directory → `dotnet add package UBookIt` → run → install.
- [x] 5.2 **From scratch, never against the TestSite** (design D4). The TestSite references
  the projects, which is the configuration that has hidden this failure from the beginning.
- [ ] 5.3 **BLOCKED on Chris — needs an authenticated backoffice session.** In the installed
  site: create and publish a booking page, take a booking through the front end, and see it in
  the backoffice Bookings view. **The front-end half is the point** — it is what
  `UBookIt.Web`'s absence would have broken, and it is invisible from the backoffice alone.

  What *was* established without logging in: the site boots, uBookIt applies all 7 migrations,
  and `/umbraco/ubookit/api/v1/resources` and `/services` both answer `200` — so
  `UBookIt.Web` is present and registered, in a site that got uBookIt only from a feed. That
  is not the same as a visitor completing a booking, and is not recorded as if it were.
- [ ] 5.4 **BLOCKED on Chris** — confirm the backoffice Packages screen shows the real version.
  The manifest it reads was fetched from the running site and says `"version": "0.1.0"`; what
  is unverified is only that the screen renders it.
- [x] 5.5 Record what was run and what was observed, **and that it was performed rather than
  automated** — `verification.md`. The automated installation guard is an existing recorded
  obligation and this does not discharge it; the record says so in its own first paragraph.

## 6. Guards that would catch the real mistakes

- [x] 6.1 **Every uBookIt dependency names a package this repo produces, at the same
  version.** This is today's defect exactly and it is assertable over the built `.nupkg`
  files without installing anything.
- [x] 6.2 **The meta reaches every packable project.** A fifth assembly added later and
  forgotten is the same silent failure wearing a different hat.
- [x] 6.3 **No package metadata is left at framework defaults** — no `Package Description`, no
  author equal to an assembly name.
- [x] 6.4 **The manifest version equals the package version.**
- [x] 6.5 Mutation-check each: remove `UBookIt.Web` from the meta, revert a description,
  desynchronise the manifest version. Each must fail something.

## 7. Close

- [x] 7.1 Full solution build at **zero** warnings from a clean `bin`/`obj` — not sweeping
  `node_modules`.
- [x] 7.2 Full suite green against the 1738 baseline; client suite against 113.
- [x] 7.3 `openspec validate --all --strict`.
- [x] 7.4 **Confirm no source change was needed.** If one was, report it as a finding about
  the assembly boundaries rather than absorbing it (design D5).
- [x] 7.5 Sweep for sentences this falsifies. Done, in this order:
  - **`packaging`'s own delta** — gained a clause and a scenario for the discovered defect:
    everything the packages carry must be produced by the build that packs them. The existing
    `openspec/specs/packaging/spec.md` falsifies nothing: it describes what the package *does*
    on install and never claims the package can be obtained. Its **Purpose** does presuppose
    "when a consumer adds the package", which is now true rather than aspirational — worth
    widening at sync to cover obtaining it, but that is prose only a sync can fix.
  - **`docs/mvp.md`** — steps 1 and 9 are ✅, "only packaging is left" is gone, and the
    account of why installation had to be proven by installing now names all three silent
    absences rather than the two known at propose time.
  - **Deferred obligations** — the SQL Server note is marked discharged, with what discharged
    it and where.
  - **This change's own proposal and design** — the Why gained the client defect and the
    reason the propose-time measurement could not see it; the design's three Open Questions
    are answered rather than left open; and both fixes for the client are recorded, including
    the first one that did not work.
  - `docs/backoffice.md`, `docs/booking-page.md`, `docs/theming.md`, `docs/notifications.md`
    and `CLAUDE.md` — checked, nothing falsified. **`CLAUDE.md`'s architecture sketch does not
    list the new `src/UBookIt` meta-project**; left alone deliberately, because it is labelled
    "initial proposal — don't treat as spec" and it is Chris's file. Flagged rather than
    edited.
- [ ] 7.6 Hand to `qa-review` in a **fresh context or subagent**.
