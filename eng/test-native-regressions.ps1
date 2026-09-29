$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'test-native.ps1')
. (Join-Path $PSScriptRoot 'test-postgres.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('vulgata-native-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
try {
    foreach ($suite in @('SelfContained','LocalTools','HistoricalBoundary')) {
        $rejected=$false
        try { Assert-TestResourceOptions $suite $true $null } catch { $rejected=$true }
        if (!$rejected) { throw "Unsupported resource combination accepted: $suite" }
    }
    $plain=Join-Path $root 'plain.sql'
    Set-Content $plain 'SELECT 1;'
    $rejected=$false
    try { Assert-TestResourceOptions 'External' $true $plain } catch { $rejected=$true }
    if (!$rejected) { throw 'Plain SQL seed was accepted.' }
    $pidFile=Join-Path $root 'child.pid'
    $child="[IO.File]::WriteAllText('$($pidFile.Replace("'","''"))', [string]`$PID); Start-Sleep -Seconds 30"
    $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($child))
    $parent="& pwsh -NoProfile -EncodedCommand $encoded"
    $timedOut=$false
    try { $null=Invoke-TestNative pwsh @('-NoProfile','-Command',$parent) 3 (Join-Path $root 'timeout.log') }
    catch { if ($_ -notmatch 'timed out') { throw }; $timedOut=$true }
    if (!$timedOut -or !(Test-Path $pidFile)) { throw 'Timeout/child-process probe did not reach its sentinel.' }
    $childId=[int](Get-Content $pidFile)
    if (Get-Process -Id $childId -ErrorAction SilentlyContinue) { throw 'Owned timed-out process tree survived.' }
    $redacted=Join-Path $root 'redacted.log'
    $result=Invoke-TestNative pwsh @('-NoProfile','-Command',"Write-Output 'seed-secret-sentinel'; exit 4") 10 $redacted -SensitiveOutput
    if ($result.ExitCode -ne 4 -or (Get-Content $redacted -Raw) -match 'seed-secret-sentinel') { throw 'Sensitive invocation was not redacted.' }
    $prior=[Environment]::GetEnvironmentVariable('VULGATA_TEST_CLEANUP_SENTINEL')
    try {
        $env:VULGATA_TEST_CLEANUP_SENTINEL='changed'
        $failed=$false
        try { Invoke-TestCleanup -Environment @{ VULGATA_TEST_CLEANUP_SENTINEL='restored' } -Cleanup { throw 'controlled cleanup failure' } }
        catch { if ($_ -notmatch 'controlled cleanup failure') { throw }; $failed=$true }
        if (!$failed -or $env:VULGATA_TEST_CLEANUP_SENTINEL -ne 'restored') { throw 'Cleanup failure bypassed environment restoration.' }
    } finally { [Environment]::SetEnvironmentVariable('VULGATA_TEST_CLEANUP_SENTINEL',$prior) }
    Write-Host 'PASS: unsupported scopes/plain seed rejected; owned timeout tree stopped; sensitive output redacted'
    Write-Host 'PASS: cleanup failure propagates after restoring saved environment'
} finally {
    if ([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
