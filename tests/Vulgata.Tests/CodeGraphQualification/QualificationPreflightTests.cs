using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationPreflightTests
{
    private const string ExactProviderIdentity =
        "Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests.Attempt0003_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync";

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Run_WithRepositoryInputs_UsesOnlyReachabilityFreeCommandsAsync()
    {
        string scriptPath = QualificationTestPaths.FromRepositoryRoot(
            "tests",
            "Vulgata.Tests",
            "CodeGraphQualification",
            "Invoke-CodeGraphQualificationPreflight.ps1");
        Assert.True(File.Exists(scriptPath), $"Missing preflight script: {scriptPath}");

        ProcessStartInfo startInfo = new(GetPowerShellExecutable())
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-RepositoryRoot");
        startInfo.ArgumentList.Add(QualificationTestPaths.RepositoryRoot);
        startInfo.ArgumentList.Add("-DescribeExecutionPlan");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The reachability-free preflight description did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, standardError);
        using JsonDocument document = JsonDocument.Parse(standardOutput);
        JsonElement root = document.RootElement;
        Assert.True(root.GetProperty("providerReachabilityFree").GetBoolean());
        Assert.Equal(
            ["dotnet"],
            root.GetProperty("externalExecutables").EnumerateArray().Select(item => item.GetString()));

        string script = await File.ReadAllTextAsync(scriptPath);
        string[] forbiddenReachability =
        [
            "Start-Process -FilePath 'docker'",
            "Start-Process -FilePath \"docker\"",
            "& docker",
            "cgc version",
            "cgc index",
            "http://",
            "https://",
            "image inspect",
            "container start",
        ];
        Assert.All(forbiddenReachability, token => Assert.DoesNotContain(token, script, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("reviews/plan-review-10.json", script, StringComparison.Ordinal);
        Assert.Contains("attempt_0003_planning_approval_observed_at", script, StringComparison.Ordinal);
        Assert.DoesNotContain("planning_approval_summary", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "User explicitly approved fresh corrective planning",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("reviews/plan-review-8.json", script, StringComparison.Ordinal);
        Assert.DoesNotContain("attempt_0002_approval_observed_at", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Wrapper_WhenInspected_BindsOneCreateNewAuthorizationToExactProviderChildAsync()
    {
        string scriptPath = QualificationTestPaths.FromRepositoryRoot(
            "tests",
            "Vulgata.Tests",
            "CodeGraphQualification",
            "Invoke-CodeGraphQualificationProvider.ps1");
        Assert.True(File.Exists(scriptPath), $"Missing provider wrapper: {scriptPath}");

        string script = await File.ReadAllTextAsync(scriptPath);
        Assert.Contains("FullyQualifiedName=$($boundary.providerFactIdentity)", script, StringComparison.Ordinal);
        Assert.Contains("Category=$($boundary.providerCategory)", script, StringComparison.Ordinal);
        Assert.Contains("FileMode]::CreateNew", script, StringComparison.Ordinal);
        Assert.Contains("VULGATA_QUALIFICATION_ATTEMPT_ID", script, StringComparison.Ordinal);
        Assert.Contains("VULGATA_QUALIFICATION_TOKEN", script, StringComparison.Ordinal);
        Assert.Contains("VULGATA_QUALIFICATION_EXACT_COMMAND", script, StringComparison.Ordinal);
        Assert.DoesNotContain("--no-build", script, StringComparison.Ordinal);
        Assert.DoesNotContain("FullyQualifiedName~", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Run_WithFakeExecutor_InvokesOnlyFreshBuildAndExactSyntheticCategoryAsync()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"vulgata-preflight-plan-{Guid.NewGuid():N}");
        string scriptPath = QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationPreflight.ps1");
        const string probe = """
            . $env:VULGATA_PROBE_SCRIPT
            $calls = [System.Collections.Generic.List[object]]::new()
            $executor = {
                param([System.Diagnostics.ProcessStartInfo] $StartInfo)
                $calls.Add([pscustomobject][ordered]@{ filePath = $StartInfo.FileName; arguments = @($StartInfo.ArgumentList); workingDirectory = $StartInfo.WorkingDirectory })
                return 0
            }
            $null = Invoke-CodeGraphQualificationPreflight -RepositoryRoot $env:VULGATA_PROBE_ROOT -CommandExecutor $executor
            ConvertTo-Json -InputObject @($calls) -Depth 6 -Compress
            """;

        try
        {
            CopyPreflightInputs(temporaryRoot);
            using JsonDocument document = await RunPowerShellProbeAsync(scriptPath, temporaryRoot, probe);
            JsonElement[] calls = document.RootElement.EnumerateArray().ToArray();
            Assert.True(calls.Length == 2, document.RootElement.GetRawText());
            Assert.All(calls, call => Assert.Equal("dotnet", call.GetProperty("filePath").GetString()));
            Assert.Equal(
                ["build", "tests/Vulgata.Tests/Vulgata.Tests.csproj"],
                calls[0].GetProperty("arguments").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(
                ["test", "tests/Vulgata.Tests/Vulgata.Tests.csproj", "--filter", "Category=CodeGraphQualificationSynthetic"],
                calls[1].GetProperty("arguments").EnumerateArray().Select(item => item.GetString()));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Run_WithIncompleteConsumedAttempt_ReconcilesInTempAndNeverCreatesAttempt0003Async()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"vulgata-preflight-reconcile-{Guid.NewGuid():N}");
        try
        {
            CopyPreflightInputs(temporaryRoot);

            string authorizationPath = Path.Combine(
                temporaryRoot,
                "specs",
                "001-codegraph-extraction",
                "qualification",
                "attempts",
                "attempt-0002",
                "authorized.json");
            byte[] authorizationBefore = await File.ReadAllBytesAsync(authorizationPath);
            string scriptPath = QualificationTestPaths.FromRepositoryRoot(
                "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationPreflight.ps1");
            const string probe = """
                . $env:VULGATA_PROBE_SCRIPT
                $calls = 0
                $executor = {
                    param([System.Diagnostics.ProcessStartInfo] $StartInfo)
                    $script:calls++
                    return 0
                }
                $null = Invoke-CodeGraphQualificationPreflight `
                    -RepositoryRoot $env:VULGATA_PROBE_ROOT `
                    -CommandExecutor $executor
                [ordered]@{ calls = $script:calls } | ConvertTo-Json -Compress
                """;

            using JsonDocument document = await RunPowerShellProbeAsync(scriptPath, temporaryRoot, probe);

            Assert.Equal(2, document.RootElement.GetProperty("calls").GetInt32());
            Assert.Equal(authorizationBefore, await File.ReadAllBytesAsync(authorizationPath));
            string attempt0002Directory = Path.GetDirectoryName(authorizationPath)!;
            Assert.False(File.Exists(Path.Combine(attempt0002Directory, "started.json")));
            Assert.False(File.Exists(Path.Combine(attempt0002Directory, "terminal.json")));
            Assert.False(Directory.Exists(Path.Combine(
                temporaryRoot,
                "specs",
                "001-codegraph-extraction",
                "qualification",
                "attempts",
                "attempt-0003")));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Wrapper_WithFakeExecutor_AndRealCSharpLedger_ConsumesIdenticalRawBoundaryAsync()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"vulgata-wrapper-probe-{Guid.NewGuid():N}");
        try
        {
            const string planReviewRelativePath = "specs/synthetic/plan-review.json";
            string planReviewPath = Path.Combine(
                temporaryRoot,
                planReviewRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(planReviewPath)!);
            byte[] planReviewBytes = Encoding.UTF8.GetBytes("""{"verdict":"ACCEPT","fixture":"synthetic"}""");
            await File.WriteAllBytesAsync(planReviewPath, planReviewBytes);
            string planReviewSha256 = Convert.ToHexStringLower(SHA256.HashData(planReviewBytes));
            const string boundaryRelativePath =
                "specs/001-codegraph-extraction/qualification/execution-boundaries/attempt-0003.json";
            string boundaryPath = Path.Combine(
                temporaryRoot,
                boundaryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(boundaryPath)!);
            byte[] boundaryBytes = Encoding.UTF8.GetBytes(
                QualificationExecutionBoundaryTests.CreateBoundaryJson(
                    planReviewRelativePath,
                    planReviewSha256));
            await File.WriteAllBytesAsync(boundaryPath, boundaryBytes);
            string boundarySha256 = Convert.ToHexStringLower(SHA256.HashData(boundaryBytes));
            string workflowStatePath = Path.Combine(
                temporaryRoot,
                "specs",
                "001-codegraph-extraction",
                "workflow-state.json");
            Directory.CreateDirectory(Path.GetDirectoryName(workflowStatePath)!);
            await File.WriteAllTextAsync(
                workflowStatePath,
                $$"""
                {
                  "plan_revision": {
                    "attempt_0003_execution_approval_gate_id": "story-4.1-attempt-0003-execution",
                    "attempt_0003_execution_boundary_sha256": "{{boundarySha256}}"
                  }
                }
                """);
            string scriptPath = QualificationTestPaths.FromRepositoryRoot(
                "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationProvider.ps1");
            const string probe = """
                . $env:VULGATA_PROBE_SCRIPT
                $observed = $null
                $executor = {
                    param([System.Diagnostics.ProcessStartInfo] $StartInfo)
                    $script:observed = [ordered]@{
                        fileName = $StartInfo.FileName
                        arguments = @($StartInfo.ArgumentList)
                        attemptId = $StartInfo.Environment['VULGATA_QUALIFICATION_ATTEMPT_ID']
                        token = $StartInfo.Environment['VULGATA_QUALIFICATION_TOKEN']
                        exactCommand = $StartInfo.Environment['VULGATA_QUALIFICATION_EXACT_COMMAND']
                        boundaryPath = $StartInfo.Environment['VULGATA_QUALIFICATION_BOUNDARY_PATH']
                        boundarySha256 = $StartInfo.Environment['VULGATA_QUALIFICATION_BOUNDARY_SHA256']
                        repositoryRoot = $StartInfo.Environment['VULGATA_QUALIFICATION_REPOSITORY_ROOT']
                    }
                    $environment = [System.Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
                    foreach ($name in @(
                        'VULGATA_QUALIFICATION_ATTEMPT_ID',
                        'VULGATA_QUALIFICATION_TOKEN',
                        'VULGATA_QUALIFICATION_EXACT_COMMAND',
                        'VULGATA_QUALIFICATION_BOUNDARY_PATH',
                        'VULGATA_QUALIFICATION_BOUNDARY_SHA256',
                        'VULGATA_QUALIFICATION_REPOSITORY_ROOT'
                    )) {
                        $environment[$name] = [string]$StartInfo.Environment[$name]
                    }
                    $assembly = [System.Reflection.Assembly]::LoadFrom($env:VULGATA_TEST_ASSEMBLY)
                    $bridge = $assembly.GetType('Vulgata.Tests.CodeGraphQualification.QualificationSyntheticChildBridge', $true)
                    $attemptsRoot = [string](Join-Path $env:VULGATA_PROBE_ROOT 'specs/001-codegraph-extraction/qualification/attempts')
                    $task = $bridge.GetMethod('ClaimAndCompleteAsync').Invoke(
                        $null,
                        [object[]]@([string]$attemptsRoot, $environment))
                    $null = $task.GetAwaiter().GetResult()
                    return 0
                }
                Invoke-CodeGraphQualificationProvider -AttemptId attempt-0003 -RepositoryRoot $env:VULGATA_PROBE_ROOT -BoundaryPath 'specs/001-codegraph-extraction/qualification/execution-boundaries/attempt-0003.json' -CommandExecutor $executor
                $script:observed | ConvertTo-Json -Depth 6 -Compress
                """;

            using JsonDocument document = await RunPowerShellProbeAsync(scriptPath, temporaryRoot, probe);
            JsonElement observed = document.RootElement;
            Assert.Equal("dotnet", observed.GetProperty("fileName").GetString());
            Assert.Equal(
                [
                    "test",
                    "tests/Vulgata.Tests/Vulgata.Tests.csproj",
                    "--filter",
                    $"FullyQualifiedName={ExactProviderIdentity}&Category=CodeGraphQualificationProvider",
                ],
                observed.GetProperty("arguments").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal("attempt-0003", observed.GetProperty("attemptId").GetString());
            Assert.Equal(
                QualificationExecutionBoundaryTests.ExpectedCommand,
                observed.GetProperty("exactCommand").GetString());
            Assert.Equal(boundaryRelativePath, observed.GetProperty("boundaryPath").GetString());
            Assert.Equal(boundarySha256, observed.GetProperty("boundarySha256").GetString());

            string authorizationPath = Path.Combine(
                temporaryRoot, "specs", "001-codegraph-extraction", "qualification", "attempts", "attempt-0003", "authorized.json");
            Assert.True(File.Exists(authorizationPath));
            using JsonDocument authorization = JsonDocument.Parse(await File.ReadAllTextAsync(authorizationPath));
            Assert.False(authorization.RootElement.TryGetProperty("token", out _));
            Assert.Equal(64, authorization.RootElement.GetProperty("tokenSha256").GetString()!.Length);
            Assert.Equal(boundarySha256, authorization.RootElement.GetProperty("executionBoundarySha256").GetString());

            string attemptDirectory = Path.GetDirectoryName(authorizationPath)!;
            Assert.True(File.Exists(Path.Combine(attemptDirectory, "started.json")));
            using JsonDocument terminal = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(attemptDirectory, "terminal.json")));
            Assert.Equal(
                "synthetic-child-classification",
                terminal.RootElement.GetProperty("category").GetString());
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false, "interrupted-before-claim")]
    [InlineData(true, "interrupted-after-claim")]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconciler_WithIncompleteDurableState_PublishesProviderUnreachableTerminalAsync(
        bool hasStart,
        string expectedCategory)
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"vulgata-reconcile-probe-{Guid.NewGuid():N}");
        try
        {
            string attemptDirectory = Path.Combine(temporaryRoot, "attempt-0003");
            Directory.CreateDirectory(attemptDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(attemptDirectory, "authorized.json"),
                """{"fixture":"synthetic-authorization"}""");
            if (hasStart)
            {
                await File.WriteAllTextAsync(
                    Path.Combine(attemptDirectory, "started.json"),
                    """{"fixture":"synthetic-start"}""");
            }
            string scriptPath = QualificationTestPaths.FromRepositoryRoot(
                "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationProvider.ps1");
            const string probe = """
                . $env:VULGATA_PROBE_SCRIPT
                Invoke-QualificationAttemptReconciliation -AttemptDirectory $env:VULGATA_ATTEMPT_DIRECTORY -AttemptId attempt-0003 |
                    ConvertTo-Json -Depth 6 -Compress
                """;

            using JsonDocument document = await RunPowerShellProbeAsync(
                scriptPath,
                temporaryRoot,
                probe,
                new Dictionary<string, string?>
                {
                    ["VULGATA_ATTEMPT_DIRECTORY"] = attemptDirectory,
                });

            Assert.Equal(expectedCategory, document.RootElement.GetProperty("category").GetString());
            Assert.True(File.Exists(Path.Combine(attemptDirectory, "terminal.json")));
            Assert.Empty(Directory.GetFiles(attemptDirectory, ".terminal.*.tmp"));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Reconciler_InDistinctProcessWhileClaimIsActive_DefersWithoutStartingOrTerminalizingAsync()
    {
        using QualificationAttemptLedgerTests.TemporaryQualificationRoot temporary = new();
        const string token = "synthetic-cross-process-race-token";
        DateTimeOffset now = new(2026, 7, 18, 12, 0, 0, TimeSpan.FromHours(8));
        QualificationExecutionBoundary boundary = await temporary.WriteBoundaryAsync();
        await temporary.WriteAuthorizationAsync(boundary, token, now.AddMinutes(5));
        QualificationAttemptLedger ledger = temporary.CreateLedger(boundary, now);
        ClaimedProviderAuthorization claim = await ledger.ClaimAsync(
            boundary.AttemptId,
            token,
            boundary.ExactCommand);
        string scriptPath = QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Invoke-CodeGraphQualificationProvider.ps1");
        const string probe = """
            . $env:VULGATA_PROBE_SCRIPT
            $result = Invoke-QualificationAttemptReconciliation `
                -AttemptDirectory $env:VULGATA_ATTEMPT_DIRECTORY `
                -AttemptId attempt-0003
            [ordered]@{
                reconciled = $null -ne $result
                terminalExists = Test-Path -LiteralPath (Join-Path $env:VULGATA_ATTEMPT_DIRECTORY 'terminal.json')
            } | ConvertTo-Json -Compress
            """;

        using JsonDocument document = await RunPowerShellProbeAsync(
            scriptPath,
            temporary.Root,
            probe,
            new Dictionary<string, string?>
            {
                ["VULGATA_ATTEMPT_DIRECTORY"] = temporary.AttemptDirectory,
            });

        Assert.False(document.RootElement.GetProperty("reconciled").GetBoolean());
        Assert.False(document.RootElement.GetProperty("terminalExists").GetBoolean());
        Assert.Single(Directory.GetFiles(temporary.AttemptDirectory, "started.json"));
        await ledger.CompleteAsync(
            claim,
            "failed",
            "synthetic-provider-incapable",
            "specs/001-codegraph-extraction/qualification/evidence/verification.md");
    }

    private static async Task<JsonDocument> RunPowerShellProbeAsync(
        string scriptPath,
        string repositoryRoot,
        string probe,
        IReadOnlyDictionary<string, string?>? additionalEnvironment = null)
    {
        string powerShellExecutable = GetPowerShellExecutable();
        string? probePath = null;
        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows()
            && powerShellExecutable.Contains(
                $"{Path.DirectorySeparatorChar}WindowsApps{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
        {
            probePath = Path.Combine(Path.GetTempPath(), $"vulgata-pwsh-probe-{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(probePath, probe);
            startInfo = new ProcessStartInfo("cmd.exe");
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add($"pwsh -NoProfile -File {probePath}");
        }
        else
        {
            startInfo = new ProcessStartInfo(powerShellExecutable);
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(probe);
        }

        startInfo.RedirectStandardError = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;
        startInfo.Environment["VULGATA_PROBE_SCRIPT"] = scriptPath;
        startInfo.Environment["VULGATA_PROBE_ROOT"] = repositoryRoot;
        startInfo.Environment["VULGATA_TEST_ASSEMBLY"] = typeof(QualificationPreflightTests).Assembly.Location;
        if (additionalEnvironment is not null)
        {
            foreach ((string key, string? value) in additionalEnvironment)
            {
                startInfo.Environment[key] = value;
            }
        }

        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The fake-executor PowerShell probe did not start.");
            string standardOutput = await process.StandardOutput.ReadToEndAsync();
            string standardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, standardError);
            return JsonDocument.Parse(standardOutput);
        }
        finally
        {
            if (probePath is not null)
            {
                File.Delete(probePath);
            }
        }
    }

    private static void CopyRepositoryInput(string temporaryRoot, string relativePath)
    {
        string destination = Path.Combine(
            temporaryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(QualificationTestPaths.FromRepositoryRoot(relativePath.Split('/')), destination);
    }

    private static void CopyPreflightInputs(string temporaryRoot)
    {
        string[] inputs =
        [
            "docker/Dockerfile",
            "docker/codegraph/python-version.txt",
            "docker/codegraph/requirements.lock",
            "specs/001-codegraph-extraction/qualification/qualification-result.schema.json",
            "specs/001-codegraph-extraction/qualification/attempts/authorized.schema.json",
            "specs/001-codegraph-extraction/qualification/attempts/started.schema.json",
            "specs/001-codegraph-extraction/qualification/attempts/terminal.schema.json",
            "specs/001-codegraph-extraction/qualification/attempts/attempt-0001/started.json",
            "specs/001-codegraph-extraction/qualification/attempts/attempt-0001/terminal.json",
            "specs/001-codegraph-extraction/qualification/attempts/attempt-0002/authorized.json",
            "specs/001-codegraph-extraction/reviews/plan-review-10.json",
            "specs/001-codegraph-extraction/workflow-state.json",
            "tests/Vulgata.Tests/CodeGraphQualification/Fixtures/qualification-fixture-manifest.json",
            "tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository/src/Eligible/OrderService.cs",
            "tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository/src/Eligible/OrderRunner.cs",
            "tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository/src/Eligible/BaseProcessor.cs",
            "tests/Vulgata.Tests/CodeGraphQualification/ProviderFixtures/Attempt0002Repository/src/Filtered/GeneratedClient.g.cs",
        ];
        foreach (string input in inputs)
        {
            CopyRepositoryInput(temporaryRoot, input);
        }
    }

    private static string GetPowerShellExecutable()
    {
        foreach (string moduleDirectory in (Environment.GetEnvironmentVariable("PSModulePath") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? powerShellHome = Directory.GetParent(moduleDirectory)?.FullName;
            string candidate = Path.Combine(powerShellHome ?? string.Empty, "pwsh.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (OperatingSystem.IsWindows()
            && Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\pwsh.exe",
                null,
                null) is string registeredPowerShell
            && File.Exists(registeredPowerShell))
        {
            return registeredPowerShell;
        }

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, "pwsh.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");
        try
        {
            string? packagedPowerShell = Directory
                .EnumerateDirectories(windowsApps, "Microsoft.PowerShell_*_x64__8wekyb3d8bbwe")
                .OrderDescending()
                .Select(directory => Path.Combine(directory, "pwsh.exe"))
                .FirstOrDefault(File.Exists);
            if (packagedPowerShell is not null)
            {
                return packagedPowerShell;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Fall through to normal executable lookup on installations that hide WindowsApps.
        }

        return "pwsh";
    }
}
