using Vulgata.Core.DomainServices;
using Microsoft.Win32.SafeHandles;
using Vulgata.Infrastructure.Source;
using ModelContextProtocol.Client;

namespace Vulgata.Infrastructure.Analysis.CodeGraph;

/// <summary>Trusted, application-selected CodeGraph stdio connection.</summary>
public sealed class PreparedCodeGraphConnection : IAsyncDisposable
{
    internal PreparedCodeGraphConnection(
        Guid repositoryId,
        string commandPath,
        string command,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        Action releaseOwner,
        Func<PreparedCodeGraphConnection, ICodeGraphWorkerToolSession>? sessionFactory = null,
        TimeSpan? remainingLifetime = null,
        TimeProvider? timeProvider = null)
    {
        RepositoryId = repositoryId;
        CommandPath = commandPath;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        Environment = environment;
        _releaseOwner = releaseOwner;
        _sessionFactory = sessionFactory ?? (connection => new NativeCodeGraphMcpClient(connection));
        _ownerLifetime = remainingLifetime ?? TimeSpan.FromMinutes(30);
        if (_ownerLifetime <= TimeSpan.Zero || _ownerLifetime > ExecutionOwnerLifetime.Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingLifetime));
        }
        _ownerClock = timeProvider ?? TimeProvider.System;
        _ownerStartedAt = _ownerClock.GetTimestamp();
        (_, _rootHandle) = PreparedSourceTraversal.OpenRoot(workingDirectory);
        if (_rootHandle is not null)
        {
            _rootIdentity = PreparedSourceTraversal.ObserveDirectory(_rootHandle);
            (_, _stateHandle) = PreparedSourceTraversal.OpenRelativeDirectory(_rootHandle, ".codegraph");
            if (_stateHandle is not null)
            {
                _stateIdentity = PreparedSourceTraversal.ObserveDirectory(_stateHandle);
            }
        }
    }

    private readonly Action _releaseOwner;
    private readonly object _lifetimeLock = new();
    private readonly SafeFileHandle? _rootHandle;
    private readonly SafeFileHandle? _stateHandle;
    private readonly PreparedSourceTraversal.DirectoryIdentity? _rootIdentity;
    private readonly PreparedSourceTraversal.DirectoryIdentity? _stateIdentity;
    private readonly Func<PreparedCodeGraphConnection, ICodeGraphWorkerToolSession> _sessionFactory;
    private readonly TimeSpan _ownerLifetime;
    private readonly TimeProvider _ownerClock;
    private readonly long _ownerStartedAt;
    private OwnedSession? _session;
    private int _sessionCount;
    private bool _disposed;
    private bool _cleanupUnconfirmed;
    private Task? _disposal;
    private readonly List<Task<ClientCompletionDetails>> _nativeCompletions = [];

    internal IReadOnlyList<Task<ClientCompletionDetails>> NativeCompletions
    {
        get { lock (_lifetimeLock) { return _nativeCompletions.ToArray(); } }
    }

    internal void ObserveNativeCompletion(Task<ClientCompletionDetails>? completion)
    {
        if (completion is null) return;
        lock (_lifetimeLock)
        {
            if (!_nativeCompletions.Contains(completion) && _nativeCompletions.Count < 21)
            {
                _nativeCompletions.Add(completion);
            }
        }
    }

    public Guid RepositoryId { get; }
    public string Command { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string WorkingDirectory { get; }
    public IReadOnlyDictionary<string, string> Environment { get; }
    internal string CommandPath { get; }

    internal bool MatchesSourceBinding(PreparedSourceGrantRequest request, SafeFileHandle heldRoot)
        => request.Approval.RepositoryId == RepositoryId &&
           string.Equals(Path.GetFullPath(request.PhysicalRootPath), Path.GetFullPath(WorkingDirectory),
               OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
           IsCurrentOwnedState(heldRoot);

    internal bool IsCurrentOwnedState(SafeFileHandle heldRoot)
    {
        lock (_lifetimeLock)
        {
            if (!OwnerAvailable ||
                !SameObject(PreparedSourceTraversal.ObserveDirectory(heldRoot), _rootIdentity))
            {
                return false;
            }
            (_, SafeFileHandle? currentRoot) = PreparedSourceTraversal.OpenRoot(WorkingDirectory);
            using (currentRoot)
            {
                if (currentRoot is null ||
                    !SameObject(PreparedSourceTraversal.ObserveDirectory(currentRoot), _rootIdentity))
                {
                    return false;
                }
                (_, SafeFileHandle? currentState) = PreparedSourceTraversal.OpenRelativeDirectory(currentRoot, ".codegraph");
                using (currentState)
                {
                    return currentState is not null &&
                        SameObject(PreparedSourceTraversal.ObserveDirectory(currentState), _stateIdentity);
                }
            }
        }
    }

    internal PreparedSourceTraversal.DirectoryIdentity? ExcludedStateFor(SafeFileHandle directory)
        => SameObject(PreparedSourceTraversal.ObserveDirectory(directory), _rootIdentity) ? _stateIdentity : null;

    internal bool MatchesPreparedObjects(
        PreparedSourceTraversal.DirectoryIdentity root, PreparedSourceTraversal.DirectoryIdentity state)
        => SameObject(_rootIdentity, root) && SameObject(_stateIdentity, state);

    private bool OwnerAvailable => !_disposed && !_cleanupUnconfirmed &&
        _ownerClock.GetElapsedTime(_ownerStartedAt) < _ownerLifetime;

    internal static bool SameObject(
        PreparedSourceTraversal.DirectoryIdentity? left, PreparedSourceTraversal.DirectoryIdentity? right)
        => left is not null && right is not null && left.VolumeSerialNumber == right.VolumeSerialNumber &&
           left.FileIndexHigh == right.FileIndexHigh && left.FileIndexLow == right.FileIndexLow;

    /// <summary>Opens the single owner-held native session for trusted composition.</summary>
    public ICodeGraphWorkerToolSession OpenSession()
        => OpenSessionCore(null);

    public ICodeGraphWorkerToolSession OpenSession(
        Func<CancellationToken, Task<PreparedSourceIssueResult>> openContainedRead)
    {
        ArgumentNullException.ThrowIfNull(openContainedRead);
        return OpenSessionCore(openContainedRead);
    }

    private ICodeGraphWorkerToolSession OpenSessionCore(
        Func<CancellationToken, Task<PreparedSourceIssueResult>>? openContainedRead)
    {
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!OwnerAvailable)
            {
                throw new InvalidOperationException("The prepared connection lifetime is closed.");
            }
            if (_session is not null)
            {
                throw new InvalidOperationException("The prepared connection already owns a native session.");
            }
            if (_sessionCount >= 21)
            {
                throw new InvalidOperationException("The prepared connection exhausted its session replacements.");
            }

            _sessionCount++;
            return _session = new OwnedSession(this, openContainedRead is null
                ? _sessionFactory(this)
                : new NativeCodeGraphMcpClient(this, openContainedRead));
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeLock)
        {
            if (_disposal is not null)
            {
                return new ValueTask(_disposal);
            }

            _disposed = true;
            _disposal = DisposeCoreAsync(_session);
            return new ValueTask(_disposal);
        }
    }

    private async Task DisposeCoreAsync(OwnedSession? session)
    {
        if (session is not null)
        {
            await session.DisposeAsync();
        }

        _releaseOwner();
        _stateHandle?.Dispose();
        _rootHandle?.Dispose();
    }

    private sealed class OwnedSession(
        PreparedCodeGraphConnection owner,
        ICodeGraphWorkerToolSession inner) : ICodeGraphWorkerToolSession
    {
        private readonly SemaphoreSlim _disposalGate = new(1, 1);
        private bool _disposed;
        private bool _cleanupFailed;

        public Task<CodeGraphToolReadiness> DiscoverAsync(CancellationToken cancellationToken)
            => owner.OwnerAvailable
                ? inner.DiscoverAsync(cancellationToken)
                : Task.FromResult(new CodeGraphToolReadiness(CodeGraphToolReadinessStatus.Expired,
                    owner.RepositoryId, CodeGraphToolStage.Discovery, CodeGraphToolIssueCode.SessionExpired));

        public Task<CodeGraphToolInvocationResult> ExploreAsync(
            string query, int maxFiles, CancellationToken cancellationToken)
            => owner.OwnerAvailable
                ? inner.ExploreAsync(query, maxFiles, cancellationToken)
                : Task.FromResult(new CodeGraphToolInvocationResult
                {
                    Status = CodeGraphToolInvocationStatus.Expired,
                    RepositoryId = owner.RepositoryId,
                    IssueCode = CodeGraphToolIssueCode.SessionExpired,
                });

        public async ValueTask DisposeAsync()
        {
            await _disposalGate.WaitAsync();
            try
            {
                if (_cleanupFailed)
                {
                    throw new InvalidOperationException("Native session cleanup was not confirmed.");
                }
                if (_disposed)
                {
                    return;
                }
                try
                {
                    await inner.DisposeAsync();
                }
                catch
                {
                    _cleanupFailed = true;
                    lock (owner._lifetimeLock)
                    {
                        owner._cleanupUnconfirmed = true;
                    }
                    throw;
                }
                _disposed = true;
                lock (owner._lifetimeLock)
                {
                    if (ReferenceEquals(owner._session, this))
                    {
                        owner._session = null;
                    }
                }
            }
            finally
            {
                _disposalGate.Release();
            }
        }
    }
}
