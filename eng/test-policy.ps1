param([string]$TestRoot = (Join-Path $PSScriptRoot '../tests/Vulgata.Tests'))
$ErrorActionPreference = 'Stop'
if (-not ('TestPolicySyntax' -as [type])) {
    Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
    $references = @(Get-ChildItem (Join-Path $PSHOME 'ref') -Filter *.dll | ForEach-Object FullName) + @(
        [Microsoft.CodeAnalysis.SyntaxNode].Assembly.Location,
        [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree].Assembly.Location
    )
    Add-Type -Path (Join-Path $PSScriptRoot 'TestPolicySyntax.cs') -CompilerOptions '/nowarn:1701,1702' -ReferencedAssemblies $references

}
$manifest = Get-Content (Join-Path $TestRoot 'TestInfrastructure/suites.json') -Raw | ConvertFrom-Json -AsHashtable
$allowed = @('SelfContained','PostgreSql','LocalTools','External','E2E','HistoricalBoundary','Policy')
$files = @(Get-ChildItem $TestRoot -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })
$sources = @{}
$types = @{}
$errors = [Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    $name = [IO.Path]::GetRelativePath($TestRoot, $file.FullName).Replace('\','/')
    $source = [string](Get-Content $file.FullName -Raw)
    if (!$manifest.ContainsKey($name)) { $errors.Add("Unclassified source: $name"); continue }
    $suite = $manifest[$name].suite
    if ($suite -notin $allowed) { $errors.Add("Invalid suite for $name : $suite") }
    if ($suite -eq 'Policy' -and $name -notin @('TestInfrastructure/SuiteAttributes.cs','TestInfrastructure/SuiteTestFramework.cs','TestInfrastructure/TestResourceScope.cs')) { $errors.Add("Policy is reserved for the three reviewed infrastructure helpers: $name") }
    if ($manifest[$name].ContainsKey('allowedPatterns')) { $errors.Add("Unrestricted allowedPatterns are forbidden: $name") }
    $exceptions = [string[]]@($manifest[$name].exceptions | Where-Object { $_ })
    if ($exceptions.Count -and [string]::IsNullOrWhiteSpace($manifest[$name].reason)) { $errors.Add("Resource exception requires a rationale: $name") }
    $syntax = [TestPolicySyntax]::Inspect($source, $suite, $name, $exceptions)
    foreach ($errorText in $syntax.Errors) { $errors.Add("$name : $errorText") }
    $code = $syntax.Code
    $sources[$name] = $code
    foreach ($type in $syntax.Types) {
        if (!$types.ContainsKey($type)) { $types[$type] = [Collections.Generic.List[string]]::new() }
        $types[$type].Add($name)
    }
    if ($suite -eq 'SelfContained' -and $code -match '\b(?:Process\s*\.\s*Start|ProcessStartInfo|NpgsqlConnection|TcpListener|TcpClient|Socket|HttpClientHandler|SocketsHttpHandler)\b|Environment\s*\.\s*GetEnvironmentVariable') {
        $errors.Add("Forbidden SelfContained resource API: $name")
    }
}
foreach ($name in $manifest.Keys) { if (!$sources.ContainsKey($name)) { $errors.Add("Stale classification: $name") } }
# Traverse source-level type references, including helper-only files. This is a conservative
# policy guard, not a claim of semantic proof or network containment of production code.
foreach ($name in $sources.Keys) {
    if ($manifest[$name].suite -ne 'SelfContained') { continue }
    foreach ($identifier in [regex]::Matches($sources[$name], '\b\w+\b') | ForEach-Object Value | Select-Object -Unique) {
        foreach ($dependency in @($types[$identifier])) {
            if ($dependency -and $dependency -ne $name -and $manifest[$dependency].suite -notin @('SelfContained','Policy')) {
                $errors.Add("SelfContained dependency crosses suite: $name -> $dependency")
            }
        }
    }
}
if ($errors.Count) { throw ($errors -join "`n") }
Write-Host "Policy passed: $($files.Count) classified source files; guarded test traits checked."
