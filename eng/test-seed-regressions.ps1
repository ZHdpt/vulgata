# Controlled fixture archive generation only. Never exports a configured/shared database.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'test-native.ps1')
. (Join-Path $PSScriptRoot 'test-postgres.ps1')
$run=[Guid]::NewGuid().ToString('N')
$results=Join-Path (Split-Path $PSScriptRoot) "TestResults/seed-probe-$run"
New-Item -ItemType Directory $results | Out-Null
$container="vulgata-seed-probe-$run"
$volume="vulgata-seed-probe-data-$run"
$previousPassword=$env:POSTGRES_PASSWORD
try {
    $env:POSTGRES_PASSWORD=[Guid]::NewGuid().ToString('N')
    $null=Invoke-TestChecked docker @('volume','create','--label',"vulgata.test.run=$run",$volume) 30 (Join-Path $results 'volume-create.log')
    $null=Invoke-TestChecked docker @('run','--detach','--name',$container,'--label',"vulgata.test.run=$run",'--mount',"type=volume,source=$volume,target=/var/lib/postgresql/data",'--env','POSTGRES_PASSWORD','postgres:17-alpine') 60 (Join-Path $results 'container.log')
    $ready=$false
    for ($i=0;$i -lt 30;$i++) {
        $probe=Invoke-TestNative docker @('exec',$container,'pg_isready','-h','127.0.0.1','-U','postgres') 5
        if ($probe.ExitCode -eq 0) { $ready=$true; break }
        Start-Sleep -Seconds 1
    }
    if (!$ready) { throw 'Controlled seed fixture did not become ready.' }
    $null=Invoke-TestChecked docker @('exec',$container,'createdb','-h','127.0.0.1','-U','postgres','controlled_seed') 30
    $sql="CREATE TABLE public.isolation_seed_probe(value text NOT NULL); INSERT INTO public.isolation_seed_probe VALUES ('controlled-local-seed');"
    $null=Invoke-TestChecked docker @('exec',$container,'psql','-h','127.0.0.1','-U','postgres','-d','controlled_seed','-v','ON_ERROR_STOP=1','-c',$sql) 30
    $null=Invoke-TestChecked docker @('exec',$container,'pg_dump','-h','127.0.0.1','-U','postgres','-Fc','-f','/tmp/controlled.dump','controlled_seed') 30
    $dump=Join-Path $results 'controlled.dump'
    $null=Invoke-TestChecked docker @('cp',"${container}:/tmp/controlled.dump",$dump) 30
    $null=Invoke-TestChecked pwsh @('-NoProfile','-File',(Join-Path $PSScriptRoot 'test.ps1'),'-Suite','External','-WithPostgreSql','-SeedDump',$dump,'-Filter','FullyQualifiedName~SeededTargetTests','-CommandTimeoutSeconds','180') 300 (Join-Path $results 'runner.log')
    Write-Host "PASS: explicit local import and owned T124 clone probes. Evidence: $results"
} finally {
    try { Remove-OwnedPostgres $container $volume $run $results }
    finally { $env:POSTGRES_PASSWORD=$previousPassword }
}
