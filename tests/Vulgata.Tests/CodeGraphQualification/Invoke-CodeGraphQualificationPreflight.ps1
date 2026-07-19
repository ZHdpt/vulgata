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

function Assert-LockedQualificationFile {
    param(
        [Parameter(Mandatory = $true)][string] $Root,
        [Parameter(Mandatory = $true)][string] $RelativePath,
        [Parameter(Mandatory = $true)][ValidatePattern('^[a-f0-9]{64}$')][string] $ExpectedSha256
    )

    $path = Join-Path $Root $RelativePath
    $actualSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $ExpectedSha256) {
        throw "Locked qualification input drifted: $RelativePath"
    }
}

function Assert-Attempt0003Absent {
    param([Parameter(Mandatory = $true)][string] $Root)

    $attemptDirectory = Join-Path $Root 'specs/001-codegraph-extraction/qualification/attempts/attempt-0003'
    foreach ($record in @('authorized.json', 'started.json', 'terminal.json')) {
        if (Test-Path -LiteralPath (Join-Path $attemptDirectory $record)) {
            throw "attempt-0003 is no longer unclaimed: $record"
        }
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
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0001/started.json'
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0001/terminal.json'
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0002/authorized.json'
        'specs/001-codegraph-extraction/reviews/plan-review-10.json'
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

    $lockedFiles = [ordered]@{
        'specs/001-codegraph-extraction/qualification/attempts/authorized.schema.json' =
            'd06441f82a23c1c2e7aa48d890ae877885f743038539f8156cdb8086ee4e8749'
        'specs/001-codegraph-extraction/qualification/attempts/started.schema.json' =
            '954b73b0144e89fc540378031ff4aaa0523783654599d071d8549b036df718b7'
        'specs/001-codegraph-extraction/qualification/attempts/terminal.schema.json' =
            '867c34eae80ca81d1c418e423d6ade77427620bf2982b6b7c2cc4fbdc034378d'
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0001/started.json' =
            '6dd9b695926b16add679b0518d2a0879f3c2b839a351ae9aacd539ad101f13fb'
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0001/terminal.json' =
            'd1c78c33610e7011b4c616b6fc70c3483d3794825ab46fa9a48c5e6f3bc2c4cc'
        'specs/001-codegraph-extraction/qualification/attempts/attempt-0002/authorized.json' =
            '65eb6c190414b253ce651b7688a68a6d94663e9909a73c8c0bcd757f2910daaa'
    }
    foreach ($lockedFile in $lockedFiles.GetEnumerator()) {
        Assert-LockedQualificationFile `
            -Root $root `
            -RelativePath $lockedFile.Key `
            -ExpectedSha256 $lockedFile.Value
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
    if ($workflowState.plan_revision.attempt_0003_planning_approval_observed_at -ne
        '2026-07-17T00:10:00+08:00') {
        throw 'The recorded attempt-0003 corrective-planning approval is absent or mismatched.'
    }
    $planReviewPath = Join-Path $root 'specs/001-codegraph-extraction/reviews/plan-review-10.json'
    if ((Get-FileHash -LiteralPath $planReviewPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
        '753a6e82cb1ca6d1f4ebf580eb36a4101917301447876e88660df694df6bfe90') {
        throw 'The accepted plan-review identity does not match.'
    }
    Assert-Attempt0003Absent -Root $root

    . (Join-Path $PSScriptRoot 'Invoke-CodeGraphQualificationProvider.ps1')
    $attempt0002Directory = Join-Path $root 'specs/001-codegraph-extraction/qualification/attempts/attempt-0002'
    if ((Test-Path -LiteralPath (Join-Path $attempt0002Directory 'started.json')) -or
        (Test-Path -LiteralPath (Join-Path $attempt0002Directory 'terminal.json'))) {
        throw 'Consumed attempt-0002 history was mutated after its authorization-boundary failure.'
    }
    $reconciliationProbeRoot = Join-Path ([System.IO.Path]::GetTempPath()) "vulgata-attempt-0002-reconcile-$([Guid]::NewGuid().ToString('N'))"
    try {
        [System.IO.Directory]::CreateDirectory($reconciliationProbeRoot) | Out-Null
        Copy-Item `
            -LiteralPath (Join-Path $attempt0002Directory 'authorized.json') `
            -Destination (Join-Path $reconciliationProbeRoot 'authorized.json')
        $attempt0002Terminal = Invoke-QualificationAttemptReconciliation `
            -AttemptDirectory $reconciliationProbeRoot `
            -AttemptId 'attempt-0002'
        if ($null -eq $attempt0002Terminal -or
            $attempt0002Terminal.outcome -ne 'failed' -or
            $attempt0002Terminal.category -ne 'interrupted-before-claim' -or
            (Test-Path -LiteralPath (Join-Path $reconciliationProbeRoot 'started.json')) -or
            -not (Test-Path -LiteralPath (Join-Path $reconciliationProbeRoot 'terminal.json'))) {
            throw 'The provider-unreachable reconciliation probe did not classify interrupted-before-claim.'
        }
    }
    finally {
        Remove-Item -LiteralPath $reconciliationProbeRoot -Recurse -Force -ErrorAction SilentlyContinue
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

    Assert-Attempt0003Absent -Root $root
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
