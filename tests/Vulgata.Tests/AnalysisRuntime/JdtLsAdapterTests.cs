using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Pipes;
using System.Reflection;
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

    [Fact]
    public async Task PreparedRevisionWorkspace_UsesContainedMutableProjectMirror_AndPreservesExactSnapshotIdentity()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story123-jdt-snapshot-" + Guid.NewGuid().ToString("N"));
        string runtimeRoot = Path.Combine(root, "runtime");
        string evidenceRoot = Path.Combine(root, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        Directory.CreateDirectory(Path.Combine(evidenceRoot, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(evidenceRoot, "src", "Fixture.java"),
            "package example;\nclass Fixture {}\n");

        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000013"),
            ProfiledRevision,
            "binding-prepared-jdt",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        TrackingSnapshotLease snapshot = new(scope, evidenceRoot);
        PreparedRevisionJdtLsWorkspaceLeaseProvider provider =
            new(new FixedPreparedRevisionLeaseResolver(snapshot), runtimeRoot);

        try
        {
            await using IJdtLsWorkspaceLease workspace =
                await provider.AcquireAsync(scope, CancellationToken.None);

            string projectRoot = Path.GetFullPath(workspace.Binding.WorkspacePath);
            string runtimePrefix = Path.GetFullPath(runtimeRoot).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            Assert.StartsWith(runtimePrefix, projectRoot, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(Path.GetFullPath(evidenceRoot), projectRoot);

            Directory.CreateDirectory(projectRoot);
            await File.WriteAllTextAsync(Path.Combine(projectRoot, ".project"), "generated");
            await File.WriteAllTextAsync(Path.Combine(projectRoot, ".classpath"), "generated");

            await snapshot.VerifyIdentityAsync(CancellationToken.None);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreparedRevisionWorkspace_PreExistingReparsePointCleanup_FailsClosedWithoutTouchingExternalSentinel(
        bool projectRootIsReparsePoint)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story123-jdt-cleanup-" + Guid.NewGuid().ToString("N"));
        string runtimeRoot = Path.Combine(root, "runtime");
        string evidenceRoot = Path.Combine(root, "evidence");
        string externalRoot = Path.Combine(root, "external");
        Directory.CreateDirectory(evidenceRoot);
        Directory.CreateDirectory(externalRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "Fixture.java"), "class Fixture {}\n");
        string sentinelPath = Path.Combine(externalRoot, "sentinel.txt");
        await File.WriteAllTextAsync(sentinelPath, "external sentinel\n");
        File.SetAttributes(sentinelPath, FileAttributes.ReadOnly);

        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000013"),
            ProfiledRevision,
            "binding-prepared-jdt",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        TrackingSnapshotLease snapshot = new(scope, evidenceRoot);
        PreparedRevisionJdtLsWorkspaceLeaseProvider provider =
            new(new FixedPreparedRevisionLeaseResolver(snapshot), runtimeRoot);
        string projectRoot = Path.Combine(
            runtimeRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision + ".jdtls-project");
        Directory.CreateDirectory(Path.GetDirectoryName(projectRoot)!);
        IJdtLsWorkspaceLease? acquired = null;

        try
        {
            if (projectRootIsReparsePoint)
            {
                Directory.CreateSymbolicLink(projectRoot, externalRoot);
            }
            else
            {
                Directory.CreateDirectory(projectRoot);
                Directory.CreateSymbolicLink(
                    Path.Combine(projectRoot, "nested-external"),
                    externalRoot);
            }

            Exception? failure = await Record.ExceptionAsync(async () =>
                acquired = await provider.AcquireAsync(scope, CancellationToken.None));

            Assert.Equal("external sentinel\n", await File.ReadAllTextAsync(sentinelPath));
            Assert.True(
                File.GetAttributes(sentinelPath).HasFlag(FileAttributes.ReadOnly),
                "Cleanup must not traverse a pre-existing mirror reparse point and mutate an external file.");
            Assert.NotNull(failure);
        }
        finally
        {
            // An unsafe implementation may unexpectedly return a lease; release it before
            // deleting the test tree so the lock file cannot mask the cleanup assertion.
            if (acquired is not null)
            {
                await acquired.DisposeAsync();
            }

            DeleteWithoutFollowingReparsePoints(root);
        }
    }

    [Fact]
    public async Task StreamJsonRpcJdtLsClient_UsesEvidenceBytesAndRejectsMirrorGeneratedResponseUri()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story123-jdt-rpc-" + Guid.NewGuid().ToString("N"));
        string evidenceRoot = Path.Combine(root, "evidence");
        string mirrorRoot = Path.Combine(root, "mirror");
        Directory.CreateDirectory(evidenceRoot);
        Directory.CreateDirectory(mirrorRoot);
        string evidenceText = "class Evidence {}\n";
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "Fixture.java"), evidenceText);
        await File.WriteAllTextAsync(Path.Combine(mirrorRoot, "Fixture.java"), "class Mirror {}\n");
        await File.WriteAllTextAsync(Path.Combine(mirrorRoot, ".project"), "generated\n");

        AnonymousPipeServerStream serverInput =
            new(PipeDirection.In, HandleInheritability.None);
        AnonymousPipeClientStream clientInput = new(
            PipeDirection.Out,
            serverInput.GetClientHandleAsString());
        AnonymousPipeServerStream serverOutput =
            new(PipeDirection.Out, HandleInheritability.None);
        AnonymousPipeClientStream clientOutput = new(
            PipeDirection.In,
            serverOutput.GetClientHandleAsString());
        RpcProcess process = new(clientInput, clientOutput);
        using CancellationTokenSource serverCancellation = new(TimeSpan.FromSeconds(5));
        List<string> openedTexts = [];
        Task server = RunRpcServerAsync(
            serverInput,
            serverOutput,
            mirrorRoot,
            openedTexts,
            serverCancellation.Token);
        StreamJsonRpcJdtLsProtocolClient client =
            new(process, responseByteLimit: 16_384);
        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000013"),
            ProfiledRevision,
            "binding-prepared-jdt",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        JdtLsWorkspaceBinding binding = new(
            scope.RepositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken,
            mirrorRoot,
            evidenceRoot);
        AnalysisOperationBounds bounds = new(itemLimit: 8, byteLimit: 4_096);
        JavaNavigationRequest request = new(
            "Fixture.java",
            new AnalysisSourcePosition(1, 1),
            bounds);

        try
        {
            await client.InitializeAsync(binding, TimeSpan.FromSeconds(2), CancellationToken.None);
            await client.InitializedAsync(CancellationToken.None);
            JavaNavigationResult first = await client.NavigateAsync(
                "textDocument/definition",
                scope,
                request,
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            JavaNavigationResult generatedResponse = await client.NavigateAsync(
                "textDocument/definition",
                scope,
                request,
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            await client.ShutdownAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            await client.ExitAsync(CancellationToken.None);
            await server;

            Assert.Equal(AnalysisOperationStatus.Succeeded, first.Status);
            Assert.Equal(evidenceText, Assert.Single(openedTexts));
            Assert.Equal(AnalysisOperationStatus.Failed, generatedResponse.Status);
            Assert.Equal(AnalysisPortFailureReason.MalformedResponse, generatedResponse.Failure?.Reason);
            Assert.Equal("jdt_response_invalid", generatedResponse.Failure?.FailureCode);
        }
        finally
        {
            await client.DisposeAsync();
            serverCancellation.Cancel();
            clientInput.Dispose();
            clientOutput.Dispose();
            serverInput.Dispose();
            serverOutput.Dispose();
            DeleteWithoutFollowingReparsePoints(root);
        }
    }

    [Fact]
    public async Task StreamJsonRpcJdtLsClient_DocumentSymbolRoot_PreservesAuthorityProvenanceAndTypeNameAsync()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "vulgata-story125-jdt-owner-" + Guid.NewGuid().ToString("N"));
        string evidenceRoot = Path.Combine(root, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(
            Path.Combine(evidenceRoot, "Fixture.java"),
            "package example;\nclass Fixture { }\n");

        AnonymousPipeServerStream serverInput =
            new(PipeDirection.In, HandleInheritability.None);
        AnonymousPipeClientStream clientInput = new(
            PipeDirection.Out,
            serverInput.GetClientHandleAsString());
        AnonymousPipeServerStream serverOutput =
            new(PipeDirection.Out, HandleInheritability.None);
        AnonymousPipeClientStream clientOutput = new(
            PipeDirection.In,
            serverOutput.GetClientHandleAsString());
        RpcProcess process = new(clientInput, clientOutput);
        using CancellationTokenSource serverCancellation = new(TimeSpan.FromSeconds(5));
        object response = new[]
        {
            new
            {
                name = "Fixture",
                kind = 5,
                range = new
                {
                    start = new { line = 1, character = 0 },
                    end = new { line = 1, character = 17 },
                },
                selectionRange = new
                {
                    start = new { line = 1, character = 6 },
                    end = new { line = 1, character = 13 },
                },
                children = Array.Empty<object>(),
            },
        };
        Task server = RunDocumentSymbolRpcServerAsync(
            serverInput,
            serverOutput,
            response,
            serverCancellation.Token);
        StreamJsonRpcJdtLsProtocolClient client =
            new(process, responseByteLimit: 16_384);
        AnalysisScope scope = new(
            _repositoryId,
            Guid.Parse("12000000-0000-4000-8000-000000000013"),
            ProfiledRevision,
            "binding-prepared-jdt-owner",
            new string('a', 64),
            new AnalysisDeadline(DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds()));
        JdtLsWorkspaceBinding binding = new(
            scope.RepositoryId,
            scope.ScanRunId,
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken,
            root,
            evidenceRoot);
        JavaNavigationRequest request = new(
            "Fixture.java",
            position: null,
            bounds: new AnalysisOperationBounds(itemLimit: 8, byteLimit: 4_096));

        try
        {
            await client.InitializeAsync(binding, TimeSpan.FromSeconds(2), CancellationToken.None);
            await client.InitializedAsync(CancellationToken.None);
            JavaNavigationResult result = await client.NavigateAsync(
                "textDocument/documentSymbol",
                scope,
                request,
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            await client.ShutdownAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            await client.ExitAsync(CancellationToken.None);
            await server;

            Assert.Equal(AnalysisOperationStatus.Succeeded, result.Status);
            AnalysisSymbolEvidence evidence = Assert.Single(result.Evidence);
            PropertyInfo? provenance = typeof(AnalysisSymbolEvidence).GetProperty("Provenance");
            Assert.NotNull(provenance);
            Assert.Equal("RootDocumentSymbol", provenance!.GetValue(evidence)?.ToString());
            PreparedJdtSymbolSpan span = Assert.IsType<PreparedJdtSymbolSpan>(evidence.ExactUtf16Range);
            PropertyInfo? typeName = typeof(PreparedJdtSymbolSpan).GetProperty("TypeName");
            Assert.NotNull(typeName);
            Assert.Equal("Fixture", typeName!.GetValue(span));
        }
        finally
        {
            await client.DisposeAsync();
            serverCancellation.Cancel();
            clientInput.Dispose();
            clientOutput.Dispose();
            serverInput.Dispose();
            serverOutput.Dispose();
            DeleteWithoutFollowingReparsePoints(root);
        }
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

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void DeleteWithoutFollowingReparsePoints(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                Directory.Delete(path, recursive: false);
            }
            else
            {
                File.Delete(path);
            }

            return;
        }

        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
            return;
        }

        foreach (FileSystemInfo entry in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            DeleteWithoutFollowingReparsePoints(entry.FullName);
        }

        Directory.Delete(path, recursive: false);
    }

    private static async Task RunRpcServerAsync(
        Stream input,
        Stream output,
        string mirrorRoot,
        List<string> openedTexts,
        CancellationToken cancellationToken)
    {
        int definitionResponse = 0;
        while (true)
        {
            using JsonDocument message = await ReadRpcMessageAsync(input, cancellationToken);
            JsonElement root = message.RootElement;
            string method = root.GetProperty("method").GetString()!;
            if (method == "initialize")
            {
                await SendRpcResponseAsync(output, root.GetProperty("id"), new { }, cancellationToken);
            }
            else if (method == "textDocument/didOpen")
            {
                openedTexts.Add(root.GetProperty("params").GetProperty("textDocument").GetProperty("text").GetString()!);
            }
            else if (method == "textDocument/definition")
            {
                definitionResponse++;
                string relativePath = definitionResponse == 1 ? "Fixture.java" : ".project";
                string responseUri = new Uri(Path.Combine(mirrorRoot, relativePath)).AbsoluteUri;
                await SendRpcResponseAsync(
                    output,
                    root.GetProperty("id"),
                    new[]
                    {
                        new
                        {
                            name = "Fixture",
                            location = new
                            {
                                uri = responseUri,
                                range = new
                                {
                                    start = new { line = 0, character = 0 },
                                    end = new { line = 0, character = 6 },
                                },
                            },
                        },
                    },
                    cancellationToken);
            }
            else if (method == "shutdown")
            {
                await SendRpcResponseAsync(output, root.GetProperty("id"), null, cancellationToken);
            }
            else if (method == "exit")
            {
                return;
            }
        }
    }

    private static async Task RunDocumentSymbolRpcServerAsync(
        Stream input,
        Stream output,
        object response,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            using JsonDocument message = await ReadRpcMessageAsync(input, cancellationToken);
            JsonElement root = message.RootElement;
            string method = root.GetProperty("method").GetString()!;
            if (method == "initialize")
            {
                await SendRpcResponseAsync(output, root.GetProperty("id"), new { }, cancellationToken);
            }
            else if (method == "textDocument/documentSymbol")
            {
                await SendRpcResponseAsync(
                    output,
                    root.GetProperty("id"),
                    response,
                    cancellationToken);
            }
            else if (method == "shutdown")
            {
                await SendRpcResponseAsync(output, root.GetProperty("id"), null, cancellationToken);
            }
            else if (method == "exit")
            {
                return;
            }
        }
    }

    private static async Task<JsonDocument> ReadRpcMessageAsync(
        Stream input,
        CancellationToken cancellationToken)
    {
        List<byte> header = [];
        byte[] one = new byte[1];
        while (true)
        {
            int read = await input.ReadAsync(one, cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("RPC stream ended before the message header.");
            }

            header.Add(one[0]);
            if (header.Count >= 4 &&
                header[^4] == '\r' && header[^3] == '\n' &&
                header[^2] == '\r' && header[^1] == '\n')
            {
                break;
            }
        }

        string headerText = Encoding.ASCII.GetString([.. header]);
        string contentLength = headerText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            .Split(':', 2)[1]
            .Trim();
        byte[] payload = new byte[int.Parse(contentLength, System.Globalization.CultureInfo.InvariantCulture)];
        int offset = 0;
        while (offset < payload.Length)
        {
            int read = await input.ReadAsync(payload.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("RPC stream ended before the message body.");
            }

            offset += read;
        }

        return JsonDocument.Parse(payload);
    }

    private static async Task SendRpcResponseAsync(
        Stream output,
        JsonElement id,
        object? result,
        CancellationToken cancellationToken)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            jsonrpc = "2.0",
            id,
            result,
        });
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {payload.Length}\r\n\r\n");
        await output.WriteAsync(header, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private sealed class FixedPreparedRevisionLeaseResolver(ICommittedRepositorySnapshotLease lease)
        : IPreparedRevisionLeaseResolver
    {
        public bool TryResolveLease(
            string bindingToken,
            out ICommittedRepositorySnapshotLease? resolved)
        {
            resolved = string.Equals(bindingToken, "binding-prepared-jdt", StringComparison.Ordinal)
                ? lease
                : null;
            return resolved is not null;
        }
    }

    private sealed class TrackingSnapshotLease(AnalysisScope scope, string root)
        : ICommittedRepositorySnapshotLease
    {
        private readonly string _identity = ComputeIdentity(root);

        public Guid RepositoryId => scope.RepositoryId;

        public Guid ScanRunId => scope.ScanRunId;

        public string ProfiledRevision => scope.ProfiledRevision;

        public string RootPath => root;

        public Task VerifyIdentityAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(_identity, ComputeIdentity(root), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("exact snapshot identity changed");
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static string ComputeIdentity(string path)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(path, file).Replace(Path.DirectorySeparatorChar, '/');
                hash.AppendData(Encoding.UTF8.GetBytes(relative));
                hash.AppendData(File.ReadAllBytes(file));
            }

            return Convert.ToHexString(hash.GetHashAndReset());
        }
    }

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

    private sealed class RpcProcess(
        Stream standardInput,
        Stream standardOutput) : IJdtLsProcess
    {
        public Stream StandardInput { get; } = standardInput;

        public Stream StandardOutput { get; } = standardOutput;

        public Task<string> ReadBoundedStandardErrorAsync(
            int byteLimit,
            CancellationToken cancellationToken) => Task.FromResult(string.Empty);

        public Task<int> WaitForExitAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken) => Task.FromResult(0);

        public Task KillTreeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            StandardInput.Dispose();
            StandardOutput.Dispose();
            return ValueTask.CompletedTask;
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
