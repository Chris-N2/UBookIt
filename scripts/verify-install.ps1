<#
.SYNOPSIS
    Builds the uBookIt packages and installs them into a brand-new Umbraco site.

.DESCRIPTION
    This is the only check that can tell you whether uBookIt is installable.

    Every packaging defect this repository has had — a dependency on a package nobody
    publishes, an assembly missing from the aggregate, a backoffice bundle that was
    never built, a manifest stuck at version zero — was present in a build that
    reported complete success. The development site (src/UBookIt.TestSite) cannot see
    any of them either, because it references the projects directly. Only a site that
    resolves the packages from a feed can.

    So: pack -> local feed -> `dotnet new umbraco` somewhere else entirely ->
    `dotnet add package UBookIt` -> run -> book something.

    WHAT THIS IS NOT: a regression test. It proves the path on the day you run it, by
    hand, and the last stretch — publishing a booking page and taking a booking —
    needs a person in a browser. An automated installation test is a separate,
    recorded obligation. Do not let a green run of this script be read as a standing
    guarantee.

.PARAMETER SiteRoot
    Where to create the site. Defaults to a temporary directory. Deliberately outside
    the repository: a site created inside it would inherit Directory.Build.props and
    Directory.Packages.props and stop being a clean host.

.PARAMETER Database
    SQL Server database name. SQL Server is required — uBookIt does not support SQLite,
    including the SQLite database `dotnet new umbraco` would otherwise give you.

.PARAMETER KeepExisting
    Reuse an existing site directory instead of recreating it, for a second pass.
#>
[CmdletBinding()]
param(
    [string] $SiteRoot = (Join-Path ([System.IO.Path]::GetTempPath()) 'ubookit-install-check'),
    [string] $SqlServer = '.',
    [string] $Database = 'uBookItInstallCheck',
    [string] $AdminEmail = 'admin@example.com',
    [string] $AdminPassword = 'InstallCheck1234!',
    [switch] $KeepExisting
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$feed = Join-Path $repo 'artifacts/install-feed'
$sitePath = Join-Path $SiteRoot 'InstallCheck'

# The version is never typed twice. It lives in Directory.Build.props and everything —
# the packages, the backoffice manifest, this script — takes it from there.
$props = [xml](Get-Content (Join-Path $repo 'Directory.Build.props'))
$version = ($props.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { throw 'No <Version> in Directory.Build.props.' }

Write-Host "uBookIt $version" -ForegroundColor Cyan

# ---------------------------------------------------------------- 1. pack

Write-Host "`n[1/5] Packing to $feed" -ForegroundColor Cyan
if (Test-Path $feed) { Remove-Item $feed -Recurse -Force }
New-Item -ItemType Directory -Path $feed | Out-Null

& dotnet pack (Join-Path $repo 'UBookIt.slnx') -c Release -o $feed
if ($LASTEXITCODE -ne 0) { throw 'pack failed' }

$packed = Get-ChildItem $feed -Filter '*.nupkg' | Where-Object { $_.Name -notlike '*.snupkg' }
Write-Host ("      " + (($packed | ForEach-Object { $_.Name }) -join "`n      "))

# Evict the previous build of this same version from the global package cache.
#
# During development the version does not change between runs, and NuGet identifies a
# package by id and version alone: a cached ubookit.backoffice/0.1.0 is reused without
# ever looking at the feed. So a second run of this script would install the FIRST run's
# packages and report success — the script would be verifying history rather than the
# build that just ran, which is the exact failure this whole change exists to prevent.
$cache = Join-Path $env:USERPROFILE '.nuget/packages'
foreach ($package in $packed) {
    $id = $package.BaseName -replace "\.$([regex]::Escape($version))$", ''
    $cached = Join-Path $cache "$($id.ToLowerInvariant())/$version"
    if (Test-Path $cached) {
        Remove-Item $cached -Recurse -Force
        Write-Host "      evicted from cache: $id/$version" -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------- 2. a site from scratch

Write-Host "`n[2/5] Creating an Umbraco site at $sitePath" -ForegroundColor Cyan
if ((Test-Path $sitePath) -and -not $KeepExisting) { Remove-Item $sitePath -Recurse -Force }

if (-not (Test-Path $sitePath)) {
    New-Item -ItemType Directory -Path $sitePath -Force | Out-Null

    $connectionString = "Server=$SqlServer;Database=$Database;Integrated Security=true;TrustServerCertificate=true"

    # -r LTS is the point: this must be an Umbraco 17 site, not whatever is newest.
    # The unattended credentials skip the install wizard so the run reaches the
    # backoffice without a person having to type anything.
    & dotnet new umbraco -n InstallCheck -o $sitePath -r LTS `
        --connection-string $connectionString `
        --friendly-name 'Install Check' --email $AdminEmail --password $AdminPassword
    if ($LASTEXITCODE -ne 0) { throw 'dotnet new umbraco failed' }

    # uBookIt comes from the local feed and everything else from nuget.org. The
    # mapping is not decoration: if the developer's own NuGet configuration enables
    # package source mapping — mine does — an unmapped local feed is silently not
    # considered, and the run fails with "no packages exist with this id" while the
    # packages sit right there. Clearing first means this file decides, not whatever
    # the machine happens to have.
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="ubookit-local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="ubookit-local">
      <package pattern="UBookIt*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $sitePath 'nuget.config') -Encoding UTF8
}

# ---------------------------------------------------------------- 3. install

Write-Host "`n[3/5] dotnet add package UBookIt --version $version" -ForegroundColor Cyan

# ONE package. If this needs a second one, the aggregate is broken and the whole
# reason for UBookIt-the-meta-package has failed.
& dotnet add (Join-Path $sitePath 'InstallCheck.csproj') package UBookIt --version $version
if ($LASTEXITCODE -ne 0) { throw 'dotnet add package failed — restore could not resolve uBookIt' }

# ---------------------------------------------------------------- 4. build

Write-Host "`n[4/5] Building the site" -ForegroundColor Cyan
& dotnet build (Join-Path $sitePath 'InstallCheck.csproj')
if ($LASTEXITCODE -ne 0) { throw 'the site does not build with uBookIt installed' }

# A cheap check that costs nothing and catches the loudest failure early: every
# assembly a working install needs is where the site can load it.
$binDir = Join-Path $sitePath 'bin/Debug/net10.0'
foreach ($assembly in 'UBookIt.Core.dll', 'UBookIt.Persistence.dll', 'UBookIt.Backoffice.dll', 'UBookIt.Web.dll') {
    if (-not (Test-Path (Join-Path $binDir $assembly))) {
        throw "$assembly did not arrive in the site's output. Installing the aggregate did not bring in everything."
    }
    Write-Host "      $assembly" -ForegroundColor DarkGray
}

# ---------------------------------------------------------------- 5. the human half

Write-Host "`n[5/5] Now run it and use it." -ForegroundColor Cyan
Write-Host @"

    cd "$sitePath"
    dotnet run

Then, in a browser — and this half is the point, because it is what a package
inspection cannot tell you:

  1. Log in at /umbraco with $AdminEmail / $AdminPassword
  2. Packages -> installed: uBookIt should be listed at version $version, not 0.0.0
  3. There is a Bookings section, and it opens (this is the backoffice bundle)
  4. Create a bookable resource with some opening hours
  5. Create and PUBLISH a page of type "Booking Page"
  6. Visit that page on the front end and take a booking
     ^ this is the step UBookIt.Web's absence used to break, silently
  7. The booking appears in the backoffice Bookings list

Record what you saw. This script proves the path once; it is not a regression test.

"@ -ForegroundColor Gray
