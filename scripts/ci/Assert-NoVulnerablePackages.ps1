<#
.SYNOPSIS
    Fails when any package in a solution's graph, direct or transitive, has a known advisory.

.DESCRIPTION
    The verification build deliberately keeps NuGet advisories (NU1901-NU1904) as warnings —
    see Directory.Build.props — because an advisory is published against a package, not
    introduced by a commit. This is where they fail instead: .github/workflows/audit.yml runs
    it for each published line on a schedule and on demand.

    It reads `dotnet list package --vulnerable --include-transitive --format json` rather
    than the human-readable table, because that command EXITS 0 WHEN IT FINDS
    VULNERABILITIES: its exit code says nothing, and a text parse is one reworded heading
    away from reporting nothing. The target must already be restored.

.PARAMETER Target
    The solution or project to audit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Target
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$raw = dotnet list $Target package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) {
    Write-Host "::error title=Audit::dotnet list package failed for $Target (exit $LASTEXITCODE). The audit was not made."
    $raw | Write-Host
    exit 1
}

$report = ($raw -join "`n") | ConvertFrom-Json

# Non-vacuity, part one: advisory data must have been consulted. Vulnerability data comes from
# nuget.org's feed; a report whose sources do not include it has checked the graph against
# nothing, and a vulnerable package then looks exactly like a clean one (its project entry simply
# has no `frameworks` key). Measured in QA: with a nuget.config holding only a local folder
# source, a project on System.Text.Json 8.0.0 reported "No known advisories" and exited 0. One
# config change (a mirror, or <clear/>) would make this audit green and meaningless.
$vulnerabilitySource = 'https://api.nuget.org/v3/index.json'
$sources = @($report.PSObject.Properties['sources'] ? $report.sources : @())
if ($sources -notcontains $vulnerabilitySource) {
    Write-Host "::error title=Audit::The report for $Target consulted no source that provides vulnerability data (sources: $($sources -join ', ')). Expected $vulnerabilitySource, so nothing was audited."
    exit 1
}

# Non-vacuity, part two: a report that names no projects has audited nothing.
$projects = @($report.projects)
if ($projects.Count -eq 0) {
    Write-Host "::error title=Audit::The report for $Target names no projects, so nothing was audited."
    exit 1
}

$findings = [System.Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    $frameworks = if ($project.PSObject.Properties['frameworks']) { @($project.frameworks) } else { @() }

    foreach ($framework in $frameworks) {
        foreach ($kind in 'topLevelPackages', 'transitivePackages') {
            $packages = if ($framework.PSObject.Properties[$kind]) { @($framework.$kind) } else { @() }

            foreach ($package in $packages) {
                foreach ($advisory in @($package.vulnerabilities)) {
                    $name = [System.IO.Path]::GetFileNameWithoutExtension($project.path)
                    $findings.Add("$name ($($framework.framework)): $($package.id) $($package.resolvedVersion) - $($advisory.severity) - $($advisory.advisoryurl)")
                }
            }
        }
    }
}

Write-Host "audited $($projects.Count) project(s) in $Target"

if ($findings.Count -gt 0) {
    foreach ($finding in ($findings | Sort-Object -Unique)) {
        Write-Host "::error title=Vulnerable package::$finding"
    }

    exit 1
}

Write-Host "No known advisories in the graph."
exit 0
