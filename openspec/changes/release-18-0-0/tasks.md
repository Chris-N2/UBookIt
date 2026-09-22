## 1. The two new guarantees, before the version moves

Guards first, deliberately. Both requirements are about what a **frozen** artifact promises, and
a release is the one moment their absence cannot be corrected afterwards.

- [x] 1.1 Guard the readme's documentation refs: every link addressing a document in this
      repository names a ref **derived from the declared version**, and a branch ref fails.
      Extend `VersionTruthTests` alongside `The_readme_links_resolve_from_anywhere` rather than
      inside it — that guard's remarks name this blind spot and say it does not claim to cover
      it, so replacing it would delete a stated limitation instead of closing it.
- [x] 1.2 Verify the guard **fires** before trusting it: point one link at a branch and watch it
      fail, point one at the previous version and watch it fail, and assert the extraction is
      non-empty first — a link scan that matches nothing satisfies "no branch refs" perfectly.

      **Done, four directions, and the one that matters least is the one usually skipped:**

      | state of `README.md` | result |
      |---|---|
      | as it stands, `blob/main`, version `17.1.1` | **fails**, naming all 12 |
      | every link repinned to `17.1.1` | **passes** — so the guard is satisfiable, not permanently red |
      | one link left at `17.1.0` | **fails** |
      | no link pointing into the repository at all | **fails on the anti-vacuity assertion**, by its own message |

      The second row is the one that is usually skipped and the one that proves the guard is a
      guard rather than a wall. The fourth was checked for its *reason*, not just its failure —
      a guard failing for the wrong reason is indistinguishable from one working.
- [x] 1.3 Guard the upper bound **against the packed nuspec, not `Directory.Packages.props`**.
      The requirement says the nuspec is the only copy a resolver sees and that the two can
      disagree; a guard reading the props file would assert the intention rather than the
      artifact.
- [x] 1.4 Verify that guard fires too: remove one bound and watch it fail, naming the dependency.

      **Three directions, and the middle one is why the check is not `EndsWith(')')`:**

      | mutation | result |
      |---|---|
      | one bound removed (`18.2.0`) | **fails**, naming `package -> dependency 'version'` |
      | one range left **open at the top** (`[18.2.0, )`) | **fails** — and a naive `EndsWith(')')` would have passed it, because that string does end in a bracket |
      | the id prefix changed to match nothing | **fails on anti-vacuity**, by its own message |

      §3.2 is covered by this guard rather than by a separate manual pack: it reads the produced
      `.nupkg`s through the existing `PackedSolution` fixture, so "all five packages" is the
      population it iterates rather than a list somebody kept.

## 2. The version

- [x] 2.1 `Directory.Build.props` `17.1.1` → `18.0.0`.
- [x] 2.2 `README.md`'s version sentence. **Expect the image-pin guard to fail here** — it derives
      the pin from the declared version, so bumping the version is what surfaces the four stale
      `17.1.1` image URLs. That failure is the guard working; repin them.

      Two more sentences went with it, neither in the task list and both false the moment the
      version moved: the readme opened *"A booking system for Umbraco 17"*, and its Requirements
      table said **Umbraco 17.x (LTS)**. Found by reading the file rather than by a guard —
      nothing checks prose against the host major, which is worth knowing but not worth a guard
      in a release change.
- [x] 2.3 The readme's **twelve** documentation links, per design D1. **Twelve, not the
      eleven this change's own proposal first said** — a hand-count missed `LICENSE`, and the
      guard from §1.1 is what produced the right number. Pin it with the rest: the licence a
      version shipped under belongs to that version.
- [x] 2.4 `CHANGELOG.md` — an `18.0.0` entry, **undated** (D5). It leads with what upgrading asks
      of a reader: for a site already on uBookIt `17.x` that is *moving to Umbraco 18*, and the
      entry says so plainly rather than describing it as an upgrade of uBookIt alone.
- [x] 2.5 `docs/publishing.md`'s **five** `17.1.1` literals. **The task as written rested on a
      false premise and the suite said so within a minute.** It offered a choice — guard them or
      record the obligation — on the belief that no guard read them. One does:
      `Every_documented_version_is_the_declared_version` failed on the bump, naming the file. No
      choice to make, no obligation to record, and five literals rather than four. See design D7.

## 3. The upper bound

- [x] 3.1 Apply `[18.2.0,19.0.0)` to the **seven** `Umbraco.Cms.*` entries in
      `Directory.Packages.props`. **Seven, not the eight stated everywhere in this change** —
      the figure came from `grep -c "Umbraco.Cms"`, which counted the comment line above them.
      Third miscount here, and all three share a cause: a number produced by a command nobody
      read the output of.
- [x] 3.2 **Pack and read the nuspec** for all five packages, not one. The spike that established
      this was a single-project pack; five packages have five nuspecs and only an inspection of
      each proves the bound reached them.
- [x] 3.3 Confirm restore still succeeds against Umbraco 18.2.0 — a malformed range fails at
      restore rather than at pack, and the pack succeeding is not evidence the range is right.

## 4. The screenshot (design D3)

- [ ] 4.1 Curate a plausible, non-personal booking set on the v18 dev site. The live-check
      residue (`K3RT-FR4D`, and `8WZR-Q9P8` cancelled) is not a shipping dataset.
- [ ] 4.2 Retake `bookings-screen.png` on Umbraco 18, matching the existing image's framing so
      the four read as one set.
- [ ] 4.3 Update its alt text if the data changed — the alt text describes the *contents*, and
      `docs-truth-and-screenshots` established that a description which stops matching its image
      is a defect of the same family as a false sentence.
- [ ] 4.4 Confirm the other three are unchanged and that this is still a deliberate decision
      rather than an omission (D3).
- [ ] 4.5 **Before the tag**, because the pinned URLs resolve through it.

## 5. Verification

- [ ] 5.1 Build the client, then each test project in Release with the TestSite stopped.
      **Rebuild the client after any branch switch** — hashed assets are not branch-tracked and a
      stale set fails the Release build with a StaticWebAssets error that looks like a broken
      change.
- [ ] 5.2 Clean Release build, **0 warnings**.
- [ ] 5.3 `openspec validate --all --strict`.
- [ ] 5.4 Pack verification per `docs/publishing.md`: five `.nupkg` + four `.snupkg`, all
      `18.0.0`, repository commit = HEAD, icon and readme declared **and present**, SourceLink
      SHA = HEAD, packed readme 0 relative links, backoffice client assets present.
- [ ] 5.5 **Install the packed `18.0.0` into a scratch Umbraco 18 site from a local feed** and
      confirm it restores, boots and shows the section. The upper bound is new metadata and
      restore is the only thing that reads it.

## 6. Publish — the order is load-bearing (design D5)

- [ ] 6.1 **Chris pushes `dev/v18`.** Nothing is packed from an unpushed commit: SourceLink embeds
      the SHA and a missing commit 404s permanently.
- [ ] 6.2 Verify the push with `git branch -r --contains HEAD` rather than trusting it — a push
      has reported success here having pushed nothing.
- [ ] 6.3 **Tag `18.0.0` and push the tag**, before packing. The readme's image and documentation
      pins both resolve through it.
- [ ] 6.4 Pack, then publish all five packages.
- [ ] 6.5 Confirm **per package** on `api.nuget.org/v3-flatcontainer/<id>/index.json`. The website
      shows versions the feed cannot yet serve; the flat-container index is what restore reads.
- [ ] 6.6 **Then** stamp the changelog date, and commit.
- [ ] 6.7 Fetch the published readme's image and documentation URLs and confirm they render —
      the human check both new requirements say a guard cannot make.

## 7. Record

- [ ] 7.1 Sync `packaging` with the two ADDED requirements, then archive.
- [ ] 7.2 Record the 17-line obligations this change deliberately did not take: the `17.x` upper
      bound, and that `17.0.0`–`17.1.1`'s readmes are frozen with `main` links and cannot be
      corrected.
- [ ] 7.3 Note what the two-line release cost against the one-line one — the next major's release
      is cheaper for knowing it, and this is the first release where "which line" was a question
      at every step.

**§1 and §3 result — the bound is real, and one thing about the guard is worth keeping.**

It reads the **packed nuspec**, not `Directory.Packages.props`, and the difference is not
pedantry: the props file is what we intended and the nuspec is what a resolver gets. A guard
derived from the file that produced a defect agrees with the defect. The existing
`PackedSolution` fixture already runs a real `dotnet pack`, so the guard iterates the packages
that actually came out — which is also why §3.2's "all five, not one" needs no separate step.

**`Umbraco.Cms.DevelopmentMode.Backoffice` is bounded in the props file and appears in no
nuspec**, because only the TestSite references it. Bounding it changes nothing a consumer sees
and keeps the file internally consistent; the guard correctly says nothing about it, because it
asserts over what shipped.
