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
Write-Host 'PASS: captured exit/output; child-only environment; owned process timeout; settlement check required.'
