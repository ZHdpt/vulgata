using Vulgata.Tests.TestInfrastructure;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;
using Vulgata.Infrastructure;
using Vulgata.Infrastructure.Source;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

public sealed class TrustedSourceWorkspaceTests(ITestOutputHelper output)
{
    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_VerifiedSourceMaterializesContainedLeaseAndCleansOnlyLeaseAsync()
    {
        byte[] textBytes = "trusted-source-text\n"u8.ToArray();
        byte[] binaryBytes = [0x00, 0x7f, 0x80, 0xff];
        byte[] executableBytes = "#!/bin/sh\necho trusted\n"u8.ToArray();
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync(
                new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal)
                {
                    ["src/trusted.txt"] = textBytes,
                    ["src/trusted.bin"] = binaryBytes,
                    ["src/trusted.sh"] = executableBytes,
                });
        await fixture.Repository.RunGitAsync(["update-index", "--chmod=+x", "src/trusted.sh"]);
        await fixture.Repository.CommitIndexAsync("mark trusted source executable");
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "trusted-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);

        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);

        Assert.False(typeof(ITrustedSourceWorkspaceProvider).IsPublic);
        Assert.DoesNotContain(
            typeof(SourceSnapshot).Assembly.GetExportedTypes(),
            type => type.Name.Contains("TrustedSourceWorkspace", StringComparison.Ordinal));
        ITrustedSourceWorkspaceProvider? provider = appFixture.Scope.ServiceProvider
            .GetService<ITrustedSourceWorkspaceProvider>();
        Assert.NotNull(provider);

        TrustedSourceWorkspaceResult result = await provider!.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        Assert.Equal(TrustedSourceWorkspaceStatus.Succeeded, result.Status);
        Assert.Equal("trusted_source_workspace_acquired", result.Code);
        ITrustedSourceWorkspaceLease lease = Assert.IsAssignableFrom<ITrustedSourceWorkspaceLease>(
            result.Workspace);
        string fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        string fullLeaseRoot = Path.GetFullPath(lease.RootPath);
        string workspacePrefix = fullWorkspaceRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Assert.StartsWith(workspacePrefix, fullLeaseRoot, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(fixture.RepositoryId, lease.RepositoryId);
        Assert.Equal(snapshot.ProfiledRevision, lease.ProfiledRevision);
        Assert.Equal(snapshot.ManifestSha256, lease.ManifestSha256);
        Assert.Equal(textBytes, await File.ReadAllBytesAsync(
            Path.Combine(fullLeaseRoot, "src", "trusted.txt"),
            testCancellation.Token));
        Assert.Equal(binaryBytes, await File.ReadAllBytesAsync(
            Path.Combine(fullLeaseRoot, "src", "trusted.bin"),
            testCancellation.Token));
        Assert.Equal(executableBytes, await File.ReadAllBytesAsync(
            Path.Combine(fullLeaseRoot, "src", "trusted.sh"),
            testCancellation.Token));

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(fullLeaseRoot));
        Assert.True(Directory.Exists(fullWorkspaceRoot));
        OpenSourceReadContextResult reopened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            snapshot.ProfiledRevision,
            new SourceReadGrantRequest(),
            testCancellation.Token);
        Assert.Equal(SourceReadStatus.Succeeded, reopened.Status);
        Assert.NotNull(reopened.Context);
        await reopened.Context!.DisposeAsync();
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; workspace={result.Status}/{result.Code}; " +
            $"leaseDisposed={!Directory.Exists(fullLeaseRoot)}; sourceRetained={reopened.Status}");
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AddRetainedSourceInfrastructure_RejectsOverlappingWorkspaceRootAndAcceptsSiblingAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string codeGraphRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "registration-codegraph");
        string jdtLsRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "registration-jdtls");
        Directory.CreateDirectory(codeGraphRoot);
        Directory.CreateDirectory(jdtLsRoot);
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceStorage:RootPath"] = fixture.SourceStorageRoot,
                ["SourceStorage:MaximumEntries"] = "10000",
                ["SourceStorage:MaximumBlobBytes"] = "8388608",
                ["SourceStorage:MaximumAggregateBlobBytes"] = "67108864",
                ["SourceStorage:CaptureDeadlineSeconds"] = "600",
                ["SourceStorage:OperationStaleAfterMinutes"] = "1440",
                ["SourceStorage:VerificationBufferBytes"] = "65536",
                ["SourceStorage:Read:DeadlineSeconds"] = "60",
            })
            .Build();
        RetainedSourceCapturePolicy policy = RetainedSourceCapturePolicy.Create(
            configuration,
            Directory.GetCurrentDirectory(),
            codeGraphRoot,
            jdtLsRoot);
        string volumeRoot = Path.GetPathRoot(policy.RootPath) ??
            throw new InvalidOperationException("The owned policy root has no volume root.");
        var overlappingServices = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            overlappingServices.AddRetainedSourceInfrastructure(policy, volumeRoot));

        string siblingWorkspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "registration-workspace");
        Directory.CreateDirectory(siblingWorkspaceRoot);
        var siblingServices = new ServiceCollection();
        siblingServices.AddRetainedSourceInfrastructure(policy, siblingWorkspaceRoot);
        output.WriteLine(
            $"policyRootConfigured=True; overlappingRejected=True; " +
            $"siblingProviderRegistered={siblingServices.Any(descriptor =>
                descriptor.ServiceType == typeof(ITrustedSourceWorkspaceProvider))}; " +
            $"volumeRootTouched=False");
        Assert.Contains(
            siblingServices,
            descriptor => descriptor.ServiceType == typeof(ITrustedSourceWorkspaceProvider));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AfterLiveAuthorityRevocation_DeniesWithoutWorkspaceLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "revoked-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            appFixture.Database.ConnectionString,
            appFixture.SystemId,
            appFixture.UserId);
        TrustedSourceWorkspaceResult denied = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; denied={denied.Status}/{denied.Code}; " +
            $"lease={denied.Workspace is not null}; workspaceEntries={Directory.GetDirectories(workspaceRoot).Length}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Denied, denied.Status);
        Assert.Equal("trusted_source_workspace_denied", denied.Code);
        Assert.Null(denied.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_UnknownWorkspacePurpose_ReturnsUnsupportedWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            (TrustedSourceWorkspacePurpose)999,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CommittedSymlinkEntry_ReturnsUnsupportedWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.AddSymbolicLinkEntryAsync(
            "src/linked.txt",
            "outside-target");
        await fixture.Repository.CommitIndexAsync("add committed symlink");
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CommittedGitlinkEntry_ReturnsUnsupportedWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.AddGitlinkEntryAsync("src/submodule");
        await fixture.Repository.CommitIndexAsync("add committed gitlink");
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CaseCollidingDirectories_ReturnsUnsupportedWithoutLeaseAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await CommitCaseCollidingDirectoriesAsync(fixture.Repository);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true);
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_HostUnrepresentableLongPathComponent_ReturnsUnsupportedWithoutLeaseAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.CommitRawTreeAsync(
            [
                (Encoding.ASCII.GetBytes(new string('a', 300)), "long-path\n"u8.ToArray()),
            ]);
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "long-path-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}; " +
            $"partialWorkspaces={Directory.GetDirectories(workspaceRoot).Length}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_UnsafeAndNonUtf8PathBytes_ReturnsUnsupportedWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await fixture.Repository.CommitRawTreeAsync(
            [
                ("unsafe\\name.txt"u8.ToArray(), "unsafe\n"u8.ToArray()),
                ([0x6e, 0x6f, 0x80, 0x2e, 0x74, 0x78, 0x74], "non-utf8\n"u8.ToArray()),
            ]);
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "unsafe-path-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult unsupported = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"unsupported={unsupported.Status}/{unsupported.Code}; " +
            $"lease={unsupported.Workspace is not null}; " +
            $"partialWorkspaces={Directory.GetDirectories(workspaceRoot).Length}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Unsupported, unsupported.Status);
        Assert.Equal("trusted_source_workspace_unsupported", unsupported.Code);
        Assert.Null(unsupported.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_AuthorityRevokedBeforeFinalReturn_DeniesAndCleansPartialWorkspaceAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string subjectId = $"retained-source-revoked-{Guid.NewGuid():N}";
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "post-copy-revoked-workspaces");
        bool copiedFileObserved = false;
        string? databaseConnectionString = null;
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                },
                beforeAuthorization: async (request, cancellationToken) =>
                {
                    if (request.Purpose == SourceAuthorizationPurpose.Read &&
                        Directory.EnumerateFiles(
                            workspaceRoot,
                            "retained-source.txt",
                            SearchOption.AllDirectories).Any())
                    {
                        copiedFileObserved = true;
                        if (databaseConnectionString is null)
                        {
                            throw new InvalidOperationException(
                                "The test database connection was not initialized.");
                        }
                        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
                            databaseConnectionString,
                            Guid.Empty,
                            subjectId);
                    }
                },
                subjectId: subjectId);
        databaseConnectionString = appFixture.Database.ConnectionString;
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult denied = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; denied={denied.Status}/{denied.Code}; " +
            $"lease={denied.Workspace is not null}; " +
            $"workspaceEntries={Directory.GetDirectories(workspaceRoot).Length}; " +
            $"copiedFileObserved={copiedFileObserved}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Denied, denied.Status);
        Assert.Equal("trusted_source_workspace_denied", denied.Code);
        Assert.Null(denied.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
        Assert.True(copiedFileObserved);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CancelledBeforeWorkspaceAcquire_ReturnsCancelledWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "pre-cancel-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var setupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            setupCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            setupCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        TrustedSourceWorkspaceResult cancelled = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            cancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"cancelled={cancelled.Status}/{cancelled.Code}; " +
            $"lease={cancelled.Workspace is not null}; " +
            $"workspaceEntries={Directory.GetDirectories(workspaceRoot).Length}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Cancelled, cancelled.Status);
        Assert.Equal("trusted_source_workspace_cancelled", cancelled.Code);
        Assert.Null(cancelled.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_CancelledAtFinalAuthorization_ReturnsCancelledAndCleansPartialWorkspaceAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "final-cancel-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        using var setupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var cancellation = new CancellationTokenSource();
        bool copiedFileObserved = false;
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                },
                authorizationWait: (_, _) =>
                {
                    if (Directory.EnumerateFiles(
                            workspaceRoot,
                            "retained-source.txt",
                            SearchOption.AllDirectories).Any())
                    {
                        copiedFileObserved = true;
                        cancellation.Cancel();
                    }

                    return Task.CompletedTask;
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            setupCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            setupCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult cancelled = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            cancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"cancelled={cancelled.Status}/{cancelled.Code}; " +
            $"lease={cancelled.Workspace is not null}; " +
            $"workspaceEntries={Directory.GetDirectories(workspaceRoot).Length}; " +
            $"copiedFileObserved={copiedFileObserved}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Cancelled, cancelled.Status);
        Assert.Equal("trusted_source_workspace_cancelled", cancelled.Code);
        Assert.Null(cancelled.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
        Assert.True(copiedFileObserved);
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_TamperedRetainedBlob_ReturnsIntegrityFailureWithoutLeaseAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "tampered-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        RetainedSourceManifestEntry entry = Assert.Single(verified.Manifest.Entries);
        string blobPath = Path.Combine(
            fixture.SourceStorageRoot,
            snapshot.InternalStorageKey.Replace('/', Path.DirectorySeparatorChar),
            entry.StorageBlobKey!.Replace('/', Path.DirectorySeparatorChar));
        byte[] tampered = await File.ReadAllBytesAsync(blobPath, testCancellation.Token);
        tampered[0] ^= 0xff;
        await File.WriteAllBytesAsync(blobPath, tampered, testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult integrity = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"integrity={integrity.Status}/{integrity.Code}; " +
            $"lease={integrity.Workspace is not null}; " +
            $"workspaceEntries={Directory.GetDirectories(workspaceRoot).Length}");

        Assert.Equal(TrustedSourceWorkspaceStatus.IntegrityFailure, integrity.Status);
        Assert.Equal("trusted_source_workspace_integrity_failed", integrity.Code);
        Assert.Null(integrity.Workspace);
        Assert.Empty(Directory.GetDirectories(workspaceRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_TwoIndependentLeases_DisposingOnePreservesSiblingAndSourceAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "independent-workspaces");
        Directory.CreateDirectory(workspaceRoot);
        string sentinel = Path.Combine(workspaceRoot, "unrelated-sentinel.txt");
        File.WriteAllText(sentinel, "preserve");
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceRoot,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult first = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        TrustedSourceWorkspaceResult second = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        ITrustedSourceWorkspaceLease firstLease =
            Assert.IsAssignableFrom<ITrustedSourceWorkspaceLease>(first.Workspace);
        ITrustedSourceWorkspaceLease secondLease =
            Assert.IsAssignableFrom<ITrustedSourceWorkspaceLease>(second.Workspace);
        string firstRoot = firstLease.RootPath;
        string secondRoot = secondLease.RootPath;
        await firstLease.DisposeAsync();
        OpenSourceReadContextResult reopened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            snapshot.ProfiledRevision,
            new SourceReadGrantRequest(),
            testCancellation.Token);
        output.WriteLine(
            $"first={first.Status}/{first.Code}; second={second.Status}/{second.Code}; " +
            $"firstDisposed={!Directory.Exists(firstRoot)}; " +
            $"secondExists={Directory.Exists(secondRoot)}; " +
            $"sentinelExists={File.Exists(sentinel)}; source={reopened.Status}/{reopened.Code}");

        Assert.Equal(TrustedSourceWorkspaceStatus.Succeeded, first.Status);
        Assert.Equal(TrustedSourceWorkspaceStatus.Succeeded, second.Status);
        Assert.NotEqual(firstRoot, secondRoot, StringComparer.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(firstRoot));
        Assert.True(Directory.Exists(secondRoot));
        Assert.True(File.Exists(sentinel));
        Assert.Equal(SourceReadStatus.Succeeded, reopened.Status);
        Assert.NotNull(reopened.Context);
        await reopened.Context!.DisposeAsync();
        await secondLease.DisposeAsync();
        Assert.True(File.Exists(sentinel));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_ConfiguredWorkspaceRootIsOwnedLink_FailsWithoutTouchingTargetAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string targetRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "workspace-link-target");
        string linkRoot = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "workspace-link");
        Directory.CreateDirectory(targetRoot);
        string sentinel = Path.Combine(targetRoot, "target-sentinel.txt");
        File.WriteAllText(sentinel, "preserve");
        Directory.CreateSymbolicLink(linkRoot, targetRoot);
        ArgumentException failure = await Assert.ThrowsAsync<ArgumentException>(() =>
            RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = linkRoot,
                }));
        output.WriteLine(
            $"registrationFailure={failure.GetType().Name}; " +
            $"targetSentinel={File.Exists(sentinel)}");

        Assert.True(File.Exists(sentinel));
        Assert.Single(Directory.GetFiles(targetRoot));
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task AcquireAsync_ConfiguredWorkspaceRootIsOwnedFile_FailsWithoutTouchingFileAsync()
    {
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        string workspaceFile = RetainedSourceOwnedPaths.ResolveChild(
            fixture.RootPath,
            "workspace-file");
        File.WriteAllText(workspaceFile, "preserve");
        await using RetainedSourceCaptureApplicationFixture appFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                standalone: true,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["AnalysisRuntime:JdtLsWorkspaceRoot"] = workspaceFile,
                });
        IRetainedSourceApplication application = appFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef,
            testCancellation.Token);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        IDbContextFactory<VulgataDbContext> factory = appFixture.Scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<VulgataDbContext>>();
        await using VulgataDbContext context = await factory.CreateDbContextAsync();
        SourceSnapshot snapshot = await context.SourceSnapshots.SingleAsync(source =>
            source.RepositoryId == fixture.RepositoryId &&
            source.ProfiledRevision == capture.ProfiledRevision);
        DurableRetainedSourceStore store = appFixture.Scope.ServiceProvider
            .GetRequiredService<DurableRetainedSourceStore>();
        VerifiedRetainedSourceObject verified = await store.OpenVerifiedAsync(
            snapshot,
            testCancellation.Token);
        ITrustedSourceWorkspaceProvider provider = appFixture.Scope.ServiceProvider
            .GetRequiredService<ITrustedSourceWorkspaceProvider>();

        TrustedSourceWorkspaceResult failure = await provider.AcquireAsync(
            verified,
            TrustedSourceWorkspacePurpose.SourceAnalysis,
            testCancellation.Token);
        output.WriteLine(
            $"capture={capture.Status}/{capture.Code}; " +
            $"failure={failure.Status}/{failure.Code}; " +
            $"lease={failure.Workspace is not null}; " +
            $"fileExists={File.Exists(workspaceFile)}");

        Assert.Equal(TrustedSourceWorkspaceStatus.StorageFailure, failure.Status);
        Assert.Equal("trusted_source_workspace_storage_unavailable", failure.Code);
        Assert.Null(failure.Workspace);
        Assert.True(File.Exists(workspaceFile));
        Assert.Equal("preserve", File.ReadAllText(workspaceFile));
    }

    private static async Task CommitCaseCollidingDirectoriesAsync(
        RetainedSourceGitRepository repository)
    {
        string firstBlob = await repository.RunGitWithInputAsync(
            ["hash-object", "-w", "--stdin"],
            Encoding.UTF8.GetBytes("first\n"));
        string secondBlob = await repository.RunGitWithInputAsync(
            ["hash-object", "-w", "--stdin"],
            Encoding.UTF8.GetBytes("second\n"));
        string firstTree = await repository.RunGitWithInputAsync(
            ["mktree"],
            Encoding.UTF8.GetBytes($"100644 blob {firstBlob}\ta.txt\n"));
        string secondTree = await repository.RunGitWithInputAsync(
            ["mktree"],
            Encoding.UTF8.GetBytes($"100644 blob {secondBlob}\tb.txt\n"));
        string rootTree = await repository.RunGitWithInputAsync(
            ["mktree"],
            Encoding.UTF8.GetBytes(
                $"040000 tree {firstTree}\tFoo\n040000 tree {secondTree}\tfoo\n"));
        string commit = await repository.RunGitWithInputAsync(
            ["commit-tree", rootTree, "-m", "case-colliding directories"] ,
            ReadOnlyMemory<byte>.Empty);
        string headRef = await repository.RunGitAsync(["symbolic-ref", "HEAD"]);
        await repository.RunGitAsync(["update-ref", headRef, commit]);
    }
}
