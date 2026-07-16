using System.Diagnostics;
using System.Text.Json;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationPreflightTests
{
    private const string ExactProviderIdentity =
        "Vulgata.Tests.CodeGraphQualification.CgcProviderQualificationTests.Attempt0002_WhenAuthorizedAndUnclaimed_RecordsSchema12EvidenceAsync";

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

        ProcessStartInfo startInfo = new("pwsh")
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
        Assert.Contains($"FullyQualifiedName={ExactProviderIdentity}", script, StringComparison.Ordinal);
        Assert.Contains("Category=CodeGraphQualificationProvider", script, StringComparison.Ordinal);
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

        using JsonDocument document = await RunPowerShellProbeAsync(scriptPath, QualificationTestPaths.RepositoryRoot, probe);
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

    [Fact]
    [Trait("Category", "CodeGraphQualificationSynthetic")]
    public async Task Wrapper_WithFakeExecutor_CreatesAuthorizationAndBindsExactProviderChildAsync()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"vulgata-wrapper-probe-{Guid.NewGuid():N}");
        try
        {
            CopyProbeInput(temporaryRoot, "specs/001-codegraph-extraction/workflow-state.json");
            CopyProbeInput(temporaryRoot, "specs/001-codegraph-extraction/reviews/plan-review-8.json");
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
                        hasToken = -not [string]::IsNullOrWhiteSpace($StartInfo.Environment['VULGATA_QUALIFICATION_TOKEN'])
                        exactCommand = $StartInfo.Environment['VULGATA_QUALIFICATION_EXACT_COMMAND']
                    }
                    return 0
                }
                Invoke-CodeGraphQualificationProvider -AttemptId attempt-0002 -RepositoryRoot $env:VULGATA_PROBE_ROOT -CommandExecutor $executor
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
            Assert.Equal("attempt-0002", observed.GetProperty("attemptId").GetString());
            Assert.True(observed.GetProperty("hasToken").GetBoolean());
            Assert.Equal(
                "pwsh -NoProfile -File tests/Vulgata.Tests/CodeGraphQualification/Invoke-CodeGraphQualificationProvider.ps1 -AttemptId attempt-0002",
                observed.GetProperty("exactCommand").GetString());

            string authorizationPath = Path.Combine(
                temporaryRoot, "specs", "001-codegraph-extraction", "qualification", "attempts", "attempt-0002", "authorized.json");
            Assert.True(File.Exists(authorizationPath));
            using JsonDocument authorization = JsonDocument.Parse(await File.ReadAllTextAsync(authorizationPath));
            Assert.False(authorization.RootElement.TryGetProperty("token", out _));
            Assert.Equal(64, authorization.RootElement.GetProperty("tokenSha256").GetString()!.Length);
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task<JsonDocument> RunPowerShellProbeAsync(string scriptPath, string repositoryRoot, string probe)
    {
        ProcessStartInfo startInfo = new("pwsh")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(probe);
        startInfo.Environment["VULGATA_PROBE_SCRIPT"] = scriptPath;
        startInfo.Environment["VULGATA_PROBE_ROOT"] = repositoryRoot;

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The fake-executor PowerShell probe did not start.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, standardError);
        return JsonDocument.Parse(standardOutput);
    }

    private static void CopyProbeInput(string temporaryRoot, string relativePath)
    {
        string destination = Path.Combine(temporaryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(QualificationTestPaths.FromRepositoryRoot(relativePath.Split('/')), destination);
    }
}
