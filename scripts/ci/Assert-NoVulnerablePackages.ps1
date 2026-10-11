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

    Suppressions and packability come from MSBuild's OWN EVALUATION of each project
    (`dotnet msbuild <project> -getProperty:IsPackable -getItem:NuGetAuditSuppress`), so
    conditions, `<Choose>`, document order (the last IsPackable wins) and imports such as
    Directory.Build.targets mean here what they mean to the build, and an item inside a
    `<Target>` - which no evaluation sees - is no suppression here either.

    That replaced reading the project file as XML, which QA showed could ACCEPT an advisory
    MSBuild would not (round 1: a later IsPackable=true, a <Choose> condition, an item in a
    <Target>, an override from an import). An earlier version of this paragraph called that
    reading fail-closed. It was not: a second, weaker model of MSBuild disagrees with MSBuild in
    both directions.

    Asking MSBuild is not enough on its own: it has to be asked UNDER THE CONDITIONS THAT DECIDE
    (QA round 2). So each project is evaluated twice, as Debug (what restore and `dotnet list`
    see) and as Release (what publish.yml packs). It counts as non-packable only if it is
    non-packable under both, and an advisory counts as suppressed only if it is suppressed under
    both. A suppression declared under one but not the other FAILS outright (QA round 3: left
    alone it was judged by nothing, neither accepted nor ever found stale). And NuGet reads suppressions per TARGET FRAMEWORK, from inner evaluations this script
    does not make. Rather than model that too, a project that targets more than one framework
    may not suppress anything: its suppressions FAIL and accept nothing. Each rule refuses where
    this script and the thing that decides could disagree, rather than guessing which way.

    A project MSBuild cannot evaluate FAILS the audit and accepts nothing.

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

# The configurations each project is evaluated in: Debug, which this audit's restore and
# `dotnet list` use (audit.yml); Release, which ci.yml builds and tests in and publish.yml packs.
# HARD-CODED: a workflow that starts restoring, building or packing in another configuration must
# add it here too.
$configurations = @('Debug', 'Release')

# What MSBuild's evaluation of a project says (see the header for why it must be MSBuild's).
# Starts packable with no suppressions, so anything that goes wrong below accepts nothing.
function Get-ProjectPolicy([string] $path) {
    $policy = [pscustomobject]@{
        Packable      = $true
        MultiTargeted = $false
        # Suppressed under EVERY configuration evaluated: only these can accept anything.
        Suppressions  = [System.Collections.Generic.List[string]]::new()
        # Suppressed under ANY configuration: what a packable or multi-targeted project is
        # refused for, so that a suppression cannot hide from the refusal in one configuration.
        Declared      = [System.Collections.Generic.List[string]]::new()
        # Suppressed under SOME configurations but not all: refused outright (QA round 3). Left
        # alone, such a suppression silences one configuration's build warning while nothing
        # here ever judges it - not accepted, so never found stale either.
        Partial       = [System.Collections.Generic.List[string]]::new()
        Error         = $null
    }

    # Debug is what this audit's own restore and `dotnet list` evaluate (audit.yml); Release is
    # what ci.yml builds and tests and publish.yml packs. A project counts as non-packable only
    # if it is non-packable under BOTH, and an advisory counts as suppressed only if it is
    # suppressed under BOTH - QA round 2 showed a project non-packable in Debug alone being let
    # accept an advisory it then shipped. The list is $configurations, at script scope below the
    # report checks.
    $evaluations = @()
    foreach ($configuration in $configurations) {
        $output = dotnet msbuild $path -nologo "-p:Configuration=$configuration" `
            -getProperty:IsPackable -getProperty:TargetFrameworks -getItem:NuGetAuditSuppress 2>&1
        if ($LASTEXITCODE -ne 0) {
            $policy.Error = "MSBuild could not evaluate it as $configuration (exit $LASTEXITCODE): $(($output | Select-Object -First 3) -join ' ')"
            return $policy
        }

        try {
            $evaluations += ,(($output -join "`n") | ConvertFrom-Json)
        }
        catch {
            $policy.Error = "MSBuild's $configuration evaluation was not the JSON expected: $(($output | Select-Object -First 3) -join ' ')"
            return $policy
        }
    }

    $policy.Packable = @($evaluations | Where-Object { -not ("$($_.Properties.IsPackable)".Trim() -ieq 'false') }).Count -gt 0
    $policy.MultiTargeted = @($evaluations | Where-Object { "$($_.Properties.TargetFrameworks)".Trim() -ne '' }).Count -gt 0

    $sets = @(foreach ($evaluation in $evaluations) {
        $set = @{}
        foreach ($item in @($evaluation.Items.NuGetAuditSuppress)) {
            $set[(ConvertTo-AdvisoryKey $item.Identity)] = $item.Identity
        }
        ,$set
    })

    $union = @{}
    foreach ($set in $sets) { foreach ($key in $set.Keys) { $union[$key] = $set[$key] } }

    foreach ($key in $union.Keys) {
        $policy.Declared.Add($union[$key])
        $in = @(for ($i = 0; $i -lt $sets.Count; $i++) { if ($sets[$i].ContainsKey($key)) { $configurations[$i] } })
        if ($in.Count -eq $sets.Count) {
            $policy.Suppressions.Add($union[$key])
        }
        else {
            $policy.Partial.Add("$($union[$key]) (declared in $($in -join ', ') only)")
        }
    }

    return $policy
}

$failures = [System.Collections.Generic.List[string]]::new()
$accepted = [System.Collections.Generic.List[string]]::new()

foreach ($project in $projects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($project.path)
    $policy = Get-ProjectPolicy $project.path

    if ($policy.Error) {
        $failures.Add("$name ($($project.path)): $($policy.Error) Nothing in it can be accepted.")
    }

    foreach ($partial in $policy.Partial) {
        $failures.Add("$name suppresses $partial. This audit accepts only a suppression declared in every configuration it evaluates ($($configurations -join ', ')) - declare it unconditionally or remove it.")
    }

    if ($policy.Packable -and $policy.Declared.Count -gt 0) {
        $failures.Add("$name is packable and suppresses $($policy.Declared -join ', '). A package uBookIt ships never accepts an advisory - remove the suppression and fix the dependency.")
    }

    # NuGet reads suppressions per target framework (the inner evaluations); this reads the outer
    # one. Rather than model that, a multi-targeted project may not suppress at all (QA round 2).
    if ($policy.MultiTargeted -and $policy.Declared.Count -gt 0) {
        $failures.Add("$name targets more than one framework and suppresses $($policy.Declared -join ', '). This audit does not judge per-framework suppressions, so it accepts none - suppress in a single-targeted project, or fix the dependency.")
    }

    # Only suppressions MSBuild evaluates under every configuration, for a project that is
    # non-packable under every configuration and targets one framework, can accept anything.
    $accepts = @{}
    if (-not $policy.Packable -and -not $policy.MultiTargeted) {
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
