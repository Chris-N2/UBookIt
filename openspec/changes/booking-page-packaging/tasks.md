## 1. Baseline

- [ ] 1.1 Check for an orphaned TestSite on 44348 **before** building — an earlier
      session's process locks the output DLLs and a `--no-incremental` build then fails
      with a confusing file-lock error.
- [ ] 1.2 `dotnet build --no-incremental` (zero warnings) then `dotnet test --no-build`.
      Record per-assembly counts, not just "Passed!" — a missing assembly is the
      loudest signal available and the easiest to skim past.

## 2. Ship the schema

- [ ] 2.1 Add an `AutomaticPackageMigrationPlan` and an embedded `package.xml` to
      `UBookIt.Web`, with the manifest in the plan type's namespace.
- [ ] 2.2 Define the Booking Page document type. Settle design.md's two open questions
      first and record the answers: allow-at-root vs composition, and whether the type
      needs any properties at all.
- [ ] 2.3 Define the template as a **one-line delegate** into the booking ViewComponent
      (design D2). No wrapper markup, no styling, nothing worth keeping.

## 3. Prove the coupling, because its failure is silent

- [ ] 3.1 Assert the embedded resource exists and its name matches the plan type's
      namespace. Mutation-check it: rename the namespace (or drop the
      `<EmbeddedResource>` entry) and confirm the test fails. A wrong coupling produces
      **no error at runtime** — the site simply comes up without the schema (design D5).
- [ ] 3.2 Assert the shipped template carries no markup of its own, so D2's constraint
      cannot erode into "just a wrapper div". Mutation-check by adding one.
- [ ] 3.3 Assert the manifest declares no content (`<Documents>`), so "installs no
      pages" is checked rather than intended.

## 4. Verify against a real site

- [ ] 4.1 Launch the TestSite **fully detached** (`Start-Process -WindowStyle Hidden`),
      never as a tracked background task — stopping the task truncates build output and
      the next start silently serves a harness that 500s on every page.
- [ ] 4.2 Confirm the document type and template are created on a site that lacks them.
      Use the **management API with a client-credentials token**, not browser
      automation: document types and templates are Umbraco's own management API, so no
      backoffice login is needed. The spike's helper is the pattern.
- [ ] 4.3 Create and publish a page of the shipped type and confirm the booking flow
      renders on it — the spec's headline scenario, and the one thing no unit test can
      answer.
- [ ] 4.4 Stop the TestSite afterwards and confirm the port is clear. Check for a
      LISTENING entry specifically; a `TIME_WAIT` line with PID 0 is your own client
      socket.

## 5. Document what an upgrade owns

- [ ] 5.1 Write the ownership documentation the spec requires: what the package
      replaces on upgrade (template contents; the type's name, icon, description,
      allow-at-root) and what it never touches (view overrides, editor-added
      properties, the content tree).
- [ ] 5.2 State the counter-intuitive case explicitly, because an editor cannot
      discover it safely: a release that does **not** change the template still
      replaces it, since the whole manifest re-imports on any change.
- [ ] 5.3 Say plainly that the Umbraco documentation's "existing schema will not be
      overwritten" does not hold for schema, so a reader who checks the upstream docs
      is not left thinking ours is wrong.

## 6. Decide the harness's fate

- [ ] 6.1 Decide explicitly what happens to `UbookitBookingTest.cshtml`. It stops being
      the only documentation, but remains the only way to reach the `?flow` and
      `?resourceId` overrides. Deleting it by reflex would remove those; keeping it
      silently leaves a dev harness reading as a sample. Record the decision.

## 7. Obligations

- [ ] 7.1 Carry design D4's finding to the deferred POST-path change: **TempData is not
      a consequence of redirecting, it is a consequence of the ViewComponent
      boundary.** The component reads the failure payload itself and ModelState cannot
      reach it, so "return `CurrentUmbracoPage()` instead of redirecting" does not
      remove TempData. That change's spike starts at "how does the page get a model",
      not at the controller.
- [ ] 7.2 Correct the standing note that ⑪'s two DI registrations are scaffolding
      awaiting demolition. `BeginUmbracoForm` now looks likely to **stay** — a surface
      controller remains the POST mechanism — so the registrations stay too. Do not
      delete them here, and stop describing them as temporary.
- [ ] 7.3 The `[ValidateAntiForgeryToken]` obligation stays **open** and moves to the
      POST-path change. It must not acquire a tick because this change touched
      packaging.
- [ ] 7.4 Record the untested cases as untested: an editor deleting a shipped property,
      or the whole document type.

## 8. Verify

- [ ] 8.1 `dotnet build --no-incremental` — zero warnings. `dotnet test` does **not**
      accept `--no-incremental` (MSBuild-only switch, a parse error); build first, then
      `dotnet test --no-build`.
- [ ] 8.2 Full solution green; report counts against the 1.2 baseline.
- [ ] 8.3 `openspec validate --strict` against the pinned CLI (1.6.0).
- [ ] 8.4 Sync-time **outward** grep for sibling specs this change falsifies. It has
      found something on most changes. Grep the vocabulary of the *mechanisms* as well
      as of the change — "template", "document type", "site author", "install".
- [ ] 8.5 Confirm the TestSite is stopped, the tree is clean, and no spike artefact
      survived.
- [ ] 8.6 QA review in a **fresh context or subagent** — never the context that applied.
