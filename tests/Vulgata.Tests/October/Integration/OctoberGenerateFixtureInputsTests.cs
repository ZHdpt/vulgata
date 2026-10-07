using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.Shared;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Integration;

public sealed class OctoberGenerateFixtureInputsTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ValidateFiles_NoGenerateInputs_PreservesExistingFixtureModes()
    {
        using var files = new OwnedFiles();
        foreach (string mode in new[] { "prepare", "select", "generate" })
            Assert.Null(OctoberGenerateFixtureInputs.ValidateFiles(files.Root, mode, null, null, null, true));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ValidateFiles_ExactOwnedGenerateTuple_ReturnsAllThreeExistingPaths()
    {
        using var files = new OwnedFiles();
        var result = OctoberGenerateFixtureInputs.ValidateFiles(files.Root, "generate", files.Runtime, files.Policy, files.Provider, false);
        Assert.NotNull(result);
        Assert.Equal(files.Runtime, result.RuntimeFilePath);
        Assert.Equal(files.Policy, result.ScanPolicyFilePath);
        Assert.Equal(files.Provider, result.WorkerProviderConfigurationFilePath);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("mode")]
    [InlineData("case")]
    [InlineData("partial")]
    [InlineData("override")]
    [InlineData("relative")]
    [InlineData("outside")]
    [InlineData("missing")]
    [InlineData("directory")]
    [InlineData("traversal")]
    public void ValidateFiles_InvalidTuple_RefusesRatherThanUsingAmbientInputs(string invalid)
    {
        using var files = new OwnedFiles();
        string mode = invalid == "mode" ? "prepare" : invalid == "case" ? "Generate" : "generate";
        string? runtime = invalid switch
        {
            "partial" => null,
            "relative" => "runtime.json",
            "outside" => files.Outside,
            "missing" => Path.Combine(files.Root, "missing.json"),
            "directory" => files.Root,
            "traversal" => Path.Combine(files.Root, "nested", "..", "runtime.json"),
            _ => files.Runtime
        };
        Assert.Throws<ArgumentException>(() => OctoberGenerateFixtureInputs.ValidateFiles(files.Root, mode, runtime, files.Policy, files.Provider, invalid == "override"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task WriteWorkerProviderAsync_ApprovedProfile_WritesDistinctProtectedWorkerOnlyDocumentAsync()
    {
        using var files = new OwnedFiles();
        var protection = new EphemeralDataProtectionProvider();
        const string plaintext = "synthetic-approved-worker-secret";
        int reads = 0;
        var profile = Profile();
        async Task<string> WriteAsync() => await OctoberGenerateFixtureInputs.WriteWorkerProviderAsync(files.Root, protection, profile,
            _ => { reads++; return Task.FromResult(plaintext); });
        string first = await WriteAsync();
        string second = await WriteAsync();
        Assert.NotEqual(first, second);
        Assert.Equal(2, reads);
        foreach (string path in new[] { first, second })
        {
            Assert.True(Path.IsPathFullyQualified(path));
            Assert.StartsWith(Path.GetFullPath(files.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
            string json = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain(plaintext, json, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Assert.Equal("only-source", root.GetProperty("precedence").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("sourceIdentity").GetString()));
            Assert.Equal(profile.Name, root.GetProperty("workerProfile").GetString());
            Assert.False(root.TryGetProperty("chatProfile", out var chat) && !string.IsNullOrEmpty(chat.GetString()));
            var worker = Assert.Single(root.GetProperty("profiles").EnumerateArray());
            Assert.Equal(profile.Name, worker.GetProperty("name").GetString());
            Assert.Equal("chat-completions", worker.GetProperty("apiType").GetString());
            Assert.Equal(profile.Endpoint, worker.GetProperty("endpoint").GetString());
            Assert.Equal(profile.Model, worker.GetProperty("model").GetString());
            Assert.Equal(profile.CredentialReference, worker.GetProperty("credentialReference").GetString());
            var credential = Assert.Single(root.GetProperty("protectedCredentials").EnumerateObject());
            Assert.Equal(profile.CredentialReference, credential.Name);
            Assert.Equal(plaintext, protection.CreateProtector(SecretPurposes.LlmApiKey).Unprotect(credential.Value.GetString()!));
        }
        Assert.Equal("{}", await File.ReadAllTextAsync(files.Provider));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("name")]
    [InlineData("model")]
    [InlineData("reference")]
    [InlineData("api")]
    [InlineData("userinfo")]
    [InlineData("query")]
    [InlineData("fragment")]
    [InlineData("scheme")]
    public async Task WriteWorkerProviderAsync_InvalidProfile_RefusesBeforeCredentialCallbackAsync(string invalid)
    {
        using var files = new OwnedFiles();
        var profile = Profile();
        profile = invalid switch
        {
            "name" => profile with { Name = " " },
            "model" => profile with { Model = " " },
            "reference" => profile with { CredentialReference = " " },
            "api" => profile with { ApiType = ApiType.ChatCompletions | ApiType.Responses },
            "userinfo" => profile with { Endpoint = "https://user:password@controlled.invalid/v1" },
            "query" => profile with { Endpoint = "https://controlled.invalid/v1?key=hidden" },
            "fragment" => profile with { Endpoint = "https://controlled.invalid/v1#hidden" },
            _ => profile with { Endpoint = "ftp://controlled.invalid/v1" }
        };
        int reads = 0;
        await Assert.ThrowsAnyAsync<ArgumentException>(() => OctoberGenerateFixtureInputs.WriteWorkerProviderAsync(files.Root,
            new EphemeralDataProtectionProvider(), profile, _ => { reads++; return Task.FromResult("synthetic-key"); }));
        Assert.Equal(0, reads);
        Assert.Equal(3, Directory.GetFiles(files.Root).Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task WriteWorkerProviderAsync_PreCancelled_DoesNotReadCredentialOrWriteFileAsync()
    {
        using var files = new OwnedFiles();
        int reads = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => OctoberGenerateFixtureInputs.WriteWorkerProviderAsync(files.Root,
            new EphemeralDataProtectionProvider(), Profile(), _ => { reads++; return Task.FromResult("synthetic-key"); }, new CancellationToken(true)));
        Assert.Equal(0, reads);
        Assert.Equal(3, Directory.GetFiles(files.Root).Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task WriteWorkerProviderAsync_CancelledIgnoredCredentialRead_NeverWritesLateResultAsync()
    {
        using var files = new OwnedFiles();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        var pending = OctoberGenerateFixtureInputs.WriteWorkerProviderAsync(files.Root, new EphemeralDataProtectionProvider(), Profile(),
            _ => { reads++; entered.TrySetResult(); return release.Task; }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        release.SetResult("synthetic-late-key");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, reads);
        Assert.Equal(3, Directory.GetFiles(files.Root).Length);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteWorkerProviderAsync_CredentialFailure_DoesNotPersistOrExposeErrorContentAsync(bool empty)
    {
        using var files = new OwnedFiles();
        const string secret = "synthetic-secret-in-provider-error";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => OctoberGenerateFixtureInputs.WriteWorkerProviderAsync(files.Root,
            new EphemeralDataProtectionProvider(), Profile(), _ => empty ? Task.FromResult(" ") : Task.FromException<string>(new IOException(secret))));
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        Assert.Equal(3, Directory.GetFiles(files.Root).Length);
    }

    private static OctoberProviderProfile Profile() => new("approved-worker", ApiType.ChatCompletions,
        "https://controlled.invalid/v1", "approved-model", "approved-worker-key");

    private sealed class OwnedFiles : IDisposable
    {
        private readonly string _container = Path.Combine(Path.GetTempPath(), "vulgata-generate-input-tests-" + Guid.NewGuid().ToString("N"));
        public string Root { get; }
        public string Runtime => Path.Combine(Root, "runtime.json");
        public string Policy => Path.Combine(Root, "policy.json");
        public string Provider => Path.Combine(Root, "provider.json");
        public string Outside => Path.Combine(_container, "outside.json");
        public OwnedFiles()
        {
            Root = Path.Combine(_container, "owned");
            Directory.CreateDirectory(Path.Combine(Root, "nested"));
            foreach (string path in new[] { Runtime, Policy, Provider, Outside }) File.WriteAllText(path, "{}");
        }
        public void Dispose() => Directory.Delete(_container, true);
    }
}
