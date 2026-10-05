using Vulgata.Core.DomainServices;

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
        Action releaseOwner)
    {
        RepositoryId = repositoryId;
        CommandPath = commandPath;
        Command = command;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        Environment = environment;
        _releaseOwner = releaseOwner;
    }

    private readonly Action _releaseOwner;
    private readonly object _lifetimeLock = new();
    private NativeCodeGraphMcpClient? _session;
    private bool _disposed;

    public Guid RepositoryId { get; }
    public string Command { get; }
    public IReadOnlyList<string> Arguments { get; }
    public string WorkingDirectory { get; }
    public IReadOnlyDictionary<string, string> Environment { get; }
    internal string CommandPath { get; }

    /// <summary>Opens the single owner-held native session for trusted composition.</summary>
    public ICodeGraphWorkerToolSession OpenSession()
    {
        lock (_lifetimeLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                throw new InvalidOperationException("The prepared connection already owns a native session.");
            }

            return _session = new NativeCodeGraphMcpClient(this);
        }
    }

    public async ValueTask DisposeAsync()
    {
        NativeCodeGraphMcpClient? session;
        lock (_lifetimeLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
        }

        try
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }
        }
        finally
        {
            _releaseOwner();
        }
    }
}
