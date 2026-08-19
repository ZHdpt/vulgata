using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using StreamJsonRpc;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.Java;

public sealed record JdtLsAdapterOptions(
    string JavaExecutablePath,
    string InstallationRoot,
    string WorkspaceRoot,
    TimeSpan InitializationTimeout,
    TimeSpan RequestTimeout,
    TimeSpan ShutdownTimeout,
    int ResponseByteLimit);

internal sealed record JdtLsWorkspaceBinding(
    Guid RepositoryId,
    Guid ScanRunId,
    string ProfiledRevision,
    string WorkingCopyBindingToken,
    string WorkspacePath);

internal interface IJdtLsWorkspaceLeaseProvider
{
    Task<IJdtLsWorkspaceLease> AcquireAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);
}

internal interface IJdtLsWorkspaceLease : IAsyncDisposable
{
    JdtLsWorkspaceBinding Binding { get; }
}

internal sealed record JdtLsProcessStartInfo(
    string FileName,
    IReadOnlyCollection<string> Arguments,
    string WorkingDirectory);

internal interface IJdtLsProcessLauncher
{
    Task<IJdtLsProcess> LaunchAsync(
        JdtLsProcessStartInfo startInfo,
        CancellationToken cancellationToken);
}

internal interface IJdtLsProcess : IAsyncDisposable
{
    Stream StandardInput { get; }

    Stream StandardOutput { get; }

    Task<string> ReadBoundedStandardErrorAsync(
        int byteLimit,
        CancellationToken cancellationToken);

    Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task KillTreeAsync(CancellationToken cancellationToken);
}

internal interface IJdtLsProtocolClientFactory
{
    IJdtLsProtocolClient Create(IJdtLsProcess process, int responseByteLimit);
}

internal interface IJdtLsProtocolClient : IAsyncDisposable
{
    Task InitializeAsync(
        JdtLsWorkspaceBinding binding,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task InitializedAsync(CancellationToken cancellationToken);

    Task<JavaNavigationResult> NavigateAsync(
        string method,
        AnalysisScope scope,
        JavaNavigationRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken);

    Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken);

    Task ExitAsync(CancellationToken cancellationToken);
}

public sealed class JdtLsAnalysisSessionFactory : IJavaAnalysisSessionFactory
{
    private readonly JdtLsAdapterOptions _options;
    private readonly IJdtLsWorkspaceLeaseProvider _workspaceLeases;
    private readonly IJdtLsProcessLauncher _processLauncher;
    private readonly IJdtLsProtocolClientFactory _protocolClients;

    public JdtLsAnalysisSessionFactory(
        JdtLsAdapterOptions options,
        string distributionArchiveName,
        string distributionArchiveSha256)
    {
        _options = ValidateOptions(options);
        ValidateDistributionArchive(
            _options,
            distributionArchiveName,
            distributionArchiveSha256);
        _workspaceLeases = new FileJdtLsWorkspaceLeaseProvider(_options.WorkspaceRoot);
        _processLauncher = new ProcessJdtLsLauncher();
        _protocolClients = new StreamJsonRpcJdtLsProtocolClientFactory();
    }

    internal JdtLsAnalysisSessionFactory(
        JdtLsAdapterOptions options,
        IJdtLsWorkspaceLeaseProvider workspaceLeases,
        IJdtLsProcessLauncher processLauncher,
        IJdtLsProtocolClientFactory protocolClients)
    {
        _options = ValidateOptions(options);
        _workspaceLeases = workspaceLeases ?? throw new ArgumentNullException(nameof(workspaceLeases));
        _processLauncher = processLauncher ?? throw new ArgumentNullException(nameof(processLauncher));
        _protocolClients = protocolClients ?? throw new ArgumentNullException(nameof(protocolClients));
    }

    public async Task<IJavaAnalysisSession> OpenAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        IJdtLsWorkspaceLease? lease = null;
        IJdtLsProcess? process = null;
        IJdtLsProtocolClient? protocol = null;
        try
        {
            lease = await _workspaceLeases.AcquireAsync(scope, cancellationToken).ConfigureAwait(false);
            ValidateBinding(scope, lease.Binding);
            JdtLsProcessStartInfo startInfo = CreateStartInfo(lease.Binding);
            process = await _processLauncher.LaunchAsync(startInfo, cancellationToken).ConfigureAwait(false);
            protocol = _protocolClients.Create(process, _options.ResponseByteLimit);
            await protocol.InitializeAsync(
                lease.Binding,
                _options.InitializationTimeout,
                cancellationToken).ConfigureAwait(false);
            await protocol.InitializedAsync(cancellationToken).ConfigureAwait(false);
            return new JdtLsAnalysisSession(
                scope,
                _options,
                lease,
                process,
                protocol);
        }
        catch (OperationCanceledException)
        {
            await DisposeFailedOpenAsync(protocol, process, lease).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await DisposeFailedOpenAsync(protocol, process, lease).ConfigureAwait(false);
            throw new InvalidOperationException(SanitizeException(exception));
        }
    }

    private JdtLsProcessStartInfo CreateStartInfo(JdtLsWorkspaceBinding binding)
    {
        string pluginsRoot = Path.Combine(_options.InstallationRoot, "plugins");
        string[] launchers = Directory.Exists(pluginsRoot)
            ? Directory.GetFiles(
                pluginsRoot,
                "org.eclipse.equinox.launcher_*.jar",
                SearchOption.TopDirectoryOnly)
            : [];
        if (launchers.Length != 1)
        {
            throw new InvalidOperationException("jdt_launcher_unavailable");
        }

        string configuration = Path.Combine(_options.InstallationRoot, "config_win");
        if (!Directory.Exists(configuration))
        {
            throw new InvalidOperationException("jdt_configuration_unavailable");
        }

        return new JdtLsProcessStartInfo(
            _options.JavaExecutablePath,
            Array.AsReadOnly(new[]
            {
                "-Declipse.application=org.eclipse.jdt.ls.core.id1",
                "-Dosgi.bundles.defaultStartLevel=4",
                "-Declipse.product=org.eclipse.jdt.ls.core.product",
                "-Dlog.protocol=false",
                "-Dlog.level=WARNING",
                "-Xmx1G",
                "-jar",
                launchers[0],
                "-configuration",
                "config_win",
                "-data",
                binding.WorkspacePath,
            }),
            _options.InstallationRoot);
    }

    private static JdtLsAdapterOptions ValidateOptions(JdtLsAdapterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!Path.IsPathFullyQualified(options.JavaExecutablePath) ||
            !Path.IsPathFullyQualified(options.InstallationRoot) ||
            !Path.IsPathFullyQualified(options.WorkspaceRoot) ||
            options.InitializationTimeout <= TimeSpan.Zero ||
            options.RequestTimeout <= TimeSpan.Zero ||
            options.ShutdownTimeout <= TimeSpan.Zero ||
            options.ResponseByteLimit is <= 0 or > 1_048_576)
        {
            throw new ArgumentException("JDT-LS options are outside the bounded contract.", nameof(options));
        }

        return options;
    }

    private static void ValidateDistributionArchive(
        JdtLsAdapterOptions options,
        string distributionArchiveName,
        string distributionArchiveSha256)
    {
        if (string.IsNullOrWhiteSpace(distributionArchiveName) ||
            Path.GetFileName(distributionArchiveName) != distributionArchiveName ||
            string.IsNullOrWhiteSpace(distributionArchiveSha256) ||
            distributionArchiveSha256.Length != 64 ||
            distributionArchiveSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("jdt_distribution_archive_checksum_missing");
        }

        string archivePath = Path.Combine(options.InstallationRoot, distributionArchiveName);
        if (!File.Exists(archivePath))
        {
            throw new InvalidOperationException("jdt_distribution_archive_missing");
        }

        string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)));
        if (!string.Equals(actualHash, distributionArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_distribution_archive_checksum_mismatch");
        }
    }

    private static void ValidateBinding(AnalysisScope scope, JdtLsWorkspaceBinding binding)
    {
        if (binding.RepositoryId != scope.RepositoryId ||
            binding.ScanRunId != scope.ScanRunId ||
            !string.Equals(binding.ProfiledRevision, scope.ProfiledRevision, StringComparison.Ordinal) ||
            !string.Equals(
                binding.WorkingCopyBindingToken,
                scope.WorkingCopyBindingToken,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("jdt_workspace_binding_mismatch");
        }
    }

    private static async Task DisposeFailedOpenAsync(
        IJdtLsProtocolClient? protocol,
        IJdtLsProcess? process,
        IJdtLsWorkspaceLease? lease)
    {
        if (protocol is not null)
        {
            await protocol.DisposeAsync().ConfigureAwait(false);
        }

        if (process is not null)
        {
            try
            {
                await process.KillTreeAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            await process.DisposeAsync().ConfigureAwait(false);
        }

        if (lease is not null)
        {
            await lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string SanitizeException(Exception exception)
    {
        string value = exception.Message;
        string[] safe = value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.Contains('/') && !token.Contains('\\') && !token.Contains('='))
            .Take(8)
            .ToArray();
        return safe.Length == 0 ? "jdt_session_failed" : string.Join(' ', safe);
    }
}

internal sealed class FileJdtLsWorkspaceLeaseProvider : IJdtLsWorkspaceLeaseProvider
{
    private readonly string _workspaceRoot;

    public FileJdtLsWorkspaceLeaseProvider(string workspaceRoot)
    {
        if (!Path.IsPathFullyQualified(workspaceRoot))
        {
            throw new ArgumentException("A fully qualified workspace root is required.", nameof(workspaceRoot));
        }

        _workspaceRoot = Path.GetFullPath(workspaceRoot);
    }

    public async Task<IJdtLsWorkspaceLease> AcquireAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        string workspacePath = Path.GetFullPath(Path.Combine(
            _workspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision));
        string rootPrefix = _workspaceRoot.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!workspacePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_workspace_escape");
        }

        Directory.CreateDirectory(workspacePath);
        string markerPath = Path.Combine(workspacePath, ".vulgata-binding");
        string expectedMarker = string.Join(
            '|',
            scope.RepositoryId.ToString("D"),
            scope.ScanRunId.ToString("D"),
            scope.ProfiledRevision,
            scope.WorkingCopyBindingToken);
        if (File.Exists(markerPath))
        {
            string marker = await File.ReadAllTextAsync(markerPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(marker, expectedMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("jdt_workspace_binding_conflict");
            }
        }
        else
        {
            await File.WriteAllTextAsync(markerPath, expectedMarker, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            FileStream lockStream = new(
                Path.Combine(workspacePath, ".vulgata-lease"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous);
            return new FileJdtLsWorkspaceLease(
                new JdtLsWorkspaceBinding(
                    scope.RepositoryId,
                    scope.ScanRunId,
                    scope.ProfiledRevision,
                    scope.WorkingCopyBindingToken,
                    workspacePath),
                lockStream);
        }
        catch (IOException)
        {
            throw new InvalidOperationException("jdt_workspace_in_use");
        }
    }
}

internal sealed class FileJdtLsWorkspaceLease(
    JdtLsWorkspaceBinding binding,
    FileStream lockStream) : IJdtLsWorkspaceLease
{
    public JdtLsWorkspaceBinding Binding { get; } = binding;

    public ValueTask DisposeAsync() => lockStream.DisposeAsync();
}

internal sealed class JdtLsAnalysisSession : IJavaAnalysisSession
{
    private readonly AnalysisScope _scope;
    private readonly JdtLsAdapterOptions _options;
    private readonly IJdtLsWorkspaceLease _lease;
    private readonly IJdtLsProcess _process;
    private readonly IJdtLsProtocolClient _protocol;
    private bool _disposed;

    public JdtLsAnalysisSession(
        AnalysisScope scope,
        JdtLsAdapterOptions options,
        IJdtLsWorkspaceLease lease,
        IJdtLsProcess process,
        IJdtLsProtocolClient protocol)
    {
        _scope = scope;
        _options = options;
        _lease = lease;
        _process = process;
        _protocol = protocol;
    }

    public Task<JavaNavigationResult> GetDocumentSymbolsAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken) =>
        NavigateAsync("textDocument/documentSymbol", scope, request, cancellationToken);

    public Task<JavaNavigationResult> GetDefinitionAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken) =>
        NavigateAsync("textDocument/definition", scope, request, cancellationToken);

    public Task<JavaNavigationResult> GetReferencesAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken) =>
        NavigateAsync("textDocument/references", scope, request, cancellationToken);

    public Task<JavaNavigationResult> GetImplementationsAsync(
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken) =>
        NavigateAsync("textDocument/implementation", scope, request, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await _protocol.ShutdownAsync(_options.ShutdownTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        try
        {
            await _protocol.ExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        try
        {
            await _protocol.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _process.WaitForExitAsync(_options.ShutdownTimeout, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                await _process.KillTreeAsync(CancellationToken.None).ConfigureAwait(false);
            }

            await _process.DisposeAsync().ConfigureAwait(false);
            await _lease.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task<JavaNavigationResult> NavigateAsync(
        string method,
        AnalysisScope scope,
        JavaNavigationRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(scope, _scope) &&
            (scope.RepositoryId != _scope.RepositoryId ||
             scope.ScanRunId != _scope.ScanRunId ||
             !string.Equals(scope.ProfiledRevision, _scope.ProfiledRevision, StringComparison.Ordinal) ||
             !string.Equals(
                 scope.WorkingCopyBindingToken,
                 _scope.WorkingCopyBindingToken,
                 StringComparison.Ordinal) ||
             !string.Equals(scope.RuntimeIdentityHash, _scope.RuntimeIdentityHash, StringComparison.Ordinal)))
        {
            return Task.FromResult(new JavaNavigationResult(
                AnalysisOperationStatus.Failed,
                [],
                new AnalysisPortFailure(
                    AnalysisPortFailureReason.WorkspaceConflict,
                    "jdt_scope_mismatch",
                    "jdt scope mismatch")));
        }

        return _protocol.NavigateAsync(
            method,
            scope,
            request,
            _options.RequestTimeout,
            cancellationToken);
    }
}

internal sealed class ProcessJdtLsLauncher : IJdtLsProcessLauncher
{
    public Task<IJdtLsProcess> LaunchAsync(
        JdtLsProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ProcessStartInfo processStartInfo = new()
        {
            FileName = startInfo.FileName,
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in startInfo.Arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        Process process = Process.Start(processStartInfo) ??
            throw new InvalidOperationException("jdt_process_start_failed");
        return Task.FromResult<IJdtLsProcess>(new ProcessJdtLsProcess(process));
    }
}

internal sealed class ProcessJdtLsProcess(Process process) : IJdtLsProcess
{
    public Stream StandardInput => process.StandardInput.BaseStream;

    public Stream StandardOutput => process.StandardOutput.BaseStream;

    public async Task<string> ReadBoundedStandardErrorAsync(
        int byteLimit,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[Math.Min(byteLimit, 4_096)];
        int read = await process.StandardError.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        return new string(buffer, 0, read);
    }

    public async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (
            timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("jdt_process_wait_timeout");
        }
    }

    public Task KillTreeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        process.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class StreamJsonRpcJdtLsProtocolClientFactory : IJdtLsProtocolClientFactory
{
    public IJdtLsProtocolClient Create(IJdtLsProcess process, int responseByteLimit) =>
        new StreamJsonRpcJdtLsProtocolClient(process, responseByteLimit);
}

internal sealed class StreamJsonRpcJdtLsProtocolClient : IJdtLsProtocolClient
{
    private readonly JsonRpc _rpc;
    private readonly int _responseByteLimit;
    private string? _workspacePath;

    public StreamJsonRpcJdtLsProtocolClient(IJdtLsProcess process, int responseByteLimit)
    {
        _responseByteLimit = responseByteLimit;
        _rpc = JsonRpc.Attach(process.StandardInput, process.StandardOutput, target: null);
    }

    public async Task InitializeAsync(
        JdtLsWorkspaceBinding binding,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CreateBoundedToken(timeout, cancellationToken);
        _workspacePath = binding.WorkspacePath;
        Uri workspaceUri = new(binding.WorkspacePath);
        object parameters = new
        {
            processId = Environment.ProcessId,
            rootUri = workspaceUri.AbsoluteUri,
            capabilities = new { },
            workspaceFolders = new[] { new { uri = workspaceUri.AbsoluteUri, name = "repository" } },
        };
        _ = await _rpc.InvokeWithParameterObjectAsync<JsonElement>(
            "initialize",
            parameters,
            argumentDeclaredTypes: null,
            linked.Token).ConfigureAwait(false);
    }

    public Task InitializedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _rpc.NotifyAsync("initialized", new { });
    }

    public async Task<JavaNavigationResult> NavigateAsync(
        string method,
        AnalysisScope scope,
        JavaNavigationRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CreateBoundedToken(timeout, cancellationToken);
        try
        {
            object parameters = new
            {
                textDocument = new { uri = request.RelativePath },
                position = request.Position is null
                    ? null
                    : new { line = request.Position.Line - 1, character = request.Position.Character - 1 },
                context = method == "textDocument/references"
                    ? new { includeDeclaration = true }
                    : null,
            };
            JsonElement response = await _rpc.InvokeWithParameterObjectAsync<JsonElement>(
                method,
                parameters,
                argumentDeclaredTypes: null,
                linked.Token).ConfigureAwait(false);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(response);
            if (bytes.Length > Math.Min(_responseByteLimit, request.Bounds.ByteLimit))
            {
                return Failed(AnalysisPortFailureReason.MalformedResponse, "jdt_response_too_large");
            }

            return ParseEvidence(response, request, _workspacePath);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed(AnalysisPortFailureReason.TimedOut, "jdt_request_timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Failed(AnalysisPortFailureReason.MalformedResponse, "jdt_response_invalid");
        }
    }

    public async Task ShutdownAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = CreateBoundedToken(timeout, cancellationToken);
        await _rpc.InvokeWithCancellationAsync(
            "shutdown",
            Array.Empty<object>(),
            linked.Token).ConfigureAwait(false);
    }

    public Task ExitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _rpc.NotifyAsync("exit");
    }

    public ValueTask DisposeAsync()
    {
        _rpc.Dispose();
        return ValueTask.CompletedTask;
    }

    private static JavaNavigationResult ParseEvidence(
        JsonElement response,
        JavaNavigationRequest request,
        string? workspacePath)
    {
        if (response.ValueKind == JsonValueKind.Null)
        {
            return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, [], null);
        }

        IEnumerable<JsonElement> elements = response.ValueKind == JsonValueKind.Array
            ? response.EnumerateArray().ToArray()
            : new[] { response };
        List<AnalysisSymbolEvidence> evidence = [];
        foreach (JsonElement element in elements.Take(request.Bounds.ItemLimit + 1))
        {
            if (evidence.Count == request.Bounds.ItemLimit)
            {
                return Failed(AnalysisPortFailureReason.MalformedResponse, "jdt_item_limit_exceeded");
            }

            JsonElement location = element.TryGetProperty("location", out JsonElement nested)
                ? nested
                : element;
            JsonElement range = location.GetProperty("range");
            int startLine = range.GetProperty("start").GetProperty("line").GetInt32() + 1;
            int endLine = range.GetProperty("end").GetProperty("line").GetInt32() + 1;
            string symbol = element.TryGetProperty("name", out JsonElement name)
                ? name.GetString() ?? "java-symbol"
                : "java-symbol";
            string relativePath = ResolveRelativePath(location, request.RelativePath, workspacePath);
            evidence.Add(new AnalysisSymbolEvidence(
                symbol,
                AnalysisSymbolKind.Type,
                relativePath,
                new AnalysisLineRange(startLine, Math.Max(startLine, endLine))));
        }

        return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, evidence, null);
    }

    private static string ResolveRelativePath(
        JsonElement location,
        string requestPath,
        string? workspacePath)
    {
        if (!location.TryGetProperty("uri", out JsonElement uriElement))
        {
            return requestPath;
        }

        if (workspacePath is null ||
            !Uri.TryCreate(uriElement.GetString(), UriKind.Absolute, out Uri? uri) ||
            !uri.IsFile)
        {
            throw new JsonException("jdt_location_uri_invalid");
        }

        string root = Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(uri.LocalPath);
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("jdt_location_outside_workspace");
        }

        return Path.GetRelativePath(workspacePath, fullPath).Replace('\\', '/');
    }

    private static JavaNavigationResult Failed(
        AnalysisPortFailureReason reason,
        string code) =>
        new(
            AnalysisOperationStatus.Failed,
            [],
            new AnalysisPortFailure(reason, code, code.Replace('_', ' ')));

    private static CancellationTokenSource CreateBoundedToken(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}
