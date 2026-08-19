using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Analysis.Java;

namespace Vulgata.Tests.AnalysisRuntime;

public sealed class JdtLsAdapterTests
{
    private static readonly Guid _repositoryId =
        Guid.Parse("12000000-0000-4000-8000-000000000012");

    private const string ProfiledRevision =
        "abcdef0123456789abcdef0123456789abcdef01";

    private const string NpmShimHash =
        "80419260f06862d7a422d13ef69e3b24c7a21dbef4309bce41db02ad642b1877";

    private const string NodeHash =
        "b3094d0b49f9ad602262a9921551737bb97637c05dd357a06ae98188d7290aa3";

    private const string CodeGraphHash =
        "03e4c791cc0dd91ed264278461bf9a56c0278aa0670d5942fc4732311c66de03";

    [Fact]
    public void JavaSession_PublicLifecycle_ProvidesAsyncDisposalForShutdownAndCleanup()
    {
        Assert.True(
            typeof(IAsyncDisposable).IsAssignableFrom(typeof(IJavaAnalysisSession)),
            "The accepted JDT-LS session seam must be asynchronously disposable so shutdown, " +
            "exit, stream closure, bounded wait, and forced process-tree cleanup remain observable.");
    }

    [Fact]
    public void JdtLsIdentity_MissingArchiveOrCoreJarSubstitution_RemainsFailClosed()
    {
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveName: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(jdtLsDistributionArchiveSha256: null));
        Assert.Throws<ArgumentException>(() => CreateIdentity(
            jdtLsDistributionArchiveSha256:
                "a58994113b7e493aa8ced51ad02dab3e424973223e7213f7ed5b9d70861582e2"));

        AnalysisRuntimeHealthResult result = AnalysisRuntimeHealthResult.NotReady(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            "JDT distribution archive checksum is not configured; root=C:\\Users\\13728\\.local");

        Assert.Equal(AnalysisRuntimeHealthStatus.NotReady, result.Status);
        Assert.Equal(
            AnalysisRuntimeHealthFailureCategory.JdtDistributionArchiveChecksumMissing,
            result.FailureCategory);
        Assert.Null(result.Identity);
        Assert.Null(result.IdentityHash);
        Assert.DoesNotContain("13728", result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain('\\', result.Diagnostic);
        Assert.DoesNotContain('/', result.Diagnostic);
        Assert.Equal("jdt_distribution_archive_checksum_missing", result.FailureCode);
    }

    [Fact]
    public async Task JdtLsAdapter_ClosedStdioLifecycle_BindsExactRuntimeAndRevisionWorkspace()
    {
        AnalysisRuntimeIdentity identity = CreateIdentity();

        Assert.Equal("jdt-language-server-1.61.0-202608141332", identity.JdtLsReleaseIdentity);
        Assert.Equal(21, identity.JavaMajorVersion);
        Assert.Equal("StreamJsonRpc", identity.ClientName);
        Assert.Equal("2.25.29", identity.ClientVersion);
        Assert.Equal("stdio", identity.Transport);
        Assert.Equal(_repositoryId, identity.RepositoryId);
        Assert.Equal(ProfiledRevision, identity.ProfiledRevision);

        string workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story121-jdt-" + Guid.NewGuid().ToString("N"));
        string workspacePath = Path.Combine(
            workspaceRoot,
            _repositoryId.ToString("N"),
            ProfiledRevision);
        JdtLsWorkspaceBinding binding = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000013"),
            ProfiledRevision,
            "fixture-binding",
            workspacePath);
        RecordingWorkspaceLeaseProvider leases = new(binding);
        RecordingProcess process = new() { ForceWaitTimeout = true };
        RecordingProcessLauncher launcher = new(process);
        RecordingProtocolClient protocol = new();
        RecordingProtocolClientFactory protocols = new(protocol);
        JdtLsAdapterOptions options = new(
            JavaExecutablePath: @"C:\Program Files\Java\latest\jdk-21\bin\java.exe",
            InstallationRoot:
                @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
            WorkspaceRoot: workspaceRoot,
            InitializationTimeout: TimeSpan.FromSeconds(2),
            RequestTimeout: TimeSpan.FromSeconds(2),
            ShutdownTimeout: TimeSpan.FromSeconds(2),
            ResponseByteLimit: 16_384);

        IJavaAnalysisSessionFactory factory = new JdtLsAnalysisSessionFactory(
            options,
            leases,
            launcher,
            protocols);
        AnalysisScope scope = new(
            _repositoryId,
            binding.ScanRunId,
            ProfiledRevision,
            binding.WorkingCopyBindingToken,
            identity.Sha256,
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        AnalysisOperationBounds bounds = new(itemLimit: 8, byteLimit: 4_096);
        JavaNavigationRequest navigation = new(
            "src/Fixture.java",
            new AnalysisSourcePosition(2, 3),
            bounds);

        await using (IJavaAnalysisSession session =
            await factory.OpenAsync(scope, CancellationToken.None))
        {
            JavaNavigationResult symbols = await session.GetDocumentSymbolsAsync(
                scope,
                new JavaNavigationRequest("src/Fixture.java", null, bounds),
                CancellationToken.None);
            JavaNavigationResult definition = await session.GetDefinitionAsync(
                scope,
                navigation,
                CancellationToken.None);
            JavaNavigationResult references = await session.GetReferencesAsync(
                scope,
                navigation,
                CancellationToken.None);
            JavaNavigationResult implementations = await session.GetImplementationsAsync(
                scope,
                navigation,
                CancellationToken.None);

            Assert.All(
                new[] { symbols, definition, implementations },
                result =>
                {
                    Assert.Equal(AnalysisOperationStatus.Succeeded, result.Status);
                    Assert.All(result.Evidence, evidence => Assert.Equal(ProfiledRevision, scope.ProfiledRevision));
                });
            Assert.Equal(AnalysisOperationStatus.Failed, references.Status);
            Assert.NotNull(references.Failure);
            Assert.DoesNotContain(workspacePath, references.Failure!.Diagnostic, StringComparison.Ordinal);

            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                session.GetDefinitionAsync(scope, navigation, cancelled.Token));
        }

        Assert.Equal(@"C:\Program Files\Java\latest\jdk-21\bin\java.exe", launcher.StartInfo.FileName);
        string[] arguments = launcher.StartInfo.Arguments.ToArray();
        int configurationIndex = Array.IndexOf(arguments, "-configuration");
        int dataIndex = Array.IndexOf(arguments, "-data");
        string expectedDataPath = Path.Combine(
            workspaceRoot,
            _repositoryId.ToString("N"),
            ProfiledRevision + ".jdtls-data");
        string expectedConfigurationPath = Path.Combine(expectedDataPath, "configuration");
        Assert.InRange(configurationIndex, 0, arguments.Length - 2);
        Assert.InRange(dataIndex, 0, arguments.Length - 2);
        Assert.Equal(expectedConfigurationPath, arguments[configurationIndex + 1]);
        Assert.Equal(expectedDataPath, arguments[dataIndex + 1]);
        Assert.NotEqual(workspacePath, arguments[dataIndex + 1]);
        Assert.True(File.Exists(Path.Combine(expectedConfigurationPath, "config.ini")));
        Assert.False(expectedConfigurationPath.StartsWith(
            options.InstallationRoot,
            StringComparison.OrdinalIgnoreCase));
        Assert.Equal(workspacePath, leases.Acquired.Binding.WorkspacePath);
        Assert.Equal(
            ["initialize", "initialized", "textDocument/documentSymbol", "textDocument/definition",
                "textDocument/references", "textDocument/implementation", "shutdown", "exit"],
            protocol.Calls);
        Assert.Equal(1, process.DisposeCount);
        Assert.Equal(1, process.KillTreeCount);
        Assert.Equal(1, leases.DisposeCount);
        Assert.DoesNotContain(workspacePath, protocol.LastFailureDiagnostic, StringComparison.Ordinal);

        leases.ThrowCollision = true;
        Exception? collision = await Record.ExceptionAsync(() =>
            factory.OpenAsync(scope, CancellationToken.None));
        Assert.NotNull(collision);
        Assert.DoesNotContain(workspacePath, collision!.ToString(), StringComparison.Ordinal);

        Directory.Delete(workspaceRoot, recursive: true);
        Assert.False(Directory.Exists(workspaceRoot));
    }

    private static AnalysisRuntimeIdentity CreateIdentity(
        string? jdtLsDistributionArchiveName =
            "jdt-language-server-1.61.0-202608141332.zip",
        string? jdtLsDistributionArchiveSha256 =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
        AnalysisRuntimeIdentity.Create(
            new AnalysisRuntimeOptions(
                CodeGraphCommandPath: @"G:\packages\npm\prefix\codegraph.ps1",
                JdtLsInstallationRoot:
                    @"C:\Users\13728\.local\jdt-language-server-1.61.0-202608141332",
                JavaHome: @"C:\Program Files\Java\latest\jdk-21"),
            _repositoryId,
            ProfiledRevision,
            "@colbymchenry/codegraph",
            "1.5.0",
            "https://github.com/colbymchenry/codegraph",
            "codegraph",
            "@colbymchenry/codegraph-win32-x64@1.5.0",
            [
                new("lib/dist/bin/codegraph.js", CodeGraphHash),
                new("node.exe", NodeHash),
                new("npm-shim.js", NpmShimHash),
            ],
            "jdt-language-server-1.61.0-202608141332",
            jdtLsDistributionArchiveName,
            jdtLsDistributionArchiveSha256,
            21,
            "StreamJsonRpc",
            "2.25.29",
            "stdio");

    private sealed class RecordingWorkspaceLeaseProvider(JdtLsWorkspaceBinding binding)
        : IJdtLsWorkspaceLeaseProvider
    {
        public RecordingWorkspaceLease Acquired { get; } = new(binding);

        public int DisposeCount => Acquired.DisposeCount;

        public bool ThrowCollision { get; set; }

        public Task<IJdtLsWorkspaceLease> AcquireAsync(
            AnalysisScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowCollision)
            {
                return Task.FromException<IJdtLsWorkspaceLease>(
                    new InvalidOperationException(
                        "workspace collision C:\\Users\\13728\\private\\workspace"));
            }

            return Task.FromResult<IJdtLsWorkspaceLease>(Acquired);
        }
    }

    private sealed class RecordingWorkspaceLease(JdtLsWorkspaceBinding binding)
        : IJdtLsWorkspaceLease
    {
        public JdtLsWorkspaceBinding Binding { get; } = binding;

        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingProcessLauncher(RecordingProcess process)
        : IJdtLsProcessLauncher
    {
        public JdtLsProcessStartInfo StartInfo { get; private set; } = null!;

        public Task<IJdtLsProcess> LaunchAsync(
            JdtLsProcessStartInfo startInfo,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartInfo = startInfo;
            return Task.FromResult<IJdtLsProcess>(process);
        }
    }

    private sealed class RecordingProcess : IJdtLsProcess
    {
        public Stream StandardInput { get; } = new MemoryStream();

        public Stream StandardOutput { get; } = new MemoryStream();

        public int DisposeCount { get; private set; }

        public int KillTreeCount { get; private set; }

        public bool ForceWaitTimeout { get; set; }

        public Task<string> ReadBoundedStandardErrorAsync(
            int byteLimit,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("stderr C:\\Users\\13728\\private\\repo\\secret.java token=secret");
        }

        public Task<int> WaitForExitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ForceWaitTimeout)
            {
                return Task.FromException<int>(new TimeoutException("bounded process wait expired"));
            }

            return Task.FromResult(0);
        }

        public Task KillTreeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KillTreeCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            StandardInput.Dispose();
            StandardOutput.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingProtocolClientFactory(RecordingProtocolClient client)
        : IJdtLsProtocolClientFactory
    {
        public IJdtLsProtocolClient Create(IJdtLsProcess process, int responseByteLimit)
        {
            client.Process = process;
            Assert.Same(process, client.Process);
            Assert.Equal(16_384, responseByteLimit);
            return client;
        }
    }

    private sealed class RecordingProtocolClient : IJdtLsProtocolClient
    {
        public IJdtLsProcess Process { get; set; } = null!;

        public List<string> Calls { get; } = [];

        public string LastFailureDiagnostic { get; private set; } = string.Empty;

        public Task InitializeAsync(
            JdtLsWorkspaceBinding binding,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("initialize");
            return Task.CompletedTask;
        }

        public Task InitializedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("initialized");
            return Task.CompletedTask;
        }

        public Task<JavaNavigationResult> NavigateAsync(
            string method,
            AnalysisScope scope,
            JavaNavigationRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(method);
            AnalysisPortFailure failure = new(
                AnalysisPortFailureReason.ProcessFailed,
                "jdt_process_failed",
                "stderr C:\\Users\\13728\\private\\repo\\secret.java token=secret");
            LastFailureDiagnostic = failure.Diagnostic;
            if (method == "textDocument/references")
            {
                return Task.FromResult(new JavaNavigationResult(
                    AnalysisOperationStatus.Failed,
                    [],
                    failure));
            }

            return Task.FromResult(new JavaNavigationResult(
                AnalysisOperationStatus.Succeeded,
                [new AnalysisSymbolEvidence(
                    "example.analysis.Fixture",
                    AnalysisSymbolKind.Type,
                    request.RelativePath,
                    new AnalysisLineRange(1, 1))],
                null));
        }

        public Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("shutdown");
            return Task.CompletedTask;
        }

        public Task ExitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add("exit");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
