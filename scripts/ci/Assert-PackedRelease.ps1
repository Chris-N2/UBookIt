<#
.SYNOPSIS
    Fails unless a release's packed output is exactly what should be published: the right
    packages, at the tag's version, pointing at the public repository at the tagged commit,
    with symbols for every assembly they carry.

.DESCRIPTION
    The second job of .github/workflows/publish.yml runs this after `dotnet pack`, and the
    publish job cannot start unless it passes. It does, for every package and every time, the
    version, repository and SourceLink part of the "Verify, do not assume" section of
    docs/publishing.md, which a maintainer used to work through by eye on one package of five.

    Every expectation is DERIVED, not listed here, and each one from what the release
    ACTUALLY CONTAINS where it can be:

      THE PACKAGE SET. Each project in the solution is evaluated with
      `dotnet msbuild -getProperty` for IsPackable, PackageId and IncludeSymbols. For every
      packable project there must be exactly one <PackageId>.<Tag>.nupkg. Anything else in the
      directory fails, so a stray package cannot ride along into the push.

      SYMBOLS, FROM THE ASSEMBLIES. A package carries assemblies if its .nupkg has any
      lib/**/*.dll. Such a package must have <PackageId>.<Tag>.snupkg, and that .snupkg must
      hold a .pdb beside every one of those assemblies. A package with no assemblies must have
      no .snupkg. IncludeSymbols is NOT where this comes from. It is checked AGAINST the
      contents, and disagreement fails. QA round 1 showed why: the first version took the
      expectation from IncludeSymbols, so setting it false on a library that ships a dll made
      the missing symbols expected, and the run passed with "3 symbol packages". A rule that
      reads the setting whose mistake it exists to catch cannot catch it.

      EACH MANIFEST. The .nuspec inside each .nupkg must carry <version> equal to the tag, and
      <repository> with url = the public repository (a trailing .git allowed) and commit = the
      tagged commit. Those are the addresses a consumer's tooling follows, frozen at push.

      SOURCELINK, PER ASSEMBLY. For every assembly a package carries, its project's
      obj/Release/*/<assembly>.sourcelink.json (written by this pack) must exist, and every URL
      in it must begin https://raw.githubusercontent.com/<Repository>/<Sha>/. A debugger follows
      these, and a wrong host or an unpushed commit is a 404 for every consumer, permanently.
      Every other map under that project's obj/Release is held to the same prefix.

    Each failure is reported as its own annotation naming the file (and, for a missing package,
    the project). Then the script exits 1.

    WHAT IT DOES NOT CHECK. Authors, copyright, licence, icon, readme, project URL, description
    and title. PackageCompositionTests checks that all but copyright are PRESENT and not framework
    defaults, and that the readme and icon are inside each package. It does this in ci's own pack
    of the same commit, and the check job requires that run to have passed. Nothing checks that
    the values are RIGHT, and nothing checks copyright at all. All of it is frozen at push, so
    docs/publishing.md keeps reading one .nuspec as a human step before approval.

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

# NOTE: PowerShell variable names are case-insensitive. A local named like a parameter
# ($repository, $tag, $sha) IS that parameter, and assigning to it coerces into the parameter's
# declared type. That cost this script a defect once; keep locals distinct.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string] $file, [string] $message) {
    $failures.Add($message)
    Write-Host "::error title=Packed release,file=${file}::$message"
}

# The entry names in a zip, with forward slashes.
function Get-ZipEntryNames([string] $path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try { return @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' }) }
    finally { $zip.Dispose() }
}

function Read-ZipEntry([string] $path, [string] $entryName) {
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entry = $zip.GetEntry($entryName)
        if ($null -eq $entry) { return $null }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally { $zip.Dispose() }
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
            Project        = $relative
            Directory      = Split-Path $project -Parent
            PackageId      = $properties.PackageId
            IncludeSymbols = ($properties.IncludeSymbols -eq 'true')
        }
    }
}

if ($expected.Count -eq 0) {
    Write-Host "::error title=Packed release::No project in $solution is packable, which cannot be a release."
    exit 1
}
Write-Host "Packable projects ($($expected.Count)): $(($expected | ForEach-Object PackageId) -join ', ')"

# --- Each package ----------------------------------------------------------------------------

$expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$symbolPackages = 0

foreach ($package in $expected) {
    $nupkgName = "$($package.PackageId).$Tag.nupkg"
    $snupkgName = "$($package.PackageId).$Tag.snupkg"
    $nupkgPath = Join-Path $PackagesDirectory $nupkgName
    $snupkgPath = Join-Path $PackagesDirectory $snupkgName
    [void] $expectedNames.Add($nupkgName)

    if (-not (Test-Path $nupkgPath)) {
        Add-Failure $nupkgName "$($package.Project) is packable but produced no $nupkgName."
        # Without the package its contents are unknown; fall back to the setting so a
        # matching .snupkg is not also reported as stray.
        if ($package.IncludeSymbols) { [void] $expectedNames.Add($snupkgName) }
        continue
    }

    $entries = Get-ZipEntryNames $nupkgPath

    # The manifest.
    $nuspecName = $entries | Where-Object { $_ -notmatch '/' -and $_ -like '*.nuspec' } | Select-Object -First 1
    if ($null -eq $nuspecName) {
        Add-Failure $nupkgName "$nupkgName contains no .nuspec."
    }
    else {
        $nuspec = [xml] (Read-ZipEntry $nupkgPath $nuspecName)
        $metadata = $nuspec.package.metadata
        if ("$($metadata.version)" -ne $Tag) {
            Add-Failure $nupkgName "$nupkgName declares version '$($metadata.version)' in its .nuspec; the release is $Tag."
        }

        $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
        if ($null -eq $repositoryNode) {
            Add-Failure $nupkgName "$nupkgName's .nuspec has no <repository> element, so a consumer cannot find the source it was built from."
        }
        else {
            $repositoryUrl = $repositoryNode.GetAttribute('url') -replace '\.git$', ''
            if ($repositoryUrl -ne $publicRepository) {
                Add-Failure $nupkgName "$nupkgName's .nuspec names repository '$($repositoryNode.GetAttribute('url'))'; it must be $publicRepository."
            }
            if ($repositoryNode.GetAttribute('commit') -ne $Sha) {
                Add-Failure $nupkgName "$nupkgName's .nuspec names commit '$($repositoryNode.GetAttribute('commit'))'; the release is $Sha."
            }
        }
    }

    # The assemblies it carries decide everything symbol-related.
    $assemblies = @($entries | Where-Object { $_ -match '^lib/.+\.dll$' })
    $carriesAssemblies = $assemblies.Count -gt 0

    if ($carriesAssemblies -ne $package.IncludeSymbols) {
        if ($carriesAssemblies) {
            Add-Failure $nupkgName "$nupkgName carries $($assemblies.Count) assembl$(if ($assemblies.Count -eq 1) { 'y' } else { 'ies' }) ($($assemblies -join ', ')) but $($package.Project) sets IncludeSymbols false, so the package would ship with no symbols."
        }
        else {
            Add-Failure $nupkgName "$($package.Project) sets IncludeSymbols true but $nupkgName carries no assemblies, so there is nothing for a symbol package to describe."
        }
    }

    if (-not $carriesAssemblies) {
        continue
    }

    [void] $expectedNames.Add($snupkgName)

    # A .pdb beside every assembly, in the symbol package.
    if (-not (Test-Path $snupkgPath)) {
        Add-Failure $snupkgName "$nupkgName carries assemblies but $snupkgName is missing, so a debugger stepping into $($package.PackageId) gets no source."
    }
    else {
        $symbolPackages++
        $symbolEntries = [System.Collections.Generic.HashSet[string]]::new([string[]] (Get-ZipEntryNames $snupkgPath), [StringComparer]::OrdinalIgnoreCase)
        foreach ($assembly in $assemblies) {
            $pdb = [IO.Path]::ChangeExtension($assembly, '.pdb')
            if (-not $symbolEntries.Contains($pdb)) {
                Add-Failure $snupkgName "$snupkgName has no $pdb for $assembly."
            }
        }
    }

    # A SourceLink map for every assembly, and every map under the project at the right prefix.
    $objRelease = Join-Path $package.Directory 'obj/Release'
    $maps = @(Get-ChildItem -Path $objRelease -Recurse -Filter '*.sourcelink.json' -File -ErrorAction SilentlyContinue)
    foreach ($assembly in $assemblies) {
        $mapName = "$([IO.Path]::GetFileNameWithoutExtension($assembly)).sourcelink.json"
        if (-not ($maps | Where-Object Name -eq $mapName)) {
            Add-Failure $package.Project "$nupkgName carries $assembly but no $mapName was found under $($package.Project)'s obj/Release, so where a debugger would fetch its source cannot be checked."
        }
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

# --- Nothing else ----------------------------------------------------------------------------

foreach ($file in @(Get-ChildItem -Path $PackagesDirectory -File -ErrorAction SilentlyContinue)) {
    if (-not $expectedNames.Contains($file.Name)) {
        Add-Failure $file.Name "$($file.Name) is in the packed output but nothing packable at $Tag accounts for it. Nothing unexpected may ride along into the push."
    }
}

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) problem(s) in the packed release. Nothing may be published from it."
    exit 1
}

Write-Host "Packed release $Tag verified: $($expected.Count) packages and $symbolPackages symbol packages, each at $Tag, naming $publicRepository at $Sha."
exit 0
