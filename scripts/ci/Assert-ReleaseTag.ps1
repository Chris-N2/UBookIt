<#
.SYNOPSIS
    Fails unless a release tag names a verified commit on the right published line, at the
    version that commit declares.

.DESCRIPTION
    The first job of .github/workflows/publish.yml. Nothing is built, and nothing reaches
    nuget.org, unless every check here passes. In order:

      1. SHAPE.     The tag is <major>.<minor>.<patch>. The workflow's trigger filter
                    already limits it, but a filter is not an anchored match, and a dry run
                    takes the tag as free text. So it is checked again here.
      2. VERSION.   The tag equals the first whole-line <Version> in Directory.Build.props
                    at the tagged commit. That is the same "first whole line" rule
                    Assert-LineParity.ps1 uses to find the package version.
      3. LINE.      Each published line's major is read from the <Version> at that line's
                    tip. Exactly one line must have the tag's major, and the tagged commit
                    must be contained in it. The line is derived rather than tabled, so
                    nothing here needs editing when main moves to a new LTS major. The
                    branch names are the same pair Assert-LineParity.ps1 holds.
      4. VERIFIED.  The ci workflow has a SUCCESSFUL run for a push of this exact commit to
                    that line. A run still queued or in progress is waited for, up to
                    -WaitMinutes; a run that failed or was cancelled, or no run at all,
                    fails here. This is what ci.yml's "a release tag names a commit whose
                    push has already been verified" is enforced by.

    The GitHub API is called with Invoke-RestMethod rather than `gh`, so that a local run and
    a runner run take the same path. It is authenticated when GITHUB_TOKEN is set, and
    anonymous otherwise, which is enough for this public repository at low volume.

    On success, writes `sha` and `line` to $GITHUB_OUTPUT when that is set.

.PARAMETER Tag
    The release tag, e.g. 17.2.2.

.PARAMETER ReleaseRoot
    A checkout containing the tag and the published lines' remote-tracking branches (a clone
    with full history, as actions/checkout gives with fetch-depth: 0).

.PARAMETER Repository
    owner/name on GitHub, e.g. Chris-N2/UBookIt. The workflow passes github.repository.

.PARAMETER Remote
    The remote whose branches are the published lines. Defaults to origin.

.PARAMETER WaitMinutes
    How long to wait for a queued or in-progress ci run. Defaults to 60 (ci's own timeout
    is 45).

.PARAMETER PollSeconds
    How often to re-check while waiting. Defaults to 30.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Tag,

    [Parameter(Mandatory)]
    [string] $ReleaseRoot,

    [Parameter(Mandatory)]
    [string] $Repository,

    [string] $Remote = 'origin',

    [int] $WaitMinutes = 60,

    [int] $PollSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$lines = @('main', 'dev/v18')

function Fail([string] $title, [string] $message) {
    Write-Host "::error title=Release tag - ${title}::$message"
    exit 1
}

# Git as a function, so every call runs against the release checkout and a failure can be
# told apart from empty output.
function Invoke-Git {
    $output = & git -C $ReleaseRoot @args 2>$null
    return [pscustomobject]@{ Ok = ($LASTEXITCODE -eq 0); Output = $output }
}

# The package version, found exactly as Assert-LineParity.ps1 finds it: the FIRST element
# that occupies a whole line.
function Get-DeclaredVersion([string] $ref) {
    $shown = Invoke-Git show "${ref}:Directory.Build.props"
    if (-not $shown.Ok) {
        return $null
    }

    foreach ($line in $shown.Output) {
        if ($line -match '^\s*<Version>([^<]*)</Version>\s*$') {
            return $Matches[1].Trim()
        }
    }

    return $null
}

# 1. Shape.
if ($Tag -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
    Fail 'shape' "'$Tag' is not a release tag. A release tag is <major>.<minor>.<patch>, with no prefix or suffix (not v17.2.2, not 17.2.2-rc1)."
}
$major = $Matches[1]

$resolved = Invoke-Git rev-parse --verify --quiet "refs/tags/$Tag^{commit}"
if (-not $resolved.Ok -or -not $resolved.Output) {
    Fail 'not found' "The tag '$Tag' does not exist in this clone, so there is no commit to release."
}
$sha = "$($resolved.Output)".Trim()
Write-Host "Tag $Tag names $sha."

# 2. Declared version.
$declared = Get-DeclaredVersion $sha
if ($null -eq $declared) {
    Fail 'version' "Directory.Build.props at $sha has no whole-line <Version>, so the version the tag claims cannot be confirmed."
}
if ($declared -ne $Tag) {
    Fail 'version' "The tag is $Tag, but $sha declares <Version>$declared</Version>. Tag the commit that declares $Tag, or bump the version first."
}
Write-Host "Declared version at ${sha}: $declared - matches."

# 3. Line.
$candidates = @()
foreach ($line in $lines) {
    $ref = "$Remote/$line"
    $tipVersion = Get-DeclaredVersion $ref
    if ($null -eq $tipVersion) {
        # Not a skip. A line that cannot be read has not been shown to be the wrong one.
        Fail 'line' "The version at the tip of $ref could not be read, so the line for major $major cannot be decided. The release checkout needs fetch-depth: 0."
    }

    $tipMajor = ($tipVersion -split '\.')[0]
    Write-Host "$ref declares $tipVersion (major $tipMajor)."
    if ($tipMajor -eq $major) {
        $candidates += $line
    }
}

if ($candidates.Count -eq 0) {
    Fail 'line' "No published line ($($lines -join ', ')) declares major $major, so $Tag belongs to no line."
}
if ($candidates.Count -gt 1) {
    Fail 'line' "More than one published line declares major ${major} ($($candidates -join ', ')), so the line for $Tag is ambiguous."
}
$line = $candidates[0]

& git -C $ReleaseRoot merge-base --is-ancestor $sha "$Remote/$line" 2>$null
switch ($LASTEXITCODE) {
    0 { Write-Host "$sha is contained in $Remote/$line." }
    1 { Fail 'line' "A $major.x tag must be on $line, but $sha is not contained in $Remote/$line. Tag the merge commit on $line." }
    default { Fail 'line' "git merge-base failed while checking whether $sha is in $Remote/$line." }
}

# 4. Verified.
$headers = @{ Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
if ($env:GITHUB_TOKEN) {
    $headers.Authorization = "Bearer $env:GITHUB_TOKEN"
}
$runsUri = "https://api.github.com/repos/$Repository/actions/workflows/ci.yml/runs?head_sha=$sha&event=push&per_page=100"

$deadline = (Get-Date).AddMinutes($WaitMinutes)
while ($true) {
    try {
        $response = Invoke-RestMethod -Uri $runsUri -Headers $headers
    }
    catch {
        Fail 'verified' "The ci runs for $sha could not be read from the GitHub API: $($_.Exception.Message)"
    }

    $runs = @($response.workflow_runs | Where-Object { $_.head_branch -eq $line })
    $succeeded = @($runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -eq 'success' })
    if ($succeeded.Count -gt 0) {
        Write-Host "ci succeeded for a push of $sha to ${line}: $($succeeded[0].html_url)"
        break
    }

    $pending = @($runs | Where-Object { $_.status -ne 'completed' })
    if ($pending.Count -eq 0) {
        if ($runs.Count -eq 0) {
            Fail 'verified' "ci has no run for a push of $sha to $line, so this commit has not been verified on its line."
        }

        $described = ($runs | ForEach-Object { "$($_.conclusion) $($_.html_url)" }) -join '; '
        Fail 'verified' "ci did not succeed for a push of $sha to ${line}: $described. Re-run it, and once it is green, re-run this workflow."
    }

    if ((Get-Date) -ge $deadline) {
        Fail 'verified' "ci for $sha on $line was still $($pending[0].status) after $WaitMinutes minutes: $($pending[0].html_url). Re-run this workflow once it has finished."
    }

    Write-Host "ci for $sha on $line is $($pending[0].status); checking again in $PollSeconds s."
    Start-Sleep -Seconds $PollSeconds
}

if ($env:GITHUB_OUTPUT) {
    "sha=$sha" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "line=$line" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

Write-Host "Release tag $Tag passes: version $declared, line $line, commit $sha verified."
exit 0
