using Vulgata.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32.SafeHandles;
using System.Reflection;
using Xunit.Abstractions;

namespace Vulgata.Tests;

public sealed class LoggingConfigurationTests(ITestOutputHelper output)
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProductionHost_LoggerDisposalObservation_WaitsForActualConcurrentDisposalAsync()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loggerDisposal = new LoggerDisposalObservation();
        string directory = Path.Combine(Path.GetTempPath(), "vulgata-tests", "logging-disposal", Guid.NewGuid().ToString("N"));
        var factory = new LoginLogoutTests.CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Logging:LocalFile:Path"] = Path.Combine(directory, "vulgata.log") }));
                builder.ConfigureServices(services =>
                {
                    loggerDisposal.Configure(services);
                    services.AddSingleton(_ => new HeldDisposal(entered, release, completed));
                });
            });
        Task? disposal = null;
        try
        {
            // Resolve last so the held disposal precedes the host-owned logger disposal.
            _ = factory.Services.GetRequiredService<HeldDisposal>();
            factory.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            disposal = loggerDisposal.DisposeFactoryAsync(factory);
            Assert.False(disposal.IsCompleted,
                "Logger disposal observation returned while the actual host service disposal was still held.");
        }
        finally
        {
            release.TrySetResult();
            if (disposal is not null)
            {
                await disposal.WaitAsync(TimeSpan.FromSeconds(10));
                await completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            else
            {
                await loggerDisposal.DisposeFactoryAsync(factory);
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class LoggerDisposalObservation
    {
        private readonly TaskCompletionSource _created = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Configure(IServiceCollection services)
        {
            services.AddSingleton(_ =>
            {
                _created.TrySetResult();
                return new DisposalCompleted(_disposed);
            });
            // Keep the real factory and DI ownership. Resolving the sentinel first means
            // reverse-order disposal signals only after the production providers finish.
            foreach (var descriptor in services.Where(item => item.ServiceType == typeof(ILoggerProvider)
                         && item.ImplementationFactory is not null).ToArray())
            {
                int index = services.IndexOf(descriptor);
                services[index] = ServiceDescriptor.Singleton(typeof(ILoggerProvider), provider =>
                {
                    _ = provider.GetRequiredService<DisposalCompleted>();
                    return descriptor.ImplementationFactory!(provider);
                });
            }
        }

        public async Task DisposeFactoryAsync(WebApplicationFactory<Program> factory)
        {
            await factory.DisposeAsync();
            // Program.RunAsync and WebApplicationFactory can dispose concurrently. A
            // second DisposeAsync may return before the first has finished DI disposal.
            if (_created.Task.IsCompleted)
            {
                await _disposed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }

        private sealed class DisposalCompleted(TaskCompletionSource completed) : IDisposable
        {
            public void Dispose() => completed.TrySetResult();
        }
    }

    private sealed class HeldDisposal(
        TaskCompletionSource entered,
        TaskCompletionSource release,
        TaskCompletionSource completed) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            entered.TrySetResult();
            await release.Task;
            completed.TrySetResult();
        }
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task ProductionHost_CriticalLog_WritesConfiguredLocalFileAsync()
    {
        string marker = $"local-file-log-{Guid.NewGuid():N}";
        string logDirectory = Path.Combine(
            Path.GetTempPath(),
            "vulgata-tests",
            "local-file-logging",
            Guid.NewGuid().ToString("N"));
        string logPath = Path.Combine(logDirectory, "vulgata.log");
        var providerHandles = new List<(ILoggerProvider Provider, FieldInfo? DisposedField, SafeFileHandle? Handle)>();
        var loggerDisposal = new LoggerDisposalObservation();
        var factory = new LoginLogoutTests.CustomWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Logging:LocalFile:Path"] = logPath }));
                builder.ConfigureServices(loggerDisposal.Configure);
            });

        try
        {
            ILogger logger = factory.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Vulgata.Tests.LocalFileLogging");

            // Observe the host-owned instances; never dispose them independently of the factory.
            foreach (var provider in factory.Services.GetServices<ILoggerProvider>().Where(provider =>
                provider.GetType().FullName == "Vulgata.Web.Logging.LocalFileLoggerProvider"))
            {
                var type = provider.GetType();
                var writer = type.GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(provider) as StreamWriter;
                var handle = (writer?.BaseStream as FileStream)?.SafeFileHandle;
                providerHandles.Add((provider,
                    type.GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic), handle));
            }

            logger.LogCritical("{Marker}", marker);

            Assert.True(File.Exists(logPath), "The configured local log file was not created.");
            using var stream = new FileStream(
                logPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            Assert.Contains(marker, reader.ReadToEnd(), StringComparison.Ordinal);
        }
        catch (Exception error)
        {
            output.WriteLine($"local-file-body-failure type={error.GetType().FullName}");
            throw;
        }
        finally
        {
            await loggerDisposal.DisposeFactoryAsync(factory);
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // Actual logger disposal was observed. Record ownership state, not paths or log contents.
                    output.WriteLine($"local-file-cleanup provider-count={providerHandles.Count}");
                    foreach (var captured in providerHandles)
                    {
                        var disposed = captured.DisposedField?.GetValue(captured.Provider) as bool?;
                        output.WriteLine($"local-file-cleanup provider-disposed={disposed?.ToString() ?? "unknown"}; " +
                            $"safe-file-handle-closed={captured.Handle?.IsClosed.ToString() ?? "unknown"}");
                    }
                    throw;
                }
            }
        }
    }
}
