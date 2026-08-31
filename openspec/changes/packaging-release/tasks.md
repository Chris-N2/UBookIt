## 0. Blocked until Chris answers

- [ ] 0.1 **Licence.** Recommendation MIT; Apache-2.0 if a patent grant matters. Goes in the
  repository, every nuspec and the readme, and relicensing later needs every contributor's
  agreement — so it is settled before anything is written, not after.
- [ ] 0.2 **First version.** Recommendation `0.1.0`: CLAUDE.md's compatibility promise starts
  at publication, and the public API has taken three deliberate breaking changes in the last
  three changes. `1.0.0` is defensible if Chris would rather signal confidence — but then the
  promise starts now and the readme must say so.

## 1. Make the libraries packable

- [ ] 1.1 Shared metadata in `Directory.Build.props`, guarded on `IsPackable`: authors,
  product, project and repository URLs, licence expression, tags, readme, and **one version
  property** (design D2).
- [ ] 1.2 Per-project `PackageId` and a **real `Description`** — what that assembly is for,
  not a repetition of its name.
- [ ] 1.3 `IsPackable=false` on `UBookIt.TestSite` and every test project, so nothing
  accidental is produced.
- [ ] 1.4 **Investigate `UBookIt.Persistence.runtimeconfig.json` in the package's `lib/`.** A
  library has no business shipping one. Find what produces it before suppressing it — a
  suppression that hides a real misconfiguration is worse than the file.

## 2. The meta-package

- [ ] 2.1 A `UBookIt` project with no code, depending on `UBookIt.Backoffice` and
  `UBookIt.Web` (design D1). This is the one a site installs.
- [ ] 2.2 **`UBookIt.Web` is the reason this exists** — nothing references it, so it is the
  assembly that has been silently absent all along. Its arrival is what the verification must
  actually confirm.
- [ ] 2.3 The full readme on the meta; a short pointer on the libraries (design open question).

## 3. Licence and readme

- [ ] 3.1 `LICENSE` at the repository root, per 0.1.
- [ ] 3.2 `README.md` serving both the repository front page and the NuGet readme: what
  uBookIt is, what v1 does and explicitly does **not** do (borrow from `docs/mvp.md`),
  requirements (**Umbraco 17 LTS, .NET 10, SQL Server** — SQLite is unsupported), how to
  install, and where the documentation is.
- [ ] 3.3 **The SQL Server requirement finally gets its home.** `persistence`'s spec has
  required it be stated in package documentation since ②, and it never has been — recorded in
  deferred obligations as unmet-and-unhomeable because there was no README. There is one now.

## 4. The manifest version

- [ ] 4.1 `umbraco-package.json` stops saying `0.0.0`; substituted at build from the version
  property rather than kept in step by hand (design D3).

## 5. Prove it by installing

- [ ] 5.1 Script the whole path so it is repeatable: pack all five → local feed → `dotnet new
  umbraco` in a temp directory → `dotnet add package UBookIt` → run → install.
- [ ] 5.2 **From scratch, never against the TestSite** (design D4). The TestSite references
  the projects, which is the configuration that has hidden this failure from the beginning.
- [ ] 5.3 In the installed site: create and publish a booking page, take a booking through the
  front end, and see it in the backoffice Bookings view. **The front-end half is the point** —
  it is what `UBookIt.Web`'s absence would have broken, and it is invisible from the
  backoffice alone.
- [ ] 5.4 Confirm the backoffice Packages screen shows the real version.
- [ ] 5.5 Record what was run and what was observed, **and that it was performed rather than
  automated**. The automated installation guard is an existing recorded obligation and this
  does not discharge it.

## 6. Guards that would catch the real mistakes

- [ ] 6.1 **Every uBookIt dependency names a package this repo produces, at the same
  version.** This is today's defect exactly and it is assertable over the built `.nupkg`
  files without installing anything.
- [ ] 6.2 **The meta reaches every packable project.** A fifth assembly added later and
  forgotten is the same silent failure wearing a different hat.
- [ ] 6.3 **No package metadata is left at framework defaults** — no `Package Description`, no
  author equal to an assembly name.
- [ ] 6.4 **The manifest version equals the package version.**
- [ ] 6.5 Mutation-check each: remove `UBookIt.Web` from the meta, revert a description,
  desynchronise the manifest version. Each must fail something.

## 7. Close

- [ ] 7.1 Full solution build at **zero** warnings from a clean `bin`/`obj` — not sweeping
  `node_modules`.
- [ ] 7.2 Full suite green against the 1738 baseline; client suite against 113.
- [ ] 7.3 `openspec validate --all --strict`.
- [ ] 7.4 **Confirm no source change was needed.** If one was, report it as a finding about
  the assembly boundaries rather than absorbing it (design D5).
- [ ] 7.5 Sweep for sentences this falsifies. **Start with `packaging` itself**, then
  `docs/mvp.md` (steps 1 and 9 become done, and "only packaging is left" stops being true),
  the deferred-obligations note about the SQL Server requirement having no home, and anything
  claiming the package is not yet installable.
- [ ] 7.6 Hand to `qa-review` in a **fresh context or subagent**.
