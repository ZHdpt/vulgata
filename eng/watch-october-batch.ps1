# Runtime helper: caller supplies local bindings; never put secrets in Arguments.
function Invoke-OctoberBatchMonitored {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$File,
        [string[]]$Arguments = @(),
        [hashtable]$Environment = @{},
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 1800,
        [ValidateRange(1, 60)][int]$UpdateSeconds = 15
    )
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command $File -ErrorAction Stop).Source
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($name in $Environment.Keys) {
        if ($null -eq $Environment[$name]) { $null = $start.Environment.Remove($name) }
        else { $start.Environment[$name] = [string]$Environment[$name] }
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $started = $false
    $clock = [Diagnostics.Stopwatch]::new()
    $timedOut = $false
    $nextUpdate = 0.0
    try {
        $started = $process.Start()
        if (!$started) { throw 'Owned batch process did not start.' }
        $clock.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        while (!$process.HasExited) {
            $elapsed = $clock.Elapsed.TotalSeconds
            if ($elapsed -ge $TimeoutSeconds) {
                $timedOut = $true
                # Only this process, created above, and its descendants. Never Docker/database cleanup.
                $process.Kill($true)
                if (!$process.WaitForExit(5000)) { throw 'Owned batch termination unconfirmed; preserve run resources.' }
                break
            }
            if ($elapsed -ge $nextUpdate) {
                $percent = [Math]::Min(99, [int](100 * $elapsed / $TimeoutSeconds))
                $remaining = [Math]::Max(0, [int]($TimeoutSeconds - $elapsed))
                Write-Progress -Id 74 -Activity 'October batch: elapsed deadline (not work completion)' -Status "Running; elapsed $([int]$elapsed)s; remaining ${remaining}s; work completion unknown" -PercentComplete $percent
                Write-Host "Batch running: $([int]$elapsed)s elapsed / ${TimeoutSeconds}s deadline; ${remaining}s remaining."
                $nextUpdate = $elapsed + $UpdateSeconds
            }
            $null = $process.WaitForExit([Math]::Max(1, [Math]::Min(250, [int](1000 * ($TimeoutSeconds - $elapsed)))))
        }
        $drained = [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            TimedOut = $timedOut
            ElapsedSeconds = [Math]::Round($clock.Elapsed.TotalSeconds, 2)
            NeedsSettlementCheck = $timedOut -or !$drained
            Output = if ($stdout.IsCompletedSuccessfully) { $stdout.Result } else { '' }
            Error = if ($stderr.IsCompletedSuccessfully) { $stderr.Result } else { '' }
        }
    }
    finally {
        if ($started -and !$process.HasExited) {
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
        }
        Write-Progress -Id 74 -Activity 'October batch' -Completed
        $process.Dispose()
    }
}
