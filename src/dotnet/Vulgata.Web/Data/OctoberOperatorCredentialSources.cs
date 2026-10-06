using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Vulgata.Web.Data;

public interface IOctoberOperatorCredentialSource
{
    ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken);
}

public interface IOctoberCredentialTerminal
{
    bool IsInteractive { get; }
    ValueTask<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken);
    void Write(string value);
}

public sealed class OctoberOperatorCredentials : IDisposable
{
    private string? _userName;
    private string? _password;
    private string? _authenticatorCode;
    private string? _recoveryCode;

    public OctoberOperatorCredentials(
        string userName,
        string password,
        string? authenticatorCode = null,
        string? recoveryCode = null)
    {
        if (authenticatorCode is not null && recoveryCode is not null)
        {
            throw new ArgumentException("Only one operator authentication factor may be supplied.");
        }

        _userName = userName ?? throw new ArgumentNullException(nameof(userName));
        _password = password ?? throw new ArgumentNullException(nameof(password));
        _authenticatorCode = authenticatorCode;
        _recoveryCode = recoveryCode;
    }

    public string UserName => _userName ?? throw new ObjectDisposedException(nameof(OctoberOperatorCredentials));
    public string Password => _password ?? throw new ObjectDisposedException(nameof(OctoberOperatorCredentials));
    public string? AuthenticatorCode => _authenticatorCode;
    public string? RecoveryCode => _recoveryCode;

    public void Dispose()
    {
        _userName = null;
        _password = null;
        _authenticatorCode = null;
        _recoveryCode = null;
    }

    public override string ToString() => nameof(OctoberOperatorCredentials);
}

public sealed class OctoberConsoleCredentialSource : IOctoberOperatorCredentialSource
{
    private const string Unavailable = "Operator credential input is unavailable.";
    private static readonly SemaphoreSlim _systemConsoleEntry = new(1, 1);
    private readonly IOctoberCredentialTerminal _terminal;
    private readonly SemaphoreSlim _entry = new(1, 1);

    public OctoberConsoleCredentialSource() : this(new SystemCredentialTerminal())
    {
    }

    public OctoberConsoleCredentialSource(IOctoberCredentialTerminal terminal)
    {
        _terminal = terminal ?? throw new ArgumentNullException(nameof(terminal));
    }

    public async ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SemaphoreSlim admission = _terminal is SystemCredentialTerminal ? _systemConsoleEntry : _entry;
        await admission.WaitAsync(cancellationToken);
        try
        {
            if (!_terminal.IsInteractive)
            {
                throw new InvalidOperationException(Unavailable);
            }

            string userName = await ReadFieldAsync("User name: ", cancellationToken);
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            string password = await ReadFieldAsync("Password: ", cancellationToken);
            if (string.IsNullOrEmpty(password))
            {
                return null;
            }

            string authenticatorCode = await ReadFieldAsync("Authenticator code (optional): ", cancellationToken);
            string recoveryCode = await ReadFieldAsync("Recovery code (optional): ", cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(authenticatorCode) && !string.IsNullOrEmpty(recoveryCode))
            {
                throw new InvalidOperationException(Unavailable);
            }

            return new OctoberOperatorCredentials(
                userName,
                password,
                string.IsNullOrEmpty(authenticatorCode) ? null : authenticatorCode,
                string.IsNullOrEmpty(recoveryCode) ? null : recoveryCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
            NotSupportedException or ArgumentOutOfRangeException)
        {
            throw new InvalidOperationException(Unavailable);
        }
        finally
        {
            admission.Release();
        }
    }

    private async ValueTask<string> ReadFieldAsync(string prompt, CancellationToken cancellationToken)
    {
        _terminal.Write(prompt);
        StringBuilder characters = new();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ConsoleKeyInfo key = await _terminal.ReadKeyAsync(intercept: true, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (key.Key == ConsoleKey.Enter)
            {
                _terminal.Write(Environment.NewLine);
                return characters.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Length > 0)
                {
                    characters.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Append(key.KeyChar);
            }
        }
    }

    private sealed class SystemCredentialTerminal : IOctoberCredentialTerminal
    {
        private static readonly TimeSpan _keyPollInterval = TimeSpan.FromMilliseconds(25);

        public bool IsInteractive =>
            !Console.IsInputRedirected && !Console.IsOutputRedirected && Environment.UserInteractive;

        public async ValueTask<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Source admission keeps this application's console reads exclusive.
            // ReadKey is entered only after a key is available; waiting is cancellable.
            while (!Console.KeyAvailable)
            {
                await Task.Delay(_keyPollInterval, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Console.ReadKey(intercept);
        }

        public void Write(string value) => Console.Write(value);
    }
}

public sealed class OctoberProtectedFileCredentialSource : IOctoberOperatorCredentialSource
{
    private const string Purpose = "october-operator-credentials-v1";
    private const string Unavailable = "Operator credential input is unavailable.";
    private readonly string _path;
    private readonly IDataProtector _protector;

    public OctoberProtectedFileCredentialSource(string path, IDataProtectionProvider protection)
    {
        _path = !string.IsNullOrWhiteSpace(path)
            ? path
            : throw new ArgumentException("An explicit operator credential file is required.", nameof(path));
        _protector = (protection ?? throw new ArgumentNullException(nameof(protection)))
            .CreateProtector(Purpose);
    }

    public async ValueTask<OctoberOperatorCredentials?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string protectedPayload;
        try
        {
            protectedPayload = await File.ReadAllTextAsync(_path, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            throw new InvalidOperationException(Unavailable);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException(Unavailable);
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string payload = _protector.Unprotect(protectedPayload);
            using JsonDocument document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(Unavailable);
            }

            string? userName = null;
            string? password = null;
            string? authenticatorCode = null;
            string? recoveryCode = null;
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException(Unavailable);
                }

                string? value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Null => null,
                    _ => throw new InvalidDataException(Unavailable)
                };
                switch (property.Name)
                {
                    case "userName": userName = value; break;
                    case "password": password = value; break;
                    case "authenticatorCode": authenticatorCode = value; break;
                    case "recoveryCode": recoveryCode = value; break;
                    default: throw new InvalidDataException(Unavailable);
                }
            }

            if (names.Count != 4 || string.IsNullOrWhiteSpace(userName) ||
                string.IsNullOrEmpty(password) ||
                authenticatorCode is not null && recoveryCode is not null)
            {
                throw new InvalidDataException(Unavailable);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new OctoberOperatorCredentials(userName, password, authenticatorCode, recoveryCode);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new InvalidOperationException(Unavailable);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(Unavailable);
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException(Unavailable);
        }
    }
}
