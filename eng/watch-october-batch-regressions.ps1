$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'watch-october-batch.ps1')
$pwsh = (Get-Process -Id $PID).Path
$marker = [Guid]::NewGuid().ToString('N')
$before = [Environment]::GetEnvironmentVariable('VULGATA_MONITOR_PROBE', 'Process')
$result = Invoke-OctoberBatchMonitored -File $pwsh -Arguments @('-NoProfile', '-Command', 'Write-Output $env:VULGATA_MONITOR_PROBE; exit 7') -Environment @{ VULGATA_MONITOR_PROBE = $marker } -TimeoutSeconds 10
if ($result.ExitCode -ne 7 -or $result.TimedOut -or $result.NeedsSettlementCheck -or $result.Output.Trim() -ne $marker) { throw 'Normal completion/capture failed.' }
if ([Environment]::GetEnvironmentVariable('VULGATA_MONITOR_PROBE', 'Process') -ne $before) { throw 'Parent environment changed.' }
$timeout = Invoke-OctoberBatchMonitored -File $pwsh -Arguments @('-NoProfile', '-Command', 'Start-Sleep -Seconds 30') -TimeoutSeconds 1 -UpdateSeconds 1
if (!$timeout.TimedOut -or !$timeout.NeedsSettlementCheck -or $timeout.ElapsedSeconds -gt 12) { throw 'Owned timeout did not stop within bound.' }
$extended = @(Invoke-OctoberBatchMonitored -File $pwsh -Arguments @('-NoProfile', '-Command', 'exit 0') -RunDeadlineSeconds 14400 6>&1)
$finished = @($extended | Where-Object { $_.PSObject.Properties['ExitCode'] })
if ($finished.Count -ne 1 -or $finished[0].ExitCode -ne 0 -or $finished[0].TimedOut -or
    $finished[0].NeedsSettlementCheck -or !($extended | Where-Object { "$_" -match '/ 14460s deadline' })) {
    throw 'Configured run watchdog or default timeout binding failed.'
}
try {
    Invoke-OctoberBatchMonitored -File 'must-not-launch' -RunDeadlineSeconds 14400 -TimeoutSeconds 10
    throw 'Conflicting watcher modes were accepted.'
} catch {
    if ($_.Exception.Message -ne 'Supply either an application run deadline or a direct process timeout.') { throw }
}
foreach ($invalid in @(0, 86401)) {
    try {
        Invoke-OctoberBatchMonitored -File 'must-not-launch' -RunDeadlineSeconds $invalid
        throw 'Invalid run deadline was accepted.'
    } catch {
        if ($_.FullyQualifiedErrorId -notmatch 'ParameterArgumentValidationError') { throw }
    }
}
Write-Host 'PASS: captured exit/output; child-only environment; owned timeout; run-derived watchdog; conflicting/invalid modes rejected.'
