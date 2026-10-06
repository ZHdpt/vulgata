using System.Diagnostics;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Vulgata.Infrastructure.Source;

namespace Vulgata.Infrastructure.Analysis.Java;

/// <summary>Starts only the inspected Python bootstrap selected by the prepared resolver.</summary>
internal sealed class PreparedJdtProcessLauncher : IPreparedJdtProcessLauncher
{
    private static readonly ConcurrentDictionary<int, PreparedJdtOwnedProcess> _unsettledProcesses = new();
    private readonly Func<ProcessStartInfo, Process?> _startProcess;

    internal PreparedJdtProcessLauncher()
        : this(static start => Process.Start(start))
    {
    }

    internal PreparedJdtProcessLauncher(Func<ProcessStartInfo, Process?> startProcess)
    {
        _startProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    public async Task<IJdtLsProcess> LaunchAsync(
        PreparedToolWorkspaceLease workspaceLease,
        PreparedJdtLaunchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceLease);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Commands.PythonCommand is null ||
            request.Commands.BootstrapScript is null ||
            request.Commands.RuntimeInputs is not { Count: > 0 } ||
            request.Commands.FixedInterpreter is null ||
            request.FixedArguments.Count != 4 ||
            request.FixedArguments[0] != "-data" ||
            request.FixedArguments[1] != request.DataPath ||
            request.FixedArguments[2] != "-configuration" ||
            request.FixedArguments[3] != request.ConfigurationPath ||
            !Path.IsPathFullyQualified(request.DataPath) ||
            !Path.IsPathFullyQualified(request.ConfigurationPath))
        {
            throw new InvalidOperationException("jdt_launch_policy_unavailable");
        }

        string stateRoot = Path.GetDirectoryName(request.DataPath) ??
            throw new InvalidOperationException("jdt_state_unavailable");
        if (!string.Equals(Path.Combine(stateRoot, "data"), request.DataPath,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.Combine(stateRoot, "configuration"), request.ConfigurationPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_state_unavailable");
        }

        bool current = false;
        PreparedToolWorkspaceCheckStatus status = await workspaceLease.UseContainedRootAsync(
            PreparedToolPurpose.JdtNavigation,
            (scope, _) =>
            {
                current = scope.ObserveRootObjectIdentity() == request.RootIdentity &&
                    string.Equals(scope.GetPhysicalRootPathForTrustedToolBinding(),
                        request.CanonicalPreparedRoot, StringComparison.OrdinalIgnoreCase) &&
                    scope.IsSeparateDisposableStateRoot(stateRoot);
                return ValueTask.CompletedTask;
            }, cancellationToken);
        if (status != PreparedToolWorkspaceCheckStatus.Current || !current)
        {
            throw new InvalidOperationException("jdt_prelaunch_authority_unavailable");
        }

        var lastPins = new List<FileStream>();
        try
        {
            if (!PreparedJdtLsRuntime.TryPinCommands(request.Commands, lastPins))
            {
                throw new InvalidOperationException("jdt_prelaunch_pin_unavailable");
            }

            cancellationToken.ThrowIfCancellationRequested();
            string tempPath = RequiredOwnedPath(request, "TEMP", stateRoot, "temp");
            string profilePath = RequiredOwnedPath(request, "USERPROFILE", stateRoot, "profile");
            if (RequiredOwnedPath(request, "TMP", stateRoot, "temp") != tempPath ||
                RequiredOwnedPath(request, "APPDATA", stateRoot, "profile") != profilePath ||
                !request.FixedChildEnvironment.TryGetValue("PYTHONDONTWRITEBYTECODE", out string? noBytecode) ||
                noBytecode != "1" ||
                !request.FixedChildEnvironment.TryGetValue("PATH", out string? fixedPath) ||
                fixedPath != string.Join(Path.PathSeparator, request.FixedHostPolicy.FixedChildPathEntries))
            {
                throw new InvalidOperationException("jdt_child_environment_unavailable");
            }

            var ownedState = new PreparedJdtOwnedStateDirectories(stateRoot);
            bool startAttempted = false;
            Process? startedProcess = null;
            try
            {
                ownedState.Prepare(request.DataPath, request.ConfigurationPath,
                    tempPath, profilePath);

                cancellationToken.ThrowIfCancellationRequested();
                if (await workspaceLease.CheckCurrentAsync(cancellationToken) !=
                    PreparedToolWorkspaceCheckStatus.Current ||
                    !PreparedJdtLsRuntime.TryPinCommands(request.Commands, lastPins))
                {
                    throw new InvalidOperationException("jdt_prelaunch_authority_unavailable");
                }

                ProcessStartInfo start = new()
                {
                    FileName = request.Commands.PythonCommand.CanonicalAbsolutePath,
                    WorkingDirectory = request.CanonicalPreparedRoot,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                start.ArgumentList.Add(request.Commands.BootstrapScript.CanonicalAbsolutePath);
                start.ArgumentList.Add("--java-executable");
                start.ArgumentList.Add(request.Commands.JavaCommand.CanonicalAbsolutePath);
                foreach (string argument in request.FixedArguments)
                {
                    start.ArgumentList.Add(argument);
                }

                start.Environment.Clear();
                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ??
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                start.Environment["SystemRoot"] = systemRoot;
                foreach ((string key, string value) in request.FixedChildEnvironment)
                {
                    start.Environment[key] = value;
                }

                cancellationToken.ThrowIfCancellationRequested();
                bool finalRootCurrent = false;
                PreparedToolWorkspaceCheckStatus finalStatus =
                    await workspaceLease.UseContainedRootAsync(
                        PreparedToolPurpose.JdtNavigation,
                        (scope, _) =>
                        {
                            finalRootCurrent = scope.ObserveRootObjectIdentity() ==
                                    request.RootIdentity &&
                                string.Equals(scope.GetPhysicalRootPathForTrustedToolBinding(),
                                    request.CanonicalPreparedRoot, StringComparison.OrdinalIgnoreCase) &&
                                scope.IsSeparateDisposableStateRoot(stateRoot);
                            return ValueTask.CompletedTask;
                        }, cancellationToken);
                if (finalStatus != PreparedToolWorkspaceCheckStatus.Current || !finalRootCurrent)
                {
                    throw new InvalidOperationException("jdt_prelaunch_authority_unavailable");
                }

                if (!PreparedJdtCommandResolver.ConfiguredJavaMatches(
                        request.Commands, request.FixedHostPolicy) ||
                    !PreparedJdtCommandResolver.AliasesCurrent(
                        request.Commands, request.FixedHostPolicy,
                        request.CanonicalPreparedRoot, stateRoot))
                {
                    throw new InvalidOperationException("jdt_prelaunch_alias_unavailable");
                }

                startAttempted = true;
                Process process = _startProcess(start) ??
                    throw new InvalidOperationException("jdt_process_start_failed");
                startedProcess = process;
                var owned = new PreparedJdtOwnedProcess(process, ownedState);
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await owned.KillTreeAsync(CancellationToken.None);
                        await owned.WaitForExitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                        await owned.DisposeAsync();
                    }
                    catch (Exception)
                    {
                        RetainForCleanup(process.Id, owned);
                        throw new PreparedJdtLsOpenFailureException(
                            PreparedJavaNavigationOpenStatus.CleanupUnconfirmed,
                            cleanupConfirmed: false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                return owned;
            }
            catch (Exception failure)
            {
                if (startedProcess is null)
                {
                    bool noChildStarted = !startAttempted ||
                        failure is Win32Exception { NativeErrorCode: 2 or 3 or 5 or 193 or 216 or 740 };
                    if (!noChildStarted)
                    {
                        throw new PreparedJdtLsOpenFailureException(
                            PreparedJavaNavigationOpenStatus.CleanupUnconfirmed,
                            cleanupConfirmed: false);
                    }

                    try
                    {
                        ownedState.CleanupAfterConfirmedExit();
                    }
                    catch (Exception)
                    {
                        throw new PreparedJdtLsOpenFailureException(
                            PreparedJavaNavigationOpenStatus.CleanupUnconfirmed,
                            cleanupConfirmed: false);
                    }
                }

                throw;
            }
        }
        finally
        {
            foreach (FileStream pin in lastPins)
            {
                pin.Dispose();
            }
        }
    }

    private static void RetainForCleanup(int processId, PreparedJdtOwnedProcess owned)
    {
        _unsettledProcesses[processId] = owned;
        _ = Task.Run(async () =>
        {
            try
            {
                await owned.KillTreeAsync(CancellationToken.None);
                await owned.WaitForExitAsync(TimeSpan.FromSeconds(10), CancellationToken.None);
                await owned.DisposeAsync();
                _unsettledProcesses.TryRemove(processId, out _);
            }
            catch (Exception)
            {
                // Keep the exact started process handle retained for an unknown settlement.
            }
        });
    }

    private static string RequiredOwnedPath(PreparedJdtLaunchRequest request, string name,
        string stateRoot, string child)
    {
        string expected = Path.Combine(stateRoot, child);
        if (!request.FixedChildEnvironment.TryGetValue(name, out string? actual) ||
            !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("jdt_child_environment_unavailable");
        }

        return actual;
    }

}

/// <summary>Tracks only directories this launch created under its selected state root.</summary>
internal sealed class PreparedJdtOwnedStateDirectories(string stateRoot)
{
    private readonly List<OwnedDirectory> _createdDirectories = [];
    private PreparedToolDirectoryObjectIdentity? _stateRootIdentity;

    internal void Prepare(string dataPath, string configurationPath,
        string tempPath, string profilePath)
    {
        string[] children = [dataPath, configurationPath, tempPath, profilePath];
        if (children.Any(path => Directory.Exists(path) || File.Exists(path)))
        {
            throw new InvalidOperationException("jdt_state_freshness_unavailable");
        }

        EnsureOwnedDirectory(stateRoot, allowExisting: true);
        _stateRootIdentity = ObserveDirectoryIdentity(stateRoot) ??
            throw new InvalidOperationException("jdt_state_reparse_unavailable");
        foreach (string child in children)
        {
            EnsureOwnedDirectory(child, allowExisting: false);
        }
    }

    internal void CleanupAfterConfirmedExit()
    {
        if (_stateRootIdentity is null ||
            ObserveDirectoryIdentity(stateRoot) != _stateRootIdentity)
        {
            throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
        }

        foreach (OwnedDirectory directory in _createdDirectories.AsEnumerable().Reverse())
        {
            if (ObserveDirectoryIdentity(directory.Path) != directory.Identity)
            {
                throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
            }

            DeleteOwnedTree(directory.Path, directory.Identity);
        }
    }

    private void EnsureOwnedDirectory(string path, bool allowExisting)
    {
        if (!allowExisting && ObserveDirectoryIdentity(stateRoot) != _stateRootIdentity)
        {
            throw new InvalidOperationException("jdt_state_reparse_unavailable");
        }

        if (Directory.Exists(path))
        {
            if (!allowExisting)
            {
                throw new InvalidOperationException("jdt_state_freshness_unavailable");
            }

            if (ObserveDirectoryIdentity(path) is null)
            {
                throw new InvalidOperationException("jdt_state_reparse_unavailable");
            }

            return;
        }

        string? parent = Path.GetDirectoryName(path);
        if (parent is null || ObserveDirectoryIdentity(parent) is null)
        {
            throw new InvalidOperationException("jdt_state_reparse_unavailable");
        }

        Directory.CreateDirectory(path);
        PreparedToolDirectoryObjectIdentity identity = ObserveDirectoryIdentity(path) ??
            throw new InvalidOperationException("jdt_state_reparse_unavailable");
        if (!allowExisting && ObserveDirectoryIdentity(stateRoot) != _stateRootIdentity)
        {
            throw new InvalidOperationException("jdt_state_reparse_unavailable");
        }

        _createdDirectories.Add(new OwnedDirectory(path, identity));
    }

    private static PreparedToolDirectoryObjectIdentity? ObserveDirectoryIdentity(string path)
    {
        (PreparedSourceTraversal.OpenStatus status, SafeFileHandle? handle) =
            PreparedSourceTraversal.OpenRoot(path);
        using (handle)
        {
            PreparedSourceTraversal.DirectoryIdentity? observed = handle is null
                ? null : PreparedSourceTraversal.ObserveDirectory(handle);
            return status == PreparedSourceTraversal.OpenStatus.Opened && observed is not null
                ? new PreparedToolDirectoryObjectIdentity(observed.VolumeSerialNumber,
                    observed.FileIndexHigh, observed.FileIndexLow)
                : null;
        }
    }

    private static void DeleteOwnedTree(string path,
        PreparedToolDirectoryObjectIdentity expected)
    {
        if (ObserveDirectoryIdentity(path) != expected)
        {
            throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
        }

        foreach (string child in Directory.EnumerateFileSystemEntries(path))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
            }

            if (Directory.Exists(child))
            {
                PreparedToolDirectoryObjectIdentity childIdentity =
                    ObserveDirectoryIdentity(child) ??
                    throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
                DeleteOwnedTree(child, childIdentity);
            }
            else
            {
                File.Delete(child);
            }
        }

        if (ObserveDirectoryIdentity(path) != expected)
        {
            throw new InvalidOperationException("jdt_state_cleanup_unconfirmed");
        }

        Directory.Delete(path);
    }

    private sealed record OwnedDirectory(
        string Path, PreparedToolDirectoryObjectIdentity Identity);
}

internal sealed class PreparedJdtOwnedProcess : IJdtLsProcess
{
    private const int MaximumCapturedStderrCharacters = 16_384;
    private readonly Process _process;
    private readonly PreparedJdtOwnedStateDirectories _ownedState;
    private readonly StringBuilder _stderr = new();
    private readonly Lock _stderrLock = new();
    private readonly Task _stderrPump;

    internal PreparedJdtOwnedProcess(Process process, PreparedJdtOwnedStateDirectories ownedState)
    {
        _process = process;
        _ownedState = ownedState;
        _stderrPump = DrainStderrAsync();
    }

    public Stream StandardInput => _process.StandardInput.BaseStream;

    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    public Task<string> ReadBoundedStandardErrorAsync(int byteLimit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (byteLimit <= 0)
        {
            return Task.FromResult(string.Empty);
        }

        lock (_stderrLock)
        {
            return Task.FromResult(_stderr.ToString(0, Math.Min(_stderr.Length, byteLimit)));
        }
    }

    public async Task<int> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadline.Token);
        try
        {
            await _process.WaitForExitAsync(linked.Token);
            await _stderrPump.WaitAsync(linked.Token);
            return _process.ExitCode;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("jdt_process_wait_timeout");
        }
    }

    public Task KillTreeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            throw new InvalidOperationException("jdt_process_still_running");
        }

        _ownedState.CleanupAfterConfirmedExit();
        _process.Dispose();

        return ValueTask.CompletedTask;
    }

    private async Task DrainStderrAsync()
    {
        char[] buffer = new char[4_096];
        while (true)
        {
            int read = await _process.StandardError.ReadAsync(buffer);
            if (read == 0)
            {
                return;
            }

            lock (_stderrLock)
            {
                int take = Math.Min(read, MaximumCapturedStderrCharacters - _stderr.Length);
                if (take > 0)
                {
                    _stderr.Append(buffer, 0, take);
                }
            }
        }
    }

}
