# UBookIt.TestSite

A development harness, not a sample site. It exists so the package can be exercised
against a real Umbraco boot — live rendering, the backoffice section, the management API
— and nothing in it is intended to be copied into a real site.

## It needs SQL Server, and the connection string lives in user secrets

uBookIt requires **SQL Server 2019+**. `UBookItPersistenceComposer` refuses any other
provider by design, with an explicit error, because the booking store depends on
`sp_getapplock` for its atomic placement contract — a guarantee SQLite cannot provide.

The connection string is therefore **not in `appsettings*.json`**. Set it in user secrets:

```bash
dotnet user-secrets --project src/UBookIt.TestSite set "ConnectionStrings:umbracoDbDSN" "Server=localhost;Database=uBookIt;Integrated Security=true;TrustServerCertificate=true"
dotnet user-secrets --project src/UBookIt.TestSite set "ConnectionStrings:umbracoDbDSN_ProviderName" "Microsoft.Data.SqlClient"
```

Umbraco shares that one connection with uBookIt (`shareUmbracoConnection: true`), so both
schemas live in the same database and there is nothing else to configure.

> **Why this file exists.** `appsettings.Development.json` used to carry a SQLite
> connection string. It never took effect — user secrets override it — but it was the
> first thing anyone found when asking which database the site uses, and it sent several
> sessions looking for a SQLite file that has never existed. The line is gone; this is
> where the answer lives now.

The site installs unattended on first boot (`InstallUnattended`), creating the admin user
from the credentials in `appsettings.Development.json`. Deleting the database and
restarting gives a clean site.

## Ports

`launchSettings.json` maps `https://localhost:44348` and `http://localhost:11262`. Use
**HTTPS** for anything touching the backoffice: OpenIddict refuses plain HTTP with
`invalid_request — This server only accepts HTTPS requests`, which is not obviously an
HTTPS problem when you meet it.

## It locks its own DLLs while running

Stop the site before rebuilding the solution. MSBuild reports the failure as an inability
to copy `UBookIt.Web.dll` and friends, which reads exactly like a code fault and has
cost time more than once.
