using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Configuration;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Controlled configuration/session behavior only; no host, provider, database or native process starts.</summary>
public sealed class OctoberScanGenerateConfigurationTests
{
    private const string Target = "owned-october-target";
    private static readonly Guid Batch = Guid.Parse("adf58c60-c81e-4240-8e28-fcfeab7a11cd");

    // Model constraint: outputStage explicitly cl-only or cl-and-bl; no default/change/downgrade;
    // trusted setup and finite budgets.
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("prepare", OctoberGeneratorMode.Prepare)]
    [InlineData("select", OctoberGeneratorMode.Select)]
    public void ReadCommand_ExistingModes_PreservesExactSelectionWithoutScanPolicy(string mode, OctoberGeneratorMode expected)
    {
        var values = CommandValues(mode);
        if (expected == OctoberGeneratorMode.Prepare)
            values["October:SetupRequestJson"] = JsonSerializer.Serialize(new OctoberSetupRequest(Target, Batch, [], [], string.Empty));
        var command = InvokeEntryPoint<OctoberGeneratorCommand>("ReadCommand", Configuration(values));
        Assert.Equal(expected, command.Mode);
        Assert.Equal(Target, command.TargetId);
        Assert.Equal(Batch, command.BatchId);
        Assert.Equal(expected == OctoberGeneratorMode.Prepare, command.SetupRequest is not null);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadProviderConfiguration_MissingExplicitDocument_DoesNotUseAmbientCompatibleKeysAsync()
    {
        var configuration = Configuration(new()
        {
            ["OpenAI:ApiKey"] = "synthetic-not-a-real-secret",
            ["OpenAI:Model"] = "ambient-model",
            ["LlmProviders:DefaultProvider"] = "ambient-profile",
        });
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeEntryPoint<Task<OctoberProviderConfiguration>>("ReadProviderConfigurationAsync", configuration));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            OctoberGeneratorEntryPoint.ReadGenerateProviderConfigurationAsync(configuration));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-profile", OctoberProviderBindingStatus.InvalidConfiguration)]
    [InlineData("missing-credential", OctoberProviderBindingStatus.InvalidConfiguration)]
    [InlineData("decrypt-failure", OctoberProviderBindingStatus.CredentialUnavailable)]
    public async Task BindProvider_SelectedFailure_DoesNotSwitchToHealthyUnselectedProfileAsync(string fault, OctoberProviderBindingStatus expected)
    {
        var selected = new OctoberProviderProfile("selected", ApiType.ChatCompletions,
            "https://selected.example.test/v1", "selected-model", "credential://selected");
        var other = new OctoberProviderProfile("healthy-unselected", ApiType.ChatCompletions,
            "https://other.example.test/v1", "other-model", "credential://other");
        var encrypted = new Dictionary<string, string> { [other.CredentialReference] = "synthetic-other-cipher" };
        if (fault != "missing-credential") encrypted[selected.CredentialReference] = "synthetic-selected-cipher";
        var configuration = new OctoberProviderConfiguration("explicit-document", "only-source",
            fault == "missing-profile" ? "absent" : selected.Name, selected.Name, [selected, other], encrypted);
        var secrets = new SelectedSecrets();
        var result = await new OctoberProviderBinder(secrets).BindAsync(configuration);
        Assert.Equal(expected, result.Status);
        Assert.Null(result.Binding);
        Assert.DoesNotContain("synthetic-other-cipher", secrets.Decryptions);
        Assert.Equal(fault == "decrypt-failure" ? 1 : 0, secrets.Decryptions.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("cl-only")]
    [InlineData("cl-and-bl")]
    public void ReadCommand_Generate_UsesExplicitPolicyStageAndFiniteDefaults(string stage)
    {
        using var policyFile = new PolicyFile(PolicyJson(stage));
        var values = CommandValues("generate");
        values["October:ScanPolicyFile"] = policyFile.Path;
        var command = InvokeEntryPoint<OctoberGeneratorCommand>("ReadCommand", Configuration(values));
        Assert.Equal(OctoberGeneratorMode.Generate, command.Mode);
        Assert.Null(command.SetupRequest);
        var policy = Assert.IsType<OctoberScanPolicy>(command.ScanPolicy);
        Assert.Equal(stage, policy.OutputStage);
        Assert.Equal(12000L, policy.MaximumTotalTokens);
        Assert.Equal(1, policy.WorkerCap);
        Assert.Equal(1800, policy.RunDeadlineSeconds);
        Assert.Equal(600, policy.FileDeadlineSeconds);
        Assert.Equal(4, policy.MaximumContinuationWindows);
        Assert.Equal(8, policy.MaximumModelRequestsPerFile);
        Assert.Equal(2, policy.MaximumTransientRetries);
        Assert.Equal(30, policy.GracefulDrainSeconds);
        Assert.True(policy.MaximumProviderRequests > 0);
        Assert.True(policy.MaximumRequestsPerMinute > 0);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("{\"maximumTotalTokens\":12000}")]
    [InlineData("{\"outputStage\":\"auto\",\"maximumTotalTokens\":12000}")]
    [InlineData("{\"outputStage\":\"cl-only\"}")]
    [InlineData("{\"outputStage\":\"cl-only\",\"outputStage\":\"cl-and-bl\",\"maximumTotalTokens\":12000}")]
    public void ReadCommand_GenerateMissingAmbiguousOrUnboundedPolicy_FailsBeforeExecution(string json)
    {
        using var policyFile = new PolicyFile(json[..^1] + ",\"sourceRules\":" + JsonSerializer.Serialize(ValidPolicy().SourceRules, new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "}");
        var values = CommandValues("generate");
        values["October:ScanPolicyFile"] = policyFile.Path;
        Assert.Throws<InvalidOperationException>(() => InvokeEntryPoint<OctoberGeneratorCommand>("ReadCommand", Configuration(values)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ReadCommand_ConflictingStageOutsidePolicy_DoesNotOverrideExplicitStage()
    {
        using var policyFile = new PolicyFile(PolicyJson("cl-and-bl"));
        var values = CommandValues("generate");
        values["October:ScanPolicyFile"] = policyFile.Path;
        values["October:OutputStage"] = "cl-only";
        Assert.Throws<InvalidOperationException>(() => InvokeEntryPoint<OctoberGeneratorCommand>("ReadCommand", Configuration(values)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("zero-cap")]
    [InlineData("negative-deadline")]
    [InlineData("file-exceeds-run")]
    [InlineData("zero-requests")]
    [InlineData("negative-retries")]
    [InlineData("zero-token-ceiling")]
    public void ValidatePolicy_NonFiniteOrInconsistentBounds_Rejects(string fault)
    {
        var policy = ValidPolicy();
        policy = fault switch
        {
            "zero-cap" => policy with { WorkerCap = 0 },
            "negative-deadline" => policy with { RunDeadlineSeconds = -1 },
            "file-exceeds-run" => policy with { FileDeadlineSeconds = 1801 },
            "zero-requests" => policy with { MaximumProviderRequests = 0 },
            "negative-retries" => policy with { MaximumTransientRetries = -1 },
            "zero-token-ceiling" => policy with { MaximumTotalTokens = 0 },
            _ => throw new InvalidOperationException(),
        };
        Assert.False(new OctoberScanPolicyValidator().Validate(policy).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("cl-only")]
    [InlineData("cl-and-bl")]
    public async Task ExecuteAsync_Generate_HoldsOwnSessionThroughExactSelectedOperationAsync(string stage)
    {
        var authority = new ControlledAuthority();
        var target = new ControlledTarget(authority);
        var runner = new ControlledGenerate();
        var provider = await BoundProviderAsync();
        var policy = ValidPolicy() with { OutputStage = stage };
        var module = new OctoberGeneratorModule(authority, target, "management", runner, provider);
        var pending = module.ExecuteAsync(new(OctoberGeneratorMode.Generate, Target, Batch, null, policy));
        try
        {
            var execution = await runner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(pending.IsCompleted);
            Assert.False(authority.Session.Disposed);
            Assert.Same(authority.Session, execution.Session);
            Assert.Same(target.Bound, execution.Setup);
            Assert.Equal(Target, execution.Setup.TargetId);
            Assert.Equal(Batch, execution.Setup.BatchId);
            Assert.Equal("database-exact", execution.Authority.CanonicalDatabaseIdentity);
            Assert.Equal("root-version-7", Assert.Single(execution.Setup.Snapshot.Request.Repositories).RootBindingIdentity);
            Assert.Equal("operator advisory, not source proof", Assert.Single(execution.Authority.SelectedContext!.Entries).Value);
            Assert.Equal(OctoberContextEvidenceKind.AdvisoryNotSourceProof, Assert.Single(execution.Authority.SelectedContext.Entries).EvidenceKind);
            Assert.Same(policy, execution.Policy);
            Assert.Equal(stage, execution.Policy.OutputStage);
            Assert.Same(provider, execution.Provider);
            Assert.Equal(1, authority.Authentications);
            Assert.Equal(OctoberGeneratorMode.Select, Assert.Single(target.Commands).Mode);
        }
        finally { runner.Release.TrySetResult(new(OctoberGeneratorExecutionStatus.Selected)); }
        Assert.Equal(OctoberGeneratorExecutionStatus.Selected, (await pending).Status);
        Assert.True(authority.Session.Disposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("batch")]
    [InlineData("database")]
    [InlineData("root")]
    [InlineData("advisory")]
    public async Task ExecuteAsync_GenerateReboundSnapshotMismatch_DeniesBeforeOperationAsync(string mismatch)
    {
        var authority = new ControlledAuthority();
        var target = new ControlledTarget(authority);
        var snapshot = target.Bound.Snapshot;
        snapshot = mismatch switch
        {
            "batch" => snapshot with { Request = snapshot.Request with { BatchId = Guid.NewGuid() } },
            "database" => snapshot with { Authority = snapshot.Authority with { CanonicalDatabaseIdentity = "foreign-database" } },
            "root" => snapshot with { Request = snapshot.Request with { Repositories = [snapshot.Request.Repositories[0] with { RootBindingIdentity = "replaced-root" }] } },
            "advisory" => snapshot with { Authority = snapshot.Authority with { SelectedContext = snapshot.Authority.SelectedContext! with { Entries = [snapshot.Authority.SelectedContext.Entries[0] with { Value = "changed advisory" }] } } },
            _ => throw new InvalidOperationException(),
        };
        target.Bound = target.Bound with { Snapshot = snapshot };
        var runner = new ControlledGenerate();
        runner.Release.TrySetResult(new(OctoberGeneratorExecutionStatus.Selected));
        var module = new OctoberGeneratorModule(authority, target, "management", runner, await BoundProviderAsync());
        var result = await module.ExecuteAsync(new(OctoberGeneratorMode.Generate, Target, Batch, null, ValidPolicy()));
        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.False(runner.Entered.Task.IsCompleted);
        Assert.True(authority.Session.Disposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ExecuteAsync_GenerateMissingBoundProvider_DoesNotDispatchAsync()
    {
        var authority = new ControlledAuthority();
        var runner = new ControlledGenerate();
        runner.Release.TrySetResult(new(OctoberGeneratorExecutionStatus.Selected));
        var module = new OctoberGeneratorModule(authority, new ControlledTarget(authority), "management", runner);
        var result = await module.ExecuteAsync(new(OctoberGeneratorMode.Generate, Target, Batch, null, ValidPolicy()));
        Assert.Equal(OctoberGeneratorExecutionStatus.Denied, result.Status);
        Assert.False(runner.Entered.Task.IsCompleted);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void ValidatePolicy_ExplicitScopeStageAndCeiling_AcceptsWithoutInventingSelection()
    {
        var validator = new OctoberScanPolicyValidator();
        Assert.True(validator.Validate(ValidPolicy()).IsValid);
        Assert.False(validator.Validate(ValidPolicy() with { SourceRules = [] }).IsValid);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_GenerateFailsOrCancels_DisposesItsSessionAfterOperationSettlementAsync(bool cancel)
    {
        var authority = new ControlledAuthority();
        var runner = new ControlledGenerate();
        using var cancellation = new CancellationTokenSource();
        var module = new OctoberGeneratorModule(authority, new ControlledTarget(authority), "management", runner, await BoundProviderAsync());
        var pending = module.ExecuteAsync(new(OctoberGeneratorMode.Generate, Target, Batch, null, ValidPolicy()), cancellation.Token);
        try
        {
            await runner.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(authority.Session.Disposed);
            if (cancel) cancellation.Cancel();
            else runner.Release.TrySetException(new InvalidOperationException("controlled-operation-failure"));
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(cancel ? OctoberGeneratorExecutionStatus.Cancelled : OctoberGeneratorExecutionStatus.TargetUnavailable, result.Status);
            Assert.True(authority.Session.Disposed);
        }
        finally
        {
            runner.Release.TrySetResult(new(OctoberGeneratorExecutionStatus.Denied));
            cancellation.Cancel();
            await pending;
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadGenerateProvider_WorkerOnlyDocument_BindsWithoutChatOrBusinessProfilesAsync()
    {
        using var document = new PolicyFile(JsonSerializer.Serialize(new
        {
            sourceIdentity = "explicit-worker-only", precedence = "only-source", workerProfile = "worker",
            profiles = new[] { new { name = "worker", apiType = "chat-completions", endpoint = "https://owned.example.test/v1", model = "exact-model", credentialReference = "credential://worker" } },
            protectedCredentials = new Dictionary<string, string> { ["credential://worker"] = "synthetic-bound-cipher" },
        }));
        var configuration = Configuration(new() { ["October:Providers:ConfigurationFile"] = document.Path });
        var selected = await OctoberGeneratorEntryPoint.ReadGenerateProviderConfigurationAsync(configuration);
        Assert.Empty(selected.ChatProfile);
        var secrets = new SelectedSecrets();
        var binding = await OctoberGeneratorEntryPoint.BindWorkerProviderAsync(selected, secrets);
        Assert.NotNull(binding);
        Assert.Equal("worker", binding.Profile.Name);
        Assert.Equal("explicit-worker-only", binding.SourceIdentity);
        Assert.Equal("only-source", binding.Precedence);
        Assert.Equal("synthetic-bound-cipher", Assert.Single(secrets.Decryptions));
        Assert.DoesNotContain("synthetic-bound-cipher", binding.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-unselected-plaintext", binding.ToString(), StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-profile")]
    [InlineData("missing-credential")]
    [InlineData("decrypt-failure")]
    public async Task BindWorkerProvider_SelectedFailure_DoesNotUseAnotherRoleOrProfileAsync(string fault)
    {
        var selected = new OctoberProviderProfile("selected", ApiType.ChatCompletions, "https://owned.example.test/v1", "selected-model", "credential://selected");
        var other = selected with { Name = "healthy-unselected", CredentialReference = "credential://other" };
        var encrypted = new Dictionary<string, string> { [other.CredentialReference] = "synthetic-other-cipher" };
        if (fault != "missing-credential") encrypted[selected.CredentialReference] = "synthetic-selected-cipher";
        var secrets = new SelectedSecrets();
        var binding = await OctoberGeneratorEntryPoint.BindWorkerProviderAsync(new(
            "explicit-document", "only-source", fault == "missing-profile" ? "absent" : selected.Name,
            other.Name, [selected, other], encrypted), secrets);
        Assert.Null(binding);
        Assert.DoesNotContain("synthetic-other-cipher", secrets.Decryptions);
        Assert.Equal(fault == "decrypt-failure" ? 1 : 0, secrets.Decryptions.Count);
    }

    private static OctoberScanPolicy ValidPolicy() => new()
    {
        OutputStage = "cl-only", MaximumTotalTokens = 12000,
        SourceRules = [new(Guid.Parse("6484b3b9-1f54-4876-8d6b-35c9b5d66a6e"), "src", "selected", "Explicit approved source scope")],
    };

    private static string PolicyJson(string stage) => JsonSerializer.Serialize(
        ValidPolicy() with { OutputStage = stage }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static Task<OctoberSelectedWorkerProvider> BoundProviderAsync() => Task.FromResult(
        new OctoberSelectedWorkerProvider("explicit-provider-document", "only-source",
            new OctoberProviderProfile("exact-worker", ApiType.ChatCompletions, "https://owned.example.test/v1", "exact-model", "credential://owned"),
            "synthetic-bound-cipher"));

    private sealed class PolicyFile : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vulgata-october-policy-" + Guid.NewGuid().ToString("N"));
        internal PolicyFile(string json)
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "scan-policy.json");
            File.WriteAllText(Path, json);
        }
        internal string Path { get; }
        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private sealed class ControlledSession : IOctoberOperatorSession
    {
        public string SubjectId => "operator";
        public string ManagementAuthorityId => "management";
        public DateTimeOffset ExpiresAtUtc => DateTimeOffset.UtcNow.AddHours(1);
        internal bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class ControlledAuthority : IOctoberLocalAuthority
    {
        internal ControlledSession Session { get; } = new();
        internal int Authentications { get; private set; }
        internal OctoberSetupRequest Request { get; }
        internal OctoberAuthorizedSelection Selection { get; }
        internal ControlledAuthority()
        {
            var repository = new OctoberRepositorySelection(Guid.Parse("6484b3b9-1f54-4876-8d6b-35c9b5d66a6e"), null, "root-version-7", "owned source");
            var context = new OctoberSelectedAdvisoryContext("management", [repository],
                [new(repository.RepositoryId, OctoberContextScope.Repository, OctoberContextApplicability.Selected, repository.RepositoryId, "operator advisory, not source proof", OctoberContextEvidenceKind.AdvisoryNotSourceProof)], []);
            var request = new OctoberSetupRequest(Target, Batch, [repository], [new(OctoberContextScope.Repository, repository.RepositoryId)], string.Empty);
            var digest = OctoberSelectionDigest.Compute(request, context);
            Request = request with { SelectionDigest = digest };
            Selection = new(Target, Batch, "operator", "database-exact", [repository.RepositoryId], digest, context);
        }
        public Task<OctoberAuthenticationResult> AuthenticateAsync(CancellationToken token)
        {
            Authentications++;
            return Task.FromResult(new OctoberAuthenticationResult(OctoberAuthenticationStatus.Authenticated, Session));
        }
        public Task<OctoberAuthoritySelectionResult> AuthorizeSelectionAsync(IOctoberOperatorSession session, OctoberAuthoritySelectionRequest request, CancellationToken token)
        {
            Assert.Same(Session, session);
            Assert.False(Session.Disposed);
            Assert.Equal(Target, request.TargetId);
            Assert.Equal(Batch, request.BatchId);
            return Task.FromResult(new OctoberAuthoritySelectionResult(OctoberAuthorityDecisionStatus.Authorized, Request, Selection));
        }
        public Task<OctoberSourceApprovalResult> IssueSourceApprovalAsync(IOctoberOperatorSession session, IOctoberHistoricalBoundSetupData setup, Guid repository, CancellationToken token) => throw new NotSupportedException();
        public Task<PreparedSourceAuthorityResult> VerifySourceApprovalAsync(IOctoberOperatorSession session, string target, PreparedSourceApproval approval, CancellationToken token) => throw new NotSupportedException();
        public Task<OctoberRevocationStatus> RevokeSourceApprovalAsync(IOctoberOperatorSession session, string target, string approval, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed record BoundData(string TargetId, Guid BatchId, OctoberBoundSetupSnapshot Snapshot) : IOctoberHistoricalBoundSetupData;
    private sealed class ControlledTarget(ControlledAuthority authority) : IOctoberConfiguredTarget
    {
        internal BoundData Bound { get; set; } = new(Target, Batch, new(authority.Request, authority.Selection, DateTimeOffset.UtcNow));
        internal List<OctoberGeneratorCommand> Commands { get; } = [];
        public Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGeneratorCommand command, OctoberAuthorizedSelection selection, CancellationToken token = default)
        {
            Assert.False(authority.Session.Disposed);
            Commands.Add(command);
            return Task.FromResult(new OctoberGeneratorExecutionResult(OctoberGeneratorExecutionStatus.Selected, BoundSetupData: Bound));
        }
    }
    private sealed class ControlledGenerate : IOctoberGenerateOperation
    {
        internal TaskCompletionSource<OctoberGenerateExecution> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<OctoberGeneratorExecutionResult> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<OctoberGeneratorExecutionResult> ExecuteAsync(OctoberGenerateExecution execution, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult(execution);
            return await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private static Dictionary<string, string?> CommandValues(string mode) => new()
    {
        ["October:Mode"] = mode,
        ["October:TargetId"] = Target,
        ["October:BatchId"] = Batch.ToString("D"),
    };

    private static IConfigurationRoot Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static T InvokeEntryPoint<T>(string method, object argument)
    {
        var member = typeof(OctoberGeneratorEntryPoint).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(member);
        try { return (T)member.Invoke(null, [argument])!; }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private sealed class SelectedSecrets : ISecretEncryptionService
    {
        internal List<string> Decryptions { get; } = [];
        public Task<string> EncryptAsync(string purpose, string plain, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<string> DecryptAsync(string purpose, string cipher, CancellationToken cancellationToken = default)
        {
            Decryptions.Add(cipher);
            Assert.Equal("Vulgata.LlmProviders.ApiKey", purpose);
            return cipher == "synthetic-selected-cipher"
                ? Task.FromException<string>(new InvalidOperationException("Controlled selected credential failure"))
                : Task.FromResult("synthetic-unselected-plaintext");
        }
    }
}
