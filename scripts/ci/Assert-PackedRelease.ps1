<#
.SYNOPSIS
    Fails unless a release's packed output is exactly what should be published: the right
    packages, at the tag's version, pointing at the public repository at the tagged commit.

.DESCRIPTION
    The second job of .github/workflows/publish.yml runs this after `dotnet pack`, and the
    publish job cannot start unless it passes. It replaces the "Verify, do not assume"
    section of docs/publishing.md, which a maintainer used to work through by eye, one
    package of five as an example.

    Every expectation is DERIVED from the release checkout, not listed here:

      THE PACKAGE SET. Each project in the solution is evaluated with
      `dotnet msbuild -getProperty` for IsPackable, PackageId and IncludeSymbols. For every
      packable project there must be exactly one <PackageId>.<Tag>.nupkg. There must be a
      <PackageId>.<Tag>.snupkg where IncludeSymbols is true, and none where it is false (the
      UBookIt meta-package carries no assemblies and sets it false). Anything else in the
      directory fails, so a stray package cannot ride along into the push.

      EACH MANIFEST. The .nuspec inside each .nupkg must carry <version> equal to the tag, and
      <repository> with url = the public repository (a trailing .git allowed) and commit = the
      tagged commit. Those are the addresses a consumer's tooling follows, frozen at push.

      SOURCELINK. For each project with symbols, its obj/Release/*/*.sourcelink.json (written
      by this pack) must exist. At least one must be found, so an empty glob cannot pass. Every
      URL in it must begin
      https://raw.githubusercontent.com/<Repository>/<Sha>/ - a debugger follows these, and a
      wrong host or an unpushed commit is a 404 for every consumer, permanently.

    Each failure is reported as its own annotation naming the file. Then the script exits 1.

.PARAMETER PackagesDirectory
    Where `dotnet pack -o` wrote the packages.

.PARAMETER ReleaseRoot
    The release checkout that was packed.

.PARAMETER Tag
    The release tag, which is also the version every package must carry.

.PARAMETER Sha
    The tagged commit.

.PARAMETER Repository
    owner/name on GitHub, e.g. Chris-N2/UBookIt. The workflow passes github.repository.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $PackagesDirectory,

    [Parameter(Mandatory)]
    [string] $ReleaseRoot,

    [Parameter(Mandatory)]
    [string] $Tag,

    [Parameter(Mandatory)]
    [string] $Sha,

    [Parameter(Mandatory)]
    [string] $Repository
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string] $file, [string] $message) {
    $failures.Add($message)
    Write-Host "::error title=Packed release,file=${file}::$message"
}

$publicRepository = "https://github.com/$Repository"
$sourcePrefix = "https://raw.githubusercontent.com/$Repository/$Sha/"

# --- The expected set, from the solution -------------------------------------------------

$solution = Join-Path $ReleaseRoot 'UBookIt.slnx'
if (-not (Test-Path $solution)) {
    Write-Host "::error title=Packed release::$solution does not exist, so the expected package set cannot be derived."
    exit 1
}

$projectPaths = @(([xml](Get-Content -Raw $solution)).SelectNodes('//Project/@Path') | ForEach-Object { $_.Value })
if ($projectPaths.Count -eq 0) {
    Write-Host "::error title=Packed release::$solution lists no projects, so the expected package set cannot be derived."
    exit 1
}

$expected = @()
foreach ($relative in $projectPaths) {
    $project = Join-Path $ReleaseRoot $relative
    $json = dotnet msbuild $project -getProperty:IsPackable -getProperty:PackageId -getProperty:IncludeSymbols -p:Configuration=Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error title=Packed release,file=$relative::$relative could not be evaluated, so whether it should have been packed is unknown."
        exit 1
    }

    $properties = ($json | ConvertFrom-Json).Properties
    if ($properties.IsPackable -eq 'true') {
        $expected += [pscustomobject]@{
            Project     = $relative
            Directory   = Split-Path $project -Parent
            PackageId   = $properties.PackageId
            WithSymbols = ($properties.IncludeSymbols -eq 'true')
        }
    }
}

if ($expected.Count -eq 0) {
    Write-Host "::error title=Packed release::No project in $solution is packable, which cannot be a release."
    exit 1
}
Write-Host "Expected packages ($($expected.Count)): $(($expected | ForEach-Object { if ($_.WithSymbols) { "$($_.PackageId) (+symbols)" } else { $_.PackageId } }) -join ', ')"

# --- The set on disk ---------------------------------------------------------------------

$expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($package in $expected) {
    [void] $expectedNames.Add("$($package.PackageId).$Tag.nupkg")
    if ($package.WithSymbols) {
        [void] $expectedNames.Add("$($package.PackageId).$Tag.snupkg")
    }
}

$present = @(Get-ChildItem -Path $PackagesDirectory -File -ErrorAction SilentlyContinue)
foreach ($file in $present) {
    if (-not $expectedNames.Contains($file.Name)) {
        Add-Failure $file.Name "$($file.Name) is in the packed output but no packable project at $Tag accounts for it. Nothing unexpected may ride along into the push."
    }
}
foreach ($name in $expectedNames) {
    if (-not (Test-Path (Join-Path $PackagesDirectory $name))) {
        Add-Failure $name "$name is missing from the packed output, but its project is packable."
    }
}

# --- Each manifest -----------------------------------------------------------------------

foreach ($package in $expected) {
    $name = "$($package.PackageId).$Tag.nupkg"
    $path = Join-Path $PackagesDirectory $name
    if (-not (Test-Path $path)) {
        continue # already reported as missing
    }

    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -notmatch '/' -and $_.Name -like '*.nuspec' } | Select-Object -First 1
        if ($null -eq $entry) {
            Add-Failure $name "$name contains no .nuspec."
            continue
        }

        $reader = [IO.StreamReader]::new($entry.Open())
        try { $nuspec = [xml] $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally {
        $zip.Dispose()
    }

    $metadata = $nuspec.package.metadata
    if ("$($metadata.version)" -ne $Tag) {
        Add-Failure $name "$name declares version '$($metadata.version)' in its .nuspec; the release is $Tag."
    }

    $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
    if ($null -eq $repositoryNode) {
        Add-Failure $name "$name's .nuspec has no <repository> element, so a consumer cannot find the source it was built from."
        continue
    }

    $url = $repositoryNode.GetAttribute('url') -replace '\.git$', ''
    if ($url -ne $publicRepository) {
        Add-Failure $name "$name's .nuspec names repository '$($repositoryNode.GetAttribute('url'))'; it must be $publicRepository."
    }
    if ($repositoryNode.GetAttribute('commit') -ne $Sha) {
        Add-Failure $name "$name's .nuspec names commit '$($repositoryNode.GetAttribute('commit'))'; the release is $Sha."
    }
}

# --- SourceLink ----------------------------------------------------------------------------

foreach ($package in ($expected | Where-Object WithSymbols)) {
    $maps = @(Get-ChildItem -Path (Join-Path $package.Directory 'obj/Release') -Recurse -Filter '*.sourcelink.json' -File -ErrorAction SilentlyContinue)
    if ($maps.Count -eq 0) {
        Add-Failure $package.Project "$($package.Project) packs symbols but no SourceLink map was found under its obj/Release, so where a debugger would fetch its source cannot be checked."
        continue
    }

    foreach ($map in $maps) {
        $documents = (Get-Content -Raw $map.FullName | ConvertFrom-Json).documents
        $urls = @($documents.PSObject.Properties | ForEach-Object { "$($_.Value)" })
        if ($urls.Count -eq 0) {
            Add-Failure $map.Name "$($map.FullName) maps no documents."
        }
        foreach ($sourceUrl in $urls) {
            if (-not $sourceUrl.StartsWith($sourcePrefix, [StringComparison]::Ordinal)) {
                Add-Failure $map.Name "$($map.FullName) sends a debugger to '$sourceUrl'; every source URL must begin $sourcePrefix."
            }
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) problem(s) in the packed release. Nothing may be published from it."
    exit 1
}

$symbolCount = @($expected | Where-Object WithSymbols).Count
Write-Host "Packed release $Tag verified: $($expected.Count) packages and $symbolCount symbol packages, each at $Tag, naming $publicRepository at $Sha."
exit 0
