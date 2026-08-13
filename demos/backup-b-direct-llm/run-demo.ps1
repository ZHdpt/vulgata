#!/usr/bin/env pwsh

param(
    [switch]$Deterministic
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if ($Deterministic) {
    $env:VULGATA_BACKUP_B_FORCE_DETERMINISTIC = "1"
} else {
    Remove-Item Env:VULGATA_BACKUP_B_FORCE_DETERMINISTIC -ErrorAction SilentlyContinue

    $requiredVariables = @(
        "VULGATA_BACKUP_B_WORKER_MODEL",
        "VULGATA_BACKUP_B_WORKER_ENDPOINT",
        "VULGATA_BACKUP_B_WORKER_API_KEY"
    )
    $missingVariables = $requiredVariables.Where({
        [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
    })
    if ($missingVariables.Count -gt 0) {
        throw "Real-provider mode requires: $($missingVariables -join ', '). Use -Deterministic only for an explicitly labelled deterministic verification run."
    }
}

Push-Location $repositoryRoot
try {
    dotnet run --project demos\backup-b-direct-llm\BackupBDemo\BackupBDemo.csproj
    if ($LASTEXITCODE -ne 0) {
        throw "Backup B demo exited with code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}
