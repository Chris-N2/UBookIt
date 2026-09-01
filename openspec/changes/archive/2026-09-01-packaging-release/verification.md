# Installation verification

**Run on 2026-08-31, by hand, on Windows 11 / .NET 10.0.301 / SQL Server (default instance).**

**Run twice.** The second run was after the static-web-asset fix described in `design.md`, and
from a deleted client output, so the packages installed below were built the way a clean
checkout builds them and not the way a warm working copy does. Both runs produced the same
results; only the second is evidence.

## What this is, and what it is not

This is evidence about one afternoon. It is **not** a regression test, and nothing below
should be read as a standing guarantee.

The distinction matters more here than it usually would. Every packaging defect this change
fixes was present in a build that reported complete success, so "the pipeline is green" has
already proved worthless once for exactly this question. `PackageCompositionTests` now guards
what is *inside* the packages on every run; only an installation can show that the packages
are *enough*, and that half is manual. **An automated installation test remains an open
obligation.**

## How it was run

```
pwsh scripts/verify-install.ps1
```

Which packs the solution to `artifacts/install-feed`, creates an Umbraco 17 LTS site in a
temporary directory with `dotnet new umbraco -r LTS`, points it at SQL Server, adds **one**
package, and builds it. The site is deliberately created outside the repository so it does
not inherit `Directory.Build.props` or `Directory.Packages.props` and stop being a clean host.

One thing the first run found, worth keeping: if the developer's own NuGet configuration
enables **package source mapping** — mine does — an unmapped local feed is silently not
considered, and restore fails with `NU1101: Unable to find package UBookIt` while the packages
sit in the feed directory. The script now writes a `packageSourceMapping` section rather than
leaving it to the machine.

## What was observed

**Restore and build.** `dotnet add package UBookIt --version 0.1.0` resolved and installed
all five packages from the local feed:

```
Installed UBookIt 0.1.0
Installed UBookIt.Core 0.1.0
Installed UBookIt.Persistence 0.1.0
Installed UBookIt.Web 0.1.0
Installed UBookIt.Backoffice 0.1.0
```

The site built at **0 warnings, 0 errors**, and all four assemblies arrived in its output —
including `UBookIt.Web.dll`, which no previous build of this package had ever shipped to
anyone.

**Boot and migrations.** `dotnet run` started the site, the unattended install completed, and:

```
[INF] uBookIt applied 7 database migration(s): 20260727191209_Initial,
      20260727193625_ClaimResourceIndexIncludesBookingId, 20260807125020_AddServices,
      20260814082537_AddCapabilities, 20260817080001_AddDirectBookability,
      20260819103548_AddVisitorSelectableRole, 20260829161913_AddBookingServiceAttribution
[INF] Migration plans run: uBookIt.
[INF] Unattended upgrade completed successfully.
```

No boot warnings attributable to uBookIt beyond the expected
`No 'UBookIt:TimeZoneId' configuration value found; ... defaulting ... to UTC`, which is the
package telling an unconfigured site what it assumed.

**What the site actually serves.** Requested over HTTPS from the running site — this is the
part a `.nupkg` inspection cannot tell you, because it depends on the assets being *found* as
well as *present*:

| | |
|---|---|
| `/umbraco` | 200 |
| `/App_Plugins/UBookItBackoffice/umbraco-package.json` | 200 — and it reads `"version": "0.1.0"`, not `0.0.0` |
| `/App_Plugins/UBookItBackoffice/u-book-it-backoffice.js` | 200 — the backoffice bundle |
| `/_content/UBookIt.Web/ubookit.css` | 200, 13,472 bytes — the shipped stylesheet, from the package's own path |
| `/umbraco/ubookit/api/v1/resources` | 200 `{"total":0,"items":[]}` |
| `/umbraco/ubookit/api/v1/services` | 200 `{"total":0,"items":[]}` |

The last two matter most. `UBookIt.Web` is the assembly nothing referenced and nothing
packed; its delivery API answering in a site that obtained uBookIt only from a NuGet feed is
the direct refutation of the defect this change exists to fix.

## The backoffice half, 2026-09-01

Chris, in the installed site: **booking page published, booking taken through the front end**,
and the Packages → Installed screen showing the `uBookIt` migration plan and the package at
**`0.1.0`**. That closes tasks 5.3 and 5.4 and, with them, `docs/mvp.md` step 1.

**Looking at it produced two findings that nothing else could have.**

**The Packages screen named the package `UBookIt.Backoffice`.** An editor installs `UBookIt`;
they have no reason to know it is four assemblies, and being shown one assembly's name invites
the question of where the other three went. This is the same rule the nuspec metadata already
had to satisfy — a package named after its own assembly reads as one nobody has looked at —
applied to the one place an editor actually looks, and it had been left out of scope on the
grounds that only the *version* was wrong. The manifest's `name` is now `uBookIt`, its `id` is
unchanged, and a guard asserts the displayed name is not the package id.

**The uBookIt section is invisible until it is granted**, which is ordinary Umbraco behaviour
for any custom section and is documented in `docs/backoffice.md` — but the README, which is
what a first-time installer reads, said nothing, so the first experience after installing was
"where is it". The README now says so at the point of install, and repeats the reason the
backoffice docs give for granting it deliberately: booking data contains the name and email
address of every person who has booked.

Neither is a packaging defect in the strict sense. Both are the difference between a package
that installs and a package somebody can adopt, which is step 9.

## What is NOT yet verified, and why

**Every step of the installation path has now been walked.** What is unguarded is that it
*stays* walked: this was done by hand, and an automated installation test remains an open
obligation from `booking-page-packaging`. Nothing here discharges it.

**The 2026-08-31 runs recorded above were real, and the runs between them were not.** Found on
2026-09-01 by sweeping the QA fixes for falsified sentences: the script recreated the site
directory but left the **SQL database** alone, and the database outlives the directory. Umbraco
therefore booted as an already-installed site — the seven uBookIt migrations did not run, the
admin user kept its original password, and the run reported success having verified neither.

That is a fourth instance of this change's recurring fault, a check measuring history, and the
only one older than the change itself. The script now drops the database on a fresh run and
refuses with an explanatory error if it cannot. Re-run afterwards, and the log is unambiguous:
Umbraco creates its entire schema from nothing, `Unattended install completed`, then
`uBookIt applied 7 database migration(s)`. The admin password is generated per site and written
beside it, outside the repository, so nothing here depends on a credential in source control.

Two smaller gaps, stated rather than implied:

- **`npm ci` is unexercised.** This machine has `node_modules`, so only `npm run build` has
  actually run. It is the standard command and its failure would be loud, but it is untested.
- **A same-version reinstall is a trap**, and the script now defends against it rather than
  documenting it. During development the version does not change between runs, and NuGet
  identifies a package by id and version alone — so a cached `ubookit.backoffice/0.1.0` is
  reused without the feed being consulted, and a second run would verify the *first* run's
  packages while reporting success. `verify-install.ps1` evicts uBookIt from the global cache
  before installing. This was found by the manifest change above not appearing.

To repeat the whole thing: `pwsh scripts/verify-install.ps1`, then `dotnet run` in the site it
creates and steps 1–7 of the checklist it prints. Add `-KeepExisting` to reinstall into the
site that is already there, keeping its database and content.
