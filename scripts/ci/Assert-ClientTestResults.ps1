<#
.SYNOPSIS
    Fails unless the backoffice client's test suite ran, and nothing in it was skipped.

.DESCRIPTION
    The client counterpart to Assert-TestResults.ps1, which reads .NET TRX only. vitest exits 0
    with an `it.skip` or an `it.todo` in the suite, so before this script a skipped client test
    passed CI while the continuous-integration spec ("A skipped test fails the run") says ANY
    test. This reads the JSON report vitest writes (`--reporter=json`) and fails when:

      1. the report is missing, or is not the shape this script knows (fails CLOSED: a reporter
         that changed its format must not read as a clean run);
      2. the report ran zero tests;
      3. any test's status is anything other than passed or failed. vitest reports a skip as
         `skipped`, a todo as `todo`, and a test that never ran as `pending`; all three fail,
         each named with its file.

    Failed tests are not counted here: `npm test` has already failed the run for them.

    `.only` needs nothing here: vitest refuses it when CI is set (`allowOnly` defaults to false).

    A separate script rather than a second format folded into Assert-TestResults.ps1, whose
    contract is "every expected .NET project reported, by TRX". One suite, one report, one rule.

.PARAMETER Report
    The JSON file vitest wrote.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Report
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Report -PathType Leaf)) {
    Write-Host "::error title=Client tests::No vitest report at '$Report', so the client suite cannot be shown to have run."
    exit 1
}

try {
    $parsed = Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
}
catch {
    Write-Host "::error title=Client tests::'$Report' is not JSON: $($_.Exception.Message)"
    exit 1
}

# Fail closed on a shape this script does not know. Both members are read below, and a report
# lacking either would otherwise be judged on nothing.
foreach ($member in 'numTotalTests', 'testResults') {
    if (-not $parsed.PSObject.Properties[$member]) {
        Write-Host "::error title=Client tests::The vitest report has no '$member'. Its format is not the one this script reads (did vitest change it?), so nothing was checked."
        exit 1
    }
}

if ([int] $parsed.numTotalTests -le 0) {
    Write-Host "::error title=Client tests::The vitest report ran zero tests."
    exit 1
}

$offenders = [System.Collections.Generic.List[string]]::new()
$seen = 0

foreach ($file in @($parsed.testResults)) {
    if (-not $file.PSObject.Properties['assertionResults']) {
        Write-Host "::error title=Client tests::A file entry in the vitest report has no 'assertionResults', so its tests cannot be checked."
        exit 1
    }

    $name = [System.IO.Path]::GetFileName("$($file.name)")

    foreach ($test in @($file.assertionResults)) {
        $seen++
        $status = "$($test.status)"

        if ($status -ne 'passed' -and $status -ne 'failed') {
            $offenders.Add("$name > $($test.fullName) [$status]")
        }
    }
}

# Non-vacuity: the total and the entries must agree, or the loop above judged less than ran.
if ($seen -ne [int] $parsed.numTotalTests) {
    Write-Host "::error title=Client tests::The vitest report counts $($parsed.numTotalTests) test(s) but lists $seen, so the listing cannot be trusted to be complete."
    exit 1
}

if ($offenders.Count -gt 0) {
    foreach ($offender in $offenders) {
        Write-Host "::error title=Client test not run::$offender"
    }

    Write-Host "$($offenders.Count) of $seen client test(s) did not run. A skipped or todo test fails CI as a .NET one does."
    exit 1
}

Write-Host "All $seen client test(s) ran."
exit 0
