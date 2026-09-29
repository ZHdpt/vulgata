using Vulgata.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vulgata.Tests;

public sealed class LoggingConfigurationTests
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
                Directory.Delete(logDirectory, recursive: true);
            }
        }
    }
}
