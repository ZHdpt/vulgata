[CmdletBinding()]
param(
    [ValidateSet('SelfContained','PostgreSql','LocalTools','External','E2E','HistoricalBoundary')]
    [string]$Suite = 'SelfContained',
    [string]$Filter,
    [ValidateRange(10,86400)][int]$TestTimeoutSeconds = 300,
    [switch]$WithPostgreSql,
    [string]$SeedDump,
    [ValidateRange(10,86400)][int]$CommandTimeoutSeconds = 600,
    [switch]$PolicyOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-results.ps1')
. (Join-Path $PSScriptRoot 'test-native.ps1')
. (Join-Path $PSScriptRoot 'test-postgres.ps1')
Assert-TestResourceOptions $Suite $WithPostgreSql.IsPresent $SeedDump
$repo = Split-Path $PSScriptRoot
& (Join-Path $PSScriptRoot 'test-policy.ps1')
& (Join-Path $PSScriptRoot 'test-policy-regressions.ps1')
if ($PolicyOnly) { return }
& (Join-Path $PSScriptRoot 'test-native-regressions.ps1')
$run = [Guid]::NewGuid().ToString('N')
$results = Join-Path $repo "TestResults/isolation-$run"
New-Item -ItemType Directory -Force $results | Out-Null
$project = Join-Path $repo 'tests/Vulgata.Tests/Vulgata.Tests.csproj'
$settings = Join-Path $repo 'tests/Vulgata.Tests/explicit-suites.runsettings'
$saved = @{}
$container = $null
$volume = $null
$exitCode = 1
function Set-RunEnvironment([string]$Name, [string]$Value) {
    if (!$saved.ContainsKey($Name)) { $saved[$Name] = [Environment]::GetEnvironmentVariable($Name) }
    [Environment]::SetEnvironmentVariable($Name, $Value)
}
try {
    # Configuration inherited from an application is never execution authorization.
    foreach ($entry in Get-ChildItem Env:) {
        if ($entry.Name -match '^(ConnectionStrings__|VULGATA_TEST_|VULGATA_STORY131_POSTGRES|VULGATA_T124_POSTGRES_ADMIN$|VULGATA_T124_SOURCE_CONNECTION$|VULGATA_T062_QUALIFICATION_CONNECTION$)' -or
            ($Suite -eq 'SelfContained' -and $entry.Name -match '^(VULGATA_|OPENAI_|DEEPSEEK_|AZURE_OPENAI_)')) {
            Set-RunEnvironment $entry.Name $null
        }
    }
    Set-RunEnvironment 'VULGATA_TEST_SUITE' $Suite
    if ($Suite -eq 'PostgreSql' -or $WithPostgreSql) {
        if (!(Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Prerequisite unavailable: Docker is required for owned PostgreSQL 17.' }
        $null = Invoke-TestChecked docker @('info','--format','{{.ServerVersion}}') 30 (Join-Path $results 'docker-readiness.log')
        $container = "vulgata-test-$run"
        $volume = "vulgata-test-data-$run"
        $password = [Guid]::NewGuid().ToString('N')
        Set-RunEnvironment 'POSTGRES_PASSWORD' $password
        $null = Invoke-TestChecked docker @('volume','create','--label',"vulgata.test.run=$run",$volume) 30 (Join-Path $results 'volume-create.log')
        $null = Invoke-TestChecked docker @('run','--detach','--name',$container,'--label',"vulgata.test.run=$run",'--mount',"type=volume,source=$volume,target=/var/lib/postgresql/data",'--publish','127.0.0.1::5432','--env','POSTGRES_PASSWORD','postgres:17-alpine') $CommandTimeoutSeconds (Join-Path $results 'container.log')
        $mounts = Invoke-TestChecked docker @('inspect','--format','{{json .Mounts}}',$container) 30 (Join-Path $results 'owned-mounts.json')
        $ready = $false
        $readiness = [Diagnostics.Stopwatch]::StartNew()
        while ($readiness.Elapsed.TotalSeconds -lt 45) {
            $probe = Invoke-TestNative docker @('exec',$container,'pg_isready','-h','127.0.0.1','-U','postgres') 5 (Join-Path $results 'postgres-readiness.log')
            if ($probe.ExitCode -eq 0) { $ready=$true; break }
            Start-Sleep -Seconds 1
        }
        if (!$ready) { throw 'Owned PostgreSQL did not become TCP-ready within 45 seconds.' }
        $binding = Invoke-TestChecked docker @('port',$container,'5432/tcp') 30 (Join-Path $results 'port.log')
        if ($binding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Invalid owned PostgreSQL port binding.' }
        $admin = "Host=127.0.0.1;Port=$($Matches[1]);Database=postgres;Username=postgres;Password=$password;Pooling=false"
        Set-RunEnvironment 'VULGATA_TEST_RESOURCE_RUN' $run
        Set-RunEnvironment 'VULGATA_TEST_OWNED_ADMIN' $admin
        foreach ($name in @('VULGATA_TEST_POSTGRES_ADMIN','VULGATA_TEST_ADMIN_CONNECTION','VULGATA_TEST_POSTGRES','VULGATA_STORY131_POSTGRES_ADMIN')) { Set-RunEnvironment $name $admin }
        Set-RunEnvironment 'VULGATA_T124_POSTGRES_ADMIN' $admin
        if ($SeedDump) {
            $seedDatabase = "vulgata_story124_seed_$run"
            $null = Invoke-TestChecked docker @('exec',$container,'createdb','-h','127.0.0.1','-U','postgres',$seedDatabase) 30 (Join-Path $results 'seed-create.log')
            Set-RunEnvironment 'VULGATA_TEST_OWNED_DATABASES' $seedDatabase
            $null = Invoke-TestChecked docker @('cp',(Resolve-Path -LiteralPath $SeedDump).Path,"${container}:/tmp/seed.dump") $CommandTimeoutSeconds (Join-Path $results 'seed-copy.log')
            $null = Invoke-TestChecked docker @('exec',$container,'pg_restore','--exit-on-error','--single-transaction','--no-owner','--no-privileges','-h','127.0.0.1','-U','postgres','--dbname',$seedDatabase,'/tmp/seed.dump') $CommandTimeoutSeconds (Join-Path $results 'seed-import.log') -SensitiveOutput
            $target = $admin.Replace('Database=postgres;', "Database=$seedDatabase;")
            Set-RunEnvironment 'VULGATA_T062_QUALIFICATION_CONNECTION' $target
            Set-RunEnvironment 'VULGATA_T124_SOURCE_CONNECTION' $target
        }
    }
    Write-Host "Building current test sources. Log: $results/build.log"
    $null = Invoke-TestChecked dotnet @('build',$project,'--verbosity','quiet') $CommandTimeoutSeconds (Join-Path $results 'build.log')
    $null = Invoke-TestChecked pwsh @('-NoProfile','-File',(Join-Path $PSScriptRoot 'test-guard-regressions.ps1')) 120 (Join-Path $results 'guard-probes.log')
    $null = Invoke-TestChecked dotnet @('test',$project,'--no-build','--settings',$settings,'--list-tests','--filter','Suite!=__unclassified__') $CommandTimeoutSeconds (Join-Path $results 'discovery.log')
    $discovered = @(Get-Content (Join-Path $results 'discovery.log') | Where-Object { $_ -match '^    \S' }).Count
    $selection = "Suite=$Suite"
    if ($Filter) { $selection = "($selection)&($Filter)" }
    # Explicit filter overrides the safe project runsettings; the attributes still require opt-in.
    $execution = Invoke-TestNative dotnet @('test',$project,'--no-build','--settings',$settings,'--filter',$selection,'--blame-hang-timeout',"${TestTimeoutSeconds}s",'--blame-hang-dump-type','none','--logger','trx;LogFileName=results.trx','--results-directory',$results) $CommandTimeoutSeconds (Join-Path $results 'execution.log')
    $testExit = $execution.ExitCode
    $trxPath = Join-Path $results 'results.trx'
    if (!(Test-Path $trxPath)) { throw 'Test runner produced no TRX result.' }
    [xml]$trx = Get-Content $trxPath -Raw
    $counts = $trx.TestRun.ResultSummary.Counters
    $total=[int]$counts.total; $passed=[int]$counts.passed; $failed=[int]$counts.failed; $skipped=[int]$counts.notExecuted
    Write-Host "Suite=$Suite discovered=$discovered selected=$total passed=$passed failed=$failed skipped=$skipped"
    $excluded = @('SelfContained','PostgreSql','LocalTools','External','E2E','HistoricalBoundary') | Where-Object { $_ -ne $Suite }
    Write-Host "Excluded suites (not qualified): $($excluded -join ', '). TRX: $trxPath"
    Assert-TestCounts $total $passed $failed $skipped $testExit
    Assert-TestSkips $trx
    $exitCode = 0
} catch {
    Write-Error $_ -ErrorAction Continue
} finally {
    try {
        Invoke-TestCleanup -Environment $saved -Cleanup {
            if ($container) { Remove-OwnedPostgres $container $volume $run $results }
        }
    } catch { $exitCode=1; Write-Warning "Owned PostgreSQL cleanup failed: $_" }
}
exit $exitCode
