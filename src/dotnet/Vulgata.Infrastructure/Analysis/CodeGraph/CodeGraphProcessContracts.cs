using System.Diagnostics;
using System.Text;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

internal sealed class CodeGraphAdapterOptions
{
    public CodeGraphAdapterOptions(
        string commandPath,
        TimeSpan operationTimeout,
        int StandardOutputByteLimit,
        int StandardErrorByteLimit)
    {
        if (string.IsNullOrWhiteSpace(commandPath) || !Path.IsPathFullyQualified(commandPath))
        {
            throw new ArgumentException("An absolute CodeGraph command path is required.", nameof(commandPath));
        }

        if (operationTimeout <= TimeSpan.Zero || operationTimeout > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout));
        }

        CommandPath = Path.GetFullPath(commandPath);
        OperationTimeout = operationTimeout;
        this.StandardOutputByteLimit = RequireByteLimit(StandardOutputByteLimit, nameof(StandardOutputByteLimit));
        this.StandardErrorByteLimit = RequireByteLimit(StandardErrorByteLimit, nameof(StandardErrorByteLimit));
    }

    public string CommandPath { get; }
    public TimeSpan OperationTimeout { get; }
    public int StandardOutputByteLimit { get; }
    public int StandardErrorByteLimit { get; }

    private static int RequireByteLimit(int value, string parameterName) =>
        value is > 0 and <= 1_048_576 ? value : throw new ArgumentOutOfRangeException(parameterName);
}

internal sealed class CodeGraphRepositorySnapshot
{
    public CodeGraphRepositorySnapshot(
        Guid repositoryId,
        Guid scanRunId,
        string profiledRevision,
        string workingCopyBindingToken,
        string repositoryRoot,
        string expectedIndexIdentity,
        string runtimeIdentityHash)
    {
        if (repositoryId == Guid.Empty || scanRunId == Guid.Empty)
        {
            throw new ArgumentException("Repository and Scan Run identities are required.");
        }

        if (string.IsNullOrWhiteSpace(profiledRevision) ||
            profiledRevision.Length is < 7 or > 64 ||
            profiledRevision.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("A bounded Profiled Revision is required.", nameof(profiledRevision));
        }

        if (string.IsNullOrWhiteSpace(workingCopyBindingToken) || workingCopyBindingToken.Length > 128)
        {
            throw new ArgumentException("A working-copy binding token is required.", nameof(workingCopyBindingToken));
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot))
        {
            throw new ArgumentException("An absolute trusted Repository root is required.", nameof(repositoryRoot));
        }

        RepositoryId = repositoryId;
        ScanRunId = scanRunId;
        ProfiledRevision = profiledRevision.ToLowerInvariant();
        WorkingCopyBindingToken = workingCopyBindingToken;
        RuntimeIdentityHash = RequireRuntimeIdentityHash(runtimeIdentityHash);
        RepositoryRoot = Path.GetFullPath(repositoryRoot);
        ExpectedIndexIdentity = RequireMachineCode(expectedIndexIdentity, nameof(expectedIndexIdentity));
    }

    public Guid RepositoryId { get; }
    public Guid ScanRunId { get; }
    public string ProfiledRevision { get; }
    public string WorkingCopyBindingToken { get; }
    public string RuntimeIdentityHash { get; }
    public string RepositoryRoot { get; }
    public string ExpectedIndexIdentity { get; }

    private static string RequireRuntimeIdentityHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64 ||
            value.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "A canonical lowercase analysis runtime identity SHA-256 is required.",
                nameof(value));
        }

        return value;
    }

    private static string RequireMachineCode(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.'))
        {
            throw new ArgumentException("A normalized index identity is required.", parameterName);
        }

        return value.ToLowerInvariant();
    }
}

internal interface ICodeGraphRepositorySnapshotResolver
{
    Task<CodeGraphRepositorySnapshot?> ResolveAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken);
}

internal sealed class CodeGraphProcessRequest
{
    public CodeGraphProcessRequest(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        int standardOutputByteLimit,
        int standardErrorByteLimit,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool closeStandardInput = false,
        bool inheritEnvironmentVariables = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (timeout <= TimeSpan.Zero || standardOutputByteLimit <= 0 || standardErrorByteLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        FileName = fileName;
        Arguments = Array.AsReadOnly(arguments.ToArray());
        WorkingDirectory = workingDirectory;
        Timeout = timeout;
        StandardOutputByteLimit = standardOutputByteLimit;
        StandardErrorByteLimit = standardErrorByteLimit;
        EnvironmentVariables = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            environmentVariables is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(environmentVariables, StringComparer.Ordinal));
        CloseStandardInput = closeStandardInput;
        InheritEnvironmentVariables = inheritEnvironmentVariables;
    }

    public string FileName { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string WorkingDirectory { get; }
    public TimeSpan Timeout { get; }
    public int StandardOutputByteLimit { get; }
    public int StandardErrorByteLimit { get; }
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }
    public bool CloseStandardInput { get; }
    public bool InheritEnvironmentVariables { get; }
}

internal sealed record CodeGraphProcessResult(
    int ExitCode,
    byte[] StandardOutput,
    byte[] StandardError,
    bool TimedOut);

internal interface ICodeGraphProcessRunner
{
    Task<CodeGraphProcessResult> RunAsync(
        CodeGraphProcessRequest request,
        CancellationToken cancellationToken);
}

internal sealed class CodeGraphProcessRunner : ICodeGraphProcessRunner
{
    public async Task<CodeGraphProcessResult> RunAsync(
        CodeGraphProcessRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using Process process = new() { StartInfo = CreateStartInfo(request) };
        if (!process.Start())
        {
            throw new InvalidOperationException("codegraph process failed to start");
        }

        using CancellationTokenSource timeout = new(request.Timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeout.Token);
        Task<byte[]> stderr = Task.FromResult(Array.Empty<byte>());
        try
        {
            if (request.CloseStandardInput)
            {
                process.StandardInput.Close();
            }

            Task<byte[]> stdout = ReadBoundedAsync(
                process.StandardOutput.BaseStream,
                request.StandardOutputByteLimit,
                cancellationToken);
            stderr = ReadBoundedAsync(
                process.StandardError.BaseStream,
                request.StandardErrorByteLimit,
                cancellationToken);

            await process.WaitForExitAsync(linked.Token);
            return new CodeGraphProcessResult(process.ExitCode,
                await stdout, await stderr, TimedOut: false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await KillAndWaitForExitAsync(process);
            return new CodeGraphProcessResult(-1, [], await IgnoreReadFailureAsync(stderr), TimedOut: true);
        }
        catch
        {
            await KillAndWaitForExitAsync(process);
            throw;
        }
    }

    private static ProcessStartInfo CreateStartInfo(CodeGraphProcessRequest request)
    {
        bool isPowerShellScript = request.FileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        ProcessStartInfo startInfo = new(isPowerShellScript ? "pwsh.exe" : request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = request.CloseStandardInput,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (isPowerShellScript)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(request.FileName);
        }

        foreach (string argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!request.InheritEnvironmentVariables)
        {
            startInfo.Environment.Clear();
        }

        foreach ((string name, string value) in request.EnvironmentVariables)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private static async Task<byte[]> ReadBoundedAsync(
        Stream stream,
        int byteLimit,
        CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new(Math.Min(byteLimit, 65_536));
        byte[] chunk = new byte[Math.Min(byteLimit + 1, 16_384)];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > byteLimit)
            {
                throw new InvalidDataException("codegraph output exceeds limit");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<byte[]> IgnoreReadFailureAsync(Task<byte[]> readTask)
    {
        try
        {
            return await readTask;
        }
        catch (InvalidDataException)
        {
            return [];
        }
    }

    private static async Task KillAndWaitForExitAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }

        await process.WaitForExitAsync(CancellationToken.None);
    }
}
