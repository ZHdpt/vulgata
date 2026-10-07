using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Configuration;
using Vulgata.OctoberGenerator;
using Vulgata.Tests.October.Integration;
using Microsoft.Extensions.Configuration;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.OctoberScan;

/// <summary>Qualification input binding only. Approval and report hydration remain with the integration owner.</summary>
internal sealed class OctoberScanGenerateQualificationAdapter
{
    private readonly Func<CancellationToken, Task> _validate;
    private readonly Func<CancellationToken, Task<(int ExitCode, string StandardOutput, string StandardError)>> _dispatch;
    private int _consumed;
    internal OctoberScanGenerateQualificationAdapter(string ownedRoot, string expectedSourceRoot,
        OctoberSetupRequest expected, OctoberProviderProfile approvedProfile, OctoberScanPolicy approvedPolicy,
        string approvedRelativeFile, OctoberGenerateInputFiles files,
        Func<CancellationToken, Task<(int ExitCode, string StandardOutput, string StandardError)>> dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        _validate = token => ValidateAsync(ownedRoot, expectedSourceRoot, expected, approvedProfile,
            approvedPolicy, approvedRelativeFile, files, token);
        _dispatch = dispatch;
    }

    internal static async Task ValidateAsync(string ownedRoot, string expectedSourceRoot,
        OctoberSetupRequest expected, OctoberProviderProfile approvedProfile, OctoberScanPolicy approvedPolicy,
        string approvedRelativeFile, OctoberGenerateInputFiles files, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(approvedProfile);
        ArgumentNullException.ThrowIfNull(approvedPolicy);
        ArgumentNullException.ThrowIfNull(files);
        OctoberGenerateInputFiles validated = OctoberGenerateFixtureInputs.ValidateFiles(ownedRoot, "generate",
            files.RuntimeFilePath, files.ScanPolicyFilePath, files.WorkerProviderConfigurationFilePath, false)
            ?? throw new InvalidOperationException("Owned qualification inputs are unavailable.");
        if (expected.Repositories.Count != 1 || expected.BatchId == Guid.Empty ||
            string.IsNullOrWhiteSpace(expected.TargetId) || !Path.IsPathFullyQualified(expectedSourceRoot) ||
            approvedProfile.ApiType != ApiType.ChatCompletions ||
            !new OctoberScanPolicyValidator().Validate(approvedPolicy).IsValid ||
            approvedPolicy.OutputStage != "cl-only" || approvedPolicy.WorkerCap != 1 ||
            approvedPolicy.SourceRules.Length != 1 ||
            approvedPolicy.SourceRules[0].RepositoryId != expected.Repositories[0].RepositoryId ||
            approvedPolicy.SourceRules[0].RelativePathPrefix != approvedRelativeFile ||
            approvedPolicy.SourceRules[0].Disposition != "selected" || string.IsNullOrEmpty(approvedRelativeFile))
            throw new InvalidOperationException("The approved qualification boundary is invalid.");

        IConfigurationRoot configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["October:RuntimeFile"] = validated.RuntimeFilePath,
                ["October:ScanPolicyFile"] = validated.ScanPolicyFilePath,
                ["October:Providers:ConfigurationFile"] = validated.WorkerProviderConfigurationFilePath
            }).Build();
        using var configurationLifetime = configuration as IDisposable;
        OctoberRuntimeConfiguration runtime = await OctoberGeneratorEntryPoint.ReadRuntimeConfigurationAsync(configuration, cancellationToken);
        OctoberScanPolicy policy = OctoberGeneratorEntryPoint.ReadScanPolicy(configuration);
        OctoberProviderConfiguration provider = await OctoberGeneratorEntryPoint.ReadGenerateProviderConfigurationAsync(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        OctoberRepositorySelection repository = expected.Repositories[0];
        if (runtime.PreparedRoots.Length != 1 || runtime.PreparedRoots[0] is not { } root ||
            root.TargetId != expected.TargetId || root.BatchId != expected.BatchId ||
            root.RepositoryId != repository.RepositoryId || root.RootBindingIdentity != repository.RootBindingIdentity ||
            !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.PhysicalRootPath)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedSourceRoot)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            (policy with { SourceRules = [] }) != (approvedPolicy with { SourceRules = [] }) ||
            !policy.SourceRules.SequenceEqual(approvedPolicy.SourceRules) ||
            provider.SourceIdentity != "owned-generate-worker" || provider.Precedence != "only-source" ||
            provider.WorkerProfile != approvedProfile.Name || !string.IsNullOrEmpty(provider.ChatProfile) ||
            provider.Profiles.Length != 1 || provider.Profiles[0] != approvedProfile ||
            provider.ProtectedCredentials.Count != 1 ||
            !provider.ProtectedCredentials.TryGetValue(approvedProfile.CredentialReference, out string? protectedValue) ||
            string.IsNullOrWhiteSpace(protectedValue))
            throw new InvalidOperationException("Qualification inputs differ from the approved boundary.");
    }

    internal async Task<(int ExitCode, string StandardOutput, string StandardError)> ExecuteOnceAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _consumed, 1) != 0)
            throw new InvalidOperationException("Qualification dispatch has already been consumed.");
        await _validate(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return await _dispatch(cancellationToken);
    }
}
