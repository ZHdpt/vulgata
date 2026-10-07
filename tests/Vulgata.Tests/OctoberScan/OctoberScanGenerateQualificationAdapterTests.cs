using System.Text.Json;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Integration;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Owned synthetic documents and inert dispatch only; no credential callback, process, provider or database access.</summary>
public sealed class OctoberScanGenerateQualificationAdapterTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExactApprovedInputs_ValidateWithoutSourceOrCredentialAccessAsync()
    {
        using var inputs = new Inputs();
        await inputs.ValidateAsync();
        Assert.False(Directory.Exists(inputs.SourceRoot));
        Assert.False(Directory.Exists(inputs.Runtime.LogDirectory));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("batch")]
    [InlineData("root-binding")]
    [InlineData("source-root")]
    [InlineData("stage")]
    [InlineData("cap")]
    [InlineData("requests")]
    [InlineData("tokens")]
    [InlineData("file-scope")]
    [InlineData("provider")]
    public async Task AlteredInputCannotExpandApprovedBindingOrBudgetAsync(string change)
    {
        using var inputs = new Inputs();
        switch (change)
        {
            case "batch": inputs.WriteRuntime(inputs.Runtime with { PreparedRoots = [inputs.Runtime.PreparedRoots[0] with { BatchId = Guid.NewGuid() }] }); break;
            case "root-binding": inputs.WriteRuntime(inputs.Runtime with { PreparedRoots = [inputs.Runtime.PreparedRoots[0] with { RootBindingIdentity = "foreign-root" }] }); break;
            case "source-root": inputs.WriteRuntime(inputs.Runtime with { PreparedRoots = [inputs.Runtime.PreparedRoots[0] with { PhysicalRootPath = Path.Combine(inputs.OwnedRoot, "other-source") }] }); break;
            case "stage": inputs.WritePolicy(inputs.Policy with { OutputStage = "cl-and-bl" }); break;
            case "cap": inputs.WritePolicy(inputs.Policy with { WorkerCap = 2 }); break;
            case "requests": inputs.WritePolicy(inputs.Policy with { MaximumProviderRequests = inputs.Policy.MaximumProviderRequests + 1 }); break;
            case "tokens": inputs.WritePolicy(inputs.Policy with { MaximumTotalTokens = inputs.Policy.MaximumTotalTokens + 1 }); break;
            case "file-scope": inputs.WritePolicy(inputs.Policy with { SourceRules = [new(inputs.Repository, "src", "selected", "broadened source directory")] }); break;
            case "provider": inputs.WriteProvider(inputs.Profile with { Model = "unapproved-model" }); break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => inputs.ValidateAsync());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenerateDispatch_IsConsumedOnceEvenFailureOrUnknownOutcomeAsync(bool throws)
    {
        using var inputs = new Inputs();
        int dispatches = 0;
        var adapter = inputs.Adapter(_ =>
        {
            dispatches++;
            return throws ? Task.FromException<(int, string, string)>(new IOException("controlled lost acknowledgement")) : Task.FromResult((4, "controlled-unknown-report", ""));
        });
        if (throws) await Assert.ThrowsAsync<IOException>(() => adapter.ExecuteOnceAsync());
        else Assert.Equal(4, (await adapter.ExecuteOnceAsync()).ExitCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.ExecuteOnceAsync());
        Assert.Equal(1, dispatches);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task InvalidInput_PreventsEvenControlledDispatchAsync()
    {
        using var inputs = new Inputs();
        inputs.WritePolicy(inputs.Policy with { WorkerCap = 2 });
        int dispatches = 0;
        var adapter = inputs.Adapter(_ => { dispatches++; return Task.FromResult((0, "unexpected", "")); });
        await Assert.ThrowsAnyAsync<Exception>(() => adapter.ExecuteOnceAsync());
        Assert.Equal(0, dispatches);
    }

    private sealed class Inputs : IDisposable
    {
        internal string OwnedRoot { get; } = Path.Combine(Path.GetTempPath(), "october-qualification-binding-" + Guid.NewGuid().ToString("N"));
        internal string SourceRoot => Path.Combine(OwnedRoot, "unopened-source");
        internal Guid Repository { get; } = Guid.NewGuid();
        private const string RelativeFile = "src/Login.cs";
        internal OctoberSetupRequest Request { get; }
        internal OctoberRuntimeConfiguration Runtime { get; }
        internal OctoberScanPolicy Policy { get; }
        internal OctoberProviderProfile Profile { get; } = new("approved-worker", ApiType.ChatCompletions, "https://never-contacted.invalid/v1", "approved-model", "credential://explicit-approved");
        internal OctoberGenerateInputFiles Files { get; }
        internal Inputs()
        {
            Directory.CreateDirectory(OwnedRoot);
            Request = new("exact-target", Guid.NewGuid(), [new(Repository, null, "exact-root-binding", "owned input")], [], new string('a', 64));
            Runtime = new([new(Request.TargetId, Request.BatchId, Repository, "exact-root-binding", SourceRoot)],
                Path.Combine(OwnedRoot, "tools", "codegraph.ps1"),
                new(Path.Combine(OwnedRoot, "state"), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(15), 2 * 1024 * 1024),
                new([Path.Combine(OwnedRoot, "tools")], [Path.Combine(OwnedRoot, "runtime")], Path.Combine(OwnedRoot, "system", "cmd.exe"), [Path.Combine(OwnedRoot, "tools")], Path.Combine(OwnedRoot, "jdk", "bin")),
                Path.Combine(OwnedRoot, "log"));
            Policy = new() { OutputStage = "cl-only", WorkerCap = 1, MaximumProviderRequests = 8, MaximumTotalTokens = 65536,
                SourceRules = [new(Repository, RelativeFile, "selected", "approved single file")] };
            Files = new(Path.Combine(OwnedRoot, "runtime.json"), Path.Combine(OwnedRoot, "policy.json"), Path.Combine(OwnedRoot, "provider.json"));
            WriteRuntime(Runtime); WritePolicy(Policy); WriteProvider(Profile);
        }
        internal void WriteRuntime(OctoberRuntimeConfiguration runtime) => File.WriteAllText(Files.RuntimeFilePath, JsonSerializer.Serialize(runtime, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        internal void WritePolicy(OctoberScanPolicy policy) => File.WriteAllText(Files.ScanPolicyFilePath, JsonSerializer.Serialize(policy, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        internal void WriteProvider(OctoberProviderProfile profile) => File.WriteAllText(Files.WorkerProviderConfigurationFilePath, JsonSerializer.Serialize(new
        {
            sourceIdentity = "owned-generate-worker", precedence = "only-source", workerProfile = profile.Name,
            profiles = new[] { new { name = profile.Name, apiType = "chat-completions", endpoint = profile.Endpoint, model = profile.Model, credentialReference = profile.CredentialReference } },
            protectedCredentials = new Dictionary<string, string> { [profile.CredentialReference] = "synthetic-protected-cipher-no-decryption" },
        }));
        internal Task ValidateAsync() => OctoberScanGenerateQualificationAdapter.ValidateAsync(OwnedRoot, SourceRoot, Request, Profile, Policy, RelativeFile, Files);
        internal OctoberScanGenerateQualificationAdapter Adapter(Func<CancellationToken, Task<(int, string, string)>> dispatch) => new(OwnedRoot, SourceRoot, Request, Profile, Policy, RelativeFile, Files, dispatch);
        public void Dispose() => Directory.Delete(OwnedRoot, true);
    }
}
