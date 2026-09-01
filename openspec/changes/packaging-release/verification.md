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

## What is NOT yet verified, and why

**Tasks 5.3 and 5.4 are outstanding.** Creating a bookable resource, publishing a Booking
Page, taking a booking through the front end, seeing it in the backoffice Bookings list, and
reading the version off the backoffice Packages screen all require an authenticated backoffice
session. I do not enter credentials, so this half needs Chris at the keyboard.

The front-end booking is the step that most deserves a human: it is what `UBookIt.Web`'s
absence used to break, and it is invisible from the backoffice. The HTTP probes above are
strong evidence the assembly is there and wired up — they are not evidence that a visitor can
complete a booking.

To pick it up:

```
cd C:\Users\cnorw\AppData\Local\Temp\ubookit-install-check\InstallCheck
dotnet run
```

Then `/umbraco`, `admin@example.com` / `InstallCheck1234!`, and steps 2–7 of the checklist the
script prints when it finishes.
