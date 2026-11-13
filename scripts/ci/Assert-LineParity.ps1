<#
.SYNOPSIS
    Fails when this commit's CI definitions differ from any other published line's tip.

.DESCRIPTION
    uBookIt publishes one line per Umbraco major it supports (main for the LTS, a dev/vNN branch
    per STS major). The lines are never merged; shared truth moves between them by cherry-pick.
    A fix reaching one line only happened three times on 2026-09-23. For CI that failure mode is
    worse than usual, because a workflow change on one line leaves another line verified by a
    different pipeline with nothing to say so.

    So the CI definitions are held identical on every published line, and this check compares
    this commit with the tip of EVERY OTHER line named in published-lines.json:

        .github/          the workflows
        scripts/ci/       the scripts the workflows run (this file and the list included)
        global.json       the pinned SDK, minus its sdk.version (below)
        Directory.Build.props, minus its <Version> line (below)

    TWO VALUES ARE EXEMPT, because each legitimately differs between lines, and nothing else is:
      - Directory.Build.props: the first whole-line <Version>, the package version (17.x against
        18.x). The rest of the file carries CI's warnings-as-errors rule and its advisory
        exception, which must not drift. It used to be excluded outright; QA pointed out that left
        the advisory exception free to drift silently.
      - global.json: sdk.version. A line pins the SDK its Umbraco major needs (Umbraco 19 needs
        .NET 11). The file is compared as parsed JSON with that one value removed, so the
        roll-forward policy, and anything added later, is still compared, and a reformat is not a
        difference.

    RUN ON PUSHES ONLY (ci.yml). A CI change reaches every line through one pull request per line,
    and a pull request's run compared against the other lines' tips would fail on all of them
    until every one had merged: a deadlock that was broken once only by merging past a red check.
    The push run is where this counts. Between a CI change reaching its first line and its last,
    the earlier lines' push runs fail here. That red is true, because at that moment the lines do
    differ, and it clears by re-running those runs once the change has reached every line. A
    release tag requires a successful push run, so a commit cannot be released while it stands.

.PARAMETER Line
    The published line this commit was pushed to. A branch that is not in the list is not a
    published line: the check says so and passes, because only published lines are compared.

.PARAMETER Remote
    The remote whose branches are compared against. Defaults to origin.

.PARAMETER LinesFile
    The published-lines list. Defaults to published-lines.json beside this script.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Line,

    [string] $Remote = 'origin',

    [string] $LinesFile = (Join-Path $PSScriptRoot 'published-lines.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PublishedLines.ps1')

try {
    $lines = Read-PublishedLines -Path $LinesFile
}
catch {
    Write-Host "::error title=Line parity::$($_.Exception.Message)"
    exit 1
}

if ($lines -cnotcontains $Line) {
    Write-Host "$Line is not a published line ($($lines -join ', ')), so its CI definitions are not parity-checked."
    exit 0
}

$others = @($lines | Where-Object { $_ -cne $Line })
# global.json is compared on its own (below), so it is not in the whole-file list.
$paths = @('.github', 'scripts/ci')

# Directory.Build.props, minus the one line that is allowed to differ.
function Get-PropsWithoutVersion([string] $ref) {
    $text = git show "${ref}:Directory.Build.props" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    # Only the FIRST whole-line <Version> element is dropped: that is the package version. A
    # second one elsewhere in the file would be something else, and must be compared.
    $dropped = $false
    $kept = foreach ($text_line in $text) {
        if (-not $dropped -and $text_line -match '^\s*<Version>[^<]*</Version>\s*$') {
            $dropped = $true
            continue
        }
        $text_line
    }

    return @($kept) -join "`n"
}

# A JSON value with every object's keys in ordinal order, so two files that say the same thing
# serialise the same way however they are laid out.
function ConvertTo-Canonical($value) {
    if ($value -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in @($value.Keys | Sort-Object -CaseSensitive)) {
            $ordered[$key] = ConvertTo-Canonical $value[$key]
        }
        return $ordered
    }
    if ($value -is [System.Collections.IList]) {
        return , @($value | ForEach-Object { ConvertTo-Canonical $_ })
    }
    return $value
}

# global.json, parsed, minus sdk.version: the one value allowed to differ. $null when the file is
# missing or is not a JSON object.
function Get-GlobalJsonWithoutSdkVersion([string] $ref) {
    $text = git show "${ref}:global.json" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    try {
        $parsed = (@($text) -join "`n") | ConvertFrom-Json -AsHashtable
    }
    catch {
        return $null
    }
    if ($parsed -isnot [System.Collections.IDictionary]) {
        return $null
    }

    if ($parsed['sdk'] -is [System.Collections.IDictionary]) {
        $parsed['sdk'].Remove('version')
    }

    return (ConvertTo-Canonical $parsed) | ConvertTo-Json -Depth 32 -Compress
}

$ourProps = Get-PropsWithoutVersion 'HEAD'
$ourGlobal = Get-GlobalJsonWithoutSdkVersion 'HEAD'
if ($null -eq $ourProps -or $null -eq $ourGlobal) {
    Write-Host "::error title=Line parity::Directory.Build.props or global.json could not be read or parsed at this commit, so parity could not be checked."
    exit 1
}

$failures = 0
foreach ($other in $others) {
    $otherRef = "$Remote/$other"

    git rev-parse --verify --quiet "$otherRef^{commit}" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        # Not a pass. A comparison that could not be made has not shown the lines agree. A listed
        # line that is not on the remote yet fails here until it is pushed.
        Write-Host "::error title=Line parity::$otherRef is not available in this clone, so parity with $other could not be checked. It is listed in published-lines.json, so it must exist on $Remote, and CI must check out with fetch-depth: 0."
        $failures++
        continue
    }

    $differing = @(git diff --name-only $otherRef HEAD -- @paths)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error title=Line parity::git diff against $otherRef failed."
        $failures++
        continue
    }

    $theirProps = Get-PropsWithoutVersion $otherRef
    if ($null -eq $theirProps) {
        Write-Host "::error title=Line parity::Directory.Build.props could not be read from $otherRef, so its parity could not be checked."
        $failures++
    }
    elseif ($ourProps -ne $theirProps) {
        # A real path, so GitHub can anchor the annotation to the file; the message says what was
        # ignored.
        $differing += 'Directory.Build.props'
        Write-Host "Directory.Build.props differs from $otherRef even with its package <Version> line ignored."
    }

    $theirGlobal = Get-GlobalJsonWithoutSdkVersion $otherRef
    if ($null -eq $theirGlobal) {
        Write-Host "::error title=Line parity,file=global.json::global.json could not be read or parsed as a JSON object at $otherRef, so its parity could not be checked."
        $failures++
    }
    elseif ($ourGlobal -cne $theirGlobal) {
        $differing += 'global.json'
        Write-Host "global.json differs from $otherRef even with its sdk.version ignored."
    }

    foreach ($file in $differing) {
        Write-Host "::error title=Line parity,file=$file::$file differs between $Line (this commit) and $otherRef. Land the same change on $other, then re-run this job."
    }
    if ($differing.Count -gt 0) {
        Write-Host "$($differing.Count) CI definition file(s) differ from $otherRef."
        $failures++
    }
    else {
        Write-Host "CI definitions are identical to $otherRef (Directory.Build.props apart from <Version>, global.json apart from sdk.version)."
    }
}

if ($failures -gt 0) {
    exit 1
}

Write-Host "CI definitions ($($paths -join ', '), global.json and Directory.Build.props) agree with every other published line: $($others -join ', ')."
exit 0
