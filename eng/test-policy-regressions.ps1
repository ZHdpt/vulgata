$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'test-results.ps1')
foreach ($counts in @(@(0,0,0,0,0), @(3,0,0,3,0), @(1,1,0,0,1), @(2,1,0,0,0))) {
    $rejected = $false
    try { Assert-TestCounts @counts } catch { $rejected = $true }
    if (!$rejected) { throw "Invalid result counters accepted: $counts" }
}
Assert-TestCounts 3 2 0 1 0
Write-Host 'PASS: empty/all-skipped/incomplete/process-failure outcomes rejected; valid counts accepted'
$root = Join-Path ([IO.Path]::GetTempPath()) ('vulgata-policy-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'TestInfrastructure') -Force | Out-Null
function Assert-Rejected([string]$Name, [hashtable]$Manifest, [hashtable]$Sources, [string]$Expected) {
    Get-ChildItem $root -Filter *.cs | Remove-Item
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root 'TestInfrastructure/suites.json')
    foreach ($path in $Sources.Keys) {
        $source = $Sources[$path]
        if ($source -notmatch '\b(class|namespace)\b') { $source = 'class Probe { ' + $source + ' }' }
        Set-Content (Join-Path $root $path) $source
    }
    try { & (Join-Path $PSScriptRoot 'test-policy.ps1') -TestRoot $root; throw 'Unexpected policy success' }
    catch { if ($_.ToString() -notmatch $Expected) { throw }; Write-Host "PASS: $Name" }
}
try {
    Assert-Rejected 'unclassified test' @{} @{'New.cs'='[Fact] public void NewTest() {}'} 'Unclassified'
    Assert-Rejected 'unguarded attribute long name' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='[FactAttribute] public void NewTest() {}'} 'Unguarded'
    Assert-Rejected 'guard mismatch' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='[Trait("Suite", "SelfContained")][SuiteFact("External")] public void NewTest() {}'} 'Guard conflicts'
    Assert-Rejected 'direct process' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { void Run() { Process.Start("bad"); } }'} 'Forbidden SelfContained'
    Assert-Rejected 'indirect helper' @{'New.cs'=@{suite='SelfContained'};'Helper.cs'=@{suite='LocalTools'}} @{'New.cs'='class New { Helper resource; }';'Helper.cs'='class Helper { }'} 'dependency crosses suite'
    Assert-Rejected 'indented helper' @{'New.cs'=@{suite='SelfContained'};'Helper.cs'=@{suite='LocalTools'}} @{'New.cs'='class New { Helper resource; }';'Helper.cs'="namespace Example {`n    class Helper { }`n}"} 'dependency crosses suite'
    Assert-Rejected 'unknown helper' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { Helper resource; }';'Helper.cs'='class Helper { }'} 'Unclassified'
    Assert-Rejected 'policy exemption abuse' @{'New.cs'=@{suite='Policy'}} @{'New.cs'='class New { }'} 'Policy is reserved'
    Assert-Rejected 'uncontrolled HTTP' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { void Run() { var c = new HttpClient(); } }'} 'controlled transport'
    Assert-Rejected 'interpolation process' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { string Run() => $"{System.Diagnostics.Process.Start("bad")}"; }'} 'Forbidden SelfContained'
    Assert-Rejected 'combined attributes' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { [Trait("Suite", "SelfContained"), global::Xunit.Fact] public void Run() {} }'} 'Unguarded'
    Assert-Rejected 'qualified mismatch' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { [Trait("Suite", "SelfContained"), global::Vulgata.Tests.TestInfrastructure.SuiteFact("External")] public void Run() {} }'} 'Guard conflicts'
    Assert-Rejected 'target typed HTTP return' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { System.Net.Http.HttpClient Run() => new(); }'} 'controlled transport'
    Assert-Rejected 'HTTP initializer without parentheses' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='class New { void Run() { var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(1) }; } }'} 'controlled transport'
    Assert-Rejected 'arbitrary regex exemption' @{'New.cs'=@{suite='SelfContained';allowedPatterns=@('(?s).*')}} @{'New.cs'='class New { void Run() { Process.Start("bad"); } }'} 'allowedPatterns are forbidden'
    Assert-Rejected 'unreviewed exception identifier' @{'New.cs'=@{suite='SelfContained';exceptions=@('everything');reason='fake'}} @{'New.cs'='class New {}'} 'Unreviewed resource exception'
    Assert-Rejected 'test attribute alias' @{'New.cs'=@{suite='SelfContained'}} @{'New.cs'='using F = global::Xunit.FactAttribute; class New { [F] public void Run() {} }'} 'aliases obscure'
    $safeAliases=[TestPolicySyntax]::Inspect('using Log = Example.ILoggerFactory; using Context = Example.ContextFactory; class Safe {}','SelfContained','Safe.cs',[string[]]@())
    if ($safeAliases.Errors.Count) { throw 'Ordinary factory aliases rejected.' }
    Write-Host 'PASS: ordinary factory aliases remain allowed'
    [xml]$skip = '<TestRun><Results><UnitTestResult outcome="NotExecuted" testName="NewUnexpectedSkip" /></Results></TestRun>'
    $rejected=$false
    try { Assert-TestSkips $skip } catch { $rejected=$true }
    if (!$rejected) { throw 'Unexpected skip accepted.' }
    [xml]$known = '<TestRun><Results><UnitTestResult outcome="NotExecuted" testName="Vulgata.Tests.PreparedScanning.PreparedScanStoreRegressionTests.PreparedStore_ConcurrentIdenticalWriters_CommitOnceAndReplayOnceAsync" /></Results></TestRun>'
    Assert-TestSkips $known
    Write-Host 'PASS: explicit skip allowlist applies to every suite'
} finally {
    # This exact fresh temporary directory was created above; no caller-supplied path is removed.
    if ([IO.Path]::GetFullPath($root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
