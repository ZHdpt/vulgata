using Vulgata.Infrastructure.Configuration;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October;
using Vulgata.Tests.October.Integration;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Physical shared-fixture binding; only the separately authorized E2E run may invoke it.</summary>
internal static class OctoberScanGenerateQualificationFixtureAdapter
{
    internal static OctoberScanGenerateQualificationAdapter ForFixture(
        OctoberHostSelectionTests.GeneratorPrepareFixture fixture,
        OctoberProviderProfile approvedProfile, OctoberScanPolicy approvedPolicy,
        string approvedRelativeFile, OctoberGenerateInputFiles files)
        => new(fixture.OwnedRoot, fixture.OwnedSourceRoot, fixture.CreatePrepareRequest(),
            approvedProfile, approvedPolicy, approvedRelativeFile, files, async token =>
            {
                token.ThrowIfCancellationRequested();
                if (!SuiteAuthorization.IsEnabled("E2E"))
                    throw new InvalidOperationException("E2E qualification is not authorized.");
                TestResourceScope.ValidateOwnedTarget(fixture.ConnectionString);
                using var process = fixture.StartGenerator("generate", null,
                    runtimeFilePath: files.RuntimeFilePath, scanPolicyFilePath: files.ScanPolicyFilePath,
                    workerProviderConfigurationFilePath: files.WorkerProviderConfigurationFilePath);
                // The fixture owns the child and retains its bounded exit/cleanup even after cancellation.
                var result = await fixture.WaitForGenerateAsync(process, approvedPolicy.RunDeadlineSeconds);
                token.ThrowIfCancellationRequested();
                return result;
            });
}
