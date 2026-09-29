# Run after a current-source build. The enabled control deliberately fails its fixture.
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$project = Join-Path $repo 'tests/Vulgata.Tests/Vulgata.Tests.csproj'
$settings = Join-Path $repo 'tests/Vulgata.Tests/explicit-suites.runsettings'
$results = Join-Path $repo ('TestResults/guard-' + [Guid]::NewGuid().ToString('N'))
$savedSuite = $env:VULGATA_TEST_SUITE
$savedProbe = $env:VULGATA_TEST_GUARD_PROBE
try {
    $env:VULGATA_TEST_SUITE = $null
    $env:VULGATA_TEST_GUARD_PROBE = '1'
    & dotnet test $project --no-build --settings $settings --filter 'FullyQualifiedName~DisabledFixtureProbe' --logger 'trx;LogFileName=disabled.trx' --results-directory $results
    if ($LASTEXITCODE) { throw 'Disabled suite initialized its sentinel fixture.' }
    [xml]$disabled = Get-Content (Join-Path $results 'disabled.trx') -Raw
    if ([int]$disabled.TestRun.ResultSummary.Counters.total -ne 0) { throw 'Disabled sentinel was not excluded before fixture initialization.' }
    $env:VULGATA_TEST_SUITE = 'External'
    & dotnet test $project --no-build --settings $settings --filter 'FullyQualifiedName~DisabledFixtureProbe' --logger 'trx;LogFileName=enabled.trx' --results-directory $results
    if (!$LASTEXITCODE) { throw 'Enabled sentinel control unexpectedly passed.' }
    [xml]$enabled = Get-Content (Join-Path $results 'enabled.trx') -Raw
    if ([int]$enabled.TestRun.ResultSummary.Counters.failed -ne 1 -or
        $enabled.OuterXml -notmatch 'Disabled external fixture was initialized') { throw 'Enabled sentinel did not demonstrate fixture execution.' }
    Write-Host "PASS: disabled fixture excluded; enabled control reached fixture. Evidence: $results"
} finally {
    $env:VULGATA_TEST_SUITE = $savedSuite
    $env:VULGATA_TEST_GUARD_PROBE = $savedProbe
}
