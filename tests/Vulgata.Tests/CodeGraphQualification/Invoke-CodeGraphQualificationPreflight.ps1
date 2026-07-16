[CmdletBinding()]
param(
    [string] $RepositoryRoot,
    [switch] $DescribeExecutionPlan
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-CodeGraphQualificationPreflightPlan {
    [ordered]@{
        providerReachabilityFree = $true
        externalExecutables = @('dotnet')
        commands = @(
            'dotnet build tests/Vulgata.Tests/Vulgata.Tests.csproj'
            'dotnet test tests/Vulgata.Tests/Vulgata.Tests.csproj --filter Category=CodeGraphQualificationSynthetic'
        )
    }
}

function Invoke-CodeGraphQualificationPreflight {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][scriptblock] $CommandExecutor
    )

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $requiredFiles = @(
        'docker/Dockerfile'
        'docker/codegraph/python-version.txt'
        'docker/codegraph/requirements.lock'
        'specs/001-codegraph-extraction/qualification/qualification-result.schema.json'
        'specs/001-codegraph-extraction/qualification/attempts/authorized.schema.json'
        'specs/001-codegraph-extraction/qualification/attempts/started.schema.json'
        'specs/001-codegraph-extraction/qualification/attempts/terminal.schema.json'
        'specs/001-codegraph-extraction/reviews/plan-review-8.json'
        'specs/001-codegraph-extraction/workflow-state.json'
        'tests/Vulgata.Tests/CodeGraphQualification/Fixtures/qualification-fixture-manifest.json'
    )
    foreach ($relativePath in $requiredFiles) {
        $path = Join-Path $root $relativePath
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Required preflight input is absent: $relativePath"
        }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.Length -ne 64) {
            throw "Required preflight input could not be hashed: $relativePath"
        }
    }

    $legacyFixture = Join-Path $root 'tests/Vulgata.Tests/CodeGraphQualification/Fixtures/QualificationRepository'
    if (Test-Path -LiteralPath $legacyFixture) {
        throw 'The revoked legacy provider fixture path exists.'
    }
    $fixtureRoot = Join-Path $root 'tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository'
    $manifestPath = Join-Path $root 'tests/Vulgata.Tests/CodeGraphQualification/Fixtures/qualification-fixture-manifest.json'
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ($manifest.sourceClassification -ne 'synthetic-non-secret') {
        throw 'The provider fixture manifest is not classified synthetic-non-secret.'
    }
    $fixturePaths = @($manifest.eligiblePaths) + @($manifest.filteredPaths | ForEach-Object { $_.path })
    foreach ($relativeSourcePath in $fixturePaths) {
        $sourcePath = Join-Path $fixtureRoot $relativeSourcePath
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Fixture source is absent: $relativeSourcePath"
        }
        if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.Length -ne 64) {
            throw "Fixture source could not be hashed: $relativeSourcePath"
        }
    }

    $workflowState = Get-Content -Raw -LiteralPath (Join-Path $root 'specs/001-codegraph-extraction/workflow-state.json') | ConvertFrom-Json
    if ($workflowState.plan_revision.attempt_0002_approval_observed_at -ne '2026-07-16T18:10:00+08:00' -or
        -not $workflowState.plan_revision.attempt_0002_approval_summary.StartsWith(
            'User explicitly approved the independently accepted corrective plan and one guarded attempt-0002.',
            [StringComparison]::Ordinal)) {
        throw 'The recorded attempt-0002 approval is absent or mismatched.'
    }
    $planReviewPath = Join-Path $root 'specs/001-codegraph-extraction/reviews/plan-review-8.json'
    if ((Get-FileHash -LiteralPath $planReviewPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
        'f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408') {
        throw 'The accepted plan-review identity does not match.'
    }
    $attemptDirectory = Join-Path $root 'specs/001-codegraph-extraction/qualification/attempts/attempt-0002'
    foreach ($record in @('authorized.json', 'started.json', 'terminal.json')) {
        if (Test-Path -LiteralPath (Join-Path $attemptDirectory $record)) {
            throw "attempt-0002 is no longer unclaimed: $record"
        }
    }

    $commands = @(
        ,@('build', 'tests/Vulgata.Tests/Vulgata.Tests.csproj')
        ,@('test', 'tests/Vulgata.Tests/Vulgata.Tests.csproj', '--filter', 'Category=CodeGraphQualificationSynthetic')
    )
    foreach ($arguments in $commands) {
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
        $startInfo.UseShellExecute = $false
        $startInfo.WorkingDirectory = $root
        foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }
        $exitCode = & $CommandExecutor $startInfo
        if ($exitCode -ne 0) {
            throw "Reachability-free command failed with exit code ${exitCode}: dotnet $($arguments -join ' ')"
        }
    }

    Get-CodeGraphQualificationPreflightPlan
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
}
if ($DescribeExecutionPlan) {
    Get-CodeGraphQualificationPreflightPlan | ConvertTo-Json -Compress
    return
}

$realExecutor = {
    param([System.Diagnostics.ProcessStartInfo] $StartInfo)
    $process = [System.Diagnostics.Process]::Start($StartInfo)
    if ($null -eq $process) { throw 'The reachability-free command did not start.' }
    try { $process.WaitForExit(); $process.ExitCode }
    finally { $process.Dispose() }
}
Invoke-CodeGraphQualificationPreflight -RepositoryRoot $RepositoryRoot -CommandExecutor $realExecutor | ConvertTo-Json -Compress
