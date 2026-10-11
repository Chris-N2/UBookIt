<#
.SYNOPSIS
    Fails when any package in a solution's graph, direct or transitive, has a known advisory,
    unless a non-packable project has accepted that exact advisory for itself.

.DESCRIPTION
    The verification build deliberately keeps NuGet advisories (NU1901-NU1904) as warnings —
    see Directory.Build.props — because an advisory is published against a package, not
    introduced by a commit. This is where they fail instead: .github/workflows/audit.yml runs
    it for each published line on a schedule and on demand.

    It reads `dotnet list package --vulnerable --include-transitive --format json` rather
    than the human-readable table, because that command EXITS 0 WHEN IT FINDS
    VULNERABILITIES: its exit code says nothing, and a text parse is one reworded heading
    away from reporting nothing. The target must already be restored.

    THE ONE EXCEPTION (continuous-integration spec, "Vulnerability advisories are audited
    separately from the build"). A project that is NOT packable may declare an advisory as
    `<NuGetAuditSuppress Include="<advisory url>" />`, for an advisory no reachable version can
    fix yet. That silences the build warning — and `dotnet list` still reports the finding
    (measured 2026-10-11), so this script judges the suppression rather than obeying it:

      - a finding is ACCEPTED only when its own project declares that advisory; it is reported
        as a notice and counted, never passed over silently;
      - a suppression in a project that matches no finding there FAILS as stale, so an
        exception cannot outlive its reason;
      - a suppression in a packable project FAILS, matched or not: a shipped package never
        accepts an advisory.

    Suppressions and packability are read from each project's OWN file as XML, not evaluated
    by MSBuild. That is deliberate, and it errs in one direction only. A suppression declared
    anywhere else (an import, Directory.Build.props) is invisible here, so its finding stays
    unaccepted and the audit FAILS; a project counts as non-packable only when its own file
    says `<IsPackable>false</IsPackable>` unconditionally, so anything unclear counts as
    packable and its suppressions FAIL. A conditional suppression fails outright, because
    whether it applies is an evaluation question this script does not answer. It can refuse an
    exception it cannot see; it can never accept one.

.PARAMETER Target
    The solution or project to audit. Named in every message.

.PARAMETER Report
    Optional. A file holding the JSON `dotnet list package --vulnerable --include-transitive
    --format json` would print. When given it is read instead of running the command — for
    the tests (tests/UBookIt.Tests/AuditScriptTests.cs), never for the workflow.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Target,

    [string] $Report
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Report) {
    $raw = Get-Content -LiteralPath $Report -Raw
}
else {
    $raw = dotnet list $Target package --vulnerable --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        Write-Host "::error title=Audit::dotnet list package failed for $Target (exit $LASTEXITCODE). The audit was not made."
        $raw | Write-Host
        exit 1
    }
}

# NOT `$report`: PowerShell names are case-insensitive, so that IS the [string] -Report parameter,
# and assigning the parsed object to it coerces it to a string. Every property then reads empty and
# the audit fails "no vulnerability source consulted" — which is how this was found.
$parsed = ($raw -join "`n") | ConvertFrom-Json

# Non-vacuity, part one: advisory data must have been consulted. Vulnerability data comes from
# nuget.org's feed; a report whose sources do not include it has checked the graph against
# nothing, and a vulnerable package then looks exactly like a clean one (its project entry simply
# has no `frameworks` key). Measured in QA: with a nuget.config holding only a local folder
# source, a project on System.Text.Json 8.0.0 reported "No known advisories" and exited 0. One
# config change (a mirror, or <clear/>) would make this audit green and meaningless.
$vulnerabilitySource = 'https://api.nuget.org/v3/index.json'
$sources = @($parsed.PSObject.Properties['sources'] ? $parsed.sources : @())
if ($sources -notcontains $vulnerabilitySource) {
    Write-Host "::error title=Audit::The report for $Target consulted no source that provides vulnerability data (sources: $($sources -join ', ')). Expected $vulnerabilitySource, so nothing was audited."
    exit 1
}

# Non-vacuity, part two: a report that names no projects has audited nothing.
$projects = @($parsed.PSObject.Properties['projects'] ? $parsed.projects : @())
if ($projects.Count -eq 0) {
    Write-Host "::error title=Audit::The report for $Target names no projects, so nothing was audited."
    exit 1
}

# One normal form for an advisory URL, so case and a trailing slash cannot make a real match look
# stale. Nothing more lenient than that: a different URL is a different advisory.
function ConvertTo-AdvisoryKey([string] $url) {
    return $url.Trim().TrimEnd('/').ToLowerInvariant()
}

# What a project's own file says, read as XML (see the header for why, and which way it errs).
function Get-ProjectPolicy([string] $path) {
    $policy = [pscustomobject]@{
        Packable     = $true
        Suppressions = [System.Collections.Generic.List[string]]::new()
        Conditional  = [System.Collections.Generic.List[string]]::new()
    }

    if (-not (Test-Path -LiteralPath $path)) {
        return $policy
    }

    [xml] $xml = Get-Content -LiteralPath $path -Raw

    foreach ($node in @($xml.SelectNodes("//*[local-name()='IsPackable']"))) {
        $conditional = $node.HasAttribute('Condition') -or $node.ParentNode.HasAttribute('Condition')
        if (-not $conditional -and $node.InnerText.Trim() -ieq 'false') {
            $policy.Packable = $false
        }
    }

    foreach ($node in @($xml.SelectNodes("//*[local-name()='NuGetAuditSuppress']"))) {
        $include = $node.GetAttribute('Include')
        if ($node.HasAttribute('Condition') -or $node.ParentNode.HasAttribute('Condition')) {
            $policy.Conditional.Add($include)
        }
        else {
            $policy.Suppressions.Add($include)
        }
    }

    return $policy
}

$failures = [System.Collections.Generic.List[string]]::new()
$accepted = [System.Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project.path)
    $policy = Get-ProjectPolicy $project.path

    foreach ($suppression in $policy.Conditional) {
        $failures.Add("$name declares a CONDITIONAL suppression of $suppression. Declare it unconditionally or not at all: this audit does not evaluate conditions, so it cannot tell whether it applies.")
    }

    if ($policy.Packable -and $policy.Suppressions.Count -gt 0) {
        $failures.Add("$name is packable and suppresses $($policy.Suppressions -join ', '). A package uBookIt ships never accepts an advisory - remove the suppression and fix the dependency.")
    }

    # Only a non-packable project's own, unconditional suppressions can accept anything.
    $accepts = @{}
    if (-not $policy.Packable) {
        foreach ($suppression in $policy.Suppressions) {
            $accepts[(ConvertTo-AdvisoryKey $suppression)] = $suppression
        }
    }
    $used = @{}

    $frameworks = if ($project.PSObject.Properties['frameworks']) { @($project.frameworks) } else { @() }

    foreach ($framework in $frameworks) {
        foreach ($kind in 'topLevelPackages', 'transitivePackages') {
            $packages = if ($framework.PSObject.Properties[$kind]) { @($framework.$kind) } else { @() }

            foreach ($package in $packages) {
                $vulnerabilities = if ($package.PSObject.Properties['vulnerabilities']) { @($package.vulnerabilities) } else { @() }

                foreach ($advisory in $vulnerabilities) {
                    $finding = "$name ($($framework.framework)): $($package.id) $($package.resolvedVersion) - $($advisory.severity) - $($advisory.advisoryurl)"
                    $key = ConvertTo-AdvisoryKey $advisory.advisoryurl

                    if ($accepts.ContainsKey($key)) {
                        $used[$key] = $true
                        $accepted.Add($finding)
                    }
                    else {
                        $failures.Add("Vulnerable package: $finding")
                    }
                }
            }
        }
    }

    foreach ($key in $accepts.Keys) {
        if (-not $used.ContainsKey($key)) {
            $failures.Add("$name suppresses $($accepts[$key]), but no such advisory is in its graph any more. The suppression is stale - remove it.")
        }
    }
}

$acceptedUnique = @($accepted | Sort-Object -Unique)
foreach ($finding in $acceptedUnique) {
    Write-Host "::notice title=Accepted advisory::$finding (suppressed by its own non-packable project)"
}

Write-Host "audited $($projects.Count) project(s) in $Target; $($acceptedUnique.Count) finding(s) accepted by a suppression"

if ($failures.Count -gt 0) {
    foreach ($failure in ($failures | Sort-Object -Unique)) {
        Write-Host "::error title=Audit::$failure"
    }

    exit 1
}

if ($acceptedUnique.Count -gt 0) {
    Write-Host "No unaccepted advisories in the graph."
}
else {
    Write-Host "No known advisories in the graph."
}
exit 0
