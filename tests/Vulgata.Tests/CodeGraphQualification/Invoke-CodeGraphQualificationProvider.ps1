[CmdletBinding()]
param(
    [ValidatePattern('^attempt-0002$')][string] $AttemptId,
    [string] $RepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LowerSha256Text([string] $Value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    try { return [Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($bytes)) }
    finally { [Array]::Clear($bytes, 0, $bytes.Length) }
}

function Invoke-CodeGraphQualificationProvider {
    param(
        [Parameter(Mandatory = $true)][ValidatePattern('^attempt-0002$')][string] $AttemptId,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][scriptblock] $CommandExecutor
    )

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $correctivePlanCommit = '84b0909b6713b410258d4db7ec37503cfe0c5980'
    $planReviewRelativePath = 'specs/001-codegraph-extraction/reviews/plan-review-8.json'
    $planReviewSha256 = 'f73f877b3c7d37b3c739ee123867e39e3b71769e4abd1d8bbe5bd2f51806b408'
    $approvalObservedAt = '2026-07-16T18:10:00+08:00'
    $approvalSummary = 'User explicitly approved the independently accepted corrective plan and one guarded attempt-0002. The approval permits task regeneration now and provider execution only after fresh compilation, provider-incapable synthetic tests, reachability-free preflight, legacy-fixture revocation, sealed lease issuance, and an atomic durable attempt claim are all satisfied. No retry is authorized.'
    $exactWrapperCommand = 'pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0002'
    $providerFilter = 'FullyQualifiedName=Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests.Attempt0002_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync&Category=CodeGraphQualificationProvider'

    $workflowState = Get-Content -Raw -LiteralPath (Join-Path $root 'specs/001-codegraph-extraction/workflow-state.json') | ConvertFrom-Json
    if ($workflowState.plan_revision.attempt_0002_approval_observed_at -ne $approvalObservedAt -or
        $workflowState.plan_revision.attempt_0002_approval_summary -ne $approvalSummary) {
        throw 'The recorded attempt-0002 approval does not match the accepted execution boundary.'
    }
    $planReviewPath = Join-Path $root $planReviewRelativePath
    if ((Get-FileHash -LiteralPath $planReviewPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $planReviewSha256) {
        throw 'The accepted plan-review identity does not match.'
    }

    $attemptDirectory = Join-Path $root "specs/001-codegraph-extraction/qualification/attempts/$AttemptId"
    foreach ($record in @('authorized.json', 'started.json', 'terminal.json')) {
        if (Test-Path -LiteralPath (Join-Path $attemptDirectory $record)) {
            throw 'The approved attempt has already been authorized, claimed, or completed.'
        }
    }
    [System.IO.Directory]::CreateDirectory($attemptDirectory) | Out-Null
    $tokenBytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    try {
        $token = [Convert]::ToHexStringLower($tokenBytes)
        $authorization = [ordered]@{
            schemaVersion = '1.0'; attemptId = $AttemptId; tokenSha256 = Get-LowerSha256Text $token
            expiresAt = [DateTimeOffset]::UtcNow.AddMinutes(5); exactCommand = $exactWrapperCommand
            correctivePlanCommit = $correctivePlanCommit; planReviewPath = $planReviewRelativePath
            planReviewSha256 = $planReviewSha256; workflowApprovalObservedAt = $approvalObservedAt
            workflowApprovalSummarySha256 = Get-LowerSha256Text $approvalSummary
        }
        $authorizationBytes = [System.Text.Encoding]::UTF8.GetBytes(($authorization | ConvertTo-Json -Depth 4))
        try {
            $stream = [System.IO.FileStream]::new(
                (Join-Path $attemptDirectory 'authorized.json'), [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write, [System.IO.FileShare]::None, 4096,
                [System.IO.FileOptions]::WriteThrough)
            try { $stream.Write($authorizationBytes, 0, $authorizationBytes.Length); $stream.Flush($true) }
            finally { $stream.Dispose() }
        }
        finally { [Array]::Clear($authorizationBytes, 0, $authorizationBytes.Length) }

        $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
        $startInfo.UseShellExecute = $false
        $startInfo.WorkingDirectory = $root
        foreach ($argument in @('test', 'tests/Vulgata.Tests/Vulgata.Tests.csproj', '--filter', $providerFilter)) {
            $startInfo.ArgumentList.Add($argument)
        }
        $startInfo.Environment['VULGATA_QUALIFICATION_ATTEMPT_ID'] = $AttemptId
        $startInfo.Environment['VULGATA_QUALIFICATION_TOKEN'] = $token
        $startInfo.Environment['VULGATA_QUALIFICATION_EXACT_COMMAND'] = $exactWrapperCommand
        $exitCode = & $CommandExecutor $startInfo
        if ($exitCode -ne 0) { throw "The exact provider child failed with exit code $exitCode." }
    }
    finally {
        [Array]::Clear($tokenBytes, 0, $tokenBytes.Length)
        Remove-Variable token -ErrorAction SilentlyContinue
    }
}

if ($MyInvocation.InvocationName -eq '.') { return }
if ([string]::IsNullOrWhiteSpace($AttemptId)) { throw 'AttemptId is required.' }
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
}
$realExecutor = {
    param([System.Diagnostics.ProcessStartInfo] $StartInfo)
    $child = [System.Diagnostics.Process]::Start($StartInfo)
    if ($null -eq $child) { throw 'The exact provider child did not start.' }
    try { $child.WaitForExit(); $child.ExitCode }
    finally { $child.Dispose() }
}
Invoke-CodeGraphQualificationProvider -AttemptId $AttemptId -RepositoryRoot $RepositoryRoot -CommandExecutor $realExecutor
