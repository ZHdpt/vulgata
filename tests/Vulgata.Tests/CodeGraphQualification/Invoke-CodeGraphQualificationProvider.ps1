[CmdletBinding()]
param(
    [ValidatePattern('^attempt-0003$')][string] $AttemptId,
    [string] $RepositoryRoot,
    [string] $BoundaryPath = 'specs/001-codegraph-extraction/qualification/execution-boundaries/attempt-0003.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-LowerSha256Text([string] $Value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    try { return [Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($bytes)) }
    finally { [Array]::Clear($bytes, 0, $bytes.Length) }
}

function Get-LowerSha256Bytes([byte[]] $Bytes) {
    return [Convert]::ToHexStringLower([System.Security.Cryptography.SHA256]::HashData($Bytes))
}

function Read-QualificationExecutionBoundary {
    param(
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $BoundaryPath
    )

    $root = [System.IO.Path]::GetFullPath($RepositoryRoot)
    $absolutePath = [System.IO.Path]::GetFullPath((Join-Path $root $BoundaryPath))
    if (-not $absolutePath.StartsWith(
        $root + [System.IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The qualification execution boundary path escapes the repository root.'
    }

    $bytes = [System.IO.File]::ReadAllBytes($absolutePath)
    try {
        $utf8 = [System.Text.UTF8Encoding]::new($false, $true)
        $json = $utf8.GetString($bytes)
        $boundary = $json | ConvertFrom-Json
        $expectedProperties = @(
            'schemaVersion', 'attemptId', 'exactCommand', 'correctivePlanCommit',
            'planReviewPath', 'planReviewSha256', 'executionApprovalGateId',
            'providerFactIdentity', 'providerCategory', 'noRetry', 'qualificationScopeId'
        )
        $actualProperties = @($boundary.PSObject.Properties.Name)
        if (@(Compare-Object $expectedProperties $actualProperties).Count -ne 0) {
            throw 'The qualification execution boundary schema is not closed.'
        }
        if ($boundary.schemaVersion -ne '1.0' -or
            $boundary.attemptId -ne 'attempt-0003' -or
            [string]::IsNullOrWhiteSpace($boundary.exactCommand) -or
            $boundary.correctivePlanCommit -notmatch '^[a-f0-9]{40}$' -or
            [string]::IsNullOrWhiteSpace($boundary.planReviewPath) -or
            $boundary.planReviewSha256 -notmatch '^[a-f0-9]{64}$' -or
            [string]::IsNullOrWhiteSpace($boundary.executionApprovalGateId) -or
            [string]::IsNullOrWhiteSpace($boundary.providerFactIdentity) -or
            $boundary.providerCategory -ne 'CodeGraphQualificationProvider' -or
            $boundary.noRetry -ne $true -or
            [string]::IsNullOrWhiteSpace($boundary.qualificationScopeId)) {
            throw 'The qualification execution boundary is invalid.'
        }
        [pscustomobject][ordered]@{
            value = $boundary
            sha256 = Get-LowerSha256Bytes $bytes
            relativePath = $BoundaryPath.Replace('\', '/')
        }
    }
    finally {
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

function Test-QualificationTerminal {
    param(
        [Parameter(Mandatory = $true)][string] $TerminalPath,
        [Parameter(Mandatory = $true)][ValidatePattern('^attempt-[0-9]{4}$')][string] $AttemptId
    )

    try {
        $json = [System.IO.File]::ReadAllText($TerminalPath, [System.Text.UTF8Encoding]::new($false, $true))
        $document = [System.Text.Json.JsonDocument]::Parse($json)
        try {
            if ($document.RootElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) {
                throw 'The terminal root is not an object.'
            }
            $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($property in $document.RootElement.EnumerateObject()) {
                if (-not $names.Add($property.Name)) {
                    throw 'The terminal contains a duplicate property.'
                }
            }
        }
        finally {
            $document.Dispose()
        }
        $terminal = $json | ConvertFrom-Json
        $expectedProperties = @(
            'schemaVersion', 'attemptId', 'completedAt', 'outcome', 'category', 'evidenceCitation'
        )
        if (@(Compare-Object $expectedProperties @($terminal.PSObject.Properties.Name)).Count -ne 0 -or
            $terminal.schemaVersion -ne '1.0' -or
            $terminal.attemptId -ne $AttemptId -or
            $terminal.outcome -notin @('passed', 'failed') -or
            [string]::IsNullOrWhiteSpace($terminal.category) -or
            -not $terminal.evidenceCitation.StartsWith(
                'specs/001-codegraph-extraction/qualification/evidence/',
                [StringComparison]::Ordinal)) {
            throw 'The terminal schema is invalid.'
        }
        $completedAt = [DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse(
            [string]$terminal.completedAt,
            [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::RoundtripKind,
            [ref]$completedAt)) {
            throw 'The terminal completion timestamp is invalid.'
        }
        return $terminal
    }
    catch {
        throw [InvalidOperationException]::new(
            'The qualification attempt terminal is ledger corruption.',
            $_.Exception)
    }
}

function Publish-QualificationTerminal {
    param(
        [Parameter(Mandatory = $true)][string] $AttemptDirectory,
        [Parameter(Mandatory = $true)][ValidatePattern('^attempt-[0-9]{4}$')][string] $AttemptId,
        [Parameter(Mandatory = $true)][string] $Category
    )

    $terminalPath = Join-Path $AttemptDirectory 'terminal.json'
    if (Test-Path -LiteralPath $terminalPath) {
        return Test-QualificationTerminal -TerminalPath $terminalPath -AttemptId $AttemptId
    }

    $temporaryPath = Join-Path $AttemptDirectory ".terminal.$([Guid]::NewGuid().ToString('N')).tmp"
    try {
        $terminal = [ordered]@{
            schemaVersion = '1.0'
            attemptId = $AttemptId
            completedAt = [DateTimeOffset]::UtcNow
            outcome = 'failed'
            category = $Category
            evidenceCitation = 'specs/001-codegraph-extraction/qualification/evidence/verification.md'
        }
        $bytes = [System.Text.Encoding]::UTF8.GetBytes(($terminal | ConvertTo-Json -Depth 4))
        try {
            $stream = [System.IO.FileStream]::new(
                $temporaryPath,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None,
                4096,
                [System.IO.FileOptions]::WriteThrough)
            try {
                $stream.Write($bytes, 0, $bytes.Length)
                $stream.Flush($true)
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            [Array]::Clear($bytes, 0, $bytes.Length)
        }
        $null = Test-QualificationTerminal -TerminalPath $temporaryPath -AttemptId $AttemptId
        try {
            [System.IO.File]::Move($temporaryPath, $terminalPath, $false)
            return Test-QualificationTerminal -TerminalPath $terminalPath -AttemptId $AttemptId
        }
        catch [System.IO.IOException] {
            if (-not (Test-Path -LiteralPath $terminalPath)) {
                throw
            }
            Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
            return Test-QualificationTerminal -TerminalPath $terminalPath -AttemptId $AttemptId
        }
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-QualificationAttemptReconciliation {
    param(
        [Parameter(Mandatory = $true)][string] $AttemptDirectory,
        [Parameter(Mandatory = $true)][ValidatePattern('^attempt-[0-9]{4}$')][string] $AttemptId
    )

    $authorizationPath = Join-Path $AttemptDirectory 'authorized.json'
    $terminalPath = Join-Path $AttemptDirectory 'terminal.json'
    if (-not (Test-Path -LiteralPath $authorizationPath)) {
        if (Test-Path -LiteralPath $terminalPath) {
            throw 'The qualification attempt terminal is ledger corruption.'
        }
        return $null
    }
    $authorizationLock = $null
    try {
        try {
            $authorizationLock = [System.IO.FileStream]::new(
                $authorizationPath,
                [System.IO.FileMode]::Open,
                [System.IO.FileAccess]::Read,
                [System.IO.FileShare]::None)
        }
        catch [System.IO.IOException] {
            return $null
        }
        if (Test-Path -LiteralPath $terminalPath) {
            return Test-QualificationTerminal -TerminalPath $terminalPath -AttemptId $AttemptId
        }
        $category = if (Test-Path -LiteralPath (Join-Path $AttemptDirectory 'started.json')) {
            'interrupted-after-claim'
        }
        else {
            'interrupted-before-claim'
        }
        return Publish-QualificationTerminal `
            -AttemptDirectory $AttemptDirectory `
            -AttemptId $AttemptId `
            -Category $category
    }
    finally {
        if ($null -ne $authorizationLock) {
            $authorizationLock.Dispose()
        }
    }
}

function Invoke-CodeGraphQualificationProvider {
    param(
        [Parameter(Mandatory = $true)][ValidatePattern('^attempt-0003$')][string] $AttemptId,
        [Parameter(Mandatory = $true)][string] $RepositoryRoot,
        [Parameter(Mandatory = $true)][string] $BoundaryPath,
        [Parameter(Mandatory = $true)][scriptblock] $CommandExecutor
    )

    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $loadedBoundary = Read-QualificationExecutionBoundary -RepositoryRoot $root -BoundaryPath $BoundaryPath
    $boundary = $loadedBoundary.value
    if ($AttemptId -ne $boundary.attemptId) {
        throw 'The requested attempt does not match the execution boundary.'
    }

    $workflowState = Get-Content -Raw -LiteralPath (Join-Path $root 'specs/001-codegraph-extraction/workflow-state.json') |
        ConvertFrom-Json
    if ($workflowState.plan_revision.attempt_0003_execution_approval_gate_id -ne
            $boundary.executionApprovalGateId -or
        $workflowState.plan_revision.attempt_0003_execution_boundary_sha256 -ne
            $loadedBoundary.sha256) {
        throw 'The recorded attempt-0003 execution approval does not bind the raw boundary.'
    }
    $planReviewPath = Join-Path $root $boundary.planReviewPath
    if ((Get-FileHash -LiteralPath $planReviewPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne
        $boundary.planReviewSha256) {
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
            schemaVersion = '1.0'
            attemptId = $AttemptId
            tokenSha256 = Get-LowerSha256Text $token
            expiresAt = [DateTimeOffset]::UtcNow.AddMinutes(5)
            executionBoundaryPath = $loadedBoundary.relativePath
            executionBoundarySha256 = $loadedBoundary.sha256
        }
        $authorizationBytes = [System.Text.Encoding]::UTF8.GetBytes(
            ($authorization | ConvertTo-Json -Depth 4))
        try {
            $stream = [System.IO.FileStream]::new(
                (Join-Path $attemptDirectory 'authorized.json'),
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None,
                4096,
                [System.IO.FileOptions]::WriteThrough)
            try {
                $stream.Write($authorizationBytes, 0, $authorizationBytes.Length)
                $stream.Flush($true)
            }
            finally {
                $stream.Dispose()
            }
        }
        finally {
            [Array]::Clear($authorizationBytes, 0, $authorizationBytes.Length)
        }

        $providerFilter = "FullyQualifiedName=$($boundary.providerFactIdentity)&Category=$($boundary.providerCategory)"
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new('dotnet')
        $startInfo.UseShellExecute = $false
        $startInfo.WorkingDirectory = $root
        foreach ($argument in @(
            'test',
            'tests/Vulgata.Tests/Vulgata.Tests.csproj',
            '--filter',
            $providerFilter
        )) {
            $startInfo.ArgumentList.Add($argument)
        }
        $startInfo.Environment['VULGATA_QUALIFICATION_ATTEMPT_ID'] = $AttemptId
        $startInfo.Environment['VULGATA_QUALIFICATION_TOKEN'] = $token
        $startInfo.Environment['VULGATA_QUALIFICATION_EXACT_COMMAND'] = $boundary.exactCommand
        $startInfo.Environment['VULGATA_QUALIFICATION_BOUNDARY_PATH'] = $loadedBoundary.relativePath
        $startInfo.Environment['VULGATA_QUALIFICATION_BOUNDARY_SHA256'] = $loadedBoundary.sha256
        $startInfo.Environment['VULGATA_QUALIFICATION_REPOSITORY_ROOT'] = $root
        $exitCode = & $CommandExecutor $startInfo
        $terminalPath = Join-Path $attemptDirectory 'terminal.json'
        $fallbackPublished = $false
        if (Test-Path -LiteralPath $terminalPath) {
            $null = Test-QualificationTerminal -TerminalPath $terminalPath -AttemptId $AttemptId
        }
        else {
            $category = if (Test-Path -LiteralPath (Join-Path $attemptDirectory 'started.json')) {
                'child-exited-without-terminal'
            }
            else {
                'interrupted-before-claim'
            }
            $null = Publish-QualificationTerminal `
                -AttemptDirectory $attemptDirectory `
                -AttemptId $AttemptId `
                -Category $category
            $fallbackPublished = $true
        }
        if ($exitCode -ne 0) {
            throw "The exact provider child failed with exit code $exitCode."
        }
        if ($fallbackPublished) {
            throw 'The exact provider child exited without a detailed terminal record.'
        }
    }
    finally {
        [Array]::Clear($tokenBytes, 0, $tokenBytes.Length)
        Remove-Variable token -ErrorAction SilentlyContinue
    }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}
if ([string]::IsNullOrWhiteSpace($AttemptId)) {
    throw 'AttemptId is required.'
}
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
}
$realExecutor = {
    param([System.Diagnostics.ProcessStartInfo] $StartInfo)
    $child = [System.Diagnostics.Process]::Start($StartInfo)
    if ($null -eq $child) {
        throw 'The exact provider child did not start.'
    }
    try {
        $child.WaitForExit()
        $child.ExitCode
    }
    finally {
        $child.Dispose()
    }
}
Invoke-CodeGraphQualificationProvider `
    -AttemptId $AttemptId `
    -RepositoryRoot $RepositoryRoot `
    -BoundaryPath $BoundaryPath `
    -CommandExecutor $realExecutor
