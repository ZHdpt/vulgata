function Assert-TestResourceOptions {
    param([string]$Suite, [bool]$WithPostgreSql, [string]$SeedDump)
    if ($WithPostgreSql -and $Suite -notin @('PostgreSql','External','E2E')) { throw '-WithPostgreSql requires PostgreSql, External or E2E.' }
    if ($SeedDump) {
        if ($Suite -ne 'PostgreSql' -and !$WithPostgreSql) { throw '-SeedDump requires an owned PostgreSQL suite resource.' }
        $stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $SeedDump -ErrorAction Stop).Path)
        try {
            $magic = [byte[]]::new(5)
            if ($stream.Read($magic,0,5) -ne 5 -or [Text.Encoding]::ASCII.GetString($magic) -ne 'PGDMP') {
                throw '-SeedDump requires a local pg_dump custom-format archive (pg_dump -Fc); plain SQL is not executed.'
            }
        } finally { $stream.Dispose() }
    }
}

function Remove-OwnedPostgres {
    param([string]$Container, [string]$Volume, [string]$Run, [string]$Results)
    $inspect = Invoke-TestNative docker @('inspect','--format','{"Label":"{{index .Config.Labels "vulgata.test.run"}}","Mounts":{{json .Mounts}}}',$Container) 30 (Join-Path $Results 'cleanup-inspect.log')
    if ($inspect.ExitCode -eq 0) {
        $state = @($inspect.Output | ConvertFrom-Json)[0]
        if ($state.Label -ne $Run) { throw 'Container ownership mismatch; cleanup refused.' }
        @($state.Mounts) | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $Results 'owned-mounts.json')
        $mounted = @($state.Mounts | Where-Object Type -eq 'volume' | ForEach-Object Name)
        if (@($mounted | Where-Object { $_ -ne $Volume }).Count) { throw 'Unexpected volume mount; cleanup requires ownership review.' }
        $null = Invoke-TestChecked docker @('rm','--force','--volumes',$Container) 30 (Join-Path $Results 'cleanup.log')
    } elseif ($inspect.Error -notmatch 'No such (object|container)') { throw 'Could not inspect owned container for cleanup.' }
    $volumeState = Invoke-TestNative docker @('volume','inspect',$Volume) 30 (Join-Path $Results 'volume-inspect.log')
    if ($volumeState.ExitCode -eq 0) {
        $state = @($volumeState.Output | ConvertFrom-Json)[0]
        if ($state.Labels.'vulgata.test.run' -ne $Run) { throw 'Volume ownership mismatch; cleanup refused.' }
        $null = Invoke-TestChecked docker @('volume','rm',$Volume) 30 (Join-Path $Results 'volume-cleanup.log')
    } elseif ($volumeState.Error -notmatch 'no such volume') { throw 'Could not inspect owned volume for cleanup.' }
    $remaining = Invoke-TestChecked docker @('volume','ls','--filter',"label=vulgata.test.run=$Run",'--format','{{.Name}}') 30 (Join-Path $Results 'volume-verification.log')
    if ($remaining) { throw 'Owned PostgreSQL volume still exists after cleanup.' }
    $verify = Invoke-TestNative docker @('volume','inspect',$Volume) 30 (Join-Path $Results 'volume-absence.log')
    if ($verify.ExitCode -eq 0 -or $verify.Error -notmatch 'no such volume') { throw 'Owned volume absence was not verified.' }
}
