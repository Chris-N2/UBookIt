## 1. Baseline

- [x] 1.1 Check for an orphaned TestSite on 44348 **before** building — an earlier
      session's process locks the output DLLs and a `--no-incremental` build then fails
      with a confusing file-lock error.
- [x] 1.2 `dotnet build --no-incremental` (zero warnings) then `dotnet test --no-build`.
      Record per-assembly counts, not just "Passed!" — a missing assembly is the
      loudest signal available and the easiest to skim past.

## 2. Ship the schema

- [x] 2.1 Add a package migration plan and an embedded `package.xml` to `UBookIt.Web`,
      with the manifest in the importing migration's namespace. **Revised at QA:** a
      run-once custom `PackageMigrationPlan`, not `AutomaticPackageMigrationPlan` —
      see design D1.
- [x] 2.2 Define the Booking Page document type. Settle design.md's two open questions
      first and record the answers: allow-at-root vs composition, and whether the type
      needs any properties at all.
- [x] 2.3 Define the template as a **one-line delegate** into the booking ViewComponent
      (design D2). No wrapper markup, no styling, nothing worth keeping.

## 3. Prove the coupling, because its failure is silent

- [x] 3.1 Assert the embedded resource exists and its name matches the importing
      migration's namespace. Mutation-check it: rename the namespace (or drop the
      `<EmbeddedResource>` entry) and confirm the test fails. **Corrected at QA:** a
      wrong coupling does NOT fail silently — it throws during boot and 500s every
      request. The test stays because it names the cause in a second (design D5).
- [x] 3.2 Assert the shipped template carries no markup of its own, so D2's constraint
      cannot erode into "just a wrapper div". Mutation-check by adding one.
- [x] 3.3 Assert the manifest declares no content (`<Documents>`), so "installs no
      pages" is checked rather than intended.

## 4. Verify against a real site

- [x] 4.1 Launch the TestSite **fully detached** (`Start-Process -WindowStyle Hidden`),
      never as a tracked background task — stopping the task truncates build output and
      the next start silently serves a harness that 500s on every page.
- [x] 4.2 Confirm the document type and template are created on a site that lacks them.
      Use the **management API with a client-credentials token**, not browser
      automation: document types and templates are Umbraco's own management API, so no
      backoffice login is needed. The spike's helper is the pattern.
- [x] 4.3 Create and publish a page of the shipped type and confirm the booking flow
      renders on it — the spec's headline scenario, and the one thing no unit test can
      answer.
- [x] 4.4 Stop the TestSite afterwards and confirm the port is clear. Check for a
      LISTENING entry specifically; a `TIME_WAIT` line with PID 0 is your own client
      socket.

## 5. Document what an upgrade owns

- [x] 5.1 Write the ownership documentation the spec requires: what the package
      replaces when an import runs (template contents; the type's name, icon,
      description, allow-at-root) and what it never touches (editor-added properties,
      the content tree, templates the site created). **Corrected at QA:** view
      overrides are NOT in the never-touched list, because they do not work at all.
- [x] 5.2 State the counter-intuitive cases explicitly, because a site author cannot
      discover them safely: a release carrying a migration step re-imports the **whole**
      manifest, so it replaces the template even though that release did not change it;
      and a deleted document type is **never** restored.
- [x] 5.3 Say plainly that the Umbraco documentation's "existing schema will not be
      overwritten" does not hold for schema, so a reader who checks the upstream docs
      is not left thinking ours is wrong.

## 6. Decide the harness's fate

- [x] 6.1 Decide explicitly what happens to `UbookitBookingTest.cshtml`. It stops being
      the only documentation, but remains the only way to reach the `?flow` and
      `?resourceId` overrides. Deleting it by reflex would remove those; keeping it
      silently leaves a dev harness reading as a sample. Record the decision.

## 7. Obligations

- [x] 7.1 Carry design D4's finding to the deferred POST-path change: **TempData is not
      a consequence of redirecting, it is a consequence of the ViewComponent
      boundary.** The component reads the failure payload itself and ModelState cannot
      reach it, so "return `CurrentUmbracoPage()` instead of redirecting" does not
      remove TempData. That change's spike starts at "how does the page get a model",
      not at the controller.
- [x] 7.2 Correct the standing note that ⑪'s two DI registrations are scaffolding
      awaiting demolition. `BeginUmbracoForm` now looks likely to **stay** — a surface
      controller remains the POST mechanism — so the registrations stay too. Do not
      delete them here, and stop describing them as temporary.
- [x] 7.3 The `[ValidateAntiForgeryToken]` obligation stays **open** and moves to the
      POST-path change. It must not acquire a tick because this change touched
      packaging.
- [x] 7.4 Record the untested cases as untested: an editor deleting a shipped property,
      or the whole document type.

## 8. Verify

- [x] 8.1 `dotnet build --no-incremental` — zero warnings. `dotnet test` does **not**
      accept `--no-incremental` (MSBuild-only switch, a parse error); build first, then
      `dotnet test --no-build`.
- [x] 8.2 Full solution green; report counts against the 1.2 baseline.
- [x] 8.3 `openspec validate --strict` against the pinned CLI (1.6.0).
- [x] 8.4 Outward grep run. **Nothing falsified** — `default-frontend:548` (renderable
      from a template) and `:810` (enter the flow at any point) are *satisfied* more
      fully, not contradicted. But it found an unmet obligation of a different kind:
      `persistence/spec.md:8` requires the SQL Server requirement to be stated in
      **package documentation**, which never existed until this change created
      `docs/`. Recorded as an obligation rather than discharged here — a database
      requirement does not belong on a page about the Booking Page, and the repo
      still has no README at all. Re-run at sync.
- [ ] 8.4b Re-run the outward grep at sync time, after the sync edits. It has
      found something on most changes. Grep the vocabulary of the *mechanisms* as well
      as of the change — "template", "document type", "site author", "install".
- [x] 8.5 Confirm the TestSite is stopped, the tree is clean, and no spike artefact
      survived.
- [ ] 8.6 QA review in a **fresh context or subagent** — never the context that applied.

## 9. QA round 1 — findings and dispositions

- [x] 9.1 **MAJOR-1 view overrides do not work — DEFERRED by Chris's decision.**
      Reproduced independently and the cause established: the package's views are
      compiled into `UBookIt.Web.dll` with **no `RazorSourceChecksum`** entries, so
      `CollectibleRuntimeViewCompiler.cs:209-218` marks them non-recompilable and uses
      the compiled copy *as-is* — a site's same-path file is never consulted. Confirmed
      as precedence not discovery (runtime compilation is live; editing the site's own
      template took effect with no rebuild). Worse in production, where the standard
      compiler is used. **Requirement 3 rewritten** from "customisation is by override"
      to "what the package owns is documented, and the package must not claim a route it
      does not have". Theming is the next change; design D3 records what it needs.
- [x] 9.2 **MAJOR-2 a deleted document type is never restored.** Corrected from
      inference to measurement in design.md Risks, and documented for site authors
      including the recovery step. Note D1's run-once plan makes this *worse*.
- [x] 9.3 **MAJOR-3 the "silent failure" premise was false** in four places including two
      shipped source comments — removing the resource throws `BootFailedException` and
      500s. Corrected in `design.md` D5, `UBookIt.Web.csproj`, `PackagingTests`, and the
      test now states why it still earns its place. D1's inverted `IgnoreCurrentState`
      rationale corrected in the artifact, not just the commit message.
- [x] 9.4 **MAJOR-4** `The_component_the_template_delegates_to_exists` ties the
      manifest's component name to a real `ViewComponent` by reflection. Mutation-checked
      with QA's exact case: `"BookinFlow"` now fails, naming the available components.
- [x] 9.5 **MAJOR-5** the template guard now anchors both lines with regex rather than
      counting lines and banning `<`. Mutation-checked with QA's exact case — text, an
      entity and branding on line 2 now fails.
- [x] 9.6 **MAJOR-6** `The_manifest_writes_no_files_into_the_site` covers
      `<PartialViews>`, `<Stylesheets>`, `<Scripts>` and `<Files>`; the content check
      also covers `<Media>`. Mutation-checked by adding a `<PartialViews>` entry.
- [x] 9.7 **MINOR-1** the docs' override section is gone with Requirement 3; replaced by
      the template-swap route, which works.
- [x] 9.8 **MINOR-2** docs now tell consumers to commit `Views/uBookItBookingPage.cshtml`
      before deploying, because a Production-mode site precompiles views at publish. The
      `.gitignore` entry says explicitly that ignoring it is specific to THIS repo.
- [x] 9.9 **MINOR-3** proposal now states the public API surface grows by two types, and
      that the plan's state ids and plan type cannot change after release.
- [x] 9.10 **NIT-1** stale `umbracoKeyValue` rows cleared by Chris (2026-08-25 evening).
- [x] 9.11 Two items from the first clean install: the `invalid Master 'null'` log line
      is an upstream nit fixed in v18 and deliberately not silenced (adding a `Layout`
      would impose ours on every consumer); the overwrite log line and
      `RunSchemaAndContentMigrations` are now both documented.
- [x] 9.12 **Verified live that the documented template-swap route works**, because the
      new Requirement 3 scenario asserts it and this change has already shipped one
      unmeasured customisation claim. Created a site-owned template, allowed it on the
      shipped document type, pointed the page at it, republished: `/book` rendered the
      site's chrome (`<div class="my-site-chrome">`, `<h1>Book with My Site</h1>`)
      wrapping uBookIt's flow. Reverted afterwards; Umbraco removed the template file
      with the template. Also answers NIT-3 — the flow starts at `<h2>` and the site's
      template supplies the `<h1>`, which the docs example now shows.
- [ ] 9.13 Re-review by the same QA subagent, with its round-1 context.
