using System.Text;
using Microsoft.Extensions.Logging;

namespace Vulgata.Web.Logging;

internal sealed class LocalFileLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    private bool _disposed;

    internal LocalFileLoggerProvider(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    public ILogger CreateLogger(string categoryName) => new LocalFileLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        ArgumentNullException.ThrowIfNull(scopeProvider);
        _scopeProvider = scopeProvider;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _writer.Dispose();
            _disposed = true;
        }
    }

    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        string timestamp = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _writer.Write(timestamp);
            _writer.Write(" [");
            _writer.Write(logLevel);
            _writer.Write("] ");
            _writer.Write(categoryName);
            if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
            {
                _writer.Write(" (");
                _writer.Write(eventId);
                _writer.Write(')');
            }

            _writer.Write(": ");
            _writer.WriteLine(message);
            if (exception is not null)
            {
                _writer.WriteLine(exception);
            }
        }
    }

    private sealed class LocalFileLogger(
        LocalFileLoggerProvider provider,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => provider._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (IsEnabled(logLevel))
            {
                provider.Write(categoryName, logLevel, eventId, state, exception, formatter);
            }
        }
    }
}
