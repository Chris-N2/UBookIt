<#
.SYNOPSIS
    Fails when this commit's CI definitions differ from the other published line's tip.

.DESCRIPTION
    uBookIt publishes two lines — main (17.x, Umbraco 17 LTS) and dev/v18 (18.x, Umbraco 18
    STS) — which are never merged; shared truth moves between them by cherry-pick. A fix
    reaching one line only happened three times on 2026-09-23. For CI that failure mode is
    worse than usual, because a workflow change on one line leaves the other line verified
    by a different pipeline with nothing to say so.

    So the CI definitions are held identical on both lines, and this check compares them:

        .github/          the workflows
        global.json       the pinned SDK band
        scripts/ci/       the scripts the workflows run (this file included)
        Directory.Build.props, minus its <Version> line (below)

    Directory.Build.props is compared TOO, with its <Version> line removed from both sides:
    the version is the one thing in it that legitimately differs (17.x against 18.x), and the
    rest carries CI's warnings-as-errors rule and its advisory exception, which must not drift
    between lines. It used to be excluded outright; QA pointed out that left the advisory
    exception free to drift silently.

    EXPECT A TRANSIENT RED. Between pushing a CI change to one line and pushing it to the
    other, the first line's run fails here. That red is true — at that moment the lines do
    differ — and it clears by re-running the job once the second push has landed.

.PARAMETER Line
    The published line this commit belongs to, or is being merged into: main or dev/v18.

.PARAMETER Remote
    The remote whose branches are compared against. Defaults to origin.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('main', 'dev/v18')]
    [string] $Line,

    [string] $Remote = 'origin'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$other = if ($Line -eq 'main') { 'dev/v18' } else { 'main' }
$otherRef = "$Remote/$other"
$paths = @('.github', 'global.json', 'scripts/ci')

git rev-parse --verify --quiet "$otherRef^{commit}" | Out-Null
if ($LASTEXITCODE -ne 0) {
    # Not a pass. A comparison that could not be made has not shown the lines agree.
    Write-Host "::error title=Line parity::$otherRef is not available in this clone, so parity with $other could not be checked. CI must check out with fetch-depth: 0."
    exit 1
}

$differing = @(git diff --name-only $otherRef HEAD -- @paths)
if ($LASTEXITCODE -ne 0) {
    Write-Host "::error title=Line parity::git diff against $otherRef failed."
    exit 1
}

# Directory.Build.props, minus the one line that is allowed to differ.
function Get-PropsWithoutVersion([string] $ref) {
    $text = git show "${ref}:Directory.Build.props" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    # Only the FIRST whole-line <Version> element is dropped: that is the package version. A
    # second one elsewhere in the file would be something else, and must be compared.
    $dropped = $false
    $kept = foreach ($line in $text) {
        if (-not $dropped -and $line -match '^\s*<Version>[^<]*</Version>\s*$') {
            $dropped = $true
            continue
        }
        $line
    }

    return @($kept) -join "`n"
}

$ourProps = Get-PropsWithoutVersion 'HEAD'
$theirProps = Get-PropsWithoutVersion $otherRef
if ($null -eq $ourProps -or $null -eq $theirProps) {
    Write-Host "::error title=Line parity::Directory.Build.props could not be read from HEAD or $otherRef, so its parity could not be checked."
    exit 1
}
if ($ourProps -ne $theirProps) {
    # A real path, so GitHub can anchor the annotation to the file; the message says what was
    # ignored.
    $differing += 'Directory.Build.props'
    Write-Host "Directory.Build.props differs from $otherRef even with its package <Version> line ignored."
}

if ($differing.Count -gt 0) {
    foreach ($file in $differing) {
        Write-Host "::error title=Line parity,file=$file::$file differs between $Line (this commit) and $otherRef. Land the same change on $other, then re-run this job."
    }

    Write-Host "$($differing.Count) CI definition file(s) differ from $otherRef."
    exit 1
}

Write-Host "CI definitions ($($paths -join ', '), and Directory.Build.props apart from <Version>) are identical to $otherRef."
exit 0
