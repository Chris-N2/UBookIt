<#
.SYNOPSIS
    Fails unless every test project in the repository ran, and nothing was skipped.

.DESCRIPTION
    `dotnet test` reports a skipped test as a success and a project that never ran as nothing
    at all. Both are ways for a run to go green having tested less than it claims — and this
    repository has had a suite silently stop running for three days (0e79a03, 415 tests). This
    script reads the TRX files a run wrote and fails when:

      1. an expected test project has no TRX file;
      2. a project's TRX executed zero tests, or executed fewer tests than it contains;
      3. any individual result is anything other than Passed or Failed (a skip is NotExecuted).

    It also REPORTS failed tests - one annotation per project, each test's name and message
    (collapsed to one line, truncated) - without counting them: `dotnet test` has already failed the run for them, but
    its log is downloadable only by a signed-in repository admin, and annotations are public.

    WHICH PROJECTS ARE EXPECTED — the rule, derived from the checkout rather than listed:
    every *.csproj under tests/ that references a TEST FRAMEWORK: the VSTest SDK
    (Microsoft.NET.Test.Sdk), the Microsoft Testing Platform (Microsoft.Testing.Platform*), or
    an xunit, MSTest, NUnit or TUnit package, or that uses the MSTest.Sdk project SDK. A project
    is a test project because it can host tests, so a new one is expected the moment it exists
    and nobody has to remember to add it here. UBookIt.Tests.ThemeFixture is excluded by that
    rule, not by name: it is a Razor class library compiled INTO the rendering tests and has no
    tests of its own, so it references no test framework. If it ever gained one, it would start
    being expected - which is the right answer.

    WHY NOT THE VSTEST SDK ALONE (it used to be): QA pointed out that a project on the Microsoft
    Testing Platform - the path xunit v3 recommends, and how MSTest.Sdk and TUnit work - has no
    Microsoft.NET.Test.Sdk, is not run by a VSTest-mode `dotnet test`, and would therefore be
    neither run NOR expected: a whole suite silently absent from a green run, the exact shape of
    0e79a03. Expecting it means such a project fails the run, loudly, until CI is taught to run it.

    KNOWN EDGES, both deliberate or out of reach (QA round 2):
      - It errs LOUD. A tests/ helper library referencing only, say, xunit.assert is expected and
        then fails with "produced no test results". That is the safe direction; such a helper
        should live outside tests/ or be named in this header as an exception.
      - A test framework supplied centrally by a GlobalPackageReference in
        Directory.Packages.props is not visible in the csproj, so this rule cannot see it. The
        repository uses none; adding one means extending this rule.

    WHY THE SUMMARY COUNTERS ARE NOT TRUSTED — measured, not assumed: a TRX from a run in
    which xunit skipped 183 tests reports <Counters notExecuted="0"> and an overall outcome of
    "Completed". The skips are visible only as per-result outcome="NotExecuted", and as
    `executed` falling short of `total`. A check reading notExecuted could never fire, so this
    one reads every result, and compares executed against total as a second, independent
    signal.

    A TRX is matched to its project by the assembly its tests were loaded from (the `storage`
    of each UnitTest definition), compared case-insensitively because the test host writes it
    lower-cased on Windows.

.PARAMETER ResultsDirectory
    The directory `dotnet test --logger trx --results-directory` wrote into.

.PARAMETER RepositoryRoot
    The checkout to derive the expected projects from. Defaults to two levels above this script.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $ResultsDirectory,

    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

# Workflow-command data escaping: GitHub reads `%`, CR and LF in an annotation's message as
# encoding and line breaks, so a raw one truncates or garbles it. Found in QA: a multi-line skip
# reason was cut off at its first line.
function ConvertTo-AnnotationData([string] $text) {
    return $text.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
}

function Add-Failure([string] $message) {
    $failures.Add($message)
    if ($env:GITHUB_ACTIONS -eq 'true') {
        # A workflow annotation, so the cause is on the run's summary page rather than
        # only in a log someone has to open.
        Write-Host "::error title=Test results::$(ConvertTo-AnnotationData $message)"
    }
    else {
        Write-Host "FAIL: $message"
    }
}

# --- The expected set -------------------------------------------------------------------

$testsRoot = Join-Path $RepositoryRoot 'tests'
$expected = @{}

foreach ($project in Get-ChildItem -Path $testsRoot -Filter '*.csproj' -Recurse -File) {
    [xml] $xml = Get-Content -LiteralPath $project.FullName -Raw

    $testFrameworkPattern = '^(Microsoft\.NET\.Test\.Sdk|Microsoft\.Testing\.Platform(\..+)?|xunit(\..+)?|MSTest(\..+)?|NUnit(\..+)?|TUnit(\..+)?)$'
    $packages = @($xml.SelectNodes('//PackageReference') | ForEach-Object { $_.GetAttribute('Include') })
    $projectSdk = $xml.DocumentElement.GetAttribute('Sdk')
    # The MSTest SDK in either form: the Project Sdk attribute, or an <Sdk Name="MSTest.Sdk"/> element.
    $sdkElements = @($xml.SelectNodes('//Sdk') | ForEach-Object { $_.GetAttribute('Name') })
    $referencesTestFramework = @($packages | Where-Object { $_ -match $testFrameworkPattern }).Count -gt 0 `
        -or $projectSdk -match '(^|;)\s*MSTest\.Sdk' `
        -or @($sdkElements | Where-Object { $_ -eq 'MSTest.Sdk' }).Count -gt 0
    if (-not $referencesTestFramework) {
        Write-Host "not expected (references no test framework): $($project.Name)"
        continue
    }

    $assemblyNode = $xml.SelectSingleNode('//AssemblyName')
    $assembly = if ($null -ne $assemblyNode -and $assemblyNode.InnerText.Trim()) { $assemblyNode.InnerText.Trim() } else { $project.BaseName }

    $expected[$assembly.ToLowerInvariant()] = $project.BaseName
}

# Non-vacuity: a discovery that stopped finding projects would expect nothing and pass.
if ($expected.Count -eq 0) {
    Add-Failure "No test projects were discovered under '$testsRoot'; the check has nothing to hold the run to."
}

Write-Host "expected to report: $(($expected.Values | Sort-Object) -join ', ')"

# --- What the run wrote -----------------------------------------------------------------

$trxFiles = @(Get-ChildItem -Path $ResultsDirectory -Filter '*.trx' -Recurse -File -ErrorAction SilentlyContinue)
$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$seen = @{}

foreach ($trx in $trxFiles) {
    [xml] $doc = Get-Content -LiteralPath $trx.FullName -Raw

    $assemblies = @(
        Select-Xml -Xml $doc -XPath '//t:TestDefinitions/t:UnitTest' -Namespace $ns |
            ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Node.storage).ToLowerInvariant() } |
            Sort-Object -Unique
    )

    if ($assemblies.Count -ne 1) {
        Add-Failure "$($trx.Name) names $($assemblies.Count) test assemblies ($($assemblies -join ', ')); expected exactly one, so it cannot be attributed to a project."
        continue
    }

    $assembly = $assemblies[0]
    $name = if ($expected.ContainsKey($assembly)) { $expected[$assembly] } else { $assembly }

    if ($seen.ContainsKey($assembly)) {
        Add-Failure "$name reported in more than one TRX ($($seen[$assembly]) and $($trx.Name)); a run should produce one each."
        continue
    }
    $seen[$assembly] = $trx.Name

    $counters = (Select-Xml -Xml $doc -XPath '//t:ResultSummary/t:Counters' -Namespace $ns).Node
    $total = [int] $counters.total
    $executed = [int] $counters.executed

    if ($executed -eq 0) {
        Add-Failure "$name executed no tests."
    }
    elseif ($executed -ne $total) {
        Add-Failure "$name executed $executed of $total tests."
    }

    $failedTests = [System.Collections.Generic.List[string]]::new()

    foreach ($hit in Select-Xml -Xml $doc -XPath '//t:Results/t:UnitTestResult' -Namespace $ns) {
        $result = $hit.Node
        if ($result.outcome -eq 'Passed') {
            continue
        }

        if ($result.outcome -eq 'Failed') {
            # Not counted here - `dotnet test` has already failed the run for it - but REPORTED:
            # the job log needs a signed-in admin to download, while annotations are public,
            # so this is how a failure's cause is readable by anyone looking at the run.
            $failedMessage = Select-Xml -Xml $result -XPath 't:Output/t:ErrorInfo/t:Message' -Namespace $ns
            # The whole message on one line, not its first line: a fixture failure's first line
            # is only "Collection fixture type … threw", and the cause is on the next.
            $oneLine = if ($null -ne $failedMessage) { $failedMessage.Node.InnerText.Trim() -replace '\s+', ' ' } else { '(no message)' }
            if ($oneLine.Length -gt 400) { $oneLine = $oneLine.Substring(0, 400) + '…' }
            $failedTests.Add("$($result.testName): $oneLine")
            continue
        }

        $messageNode = Select-Xml -Xml $result -XPath 't:Output/t:ErrorInfo/t:Message' -Namespace $ns
        $reason = if ($null -ne $messageNode) { $messageNode.Node.InnerText.Trim() } else { '(no reason recorded)' }

        Add-Failure "$name`: $($result.testName) was $($result.outcome) - $reason"
    }

    if ($failedTests.Count -gt 0) {
        # ONE annotation per project, because GitHub keeps at most ten error annotations per
        # step; a line each would silently drop all but the first ten failures. %0A is the
        # workflow-command encoding of a newline.
        # Each entry escaped BEFORE joining, so the joining %0A is the only line break GitHub sees.
        $listing = ($failedTests | Select-Object -First 40 | ForEach-Object { ConvertTo-AnnotationData $_ }) -join '%0A'
        $more = if ($failedTests.Count -gt 40) { "%0A… and $($failedTests.Count - 40) more" } else { '' }
        if ($env:GITHUB_ACTIONS -eq 'true') {
            Write-Host "::error title=$name - $($failedTests.Count) failed test(s)::$listing$more"
        }
        else {
            Write-Host "failed in $name ($($failedTests.Count)):"
            $failedTests | ForEach-Object { Write-Host "  $_" }
        }
    }

    if ($expected.ContainsKey($assembly)) {
        Write-Host "reported: $name - $executed of $total executed"
    }
    else {
        Write-Host "reported, not expected by the rule: $name - $executed of $total executed"
    }
}

foreach ($assembly in $expected.Keys) {
    if (-not $seen.ContainsKey($assembly)) {
        Add-Failure "$($expected[$assembly]) produced no test results. It was not built, was filtered out, or failed to start."
    }
}

if ($failures.Count -gt 0) {
    Write-Host "$($failures.Count) problem(s): the run did not demonstrably run every suite."
    exit 1
}

Write-Host "Every expected test project reported, and nothing was skipped."
exit 0
