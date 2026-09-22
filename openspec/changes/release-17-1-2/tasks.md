## 1. The guard, before the bound

- [ ] 1.1 Cherry-pick `Every_umbraco_dependency_names_an_upper_bound` from `dev/v18` (design D3).
      **Do not rewrite it** — one requirement, one implementation, or the two lines drift.
- [ ] 1.2 Run it **before** applying the bound and watch it **fail**, naming the seven unbounded
      declarations. A guard first seen green on this branch is a guard nobody has tested here;
      this is the one moment it can be seen failing for the real reason rather than a mutated one.

## 2. The bound

- [ ] 2.1 `[17.6.2,18.0.0)` on the `Umbraco.Cms.*` entries in `Directory.Packages.props`.
      **Count them rather than trusting the 18-line figure** — `grep -c "Umbraco.Cms"` counts the
      comment line above them, which produced a wrong number three times in `run-on-umbraco-18`.
- [ ] 2.2 Confirm restore still succeeds against Umbraco 17.6.2 — a malformed range fails at
      restore, and pack succeeding is not evidence the range is right.
- [ ] 2.3 Run the guard again and watch it pass.

## 3. The version

- [ ] 3.1 `Directory.Build.props` `17.1.1` → `17.1.2`.
- [ ] 3.2 **Expect the suite to go red on the bump, in more than one place.** The image-pin guard
      and `Every_documented_version_is_the_declared_version` both derive from the declared
      version; that is them working. Repin the readme's four images and fix
      `docs/publishing.md`'s literals.
- [ ] 3.3 `CHANGELOG.md` — a `17.1.2` entry, **undated** (design D5), leading with the restore
      failure for the one reader it affects (design D4). Do not write "nothing to do" alone: it
      is false for exactly the people this release is for.

## 4. Proving the fix does what the evidence says

The whole change rests on one measurement. It is worth proving the *fix* the same way rather than
trusting that a range does what ranges do.

- [ ] 4.1 Pack `17.1.2` locally and read the bound out of the **packed nuspec** for every package
      that declares an Umbraco dependency — not out of `Directory.Packages.props`.
- [ ] 4.2 **Attempt the install that currently bricks a site**: a scratch Umbraco 18 site,
      `dotnet add package UBookIt --version 17.1.2` from a local feed, and confirm NuGet
      **refuses it and names the constraint**. This is the same harness that produced the
      original measurement, pointed at the fix.
- [ ] 4.3 Confirm an Umbraco **17** site still installs `17.1.2` and boots — the bound must not
      have narrowed what the line supports. Needs a scratch v17 site and a database: **Chris's,
      and only if he wants the belt-and-braces**; the guard plus 4.1 already prove the range's
      shape.

## 5. Verification

- [ ] 5.1 Build the client first, then each test project in Release with the TestSite stopped.
      **Rebuild the client after any branch switch** — hashed assets are not branch-tracked and a
      stale set fails the Release build with a StaticWebAssets error that reads like a broken
      change.
- [ ] 5.2 Clean Release build, **0 warnings**. Record counts against `main`'s baseline of
      1809 / 167 / 1168 / 290, plus the cherry-picked guard.
- [ ] 5.3 `openspec validate --all --strict`.
- [ ] 5.4 Pack verification per `docs/publishing.md`: five `.nupkg` + four `.snupkg`, all
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
