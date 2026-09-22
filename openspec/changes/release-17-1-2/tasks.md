## 1. The guard, before the bound

- [x] 1.1 Cherry-pick `Every_umbraco_dependency_names_an_upper_bound` from `dev/v18` (design D3).
      **Do not rewrite it** — one requirement, one implementation, or the two lines drift.
- [x] 1.2 Run it **before** applying the bound and watch it **fail**, naming the seven unbounded
      declarations. A guard first seen green on this branch is a guard nobody has tested here;
      this is the one moment it can be seen failing for the real reason rather than a mutated one.

## 2. The bound

- [x] 2.1 `[17.6.2,18.0.0)` on the `Umbraco.Cms.*` entries in `Directory.Packages.props`.
      **Count them rather than trusting the 18-line figure** — `grep -c "Umbraco.Cms"` counts the
      comment line above them, which produced a wrong number three times in `run-on-umbraco-18`.
- [x] 2.2 Confirm restore still succeeds against Umbraco 17.6.2 — a malformed range fails at
      restore, and pack succeeding is not evidence the range is right.
- [x] 2.3 Run the guard again and watch it pass.

## 3. The version

- [x] 3.1 `Directory.Build.props` `17.1.1` → `17.1.2`.
- [x] 3.2 **Expect the suite to go red on the bump, in more than one place.** The image-pin guard
      and `Every_documented_version_is_the_declared_version` both derive from the declared
      version; that is them working. Repin the readme's four images and fix
      `docs/publishing.md`'s literals.
- [x] 3.3 `CHANGELOG.md` — a `17.1.2` entry, **undated** (design D5), leading with the restore
      failure for the one reader it affects (design D4). Do not write "nothing to do" alone: it
      is false for exactly the people this release is for.

## 4. Proving the fix does what the evidence says

The whole change rests on one measurement. It is worth proving the *fix* the same way rather than
trusting that a range does what ranges do.

- [x] 4.1 Pack `17.1.2` locally and read the bound out of the **packed nuspec** for every package
      that declares an Umbraco dependency — not out of `Directory.Packages.props`.
- [x] 4.2 **Attempt the install that currently bricks a site**: a scratch Umbraco 18 site,
      `dotnet add package UBookIt --version 17.1.2` from a local feed, and confirm NuGet
      **refuses it and names the constraint**. This is the same harness that produced the
      original measurement, pointed at the fix.
- [ ] 4.3 Confirm an Umbraco **17** site still installs `17.1.2` and boots — the bound must not
      have narrowed what the line supports. Needs a scratch v17 site and a database: **Chris's,
      and only if he wants the belt-and-braces**; the guard plus 4.1 already prove the range's
      shape.

## 5. Verification

- [x] 5.1 Build the client first, then each test project in Release with the TestSite stopped.
      **Rebuild the client after any branch switch** — hashed assets are not branch-tracked and a
      stale set fails the Release build with a StaticWebAssets error that reads like a broken
      change.
- [x] 5.2 Clean Release build, **0 warnings**. Record counts against `main`'s baseline of
      1809 / 167 / 1168 / 290, plus the cherry-picked guard.
- [x] 5.3 `openspec validate --all --strict`.
- [x] 5.4 Pack verification per `docs/publishing.md`: five `.nupkg` + four `.snupkg`, all
      `17.1.2`, repository commit = HEAD, icon and readme declared **and present**, SourceLink
      SHA = HEAD, packed readme 0 relative links.

## 6. Publish — the order is load-bearing

- [ ] 6.1 **Chris pushes `main`**, and verify with `git branch -r --contains HEAD` rather than
      trusting it.
- [ ] 6.2 **Tag `17.1.2` and push the tag before packing** — the readme's four images resolve
      through it.
- [ ] 6.3 Pack, then Chris publishes; the API key is his.
- [ ] 6.4 Confirm **per package** on `api.nuget.org/v3-flatcontainer/<id>/index.json`, not the
      website.
- [ ] 6.5 **Then** stamp the changelog date, and commit.
- [ ] 6.6 Fetch the published readme's image URLs and confirm they render.

## 7. Record

- [ ] 7.1 `skip_specs: true` — nothing to sync. Archive.
- [ ] 7.2 Record that `17.0.0`–`17.1.1` remain installable into Umbraco 18 permanently, and that
      the bound protects only releases from here.
- [ ] 7.3 Record the readme-ref obligation this change deliberately did not take (design's open
      question): the 17 readme still names `blob/main`, which is true today and becomes false when
      the lines' documentation diverges.

## 8. §1–5 results

**1.2 — the guard was seen failing for the real reason before it was ever seen passing.** Run
against the unbounded 17 line it named all **seven** declarations in the actual packed artifacts:

```
7 Umbraco dependency declaration(s) carry no upper bound …
  UBookIt.Backoffice -> Umbraco.Cms.Api.Common '17.6.2'
  … UBookIt.Web -> Umbraco.Cms.Web.Website '17.6.2'
```

That is the one moment on this branch when it could fail genuinely rather than by mutation, and
it is worth more than the three mutations that proved it on `dev/v18`.

**2.1 — seven entries, counted rather than carried over.** The same number as the 18 line, and
checked independently, because `grep -c "Umbraco.Cms"` counts the comment line above them and
produced a wrong figure three times in `run-on-umbraco-18`.

**3.2 — the bump turned five guards red, all of them correctly.** Three `ChangelogTests` (no
`17.1.2` entry yet), the image-pin guard and `Every_documented_version_is_the_declared_version`.
Nothing here needed a list of files to remember: the version is the single source and the guards
derive from it.

**§4 — the fix is measured against the same harness that produced the original defect, and the
before/after is the whole argument for the version number:**

| | `17.1.1` | `17.1.2` |
|---|---|---|
| `dotnet add package` on Umbraco 18 | succeeds, **no warning** | **NU1107**, naming both sides of the conflict |
| `dotnet build` | succeeds, **0 errors** | **FAILS**, `NU1107` naming `UBookIt.Backoffice 17.1.2` |
| `dotnet run` | **unhandled `TypeLoadException`, process exits** | never reached |

**One precision, because the obvious summary overstates it:** `dotnet add package` prints the
NU1107 error but **still writes the `PackageReference` and exits 0**. It is the *build* that
stops, not the add. So the accurate claim is "the project will not build and the error names
uBookIt", not "NuGet refuses to add it". Saying the stronger thing would be the kind of sentence
this project has spent four QA rounds learning to check.

**4.3 not done, and it does not need Chris.** Confirming an Umbraco **17** site still installs
`17.1.2` needs a scratch v17 site and a database. The guard plus 4.1 already prove the range's
shape, and `2.2` proved restore still succeeds against 17.6.2 in this repository — which is an
Umbraco 17 site resolving the bounded dependencies. Recorded as a deliberate omission rather than
an oversight.

**5.x — 1810 / 167 / 1168 / 290**, one above `main`'s baseline of 1809, which is the
cherry-picked guard. **0 warnings** in a clean Release build, `--strict` 23/23, and the pack
gives 5 `.nupkg` + 4 `.snupkg` at `17.1.2` with icon and readme present in every one.
