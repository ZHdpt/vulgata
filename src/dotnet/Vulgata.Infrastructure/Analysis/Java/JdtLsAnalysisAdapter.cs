using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NewtonsoftJsonFormatting = Newtonsoft.Json.Formatting;
using StreamJsonRpc;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Git;
using Vulgata.Infrastructure.Source;

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
    string WorkspacePath,
    string? EvidenceRootPath = null);

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

internal sealed class PreparedJdtLsRuntime : IPreparedJdtLsRuntime
{
    private readonly IPreparedJdtCommandResolver _commands;
    private readonly IPreparedJdtProcessLauncher _launcher;
    private readonly IPreparedJdtProtocolFactory _protocols;
    private readonly PreparedJdtHostPolicy _fixedHostPolicy;

    internal PreparedJdtLsRuntime(
        IPreparedJdtCommandResolver commands,
        IPreparedJdtProcessLauncher launcher,
        IPreparedJdtProtocolFactory protocols,
        PreparedJdtHostPolicy fixedHostPolicy)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _fixedHostPolicy = fixedHostPolicy ?? throw new ArgumentNullException(nameof(fixedHostPolicy));
    }

    public async Task<IPreparedJdtLsSession> OpenAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJavaNavigationProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceLease);
        ArgumentNullException.ThrowIfNull(profile);
        if (workspaceLease.ToolPurpose != PreparedToolPurpose.JdtNavigation ||
            profile.InitializationTimeout <= TimeSpan.Zero ||
            profile.RequestTimeout <= TimeSpan.Zero ||
            profile.ShutdownTimeout <= TimeSpan.Zero ||
            profile.MaximumResponseBytes <= 0 ||
            !Path.IsPathFullyQualified(profile.DisposableStateRoot))
        {
            throw new PreparedJdtLsOpenFailureException(
                PreparedJavaNavigationOpenStatus.ConfigurationUnavailable,
                cleanupConfirmed: true);
        }

        IJdtLsProcess? process = null;
        IPreparedJdtLsSession? initialized = null;
        Task<IJdtLsProcess>? pendingLaunch = null;
        Task<IPreparedJdtLsSession>? pendingInitialization = null;
        var pinnedFiles = new List<FileStream>();
        using var timeout = new CancellationTokenSource(profile.InitializationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeout.Token);
        try
        {
            string? root = null;
            PreparedToolDirectoryObjectIdentity? rootIdentity = null;
            bool separateState = false;
            PreparedToolWorkspaceCheckStatus scopeStatus = await workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    root = scope.GetPhysicalRootPathForTrustedToolBinding();
                    rootIdentity = scope.ObserveRootObjectIdentity();
                    separateState = scope.IsSeparateDisposableStateRoot(profile.DisposableStateRoot);
                    return ValueTask.CompletedTask;
                },
                linked.Token);
            if (scopeStatus != PreparedToolWorkspaceCheckStatus.Current ||
                root is null || rootIdentity is null || !separateState)
            {
                throw new InvalidOperationException("jdt_root_unavailable");
            }

            string stateRoot = Path.GetFullPath(profile.DisposableStateRoot);
            PreparedJdtCommandResolution resolution = await _commands.ResolveAsync(
                new PreparedJdtCommandRequest(
                    Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
                    root,
                    stateRoot,
                    _fixedHostPolicy),
                linked.Token).WaitAsync(linked.Token);
            if (resolution.Status != PreparedJdtCommandResolutionStatus.Pinned ||
                resolution.Commands is not { } commands)
            {
                throw new InvalidOperationException("jdt_command_unavailable");
            }

            PreparedToolWorkspaceCheckStatus finalScope = await workspaceLease.UseContainedRootAsync(
                PreparedToolPurpose.JdtNavigation,
                (scope, _) =>
                {
                    if (scope.ObserveRootObjectIdentity() != rootIdentity ||
                        !string.Equals(scope.GetPhysicalRootPathForTrustedToolBinding(),
                            root, StringComparison.OrdinalIgnoreCase) ||
                        !scope.IsSeparateDisposableStateRoot(stateRoot))
                    {
                        rootIdentity = null;
                    }

                    return ValueTask.CompletedTask;
                },
                linked.Token);
            if (finalScope != PreparedToolWorkspaceCheckStatus.Current || rootIdentity is null ||
                !TryPinCommands(commands, pinnedFiles))
            {
                throw new InvalidOperationException("jdt_pin_unavailable");
            }

            string rootUri = new Uri(root.TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar).AbsoluteUri;
            string dataPath = Path.Combine(stateRoot, "data");
            string configurationPath = Path.Combine(stateRoot, "configuration");
            var childEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PATH"] = string.Join(Path.PathSeparator, _fixedHostPolicy.FixedChildPathEntries),
                ["TEMP"] = Path.Combine(stateRoot, "temp"),
                ["TMP"] = Path.Combine(stateRoot, "temp"),
                ["USERPROFILE"] = Path.Combine(stateRoot, "profile"),
                ["APPDATA"] = Path.Combine(stateRoot, "profile"),
                ["PYTHONDONTWRITEBYTECODE"] = "1",
            };
            var launch = new PreparedJdtLaunchRequest(
                root,
                rootIdentity,
                rootUri,
                dataPath,
                configurationPath,
                commands,
                ["-data", dataPath, "-configuration", configurationPath],
                childEnvironment,
                _fixedHostPolicy);
            bool launchRootCurrent = false;
            PreparedToolWorkspaceCheckStatus launchScope =
                await workspaceLease.UseContainedRootAsync(
                    PreparedToolPurpose.JdtNavigation,
                    (scope, _) =>
                    {
                        launchRootCurrent = scope.ObserveRootObjectIdentity() ==
                                launch.RootIdentity &&
                            string.Equals(scope.GetPhysicalRootPathForTrustedToolBinding(),
                                launch.CanonicalPreparedRoot, StringComparison.OrdinalIgnoreCase) &&
                            scope.IsSeparateDisposableStateRoot(stateRoot);
                        return ValueTask.CompletedTask;
                    },
                    linked.Token);
            if (launchScope != PreparedToolWorkspaceCheckStatus.Current || !launchRootCurrent)
            {
                throw new InvalidOperationException("jdt_prelaunch_authority_unavailable");
            }

            pendingLaunch = _launcher.LaunchAsync(workspaceLease, launch, linked.Token);
            process = await pendingLaunch.WaitAsync(linked.Token);
            if (process is null)
            {
                throw new InvalidOperationException("jdt_process_unavailable");
            }

            pendingInitialization = _protocols.InitializeAsync(
                process,
                new PreparedJdtInitializeRequest(
                    rootUri, rootUri, "repository", FixedOfflineSettings(),
                    profile.MaximumResponseBytes),
                linked.Token);
            initialized = await pendingInitialization.WaitAsync(linked.Token);
            if (initialized is null ||
                await workspaceLease.CheckCurrentAsync(linked.Token) !=
                    PreparedToolWorkspaceCheckStatus.Current)
            {
                throw new InvalidOperationException("jdt_initialization_unavailable");
            }

            return new OwnedPreparedJdtLsSession(
                initialized, process, pinnedFiles.ToArray(), profile.ShutdownTimeout);
        }
        catch (Exception exception)
        {
            bool pendingUnconfirmed = false;
            bool pinsTransferred = false;
            if (pendingLaunch is not null && process is null)
            {
                try
                {
                    process = await pendingLaunch.WaitAsync(profile.ShutdownTimeout);
                }
                catch (TimeoutException)
                {
                    pendingUnconfirmed = true;
                    pinsTransferred = true;
                    _ = SettleLateLaunchAsync(pendingLaunch, pinnedFiles.ToArray(),
                        profile.ShutdownTimeout);
                }
                catch (Exception)
                {
                    // A completed failed launch returned no process to settle.
                }
            }

            if (pendingInitialization is not null && initialized is null)
            {
                try
                {
                    initialized = await pendingInitialization.WaitAsync(profile.ShutdownTimeout);
                }
                catch (TimeoutException)
                {
                    pendingUnconfirmed = true;
                    pinsTransferred = true;
                    _ = SettleLateInitializationAsync(pendingInitialization,
                        pinnedFiles.ToArray(), profile.ShutdownTimeout);
                }
                catch (Exception)
                {
                    // A completed failed initialization returned no session.
                }
            }

            bool settled = await SettleAsync(initialized, process, profile.ShutdownTimeout);
            if (!pinsTransferred)
            {
                DisposePins(pinnedFiles);
            }

            PreparedJavaNavigationOpenStatus status = cancellationToken.IsCancellationRequested
                ? PreparedJavaNavigationOpenStatus.Cancelled
                : timeout.IsCancellationRequested
                    ? PreparedJavaNavigationOpenStatus.DeadlineExceeded
                    : exception is PreparedJdtLsOpenFailureException failure
                        ? failure.Status
                        : PreparedJavaNavigationOpenStatus.ConfigurationUnavailable;
            throw new PreparedJdtLsOpenFailureException(
                status,
                settled && !pendingUnconfirmed &&
                (exception is not PreparedJdtLsOpenFailureException prior ||
                    prior.CleanupConfirmed));
        }
    }

    private static async Task SettleLateLaunchAsync(
        Task<IJdtLsProcess> pendingLaunch,
        FileStream[] pinnedFiles,
        TimeSpan shutdownTimeout)
    {
        try
        {
            IJdtLsProcess? lateProcess = await pendingLaunch;
            _ = await SettleAsync(null, lateProcess, shutdownTimeout);
        }
        catch (Exception)
        {
            // The failed launcher returned no process; this path never attests cleanup.
        }
        finally
        {
            DisposePins(pinnedFiles);
        }
    }

    private static async Task SettleLateInitializationAsync(
        Task<IPreparedJdtLsSession> pendingInitialization,
        FileStream[] pinnedFiles,
        TimeSpan shutdownTimeout)
    {
        try
        {
            IPreparedJdtLsSession? lateSession = await pendingInitialization;
            _ = await SettleAsync(lateSession, null, shutdownTimeout);
        }
        catch (Exception)
        {
            // The failed protocol factory returned no session; cleanup was unconfirmed.
        }
        finally
        {
            DisposePins(pinnedFiles);
        }
    }

    private static void DisposePins(IEnumerable<FileStream> pinnedFiles)
    {
        foreach (FileStream pinnedFile in pinnedFiles)
        {
            pinnedFile.Dispose();
        }
    }

    private static IReadOnlyDictionary<string, object> FixedOfflineSettings() =>
        new Dictionary<string, object>
        {
            ["java"] = new
            {
                import = new
                {
                    maven = new { enabled = false, offline = new { enabled = true } },
                    gradle = new
                    {
                        enabled = false,
                        offline = new { enabled = true },
                        wrapper = new { enabled = false },
                    },
                },
                autobuild = new { enabled = false },
                configuration = new { updateBuildConfiguration = "disabled" },
                maven = new { downloadSources = false, updateSnapshots = false },
            },
        };

    internal static bool TryPinCommands(
        PreparedJdtPinnedCommands commands,
        List<FileStream> pinnedFiles)
    {
        return TryPinFile(commands.JdtCommand, pinnedFiles) &&
            TryPinFile(commands.JavaCommand, pinnedFiles) &&
            (commands.FixedInterpreter is null ||
                TryPinFile(commands.FixedInterpreter, pinnedFiles)) &&
            (commands.PythonCommand is null ||
                TryPinFile(commands.PythonCommand, pinnedFiles)) &&
            (commands.BootstrapScript is null ||
                TryPinFile(commands.BootstrapScript, pinnedFiles)) &&
            (commands.RuntimeInputs is null ||
                commands.RuntimeInputs.All(pin => TryPinFile(pin, pinnedFiles)));
    }

    private static bool TryPinFile(PreparedJdtPinnedFile pin, List<FileStream> pinnedFiles)
    {
        if (!Path.IsPathFullyQualified(pin.CanonicalAbsolutePath) ||
            !string.Equals(Path.GetFullPath(pin.CanonicalAbsolutePath),
                pin.CanonicalAbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string? directory = Path.GetDirectoryName(pin.CanonicalAbsolutePath);
        if (directory is null)
        {
            return false;
        }

        (PreparedSourceTraversal.OpenStatus directoryStatus, SafeFileHandle? heldDirectory) =
            PreparedSourceTraversal.OpenRoot(directory);
        using (heldDirectory)
        {
            if (directoryStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                heldDirectory is null)
            {
                return false;
            }

            (PreparedSourceTraversal.OpenStatus fileStatus, SafeFileHandle? heldFile) =
                PreparedSourceTraversal.OpenRelativeFile(
                    heldDirectory, Path.GetFileName(pin.CanonicalAbsolutePath));
            using (heldFile)
            {
                PreparedSourceTraversal.FileIdentity? observed = heldFile is null
                    ? null : PreparedSourceTraversal.ObserveFile(heldFile);
                if (fileStatus != PreparedSourceTraversal.OpenStatus.Opened ||
                    observed is null ||
                    new PreparedJdtFileIdentity(observed.VolumeSerialNumber,
                        observed.FileIndexHigh, observed.FileIndexLow) != pin.FileIdentity)
                {
                    return false;
                }

                var stream = new FileStream(pin.CanonicalAbsolutePath,
                    FileMode.Open, FileAccess.Read, FileShare.Read);
                try
                {
                    PreparedSourceTraversal.FileIdentity? readIdentity =
                        PreparedSourceTraversal.ObserveFile(stream.SafeFileHandle);
                    if (readIdentity is null || readIdentity != observed ||
                        !string.Equals(Convert.ToHexString(SHA256.HashData(stream)),
                            pin.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        stream.Dispose();
                        return false;
                    }

                    pinnedFiles.Add(stream);
                    return true;
                }
                catch (Exception)
                {
                    stream.Dispose();
                    throw;
                }
            }
        }
    }

    private static async Task<bool> SettleAsync(
        IPreparedJdtLsSession? initialized,
        IJdtLsProcess? process,
        TimeSpan shutdownTimeout)
    {
        bool settled = true;
        using var deadline = new CancellationTokenSource(shutdownTimeout > TimeSpan.Zero
            ? shutdownTimeout : TimeSpan.FromSeconds(1));
        if (initialized is not null)
        {
            try { await initialized.DisposeAsync().AsTask().WaitAsync(deadline.Token); }
            catch (Exception) { settled = false; }
        }

        if (process is not null)
        {
            try { await process.KillTreeAsync(CancellationToken.None).WaitAsync(deadline.Token); }
            catch (Exception) { settled = false; }
            try { await process.WaitForExitAsync(shutdownTimeout, CancellationToken.None)
                .WaitAsync(deadline.Token); }
            catch (Exception) { settled = false; }
            try { await process.DisposeAsync().AsTask().WaitAsync(deadline.Token); }
            catch (Exception) { settled = false; }
        }

        return settled;
    }

    private sealed class OwnedPreparedJdtLsSession(
        IPreparedJdtLsSession inner,
        IJdtLsProcess process,
        FileStream[] pinnedFiles,
        TimeSpan shutdownTimeout) : IPreparedJdtLsSession
    {
        private int _disposed;

        public Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default) =>
            inner.NavigateAsync(operation, request, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            bool settled = await SettleAsync(inner, process, shutdownTimeout);
            foreach (FileStream pinnedFile in pinnedFiles)
            {
                pinnedFile.Dispose();
            }

            if (!settled)
            {
                throw new TimeoutException("jdt_cleanup_unconfirmed");
            }
        }
    }
}

internal interface IPreparedJdtCommandResolver
{
    Task<PreparedJdtCommandResolution> ResolveAsync(
        PreparedJdtCommandRequest request,
        CancellationToken cancellationToken);
}

internal sealed class PreparedJdtCommandResolver : IPreparedJdtCommandResolver
{
    private static readonly Regex PythonBootstrap = new(
        "\\A\\uFEFF?@echo off\\r?\\npython \"(?<bootstrap>[^\"\\r\\n]+)\" %\\*\\r?\\n?\\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public Task<PreparedJdtCommandResolution> ResolveAsync(
        PreparedJdtCommandRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || string.IsNullOrWhiteSpace(request.HostPathSnapshot) ||
            !Path.IsPathFullyQualified(request.PreparedRoot) ||
            !Path.IsPathFullyQualified(request.DisposableStateRoot))
        {
            return Unavailable();
        }

        try
        {
            string preparedRoot = Path.GetFullPath(request.PreparedRoot)
                .TrimEnd(Path.DirectorySeparatorChar);
            string preparedPrefix = preparedRoot + Path.DirectorySeparatorChar;
            string stateRoot = Path.GetFullPath(request.DisposableStateRoot)
                .TrimEnd(Path.DirectorySeparatorChar);
            string statePrefix = stateRoot + Path.DirectorySeparatorChar;
            string[] pathEntries = request.HostPathSnapshot.Split(Path.PathSeparator);
            for (int index = 0; index < pathEntries.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entry = pathEntries[index];
                if (!Path.IsPathFullyQualified(entry))
                {
                    return Unavailable();
                }

                string directory = Path.GetFullPath(entry).TrimEnd(Path.DirectorySeparatorChar);
                bool executable = File.Exists(Path.Combine(directory, "jdtls.exe"));
                bool batch = File.Exists(Path.Combine(directory, "jdtls.bat"));
                if (!executable && !batch)
                {
                    continue;
                }

                bool insidePreparedRoot =
                    string.Equals(directory, preparedRoot, StringComparison.OrdinalIgnoreCase) ||
                    directory.StartsWith(preparedPrefix, StringComparison.OrdinalIgnoreCase);
                bool insideDisposableStateRoot =
                    string.Equals(directory, stateRoot, StringComparison.OrdinalIgnoreCase) ||
                    directory.StartsWith(statePrefix, StringComparison.OrdinalIgnoreCase);
                if (insidePreparedRoot || insideDisposableStateRoot || executable && batch || executable ||
                    !IsApprovedDirectory(directory, request.FixedHostPolicy.ApprovedExecutableDirectories))
                {
                    return Rejected();
                }

                string batchPath = Path.Combine(directory, "jdtls.bat");
                Match match = PythonBootstrap.Match(File.ReadAllText(batchPath));
                if (!match.Success || !Path.IsPathFullyQualified(match.Groups["bootstrap"].Value))
                {
                    return Rejected();
                }

                string bootstrapPath = Path.GetFullPath(match.Groups["bootstrap"].Value);
                string runtimeRoot = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(bootstrapPath)!, ".."));
                if (!IsApprovedDirectory(runtimeRoot, request.FixedHostPolicy.DeclaredRuntimeReadRoots) ||
                    IsWithin(runtimeRoot, preparedRoot) || IsWithin(runtimeRoot, stateRoot) ||
                    !string.Equals(Path.GetFileName(bootstrapPath), "jdtls", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetFileName(Path.GetDirectoryName(bootstrapPath)), "bin", StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(Path.Combine(runtimeRoot, "bin", "jdtls.py")) ||
                    !File.Exists(Path.Combine(runtimeRoot, "config_win", "config.ini")) ||
                    Directory.GetFiles(Path.Combine(runtimeRoot, "plugins"),
                        "org.eclipse.equinox.launcher_*.jar").Length != 1)
                {
                    return Rejected();
                }

                (string? javaPath, int javaIndex) = FindFirst(pathEntries, "java.exe");
                (string? pythonPath, int pythonIndex) = FindFirst(pathEntries, "python.exe");
                string cmdPath = request.FixedHostPolicy.FixedCmdAbsolutePath;
                if (javaPath is null || pythonPath is null || !Path.IsPathFullyQualified(cmdPath) ||
                    !string.Equals(Path.GetFileName(cmdPath), "cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                    !IsApprovedExecutable(javaPath, request, preparedRoot, stateRoot) ||
                    !IsApprovedExecutable(pythonPath, request, preparedRoot, stateRoot) ||
                    !IsApprovedExecutable(cmdPath, request, preparedRoot, stateRoot))
                {
                    return Rejected();
                }

                if (!TryDescribe(batchPath, index, out PreparedJdtPinnedFile? jdtPin) ||
                    !TryDescribe(javaPath, javaIndex, out PreparedJdtPinnedFile? javaPin) ||
                    !TryDescribe(pythonPath, pythonIndex, out PreparedJdtPinnedFile? pythonPin) ||
                    !TryDescribe(cmdPath, null, out PreparedJdtPinnedFile? cmdPin) ||
                    !TryDescribe(bootstrapPath, null, out PreparedJdtPinnedFile? bootstrapPin) ||
                    !TryDescribeRuntime(request.FixedHostPolicy.DeclaredRuntimeReadRoots,
                        preparedRoot, stateRoot, cancellationToken,
                        out IReadOnlyList<PreparedJdtPinnedFile>? runtimePins))
                {
                    return Rejected();
                }

                return Task.FromResult(new PreparedJdtCommandResolution(
                    PreparedJdtCommandResolutionStatus.Pinned,
                    new PreparedJdtPinnedCommands(jdtPin!, javaPin!, cmdPin!,
                        pythonPin, bootstrapPin, runtimePins)));
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or
            PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Unavailable();
        }

        return Unavailable();
    }

    private static Task<PreparedJdtCommandResolution> Unavailable() =>
        Task.FromResult(new PreparedJdtCommandResolution(
            PreparedJdtCommandResolutionStatus.Unavailable, null));

    private static Task<PreparedJdtCommandResolution> Rejected() =>
        Task.FromResult(new PreparedJdtCommandResolution(
            PreparedJdtCommandResolutionStatus.Rejected, null));

    private static bool IsWithin(string candidate, string root) =>
        string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static bool IsApprovedDirectory(string candidate, IReadOnlyList<string> approved) =>
        approved.Any(directory => Path.IsPathFullyQualified(directory) &&
            string.Equals(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
                candidate.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase));

    private static bool IsApprovedExecutable(string path, PreparedJdtCommandRequest request,
        string preparedRoot, string stateRoot) =>
        !IsWithin(path, preparedRoot) && !IsWithin(path, stateRoot) &&
        IsApprovedDirectory(Path.GetDirectoryName(path)!,
            request.FixedHostPolicy.ApprovedExecutableDirectories);

    private static (string? Path, int Index) FindFirst(string[] entries, string fileName)
    {
        for (int i = 0; i < entries.Length; i++)
        {
            if (!Path.IsPathFullyQualified(entries[i]))
            {
                return (null, -1);
            }

            string candidate = Path.Combine(Path.GetFullPath(entries[i]), fileName);
            if (File.Exists(candidate))
            {
                return (candidate, i);
            }
        }

        return (null, -1);
    }

    private static bool TryDescribe(string path, int? index, out PreparedJdtPinnedFile? pin)
    {
        pin = null;
        string absolute = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(absolute);
        if (parent is null)
        {
            return false;
        }

        (PreparedSourceTraversal.OpenStatus directoryStatus, SafeFileHandle? directory) =
            PreparedSourceTraversal.OpenRoot(parent);
        using (directory)
        {
            if (directoryStatus != PreparedSourceTraversal.OpenStatus.Opened || directory is null)
            {
                return false;
            }

            (PreparedSourceTraversal.OpenStatus fileStatus, SafeFileHandle? file) =
                PreparedSourceTraversal.OpenRelativeFile(directory, Path.GetFileName(absolute));
            using (file)
            {
                PreparedSourceTraversal.FileIdentity? identity = file is null
                    ? null : PreparedSourceTraversal.ObserveFile(file);
                if (fileStatus != PreparedSourceTraversal.OpenStatus.Opened || identity is null)
                {
                    return false;
                }

                using var stream = new FileStream(absolute, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (PreparedSourceTraversal.ObserveFile(stream.SafeFileHandle) != identity)
                {
                    return false;
                }

                pin = new PreparedJdtPinnedFile(absolute,
                    new PreparedJdtFileIdentity(identity.VolumeSerialNumber,
                        identity.FileIndexHigh, identity.FileIndexLow),
                    Convert.ToHexString(SHA256.HashData(stream)), index);
                return true;
            }
        }
    }

    private static bool TryDescribeRuntime(IReadOnlyList<string> roots,
        string preparedRoot, string stateRoot, CancellationToken cancellationToken,
        out IReadOnlyList<PreparedJdtPinnedFile>? pins)
    {
        pins = null;
        var result = new List<PreparedJdtPinnedFile>();
        var pending = new Stack<string>();
        foreach (string declaredRoot in roots)
        {
            if (!Path.IsPathFullyQualified(declaredRoot))
            {
                return false;
            }

            string root = Path.GetFullPath(declaredRoot);
            if (IsWithin(root, preparedRoot) || IsWithin(root, stateRoot))
            {
                return false;
            }

            pending.Push(root);
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (pending.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(directory))
            {
                continue;
            }

            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                pending.Push(child);
            }

            foreach (string file in Directory.EnumerateFiles(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryDescribe(file, null, out PreparedJdtPinnedFile? pin))
                {
                    return false;
                }

                result.Add(pin!);
            }
        }

        pins = result;
        return result.Count > 0;
    }
}

internal sealed record PreparedJdtCommandRequest(
    string HostPathSnapshot,
    string PreparedRoot,
    string DisposableStateRoot,
    PreparedJdtHostPolicy FixedHostPolicy);

internal enum PreparedJdtCommandResolutionStatus
{
    Pinned,
    Missing,
    Rejected,
    Unavailable,
}

internal sealed record PreparedJdtCommandResolution(
    PreparedJdtCommandResolutionStatus Status,
    PreparedJdtPinnedCommands? Commands);

internal sealed record PreparedJdtPinnedCommands(
    PreparedJdtPinnedFile JdtCommand,
    PreparedJdtPinnedFile JavaCommand,
    PreparedJdtPinnedFile? FixedInterpreter,
    PreparedJdtPinnedFile? PythonCommand = null,
    PreparedJdtPinnedFile? BootstrapScript = null,
    IReadOnlyList<PreparedJdtPinnedFile>? RuntimeInputs = null);

internal sealed record PreparedJdtFileIdentity(
    uint VolumeSerialNumber,
    uint FileIndexHigh,
    uint FileIndexLow);

internal sealed record PreparedJdtPinnedFile(
    string CanonicalAbsolutePath,
    PreparedJdtFileIdentity FileIdentity,
    string Sha256,
    int? FirstPathEntryIndex);

internal sealed record PreparedJdtHostPolicy(
    IReadOnlyList<string> ApprovedExecutableDirectories,
    IReadOnlyList<string> DeclaredRuntimeReadRoots,
    string FixedCmdAbsolutePath,
    IReadOnlyList<string> FixedChildPathEntries);

internal interface IPreparedJdtProcessLauncher
{
    Task<IJdtLsProcess> LaunchAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJdtLaunchRequest request,
        CancellationToken cancellationToken);
}

internal sealed record PreparedJdtLaunchRequest(
    string CanonicalPreparedRoot,
    PreparedToolDirectoryObjectIdentity RootIdentity,
    string RootUri,
    string DataPath,
    string ConfigurationPath,
    PreparedJdtPinnedCommands Commands,
    IReadOnlyList<string> FixedArguments,
    IReadOnlyDictionary<string, string> FixedChildEnvironment,
    PreparedJdtHostPolicy FixedHostPolicy);

internal interface IPreparedJdtProtocolFactory
{
    Task<IPreparedJdtLsSession> InitializeAsync(
        IJdtLsProcess process,
        PreparedJdtInitializeRequest request,
        CancellationToken cancellationToken);
}

internal sealed record PreparedJdtInitializeRequest(
    string RootUri,
    string WorkspaceFolderUri,
    string WorkspaceFolderName,
    IReadOnlyDictionary<string, object> FixedSettings,
    int MaximumResponseBytes);

internal sealed class PreparedJdtProtocolFactory : IPreparedJdtProtocolFactory
{
    public async Task<IPreparedJdtLsSession> InitializeAsync(
        IJdtLsProcess process,
        PreparedJdtInitializeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaximumResponseBytes <= 0 ||
            !string.Equals(request.RootUri, request.WorkspaceFolderUri, StringComparison.Ordinal) ||
            request.FixedSettings is null)
        {
            throw new InvalidOperationException("jdt_prepared_protocol_unavailable");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var boundedOutput = new PreparedJdtBoundedResponseStream(
            process.StandardOutput, request.MaximumResponseBytes);
        JsonRpc rpc = JsonRpc.Attach(process.StandardInput, boundedOutput, target: null);
        try
        {
            _ = await rpc.InvokeWithParameterObjectAsync<JsonElement>(
                "initialize",
                new
                {
                    processId = Environment.ProcessId,
                    rootUri = request.RootUri,
                    capabilities = new { },
                    workspaceFolders = new[]
                    {
                        new { uri = request.WorkspaceFolderUri, name = request.WorkspaceFolderName },
                    },
                    initializationOptions = new { settings = request.FixedSettings },
                },
                argumentDeclaredTypes: null,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await rpc.NotifyWithParameterObjectAsync("initialized", new { }).WaitAsync(cancellationToken);
            await rpc.NotifyWithParameterObjectAsync(
                "workspace/didChangeConfiguration",
                new { settings = request.FixedSettings }).WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return new PreparedJdtProtocolSession(rpc, new Uri(request.RootUri), boundedOutput);
        }
        catch
        {
            rpc.Dispose();
            throw;
        }
    }

    private sealed class PreparedJdtProtocolSession(
        JsonRpc rpc,
        Uri rootUri,
        PreparedJdtBoundedResponseStream boundedOutput) : IPreparedJdtLsSession
    {
        public async Task<PreparedJdtLsRawReply> NavigateAsync(
            PreparedJavaNavigationOperation operation,
            PreparedJavaNavigationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (operation is not (PreparedJavaNavigationOperation.Definition or
                    PreparedJavaNavigationOperation.References) || request is null ||
                !PreparedSourceTraversal.IsCanonicalRelativePath(request.RelativePath, allowEmpty: false) ||
                request.Line <= 0 || request.Character <= 0)
            {
                throw new InvalidOperationException("jdt_prepared_navigation_unavailable");
            }

            string relativeUri = string.Join('/',
                request.RelativePath.Split('/').Select(Uri.EscapeDataString));
            string documentUri = new Uri(rootUri, relativeUri).AbsoluteUri;
            var textDocument = new { uri = documentUri };
            var position = new { line = request.Line - 1, character = request.Character - 1 };
            object parameters = operation == PreparedJavaNavigationOperation.References
                ? new { textDocument, position, context = new { includeDeclaration = true } }
                : new { textDocument, position };
            JToken responseToken;
            try
            {
                responseToken = await rpc.InvokeWithParameterObjectAsync<JToken>(
                    operation == PreparedJavaNavigationOperation.References
                        ? "textDocument/references" : "textDocument/definition",
                    parameters,
                    argumentDeclaredTypes: null,
                    cancellationToken);
            }
            catch (Exception) when (boundedOutput.LimitExceeded && !cancellationToken.IsCancellationRequested)
            {
                throw new PreparedJdtResponseBytesExceededException();
            }
            using JsonDocument responseDocument = JsonDocument.Parse(
                responseToken.ToString(NewtonsoftJsonFormatting.None));
            JsonElement response = responseDocument.RootElement;
            if (response.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("jdt_prepared_definition_invalid");
            }

            var locations = new List<PreparedJdtLsRawLocation>();
            foreach (JsonElement location in response.EnumerateArray())
            {
                JsonElement range = location.GetProperty("range");
                JsonElement start = range.GetProperty("start");
                JsonElement end = range.GetProperty("end");
                locations.Add(new PreparedJdtLsRawLocation(
                    location.GetProperty("uri").GetString() ??
                        throw new InvalidOperationException("jdt_prepared_definition_invalid"),
                    checked(start.GetProperty("line").GetInt32() + 1),
                    checked(start.GetProperty("character").GetInt32() + 1),
                    checked(end.GetProperty("line").GetInt32() + 1),
                    checked(end.GetProperty("character").GetInt32() + 1)));
            }

            return new PreparedJdtLsRawReply(locations, LimitReached: false, CapabilityLimitation: null);
        }

        public ValueTask DisposeAsync()
        {
            rpc.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class PreparedJdtResponseBytesExceededException : Exception { }

internal sealed class PreparedJdtBoundedResponseStream(Stream inner, int maximumBodyBytes) : Stream
{
    private const int MaximumHeaderBytes = 4096;
    private readonly List<byte> _header = [];
    private int _bodyRemaining;
    private bool _readingBody;
    private int _limitExceeded;

    internal bool LimitExceeded => Volatile.Read(ref _limitExceeded) != 0;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        int read = inner.Read(buffer[..ReadLength(buffer.Length)]);
        Inspect(buffer[..read]);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        int read = await inner.ReadAsync(buffer[..ReadLength(buffer.Length)], cancellationToken);
        Inspect(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int ReadLength(int requested) =>
        LimitExceeded ? throw new InvalidDataException("jdt_prepared_response_too_large") :
        _readingBody ? Math.Min(requested, _bodyRemaining) : 1;

    private void Inspect(ReadOnlySpan<byte> bytes)
    {
        if (_readingBody)
        {
            _bodyRemaining -= bytes.Length;
            if (_bodyRemaining == 0)
            {
                _readingBody = false;
            }

            return;
        }

        foreach (byte value in bytes)
        {
            _header.Add(value);
            if (_header.Count > MaximumHeaderBytes)
            {
                throw new InvalidDataException("jdt_prepared_header_invalid");
            }

            int count = _header.Count;
            if (count < 4 || _header[count - 4] != '\r' || _header[count - 3] != '\n' ||
                _header[count - 2] != '\r' || _header[count - 1] != '\n')
            {
                continue;
            }

            string header = Encoding.ASCII.GetString(_header.ToArray());
            string[] lengths = header.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (lengths.Length != 1 || !int.TryParse(lengths[0]["Content-Length:".Length..].Trim(),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int bodyLength) ||
                bodyLength < 0)
            {
                throw new InvalidDataException("jdt_prepared_header_invalid");
            }

            _header.Clear();
            if (bodyLength > maximumBodyBytes)
            {
                Volatile.Write(ref _limitExceeded, 1);
                throw new InvalidDataException("jdt_prepared_response_too_large");
            }

            _bodyRemaining = bodyLength;
            _readingBody = bodyLength != 0;
        }
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
        string distributionArchivePath,
        string distributionArchiveSha256)
    {
        _options = ValidateOptions(options);
        ValidateDistributionArchive(
            distributionArchivePath,
            distributionArchiveSha256);
        _workspaceLeases = new FileJdtLsWorkspaceLeaseProvider(_options.WorkspaceRoot);
        _processLauncher = new ProcessJdtLsLauncher();
        _protocolClients = new StreamJsonRpcJdtLsProtocolClientFactory();
    }

    public JdtLsAnalysisSessionFactory(
        JdtLsAdapterOptions options,
        string distributionArchivePath,
        string distributionArchiveSha256,
        IPreparedRevisionLeaseResolver revisionLeases)
    {
        _options = ValidateOptions(options);
        ValidateDistributionArchive(
            distributionArchivePath,
            distributionArchiveSha256);
        _workspaceLeases = new PreparedRevisionJdtLsWorkspaceLeaseProvider(
            revisionLeases,
            _options.WorkspaceRoot);
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

        string distributionConfiguration = Path.Combine(
            _options.InstallationRoot,
            "config_win",
            "config.ini");
        if (!File.Exists(distributionConfiguration))
        {
            throw new InvalidOperationException("jdt_configuration_unavailable");
        }

        string dataPath = Path.GetFullPath(Path.Combine(
            _options.WorkspaceRoot,
            binding.RepositoryId.ToString("N"),
            binding.ProfiledRevision + ".jdtls-data"));
        string workspaceRootPrefix = Path.GetFullPath(_options.WorkspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!dataPath.StartsWith(workspaceRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_data_path_escape");
        }
        Directory.CreateDirectory(dataPath);
        string configurationPath = Path.Combine(dataPath, "configuration");
        Directory.CreateDirectory(configurationPath);
        File.Copy(
            distributionConfiguration,
            Path.Combine(configurationPath, "config.ini"),
            overwrite: true);

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
                configurationPath,
                "-data",
                dataPath,
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
        string distributionArchivePath,
        string distributionArchiveSha256)
    {
        if (string.IsNullOrWhiteSpace(distributionArchivePath) ||
            !Path.IsPathFullyQualified(distributionArchivePath) ||
            string.IsNullOrWhiteSpace(distributionArchiveSha256) ||
            distributionArchiveSha256.Length != 64 ||
            distributionArchiveSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException("jdt_distribution_archive_checksum_missing");
        }

        string archivePath = Path.GetFullPath(distributionArchivePath);
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

internal sealed class PreparedRevisionJdtLsWorkspaceLeaseProvider(
    IPreparedRevisionLeaseResolver revisionLeases,
    string runtimeWorkspaceRoot) : IJdtLsWorkspaceLeaseProvider
{
    private readonly string _runtimeWorkspaceRoot = RequireRuntimeRoot(runtimeWorkspaceRoot);

    public async Task<IJdtLsWorkspaceLease> AcquireAsync(
        AnalysisScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();
        if (!revisionLeases.TryResolveLease(
                scope.WorkingCopyBindingToken,
                out ICommittedRepositorySnapshotLease? revisionLease) || revisionLease is null)
        {
            throw new InvalidOperationException("jdt_revision_lease_unavailable");
        }
        if (revisionLease.RepositoryId != scope.RepositoryId ||
            revisionLease.ScanRunId != scope.ScanRunId ||
            !string.Equals(
                revisionLease.ProfiledRevision,
                scope.ProfiledRevision,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("jdt_revision_lease_binding_mismatch");
        }

        await revisionLease.VerifyIdentityAsync(cancellationToken).ConfigureAwait(false);
        string evidenceRootPath = Path.GetFullPath(revisionLease.RootPath);
        string runtimePath = Path.GetFullPath(Path.Combine(
            _runtimeWorkspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision + ".jdtls-data"));
        string workspacePath = Path.GetFullPath(Path.Combine(
            _runtimeWorkspaceRoot,
            scope.RepositoryId.ToString("N"),
            scope.ProfiledRevision + ".jdtls-project"));
        string runtimePrefix = _runtimeWorkspaceRoot.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!runtimePath.StartsWith(runtimePrefix, StringComparison.OrdinalIgnoreCase) ||
            !workspacePath.StartsWith(runtimePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_runtime_workspace_escape");
        }

        JdtLsProjectMirrorDirectory.RequireSafeAncestors(Path.GetDirectoryName(runtimePath)!);
        Directory.CreateDirectory(runtimePath);
        JdtLsProjectMirrorDirectory.RequireSafeAncestors(runtimePath);
        FileStream? lockStream = null;
        try
        {
            lockStream = new FileStream(
                Path.Combine(runtimePath, ".vulgata-lease"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous);
            JdtLsProjectMirrorDirectory.Delete(workspacePath);
            await CopySnapshotAsync(
                evidenceRootPath,
                workspacePath,
                cancellationToken).ConfigureAwait(false);
            await revisionLease.VerifyIdentityAsync(cancellationToken).ConfigureAwait(false);
            return new PreparedRevisionJdtLsWorkspaceLease(
                new JdtLsWorkspaceBinding(
                    scope.RepositoryId,
                    scope.ScanRunId,
                    scope.ProfiledRevision,
                    scope.WorkingCopyBindingToken,
                    workspacePath,
                    evidenceRootPath),
                revisionLease,
                lockStream,
                workspacePath);
        }
        catch (IOException)
        {
            if (lockStream is not null)
            {
                await lockStream.DisposeAsync().ConfigureAwait(false);
                JdtLsProjectMirrorDirectory.Delete(workspacePath);
            }

            throw new InvalidOperationException("jdt_workspace_in_use");
        }
        catch
        {
            if (lockStream is not null)
            {
                await lockStream.DisposeAsync().ConfigureAwait(false);
                JdtLsProjectMirrorDirectory.Delete(workspacePath);
            }

            throw;
        }
    }

    private static async Task CopySnapshotAsync(
        string evidenceRootPath,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        const int maximumEntries = 10_000;
        const long maximumFileBytes = 8 * 1024 * 1024;
        const long maximumTotalBytes = 64 * 1024 * 1024;
        DirectoryInfo evidenceRoot = new(evidenceRootPath);
        if (!evidenceRoot.Exists ||
            (evidenceRoot.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("jdt_evidence_root_invalid");
        }

        Directory.CreateDirectory(workspacePath);
        string workspacePrefix = workspacePath.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        Queue<DirectoryInfo> pending = new();
        pending.Enqueue(evidenceRoot);
        int entries = 0;
        long totalBytes = 0;
        while (pending.Count != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo sourceDirectory = pending.Dequeue();
            foreach (FileSystemInfo entry in sourceDirectory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++entries > maximumEntries ||
                    (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("jdt_evidence_outside_bounds");
                }

                string relativePath = Path.GetRelativePath(evidenceRootPath, entry.FullName);
                string targetPath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));
                if (!targetPath.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("jdt_project_mirror_escape");
                }

                if (entry is DirectoryInfo directory)
                {
                    Directory.CreateDirectory(targetPath);
                    pending.Enqueue(directory);
                    continue;
                }

                FileInfo file = (FileInfo)entry;
                totalBytes = checked(totalBytes + file.Length);
                if (file.Length > maximumFileBytes || totalBytes > maximumTotalBytes)
                {
                    throw new InvalidOperationException("jdt_evidence_outside_bounds");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using FileStream source = new(
                    file.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await using FileStream target = new(
                    targetPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await source.CopyToAsync(target, 64 * 1024, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static string RequireRuntimeRoot(string value)
    {
        if (!Path.IsPathFullyQualified(value))
        {
            throw new ArgumentException(
                "A fully qualified runtime workspace root is required.",
                nameof(value));
        }

        string path = Path.GetFullPath(value);
        JdtLsProjectMirrorDirectory.RequireSafeAncestors(path);
        return path;
    }
}

internal sealed class PreparedRevisionJdtLsWorkspaceLease(
    JdtLsWorkspaceBinding binding,
    ICommittedRepositorySnapshotLease revisionLease,
    FileStream lockStream,
    string workspacePath) : IJdtLsWorkspaceLease
{
    public JdtLsWorkspaceBinding Binding { get; } = binding;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await revisionLease.VerifyIdentityAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                JdtLsProjectMirrorDirectory.Delete(workspacePath);
            }
            finally
            {
                await lockStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

}

internal static class JdtLsProjectMirrorDirectory
{
    public static void RequireSafeAncestors(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            throw new InvalidOperationException("jdt_runtime_workspace_invalid");
        }

        string current = root;
        foreach (string segment in fullPath[root.Length..].Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (TryGetAttributes(current, out FileAttributes attributes) &&
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("jdt_runtime_workspace_reparse_point");
            }
        }
    }

    public static void Delete(string path)
    {
        if (!TryGetAttributes(path, out FileAttributes rootAttributes))
        {
            return;
        }
        if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("jdt_project_mirror_reparse_point");
        }
        if ((rootAttributes & FileAttributes.Directory) == 0)
        {
            throw new InvalidOperationException("jdt_project_mirror_invalid");
        }

        RequireSafeAncestors(path);
        List<FileInfo> files = [];
        List<DirectoryInfo> directories = [];
        Queue<DirectoryInfo> pending = new();
        pending.Enqueue(new DirectoryInfo(path));
        while (pending.Count != 0)
        {
            DirectoryInfo directory = pending.Dequeue();
            foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("jdt_project_mirror_reparse_point");
                }

                if (entry is DirectoryInfo child)
                {
                    directories.Add(child);
                    pending.Enqueue(child);
                }
                else
                {
                    files.Add((FileInfo)entry);
                }
            }
        }

        foreach (FileInfo file in files)
        {
            file.Attributes = FileAttributes.Normal;
            file.Delete();
        }

        for (int index = directories.Count - 1; index >= 0; index--)
        {
            directories[index].Delete(recursive: false);
        }

        Directory.Delete(path, recursive: false);
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            return false;
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
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly JsonRpc _rpc;
    private readonly int _responseByteLimit;
    private readonly HashSet<string> _openedDocuments = new(StringComparer.OrdinalIgnoreCase);
    private string? _workspacePath;
    private string? _evidenceRootPath;

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
        _evidenceRootPath = binding.EvidenceRootPath ?? binding.WorkspacePath;
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
        return _rpc.NotifyWithParameterObjectAsync("initialized", new { });
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
            string documentPath = Path.GetFullPath(Path.Combine(
                _workspacePath ?? throw new JsonException("jdt_workspace_unavailable"),
                request.RelativePath));
            string workspacePrefix = Path.GetFullPath(_workspacePath)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!documentPath.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonException("jdt_document_outside_workspace");
            }

            string documentUri = new Uri(documentPath).AbsoluteUri;
            if (_openedDocuments.Add(documentUri))
            {
                string evidencePath = ResolveContainedPath(
                    _evidenceRootPath ?? throw new JsonException("jdt_evidence_root_unavailable"),
                    request.RelativePath,
                    "jdt_document_outside_evidence");
                var file = new FileInfo(evidencePath);
                if (!file.Exists || file.Length is <= 0 or > 4_194_304)
                {
                    throw new JsonException("jdt_document_outside_bounds");
                }

                byte[] documentBytes = await File.ReadAllBytesAsync(evidencePath, linked.Token)
                    .ConfigureAwait(false);
                string documentText = _strictUtf8.GetString(documentBytes);
                await _rpc.NotifyWithParameterObjectAsync(
                    "textDocument/didOpen",
                    new
                    {
                        textDocument = new
                        {
                            uri = documentUri,
                            languageId = "java",
                            version = 1,
                            text = documentText,
                        },
                    }).ConfigureAwait(false);
            }

            object parameters = new
            {
                textDocument = new { uri = documentUri },
                position = request.Position is null
                    ? null
                    : new { line = request.Position.Line - 1, character = request.Position.Character - 1 },
                context = method == "textDocument/references"
                    ? new { includeDeclaration = true }
                    : null,
            };
            JToken responseToken;
            do
            {
                responseToken = await _rpc.InvokeWithParameterObjectAsync<JToken>(
                    method,
                    parameters,
                    argumentDeclaredTypes: null,
                    linked.Token).ConfigureAwait(false);
                bool waitsForReadiness = method is
                    "textDocument/documentSymbol" or
                    "textDocument/implementation";
                if (!waitsForReadiness ||
                    (responseToken.Type != JTokenType.Null && responseToken.HasValues))
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), linked.Token)
                    .ConfigureAwait(false);
            }
            while (true);
            using JsonDocument responseDocument = JsonDocument.Parse(
                responseToken.ToString(NewtonsoftJsonFormatting.None));
            JsonElement response = responseDocument.RootElement;
            byte[] responseBytes = JsonSerializer.SerializeToUtf8Bytes(response);
            if (responseBytes.Length > Math.Min(_responseByteLimit, request.Bounds.ByteLimit))
            {
                return Failed(AnalysisPortFailureReason.MalformedResponse, "jdt_response_too_large");
            }

            return ParseEvidence(
                method,
                response,
                request,
                _workspacePath,
                _evidenceRootPath);
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
        string method,
        JsonElement response,
        JavaNavigationRequest request,
        string? workspacePath,
        string? evidenceRootPath)
    {
        if (response.ValueKind == JsonValueKind.Null)
        {
            return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, [], null);
        }

        if (string.Equals(method, "textDocument/documentSymbol", StringComparison.Ordinal))
        {
            return ParseDocumentSymbolEvidence(
                response,
                request,
                workspacePath,
                evidenceRootPath);
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
            string evidencePath = ResolveContainedPath(
                evidenceRootPath ?? throw new JsonException("jdt_evidence_root_unavailable"),
                relativePath,
                "jdt_location_outside_evidence");
            if (!File.Exists(evidencePath))
            {
                throw new JsonException("jdt_location_outside_evidence");
            }

            int startCharacter = range.GetProperty("start").GetProperty("character").GetInt32();
            int endCharacter = range.GetProperty("end").GetProperty("character").GetInt32();
            evidence.Add(new AnalysisSymbolEvidence(
                symbol,
                AnalysisSymbolKind.Type,
                relativePath,
                new AnalysisLineRange(startLine, Math.Max(startLine, endLine)))
            {
                ExactUtf16Range = new PreparedJdtSymbolSpan(
                    startLine - 1,
                    startCharacter,
                    endLine - 1,
                    endCharacter),
            });
        }

        return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, evidence, null);
    }

    private static JavaNavigationResult ParseDocumentSymbolEvidence(
        JsonElement response,
        JavaNavigationRequest request,
        string? workspacePath,
        string? evidenceRootPath)
    {
        if (response.ValueKind != JsonValueKind.Array)
        {
            return Failed(
                AnalysisPortFailureReason.MalformedResponse,
                "jdt_document_symbol_response_malformed");
        }

        JsonElement[] elements = response.EnumerateArray().ToArray();
        if (elements.Length == 0)
        {
            return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, [], null);
        }

        bool[] flatShapes = [.. elements.Select(element => element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("location", out _))];
        if (flatShapes.Any(value => value) && !flatShapes.All(value => value))
        {
            return Failed(
                AnalysisPortFailureReason.MalformedResponse,
                "jdt_document_symbol_response_malformed");
        }

        if (flatShapes.All(value => value))
        {
            JavaNavigationResult flat = ParseFlatDocumentSymbols(
                elements,
                request,
                workspacePath,
                evidenceRootPath);
            return flat;
        }

        var evidence = new List<AnalysisSymbolEvidence>(elements.Length);
        foreach (JsonElement element in elements)
        {
            if (evidence.Count == request.Bounds.ItemLimit)
            {
                return Failed(AnalysisPortFailureReason.MalformedResponse, "jdt_item_limit_exceeded");
            }

            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("name", out JsonElement nameElement) ||
                nameElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(nameElement.GetString()) ||
                !element.TryGetProperty("kind", out JsonElement kindElement) ||
                kindElement.ValueKind != JsonValueKind.Number ||
                !kindElement.TryGetInt32(out int kind))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kind", out _)
                        ? "jdt_document_symbol_response_malformed"
                        : "jdt_document_symbol_kind_malformed");
            }

            if (kind is not (5 or 10 or 11 or 23))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    kind is >= 1 and <= 26
                        ? "jdt_document_symbol_kind_unsupported"
                        : "jdt_document_symbol_kind_malformed");
            }

            if (!TryReadSpan(element, "range", out PreparedJdtSymbolSpan? range) ||
                !TryReadSpan(element, "selectionRange", out PreparedJdtSymbolSpan? selectionRange) ||
                !Contains(range!, selectionRange!) ||
                (element.TryGetProperty("children", out JsonElement children) &&
                    children.ValueKind != JsonValueKind.Array))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    "jdt_document_symbol_response_malformed");
            }

            string relativePath = request.RelativePath;
            string evidencePath = ResolveContainedPath(
                evidenceRootPath ?? throw new JsonException("jdt_evidence_root_unavailable"),
                relativePath,
                "jdt_location_outside_evidence");
            if (!File.Exists(evidencePath))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    "jdt_document_symbol_response_malformed");
            }

            string typeName = nameElement.GetString()!;
            PreparedJdtSymbolSpan typedRange = range! with { TypeName = typeName };
            int startLine = typedRange.StartLine + 1;
            int endLine = Math.Max(startLine, typedRange.EndLine + 1);
            evidence.Add(new AnalysisSymbolEvidence(
                typeName,
                AnalysisSymbolKind.Type,
                relativePath,
                new AnalysisLineRange(startLine, endLine))
            {
                ExactUtf16Range = typedRange,
                Provenance = AnalysisSymbolHierarchyProvenance.RootDocumentSymbol,
            });
        }

        return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, evidence, null);
    }

    private static JavaNavigationResult ParseFlatDocumentSymbols(
        IReadOnlyList<JsonElement> elements,
        JavaNavigationRequest request,
        string? workspacePath,
        string? evidenceRootPath)
    {
        var evidence = new List<AnalysisSymbolEvidence>(elements.Count);
        foreach (JsonElement element in elements)
        {
            if (evidence.Count == request.Bounds.ItemLimit ||
                !element.TryGetProperty("name", out JsonElement name) ||
                name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(name.GetString()) ||
                !element.TryGetProperty("location", out JsonElement location) ||
                !TryReadSpan(location, "range", out PreparedJdtSymbolSpan? range))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    "jdt_document_symbol_response_malformed");
            }

            string relativePath = ResolveRelativePath(location, request.RelativePath, workspacePath);
            string evidencePath = ResolveContainedPath(
                evidenceRootPath ?? throw new JsonException("jdt_evidence_root_unavailable"),
                relativePath,
                "jdt_location_outside_evidence");
            if (!File.Exists(evidencePath))
            {
                return Failed(
                    AnalysisPortFailureReason.MalformedResponse,
                    "jdt_document_symbol_response_malformed");
            }

            int startLine = range!.StartLine + 1;
            evidence.Add(new AnalysisSymbolEvidence(
                name.GetString()!,
                AnalysisSymbolKind.Type,
                relativePath,
                new AnalysisLineRange(startLine, Math.Max(startLine, range.EndLine + 1)))
            {
                ExactUtf16Range = range,
                Provenance = AnalysisSymbolHierarchyProvenance.FlatSymbolInformation,
            });
        }

        return new JavaNavigationResult(AnalysisOperationStatus.Succeeded, evidence, null);
    }

    private static bool TryReadSpan(
        JsonElement owner,
        string propertyName,
        out PreparedJdtSymbolSpan? span)
    {
        span = null;
        if (!owner.TryGetProperty(propertyName, out JsonElement range) ||
            range.ValueKind != JsonValueKind.Object ||
            !range.TryGetProperty("start", out JsonElement start) ||
            !range.TryGetProperty("end", out JsonElement end) ||
            !TryReadPosition(start, out int startLine, out int startCharacter) ||
            !TryReadPosition(end, out int endLine, out int endCharacter) ||
            Compare(startLine, startCharacter, endLine, endCharacter) >= 0)
        {
            return false;
        }

        span = new(startLine, startCharacter, endLine, endCharacter);
        return true;
    }

    private static bool TryReadPosition(
        JsonElement value,
        out int line,
        out int character)
    {
        line = 0;
        character = 0;
        return value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("line", out JsonElement lineElement) &&
            lineElement.ValueKind == JsonValueKind.Number &&
            lineElement.TryGetInt32(out line) && line >= 0 &&
            value.TryGetProperty("character", out JsonElement characterElement) &&
            characterElement.ValueKind == JsonValueKind.Number &&
            characterElement.TryGetInt32(out character) && character >= 0;
    }

    private static bool Contains(PreparedJdtSymbolSpan outer, PreparedJdtSymbolSpan inner) =>
        Compare(outer.StartLine, outer.StartCharacter, inner.StartLine, inner.StartCharacter) <= 0 &&
        Compare(inner.EndLine, inner.EndCharacter, outer.EndLine, outer.EndCharacter) <= 0;

    private static int Compare(int leftLine, int leftCharacter, int rightLine, int rightCharacter) =>
        leftLine != rightLine
            ? leftLine.CompareTo(rightLine)
            : leftCharacter.CompareTo(rightCharacter);

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

    private static string ResolveContainedPath(string rootPath, string relativePath, string error)
    {
        string root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException(error);
        }

        return fullPath;
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
