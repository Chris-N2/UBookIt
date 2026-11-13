<#
.SYNOPSIS
    Reads the list of published lines: the one place that names them.

.DESCRIPTION
    Dot-sourced by Assert-LineParity.ps1 and Assert-ReleaseTag.ps1, and run by audit.yml to build
    its matrix. Every check that depends on which lines are published takes them from
    published-lines.json beside this file, so adding or dropping a line is one edit there.

    ci.yml cannot read a file in a static filter, so it matches published lines by pattern
    (main, and dev/v followed by the major). A unit test holds every listed name to that pattern,
    so a line can never be listed that the workflows do not treat as published.

    A malformed list is refused, not read as far as it goes: a check run against part of the
    list would report the lines agree when it never looked at some of them.

    Dot-sourcing this file defines the function and nothing else. In particular it sets no
    strict mode on its caller: strict mode is set inside the function, where it applies only to
    the function.
#>

function Read-PublishedLines {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    Set-StrictMode -Version Latest

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "The published-lines list '$Path' does not exist."
    }

    try {
        # -NoEnumerate: a one-element array must stay an array, so its shape can be checked.
        $parsed = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -NoEnumerate
    }
    catch {
        throw "The published-lines list '$Path' is not valid JSON: $($_.Exception.Message)"
    }

    if ($parsed -isnot [array]) {
        throw "The published-lines list '$Path' must be a JSON array of branch names."
    }
    if ($parsed.Count -eq 0) {
        throw "The published-lines list '$Path' is empty, so there would be no published line to check."
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($entry in $parsed) {
        if ($entry -isnot [string] -or [string]::IsNullOrWhiteSpace($entry)) {
            throw "The published-lines list '$Path' must contain only non-empty branch names; found '$entry'."
        }
        if (-not $seen.Add($entry)) {
            throw "The published-lines list '$Path' names '$entry' more than once."
        }
    }

    # The comma keeps a one-line list an array for the caller.
    return , [string[]] $parsed
}
