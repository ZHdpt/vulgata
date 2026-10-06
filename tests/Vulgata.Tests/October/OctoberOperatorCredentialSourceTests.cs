using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Tests.TestInfrastructure;
using Vulgata.Web.Data;

namespace Vulgata.Tests.October;

public sealed class OctoberOperatorCredentialSourceTests
{
    private const string Purpose = "october-operator-credentials-v1";
    private const string UserName = "october.operator@example.test";
    private const string Password = "Synthetic-only-operator-password-2042";
    private const string AuthenticatorCode = "123456";
    private const string ValidPayload = """{"userName":"october.operator@example.test","password":"Synthetic-only-operator-password-2042","authenticatorCode":"123456","recoveryCode":null}""";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_RedirectedConsole_FailsVisiblyWithSanitizedUnavailable()
    {
        Assert.True(
            Console.IsInputRedirected || Console.IsOutputRedirected,
            "The SelfContained testhost must redirect at least one stream and must not act as a credential terminal.");

        OctoberConsoleCredentialSource source = new();
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await source.ReadAsync(CancellationToken.None);
        });

        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.DoesNotContain("password", failure.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_PreCancelledRequest_PropagatesCancellationBeforeConsoleCheck()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        OctoberConsoleCredentialSource source = new();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await source.ReadAsync(cancellation.Token);
        });
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_InteractiveTerminal_ReturnsCredentialsWithoutEchoingSecrets()
    {
        const string consoleUserName = "october.console.operator@example.test";
        const string consolePassword = "Synthetic-only-console-password-2044";
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, consoleUserName);
        QueueTextField(keys, consolePassword);
        QueueTextField(keys, string.Empty);
        QueueTextField(keys, string.Empty);
        ControlledCredentialTerminal terminal = new(keys);
        OctoberConsoleCredentialSource source = new(terminal);

        using OctoberOperatorCredentials credentials = Assert.IsType<OctoberOperatorCredentials>(
            await source.ReadAsync(CancellationToken.None));

        Assert.Equal(consoleUserName, credentials.UserName);
        Assert.Equal(consolePassword, credentials.Password);
        Assert.Null(credentials.AuthenticatorCode);
        Assert.Null(credentials.RecoveryCode);
        Assert.Equal(0, terminal.RemainingKeyCount);
        Assert.DoesNotContain(consoleUserName, terminal.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(consolePassword, terminal.Output, StringComparison.Ordinal);
        Assert.Equal(0, terminal.DisposeCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("authenticator")]
    [InlineData("recovery")]
    public async Task ReadAsync_OptionalFactor_ReturnsOnlyTheSuppliedFactor(string factor)
    {
        const string consoleUserName = "october.factor.operator@example.test";
        const string consolePassword = "Synthetic-only-factor-password-2044";
        const string factorValue = "Synthetic-only-factor-value-2044";
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, consoleUserName);
        QueueTextField(keys, consolePassword);
        QueueTextField(keys, factor == "authenticator" ? factorValue : string.Empty);
        QueueTextField(keys, factor == "recovery" ? factorValue : string.Empty);
        ControlledCredentialTerminal terminal = new(keys);
        OctoberConsoleCredentialSource source = new(terminal);

        using OctoberOperatorCredentials credentials = Assert.IsType<OctoberOperatorCredentials>(
            await source.ReadAsync(CancellationToken.None));

        Assert.Equal(consoleUserName, credentials.UserName);
        Assert.Equal(consolePassword, credentials.Password);
        Assert.Equal(factor == "authenticator" ? factorValue : null, credentials.AuthenticatorCode);
        Assert.Equal(factor == "recovery" ? factorValue : null, credentials.RecoveryCode);
        Assert.DoesNotContain(factorValue, terminal.Output, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing-user")]
    [InlineData("missing-password")]
    public async Task ReadAsync_MissingRequiredField_ReturnsNoCredentials(string missingField)
    {
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, missingField == "missing-user" ? string.Empty : "october.missing.operator@example.test");
        if (missingField == "missing-password")
        {
            QueueTextField(keys, string.Empty);
        }

        ControlledCredentialTerminal terminal = new(keys);
        OctoberConsoleCredentialSource source = new(terminal);

        Assert.Null(await source.ReadAsync(CancellationToken.None));
        Assert.DoesNotContain("october.missing.operator@example.test", terminal.Output, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_SimultaneousFactors_ReturnsSanitizedUnavailable()
    {
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, "october.factor.operator@example.test");
        QueueTextField(keys, "Synthetic-only-factor-password-2044");
        QueueTextField(keys, "Synthetic-only-authenticator-value-2044");
        QueueTextField(keys, "Synthetic-only-recovery-value-2044");
        ControlledCredentialTerminal terminal = new(keys);
        OctoberConsoleCredentialSource source = new(terminal);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await source.ReadAsync(CancellationToken.None);
        });

        Assert.Equal("Operator credential input is unavailable.", failure.Message);
        Assert.DoesNotContain("Synthetic-only-factor-password-2044", failure.ToString());
        Assert.DoesNotContain("Synthetic-only-authenticator-value-2044", failure.ToString());
        Assert.DoesNotContain("Synthetic-only-recovery-value-2044", failure.ToString());
        Assert.DoesNotContain("october.factor.operator@example.test", terminal.Output, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_BackspaceAndControlKeys_KeepOnlyTextualInput()
    {
        const string expectedUserName = "october.console.operator@example.test";
        const string consolePassword = "Synthetic-only-console-password-2044";
        Queue<ConsoleKeyInfo> keys = new();
        QueueText(keys, "october.consolx");
        keys.Enqueue(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
        keys.Enqueue(new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, false, false, false));
        QueueText(keys, "e.operator@example.test");
        QueueEnter(keys);
        QueueTextField(keys, consolePassword);
        QueueTextField(keys, string.Empty);
        QueueTextField(keys, string.Empty);
        ControlledCredentialTerminal terminal = new(keys);
        OctoberConsoleCredentialSource source = new(terminal);

        using OctoberOperatorCredentials credentials = Assert.IsType<OctoberOperatorCredentials>(
            await source.ReadAsync(CancellationToken.None));

        Assert.Equal(expectedUserName, credentials.UserName);
        Assert.Equal(consolePassword, credentials.Password);
        Assert.DoesNotContain(expectedUserName, terminal.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(consolePassword, terminal.Output, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unavailable")]
    [InlineData("read-failure")]
    [InlineData("write-failure")]
    public async Task ReadAsync_UnavailableTerminalOrIoFailure_ReturnsSanitizedUnavailable(string scenario)
    {
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, "Synthetic-only-terminal-username-2044");
        ControlledCredentialTerminal terminal = new(keys)
        {
            IsTerminalInteractive = scenario != "unavailable",
            ReadFailure = scenario == "read-failure" ? new IOException("Synthetic terminal read detail.") : null,
            WriteFailure = scenario == "write-failure" ? new IOException("Synthetic terminal write detail.") : null,
        };
        OctoberConsoleCredentialSource source = new(terminal);

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await source.ReadAsync(CancellationToken.None);
        });

        Assert.Equal("Operator credential input is unavailable.", failure.Message);
        Assert.DoesNotContain("Synthetic", failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic-only-terminal-username-2044", terminal.Output, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_CallerCancelsDuringField_PropagatesWithoutPartialCredentials()
    {
        using CancellationTokenSource cancellation = new();
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, "october.cancelled.operator@example.test");
        ControlledCredentialTerminal terminal = new(keys)
        {
            CancelOnRead = 3,
            Cancellation = cancellation,
        };
        OctoberConsoleCredentialSource source = new(terminal);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await source.ReadAsync(cancellation.Token);
        });

        Assert.DoesNotContain("october.cancelled.operator@example.test", terminal.Output, StringComparison.Ordinal);
        Assert.Equal(0, terminal.DisposeCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_OverlappingRequests_AreAdmittedOneAtATime()
    {
        Queue<ConsoleKeyInfo> keys = new();
        QueueTextField(keys, "october.first.operator@example.test");
        QueueTextField(keys, "Synthetic-only-first-password-2044");
        QueueTextField(keys, string.Empty);
        QueueTextField(keys, string.Empty);
        QueueTextField(keys, "october.second.operator@example.test");
        QueueTextField(keys, "Synthetic-only-second-password-2044");
        QueueTextField(keys, string.Empty);
        QueueTextField(keys, string.Empty);
        ControlledCredentialTerminal terminal = new(keys) { BlockFirstRead = true };
        OctoberConsoleCredentialSource source = new(terminal);

        ValueTask<OctoberOperatorCredentials?> firstTask = source.ReadAsync(CancellationToken.None);
        await terminal.FirstReadEntered;
        ValueTask<OctoberOperatorCredentials?> secondTask = source.ReadAsync(CancellationToken.None);

        Assert.False(secondTask.IsCompleted);
        Assert.Equal("User name: ", terminal.Output);
        terminal.ReleaseFirstRead();

        using OctoberOperatorCredentials first = Assert.IsType<OctoberOperatorCredentials>(await firstTask);
        using OctoberOperatorCredentials second = Assert.IsType<OctoberOperatorCredentials>(await secondTask);
        Assert.Equal("october.first.operator@example.test", first.UserName);
        Assert.Equal("october.second.operator@example.test", second.UserName);
        Assert.False(terminal.OverlappingReadDetected);
        Assert.Equal(0, terminal.RemainingKeyCount);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_ValidProtectedPayload_ReturnsDeclaredCredentials()
    {
        string path = NewPath();
        await using ServiceProvider provider = CreateProtectionProvider();
        IDataProtectionProvider protection = provider.GetRequiredService<IDataProtectionProvider>();

        try
        {
            await WriteProtectedAsync(path, ValidPayload, protection);
            OctoberProtectedFileCredentialSource source = new(path, protection);

            using OctoberOperatorCredentials credentials = Assert.IsType<OctoberOperatorCredentials>(
                await source.ReadAsync(CancellationToken.None));

            Assert.Equal(UserName, credentials.UserName);
            Assert.Equal(Password, credentials.Password);
            Assert.Equal(AuthenticatorCode, credentials.AuthenticatorCode);
            Assert.Null(credentials.RecoveryCode);
            Assert.DoesNotContain(UserName, credentials.ToString());
            Assert.DoesNotContain(Password, credentials.ToString());
            Assert.DoesNotContain(AuthenticatorCode, credentials.ToString());

            credentials.Dispose();
            Assert.Throws<ObjectDisposedException>(() => credentials.UserName);
            Assert.Throws<ObjectDisposedException>(() => credentials.Password);
            Assert.Null(credentials.AuthenticatorCode);
            Assert.Null(credentials.RecoveryCode);
        }
        finally
        {
            DeleteOwnedFile(path);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("wrong-purpose")]
    [InlineData("wrong-key")]
    public async Task ReadAsync_IncompatibleProtection_ReturnsSanitizedUnavailable(string scenario)
    {
        string path = NewPath();
        await using ServiceProvider sourceProvider = CreateProtectionProvider();
        await using ServiceProvider payloadProvider = CreateProtectionProvider();
        IDataProtectionProvider sourceProtection = sourceProvider.GetRequiredService<IDataProtectionProvider>();
        IDataProtectionProvider payloadProtection = scenario == "wrong-key"
            ? payloadProvider.GetRequiredService<IDataProtectionProvider>()
            : sourceProtection;

        try
        {
            string payloadPurpose = scenario == "wrong-purpose" ? Purpose + ".other" : Purpose;
            await WriteProtectedAsync(path, ValidPayload, payloadProtection, payloadPurpose);
            await AssertUnavailableAsync(new OctoberProtectedFileCredentialSource(path, sourceProtection));
        }
        finally
        {
            DeleteOwnedFile(path);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("plaintext")]
    [InlineData("tampered")]
    public async Task ReadAsync_UnprotectedOrCorruptedPayload_ReturnsSanitizedUnavailable(string scenario)
    {
        string path = NewPath();
        await using ServiceProvider provider = CreateProtectionProvider();
        IDataProtectionProvider protection = provider.GetRequiredService<IDataProtectionProvider>();

        try
        {
            if (scenario == "plaintext")
            {
                await File.WriteAllTextAsync(path, ValidPayload, Encoding.UTF8);
            }
            else
            {
                string protectedPayload = protection.CreateProtector(Purpose).Protect(ValidPayload);
                char[] tamperedPayload = protectedPayload.ToCharArray();
                int finalIndex = tamperedPayload.Length - 1;
                tamperedPayload[finalIndex] = tamperedPayload[finalIndex] == 'a' ? 'b' : 'a';
                await File.WriteAllTextAsync(path, new string(tamperedPayload), Encoding.UTF8);
            }

            await AssertUnavailableAsync(new OctoberProtectedFileCredentialSource(path, protection));
        }
        finally
        {
            DeleteOwnedFile(path);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("duplicate-property", """{"userName":"october.operator@example.test","userName":"other@example.test","password":"synthetic","authenticatorCode":null,"recoveryCode":null}""")]
    [InlineData("unknown-property", """{"userName":"october.operator@example.test","password":"synthetic","authenticatorCode":null,"recoveryCode":null,"extra":"value"}""")]
    [InlineData("missing-property", """{"userName":"october.operator@example.test","password":"synthetic","authenticatorCode":null}""")]
    [InlineData("wrong-property-type", """{"userName":"october.operator@example.test","password":42,"authenticatorCode":null,"recoveryCode":null}""")]
    [InlineData("blank-user-name", """{"userName":"  ","password":"synthetic","authenticatorCode":null,"recoveryCode":null}""")]
    [InlineData("empty-password", """{"userName":"october.operator@example.test","password":"","authenticatorCode":null,"recoveryCode":null}""")]
    [InlineData("conflicting-factors", """{"userName":"october.operator@example.test","password":"synthetic","authenticatorCode":"123456","recoveryCode":"synthetic-recovery"}""")]
    public async Task ReadAsync_InvalidProtectedFields_ReturnsSanitizedUnavailable(string scenario, string payload)
    {
        string path = NewPath();
        await using ServiceProvider provider = CreateProtectionProvider();
        IDataProtectionProvider protection = provider.GetRequiredService<IDataProtectionProvider>();

        try
        {
            await WriteProtectedAsync(path, payload, protection);
            await AssertUnavailableAsync(new OctoberProtectedFileCredentialSource(path, protection));
        }
        finally
        {
            DeleteOwnedFile(path);
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_MissingFile_ReturnsNoCredentials()
    {
        string path = NewPath();
        await using ServiceProvider provider = CreateProtectionProvider();
        OctoberProtectedFileCredentialSource source = new(
            path,
            provider.GetRequiredService<IDataProtectionProvider>());

        Assert.False(File.Exists(path));
        Assert.Null(await source.ReadAsync(CancellationToken.None));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ReadAsync_PreCancelledRequest_PropagatesCancellationBeforeMissingFileResult()
    {
        string path = NewPath();
        await using ServiceProvider provider = CreateProtectionProvider();
        OctoberProtectedFileCredentialSource source = new(
            path,
            provider.GetRequiredService<IDataProtectionProvider>());
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.False(File.Exists(path));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await source.ReadAsync(cancellation.Token);
        });
    }

    private static ServiceProvider CreateProtectionProvider()
    {
        ServiceCollection services = new();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        return services.BuildServiceProvider();
    }

    private static string NewPath()
        => Path.Combine(Path.GetTempPath(), $"vulgata-october-credentials-{Guid.NewGuid():N}.protected");

    private static void QueueTextField(Queue<ConsoleKeyInfo> keys, string value)
    {
        QueueText(keys, value);
        QueueEnter(keys);
    }

    private static void QueueText(Queue<ConsoleKeyInfo> keys, string value)
    {
        foreach (char character in value)
        {
            keys.Enqueue(new ConsoleKeyInfo(character, ConsoleKey.NoName, false, false, false));
        }
    }

    private static void QueueEnter(Queue<ConsoleKeyInfo> keys)
        => keys.Enqueue(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));

    private static async Task WriteProtectedAsync(
        string path,
        string payload,
        IDataProtectionProvider protection,
        string purpose = Purpose)
    {
        string protectedPayload = protection.CreateProtector(purpose).Protect(payload);
        await File.WriteAllTextAsync(path, protectedPayload, Encoding.UTF8);
    }

    private static async Task AssertUnavailableAsync(OctoberProtectedFileCredentialSource source)
    {
        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await source.ReadAsync(CancellationToken.None);
        });

        Assert.Equal("Operator credential input is unavailable.", error.Message);
        Assert.DoesNotContain(Password, error.ToString());
        Assert.DoesNotContain("synthetic", error.ToString());
        Assert.DoesNotContain("userName", error.ToString());
        Assert.DoesNotContain(AuthenticatorCode, error.ToString());
        Assert.DoesNotContain("synthetic-recovery", error.ToString());
    }

    private static void DeleteOwnedFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class ControlledCredentialTerminal(IEnumerable<ConsoleKeyInfo> keys) : IOctoberCredentialTerminal, IDisposable
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new(keys);
        private readonly StringBuilder _output = new();
        private readonly object _outputLock = new();
        private readonly TaskCompletionSource<bool> _firstReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseFirstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _activeReads;
        private int _readCount;

        public bool IsTerminalInteractive { get; init; } = true;
        public bool IsInteractive => IsTerminalInteractive;
        public string Output
        {
            get
            {
                lock (_outputLock)
                {
                    return _output.ToString();
                }
            }
        }
        public int RemainingKeyCount => _keys.Count;
        public int DisposeCount { get; private set; }
        public bool OverlappingReadDetected { get; private set; }
        public bool BlockFirstRead { get; init; }
        public int CancelOnRead { get; init; }
        public CancellationTokenSource? Cancellation { get; init; }
        public Exception? ReadFailure { get; init; }
        public Exception? WriteFailure { get; init; }
        public Task FirstReadEntered => _firstReadEntered.Task;

        public void ReleaseFirstRead() => _releaseFirstRead.TrySetResult(true);

        public async ValueTask<ConsoleKeyInfo> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _activeReads) != 1)
            {
                OverlappingReadDetected = true;
                Interlocked.Decrement(ref _activeReads);
                throw new IOException("Controlled terminal received overlapping key reads.");
            }

            try
            {
                int readCount = Interlocked.Increment(ref _readCount);
                if (BlockFirstRead && readCount == 1)
                {
                    _firstReadEntered.TrySetResult(true);
                    await _releaseFirstRead.Task.WaitAsync(cancellationToken);
                }

                if (readCount == CancelOnRead)
                {
                    Cancellation?.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ReadFailure is not null)
                {
                    throw ReadFailure;
                }

                ConsoleKeyInfo key = _keys.Dequeue();
                if (!intercept && !char.IsControl(key.KeyChar))
                {
                    lock (_outputLock)
                    {
                        _output.Append(key.KeyChar);
                    }
                }

                return key;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReads);
            }
        }

        public void Write(string value)
        {
            if (WriteFailure is not null)
            {
                throw WriteFailure;
            }

            lock (_outputLock)
            {
                _output.Append(value);
            }
        }

        public void Dispose() => DisposeCount++;
    }
}
