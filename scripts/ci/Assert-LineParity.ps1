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

# A file as it stands at a ref, decoded as UTF-8, or $null when git cannot show it.
#
# Read through its own process rather than as PowerShell native output, which is decoded with
# [Console]::OutputEncoding. Without a console (a test host, some runners) that is a legacy code page,
# and a UTF-8 file's bytes arrive as other characters. Setting [Console]::OutputEncoding is not the
# fix: on Windows it changes the code page of the whole console the process shares, so it leaks
# into the terminal or runner that started this script, and into every process attached to it
# (QA round 1 found it doing exactly that between parallel tests). The reader strips a leading
# UTF-8 byte-order mark, which the SDK accepts in global.json.
function Get-GitFile([string] $ref, [string] $path) {
    $info = [System.Diagnostics.ProcessStartInfo]::new('git')
    $info.ArgumentList.Add('show')
    $info.ArgumentList.Add("${ref}:$path")
    $info.WorkingDirectory = (Get-Location).ProviderPath
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)

    $process = [System.Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [void] $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            return $null
        }
        return $stdout.GetAwaiter().GetResult()
    }
    finally {
        $process.Dispose()
    }
}

# Directory.Build.props, minus the one line that is allowed to differ.
function Get-PropsWithoutVersion([string] $ref) {
    $file = Get-GitFile $ref 'Directory.Build.props'
    if ($null -eq $file) {
        return $null
    }
    $text = $file -split '\r?\n'

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

# A JSON value written out canonically: every object's keys in ordinal order, arrays in their own
# order, and scalars exactly as written. Two files that say the same thing serialise the same way
# however they are laid out. Built on System.Text.Json rather than ConvertFrom-Json, because a
# PowerShell hashtable folds keys that differ only in case into one, and keeps only the last of a
# duplicated key: two files that differ could then compare equal. Here a duplicated key, or two
# keys differing only in case, is refused outright: which one the SDK would read is not something
# this check should guess. $skip names one property path to leave out ('$.sdk.version').
function ConvertTo-CanonicalJson([System.Text.Json.JsonElement] $element, [string] $path, [string] $skip) {
    switch ($element.ValueKind) {
        'Object' {
            $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            $byName = @{}
            $names = [System.Collections.Generic.List[string]]::new()
            foreach ($property in $element.EnumerateObject()) {
                if (-not $seen.Add($property.Name)) {
                    throw "the key '$($property.Name)' appears more than once at $path (two keys that differ only in case count as the same key)"
                }
                if ("$path.$($property.Name)" -ceq $skip) {
                    continue
                }
                $names.Add($property.Name)
                $byName[$property.Name] = $property.Value
            }

            $sorted = $names.ToArray()
            [System.Array]::Sort($sorted, [System.StringComparer]::Ordinal)
            $members = foreach ($name in $sorted) {
                '"' + [System.Text.Json.JsonEncodedText]::Encode($name).ToString() + '":' +
                    (ConvertTo-CanonicalJson $byName[$name] "$path.$name" $skip)
            }
            return '{' + (@($members) -join ',') + '}'
        }
        'Array' {
            $index = 0
            $items = foreach ($item in $element.EnumerateArray()) {
                ConvertTo-CanonicalJson $item "$path[$index]" $skip
                $index++
            }
            return '[' + (@($items) -join ',') + ']'
        }
        default {
            return $element.GetRawText()
        }
    }
}

# global.json at a ref, canonical and minus sdk.version: the one value allowed to differ. The
# exemption covers the version's VALUE, not its presence: a line whose global.json pins no SDK
# version at all is a failure, not a difference that happens to be exempt (QA round 1). Returns
# Canonical, or Problem saying why it could not be compared.
function Get-ComparableGlobalJson([string] $ref) {
    $joined = Get-GitFile $ref 'global.json'
    if ($null -eq $joined) {
        return [pscustomobject]@{ Canonical = $null; Problem = 'it could not be read' }
    }
    $options = [System.Text.Json.JsonDocumentOptions]@{
        CommentHandling     = [System.Text.Json.JsonCommentHandling]::Skip
        AllowTrailingCommas = $true
    }

    try {
        $document = [System.Text.Json.JsonDocument]::Parse($joined, $options)
    }
    catch {
        return [pscustomobject]@{ Canonical = $null; Problem = "it is not valid JSON ($($_.Exception.Message))" }
    }

    try {
        $root = $document.RootElement
        if ($root.ValueKind -ne 'Object') {
            return [pscustomobject]@{ Canonical = $null; Problem = 'it is not a JSON object' }
        }

        try {
            $canonical = ConvertTo-CanonicalJson $root '$' '$.sdk.version'
        }
        catch {
            return [pscustomobject]@{ Canonical = $null; Problem = "$($_.Exception.Message)" }
        }

        $sdk = [System.Text.Json.JsonElement]::new()
        $version = [System.Text.Json.JsonElement]::new()
        if (-not $root.TryGetProperty('sdk', [ref] $sdk) -or $sdk.ValueKind -ne 'Object' -or
            -not $sdk.TryGetProperty('version', [ref] $version) -or $version.ValueKind -ne 'String' -or
            [string]::IsNullOrWhiteSpace($version.GetString())) {
            return [pscustomobject]@{ Canonical = $null; Problem = 'it pins no SDK version (sdk.version must be a non-empty string; only its value may differ between lines)' }
        }

        return [pscustomobject]@{ Canonical = $canonical; Problem = $null }
    }
    finally {
        $document.Dispose()
    }
}

$ourProps = Get-PropsWithoutVersion 'HEAD'
if ($null -eq $ourProps) {
    Write-Host "::error title=Line parity::Directory.Build.props could not be read at this commit, so parity could not be checked."
    exit 1
}
$ourGlobal = Get-ComparableGlobalJson 'HEAD'
if ($ourGlobal.Problem) {
    Write-Host "::error title=Line parity,file=global.json::global.json at this commit cannot be compared: $($ourGlobal.Problem)."
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

    $theirGlobal = Get-ComparableGlobalJson $otherRef
    if ($theirGlobal.Problem) {
        Write-Host "::error title=Line parity,file=global.json::global.json at $otherRef cannot be compared: $($theirGlobal.Problem)."
        $failures++
    }
    elseif ($ourGlobal.Canonical -cne $theirGlobal.Canonical) {
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
