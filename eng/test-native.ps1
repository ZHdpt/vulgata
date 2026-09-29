function Invoke-TestNative {
    param([string]$File, [string[]]$Arguments, [int]$TimeoutSeconds = 120, [string]$Log, [switch]$SensitiveOutput)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = (Get-Command $File -ErrorAction Stop).Source
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (!$process.Start()) { throw "Could not start $File." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = !$process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) {
            # This Process instance was started above; never enumerate or kill other processes.
            $process.Kill($true)
            $null = $process.WaitForExit(5000)
        }
        $drained = [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr), 5000)
        $out = if ($stdout.IsCompletedSuccessfully) { $stdout.Result } else { '[stdout did not close]' }
        $err = if ($stderr.IsCompletedSuccessfully) { $stderr.Result } else { '[stderr did not close]' }
        if ($SensitiveOutput) { $out = '[sensitive command output withheld]'; $err = '[sensitive command diagnostics withheld]' }
        if ($Log) { [IO.File]::WriteAllText($Log, $out + $err) }
        if ($timedOut -or !$drained) { throw "Owned $File invocation timed out after $TimeoutSeconds seconds; see $Log." }
        return [pscustomobject]@{ ExitCode=$process.ExitCode; Output=$out.Trim(); Error=$err.Trim() }
    } finally { $process.Dispose() }
}

function Invoke-TestChecked {
    param([string]$File, [string[]]$Arguments, [int]$TimeoutSeconds = 120, [string]$Log, [switch]$SensitiveOutput)
    $result = Invoke-TestNative $File $Arguments $TimeoutSeconds $Log -SensitiveOutput:$SensitiveOutput
    if ($result.ExitCode -ne 0) { throw "$File failed (exit=$($result.ExitCode)); see $Log. $($result.Error)" }
    return $result.Output
}

function Invoke-TestCleanup {
    param([scriptblock]$Cleanup, [hashtable]$Environment)
    try { & $Cleanup }
    finally {
        foreach ($name in $Environment.Keys) { [Environment]::SetEnvironmentVariable($name, $Environment[$name]) }
    }
}
