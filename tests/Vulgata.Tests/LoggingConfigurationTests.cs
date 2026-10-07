using Vulgata.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using System.Reflection;
using Xunit.Abstractions;

namespace Vulgata.Tests;

public sealed class LoggingConfigurationTests(ITestOutputHelper output)
{
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

        try
        {
            await using var factory = new LoginLogoutTests.CustomWebApplicationFactory()
                .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
                    (_, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Logging:LocalFile:Path"] = logPath,
                        })));

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
        finally
        {
            if (Directory.Exists(logDirectory))
            {
                try
                {
                    Directory.Delete(logDirectory, recursive: true);
                }
                catch (IOException)
                {
                    // The original await-using scope has exited. Record ownership state, not paths or log contents.
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
