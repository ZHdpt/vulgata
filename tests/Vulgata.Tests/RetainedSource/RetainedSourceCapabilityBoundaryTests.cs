using Vulgata.Tests.TestInfrastructure;
using System.Text;
using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;
using Vulgata.Infrastructure.Data;
using Xunit.Abstractions;

namespace Vulgata.Tests.RetainedSource;

[Collection(RetainedSourceProcessGlobalCollection.Name)]
public sealed class RetainedSourceCapabilityBoundaryTests
{
    private readonly ITestOutputHelper _output;

    public RetainedSourceCapabilityBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public async Task SourceCapability_WhenForbiddenSeamsAreRemoved_CompletesSourceOperationsOnly()
    {
        var forbidden = new RetainedSourceForbiddenDependencyGuard();
        var sqlGuard = new RetainedSourceDomainSqlGuard();
        await using RetainedSourceFixture fixture =
            await RetainedSourceFixture.CreateAsync();
        await using RetainedSourceCaptureApplicationFixture applicationFixture =
            await RetainedSourceCaptureApplicationFixture.CreateAsync(
                fixture,
                configureServices: services =>
                {
                    forbidden.RemoveForbiddenServices(services);
                    sqlGuard.Configure(services);
                });
        IRetainedSourceApplication application = applicationFixture.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        string permittedRef = await fixture.Repository.RunGitAsync(["symbolic-ref", "HEAD"]);

        SourceCaptureResult capture = await application.AcquireAsync(
            fixture.RepositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, capture.Status);
        Assert.Equal("source_completed", capture.Code);
        string revision = capture.ProfiledRevision ??
                          throw new InvalidOperationException("The source capture did not return a revision.");

        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(
                maximumOperations: 20,
                maximumMetadataEntries: 20,
                maximumReturnedBytes: 4096));
        Assert.Equal(SourceReadStatus.Succeeded, opened.Status);
        await using ISourceReadContext context = opened.Context!;
        SourceListResult listed = await context.ListAsync("src", 10, null);
        Assert.Equal(SourceReadStatus.Succeeded, listed.Status);
        Assert.Contains(listed.Entries, entry =>
            entry.RelativePath == "src/retained-source.txt");
        SourceBytesResult bytes = await context.ReadBytesAsync(
            "src/retained-source.txt",
            0,
            1024);
        Assert.Equal(SourceReadStatus.Succeeded, bytes.Status);
        Assert.Equal(
            "retained-source-fixture\n",
            Encoding.UTF8.GetString(bytes.Content.Span));

        SourceBytesResult[] concurrent = await Task.WhenAll(
            context.ReadBytesAsync("src/retained-source.txt", 0, 24),
            context.ReadBytesAsync("src/retained-source.txt", 0, 24));
        Assert.All(concurrent, result =>
            Assert.Equal(SourceReadStatus.Succeeded, result.Status));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        SourceEntryResult cancelled = await context.GetEntryAsync(
            "src/retained-source.txt",
            cancellation.Token);
        Assert.Equal(SourceReadStatus.Cancelled, cancelled.Status);
        Assert.Equal("source_cancelled", cancelled.Code);

        await context.DisposeAsync();
        await using RetainedSourceAttachedApplication freshApplication =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                applicationFixture.Database.ConnectionString,
                fixture.SourceStorageRoot,
                applicationFixture.UserId,
                createAuxiliaryRoots: false,
                configureServices: services =>
                {
                    forbidden.RemoveForbiddenServices(services);
                    sqlGuard.Configure(services);
                });
        IRetainedSourceApplication fresh = freshApplication.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        OpenSourceReadContextResult reopened = await fresh.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest(maximumOperations: 8, maximumReturnedBytes: 1024));
        Assert.Equal(SourceReadStatus.Succeeded, reopened.Status);
        await using ISourceReadContext reopenedContext = reopened.Context!;
        SourceBytesResult reopenedBytes = await reopenedContext.ReadBytesAsync(
            "src/retained-source.txt",
            0,
            24);
        Assert.Equal(SourceReadStatus.Succeeded, reopenedBytes.Status);
        Assert.Equal(bytes.Content.ToArray(), reopenedBytes.Content.ToArray());

        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            applicationFixture.Database.ConnectionString,
            applicationFixture.SystemId,
            applicationFixture.UserId);
        SourceEntryResult denied = await reopenedContext.GetEntryAsync(
            "src/retained-source.txt");
        Assert.Equal(SourceReadStatus.Denied, denied.Status);
        Assert.Equal("source_denied", denied.Code);
        OpenSourceReadContextResult deniedOpen = await fresh.OpenReadContextAsync(
            fixture.RepositoryId,
            revision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Denied, deniedOpen.Status);
        Assert.Equal("source_denied", deniedOpen.Code);
        Assert.Null(deniedOpen.Context);

        Assert.NotEmpty(forbidden.RemovedServiceTypes);
        Assert.True(sqlGuard.CommandCount > 0);
        Assert.Empty(sqlGuard.ForbiddenAttempts);
        Assert.Contains("Repositories", sqlGuard.ObservedTables);
        Assert.Contains("SourceSnapshots", sqlGuard.ObservedTables);
        _output.WriteLine(
            $"sourceOnly=true; forbiddenServicesRemoved={forbidden.RemovedServiceTypes.Count}; " +
            $"capture={capture.Status}/{capture.Code}; list={listed.Status}/{listed.Code}; " +
            $"read={bytes.Status}/{bytes.Code}; concurrent={concurrent.Length} succeeded; " +
            $"cancelled={cancelled.Status}/{cancelled.Code}; " +
            $"reopen={reopened.Status}/{reopened.Code}; " +
            $"denied={denied.Status}/{denied.Code}; freshOpenDenied={deniedOpen.Status}/{deniedOpen.Code}; " +
            $"domainCommandsObserved={sqlGuard.CommandCount}; " +
            $"observedTables={string.Join(',', sqlGuard.ObservedTables.Order())}; " +
            $"forbiddenTableAttempts={sqlGuard.ForbiddenAttempts.Count}; " +
            "resultLabel=retained-source-capability; " +
            "guardAppliedAfterFixtureSetup=true");
    }

    [Trait("Suite", "PostgreSql")]
    [SuiteFact("PostgreSql")]
    public void OutcomeEvidence_WhenMappedToCurrentPublicCodes_RemainsSourceOnly()
    {
        (MethodInfo? Test, RetainedSourceTestEvidence Evidence)[] traced =
        [
            (typeof(RetainedSourceCaptureTests).GetMethod(nameof(RetainedSourceCaptureTests.AcquireAsync_AuthorizedSystemRepository_ResolvesFullRefAndReturnsCompletedIdentity)),
                RetainedSourceEvidence.Success("source_completed")),
            (typeof(RetainedSourceRangeReadTests).GetMethod(nameof(RetainedSourceRangeReadTests.ReadContext_StrictTextRejectsScalarSplitAndUnsupportedEncoding)),
                RetainedSourceEvidence.Limitation("source_unsupported")),
            (typeof(RetainedSourceReadContextTests).GetMethod(nameof(RetainedSourceReadContextTests.OpenReadContextAsync_AfterAuthorityRevocation_DeniesFreshGrant)),
                RetainedSourceEvidence.Denial("source_denied")),
            (typeof(RetainedSourceReadContextTests).GetMethod(nameof(RetainedSourceReadContextTests.ReadContext_WhenCallerCancelsAfterMetadataAuthorization_ReturnsCancelled)),
                RetainedSourceEvidence.Cancellation("source_cancelled")),
            (typeof(RetainedSourceCaptureTests).GetMethod(nameof(RetainedSourceCaptureTests.AcquireAsync_GitTransportFailure_ReturnsSanitizedAcquisitionFailure)),
                RetainedSourceEvidence.Failure("source_acquisition_failed")),
            (typeof(RetainedSourceReadContextTests).GetMethod(nameof(RetainedSourceReadContextTests.OpenReadContextAsync_WhenManifestIsCorrupt_ReturnsIntegrityFailureWithoutContext)),
                RetainedSourceEvidence.Failure("source_integrity_failed")),
            (typeof(RetainedSourceCaptureTests).GetMethod(nameof(RetainedSourceCaptureTests.AcquireAsync_StorageRootUnavailable_ReturnsSanitizedStorageFailureWithoutPartialIdentity)),
                RetainedSourceEvidence.Failure("source_storage_unavailable")),
        ];

        Assert.Equal(
            [
                RetainedSourceEvidenceKind.Success,
                RetainedSourceEvidenceKind.Limitation,
                RetainedSourceEvidenceKind.Denial,
                RetainedSourceEvidenceKind.Cancellation,
                RetainedSourceEvidenceKind.Failure,
                RetainedSourceEvidenceKind.Failure,
                RetainedSourceEvidenceKind.Failure,
            ],
            traced.Select(item => item.Evidence.Kind));
        Assert.All(traced, item =>
        {
            Assert.NotNull(item.Test);
            Assert.Equal(RetainedSourceTestEvidence.Scope, item.Evidence.EvidenceScope);
            Assert.Equal(RetainedSourceTestEvidence.SourceOnlyBoundary, item.Evidence.Boundary);
        });
        Assert.DoesNotContain(
            traced,
            item => item.Evidence.Description.Contains("Scan", StringComparison.OrdinalIgnoreCase) ||
                    item.Evidence.Description.Contains("Chat", StringComparison.OrdinalIgnoreCase) ||
                    item.Evidence.Description.Contains("Knowledge", StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class RetainedSourceForbiddenDependencyGuard
{
    private static readonly string[] ForbiddenTokens =
    [
        "CodeGraph",
        "JdtLs",
        "Llm",
        "Chat",
        "Knowledge",
        "PreparedScan",
        "ScanExecution",
        "ScanRun",
        "WorkerAgent",
        "SequentialScan",
        "ExactResolution",
        "Pass1",
        "Pass2",
        "BackupB",
    ];

    internal List<string> RemovedServiceTypes { get; } = [];

    internal void RemoveForbiddenServices(IServiceCollection services)
    {
        for (int index = services.Count - 1; index >= 0; index--)
        {
            ServiceDescriptor descriptor = services[index];
            string serviceType = descriptor.ServiceType.FullName ??
                                 descriptor.ServiceType.Name;
            if (!ForbiddenTokens.Any(token =>
                    serviceType.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            services.RemoveAt(index);
            if (!RemovedServiceTypes.Contains(serviceType, StringComparer.Ordinal))
            {
                RemovedServiceTypes.Add(serviceType);
            }
        }
    }
}

internal sealed class RetainedSourceDomainSqlGuard : DbCommandInterceptor
{
    private static readonly Regex TableReference = new(
        "(?:FROM|JOIN|UPDATE|INTO|DELETE\\s+FROM)\\s+(?:(?:\\\"[^\\\"]+\\\"|[A-Za-z0-9_]+)\\.)?(?:\\\"(?<quoted>[A-Za-z0-9_]+)\\\"|(?<plain>[A-Za-z0-9_]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedTables = new(
        [
            "Repositories",
            "SourceSnapshots",
            "Systems",
            "SystemOwnerAssignments",
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly object _gate = new();
    private readonly HashSet<string> _observedTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _forbiddenAttempts = [];
    private int _commandCount;

    internal int CommandCount => Volatile.Read(ref _commandCount);

    internal IReadOnlyCollection<string> ObservedTables
    {
        get
        {
            lock (_gate)
            {
                return [.. _observedTables];
            }
        }
    }

    internal IReadOnlyCollection<string> ForbiddenAttempts
    {
        get
        {
            lock (_gate)
            {
                return [.. _forbiddenAttempts];
            }
        }
    }

    internal void Configure(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<VulgataDbContext>>(_ =>
            new RetainedSourceDomainSqlGuardConfiguration(this));
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Inspect(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Inspect(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Inspect(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Inspect(DbCommand command)
    {
        Interlocked.Increment(ref _commandCount);
        foreach (Match match in TableReference.Matches(command.CommandText))
        {
            string table = match.Groups["quoted"].Success
                ? match.Groups["quoted"].Value
                : match.Groups["plain"].Value;
            lock (_gate)
            {
                if (!AllowedTables.Contains(table))
                {
                    _forbiddenAttempts.Add(table);
                    throw new InvalidOperationException(
                        "The retained-source boundary attempted a forbidden domain table.");
                }

                _observedTables.Add(table);
            }
        }
    }
}

internal sealed class RetainedSourceDomainSqlGuardConfiguration(
    RetainedSourceDomainSqlGuard guard)
    : Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<VulgataDbContext>
{
    public void Configure(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(guard);
    }
}