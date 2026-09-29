using Vulgata.Tests.TestInfrastructure;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Marker and mode contract for the opt-in child-process qualification. Each
/// operation is explicitly selected so the ordinary test run remains skipped.
/// </summary>
internal static class RetainedSourceSeparateProcessChild
{
    internal const string ModeVariable = "VULGATA_RETAINED_SOURCE_CHILD";

    internal const string ModeValue = "1";

    internal const string ConnectionVariable = "VULGATA_RETAINED_SOURCE_CHILD_CONNECTION";

    internal const string StorageRootVariable = "VULGATA_RETAINED_SOURCE_CHILD_STORAGE_ROOT";

    internal const string UserVariable = "VULGATA_RETAINED_SOURCE_CHILD_USER";

    internal const string RepositoryVariable = "VULGATA_RETAINED_SOURCE_CHILD_REPOSITORY";

    internal const string RefVariable = "VULGATA_RETAINED_SOURCE_CHILD_REF";

    internal const string ReadyMarkerVariable = "VULGATA_RETAINED_SOURCE_CHILD_READY";

    internal const string StaleMinutesVariable = "VULGATA_RETAINED_SOURCE_CHILD_STALE_MINUTES";

    internal const string OperationVariable = "VULGATA_RETAINED_SOURCE_CHILD_OPERATION";

    internal const string ResultVariable = "VULGATA_RETAINED_SOURCE_CHILD_RESULT";

    internal const string ExpectedSha256Variable = "VULGATA_RETAINED_SOURCE_CHILD_EXPECTED_SHA256";

    internal const string ExpectedByteLengthVariable = "VULGATA_RETAINED_SOURCE_CHILD_EXPECTED_BYTE_LENGTH";

    internal const string CaptureOperation = "capture";

    internal const string ReopenOperation = "reopen";

    internal const string CorruptionOperation = "corrupt-reopen";

    internal const string CrashOperation = "crash";

    internal static IReadOnlyList<string> ContractVariables =>
    [
        ModeVariable,
        ConnectionVariable,
        StorageRootVariable,
        UserVariable,
        RepositoryVariable,
        RefVariable,
        ReadyMarkerVariable,
        StaleMinutesVariable,
        OperationVariable,
        ResultVariable,
        ExpectedSha256Variable,
        ExpectedByteLengthVariable,
    ];

    internal static bool IsEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(ModeVariable),
            ModeValue,
            StringComparison.Ordinal);

    internal static bool IsEnabled(string operation) =>
        IsEnabled() && string.Equals(
            Environment.GetEnvironmentVariable(OperationVariable),
            operation,
            StringComparison.Ordinal);

    internal static bool IsCrashEnabled() =>
        IsEnabled() &&
        (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OperationVariable)) ||
            IsEnabled(CrashOperation));

    internal static void RequireEnabled()
    {
        if (!IsEnabled())
        {
            throw new InvalidOperationException(
                "The retained-source child process requires its explicit test mode.");
        }
    }
}

public sealed class RetainedSourceSeparateProcessChildTests
{
    [Trait("Suite", "PostgreSql")]
    [RetainedSourceChildFact]
    public async Task CaptureAsync_WhenParentTerminatesAfterSeal_LeavesOwnedAbandonedOperation()
    {
        RetainedSourceSeparateProcessChild.RequireEnabled();
        string connectionString = RequireVariable(
            RetainedSourceSeparateProcessChild.ConnectionVariable);
        string sourceStorageRoot = RequireVariable(
            RetainedSourceSeparateProcessChild.StorageRootVariable);
        string userId = RequireVariable(RetainedSourceSeparateProcessChild.UserVariable);
        string markerPath = RequireVariable(
            RetainedSourceSeparateProcessChild.ReadyMarkerVariable);
        Guid repositoryId = Guid.Parse(RequireVariable(
            RetainedSourceSeparateProcessChild.RepositoryVariable));
        string permittedRef = RequireVariable(RetainedSourceSeparateProcessChild.RefVariable);
        string staleMinutes = RequireVariable(
            RetainedSourceSeparateProcessChild.StaleMinutesVariable);
        int finalAuthorizationCount = 0;

        async Task WaitAtFinalAuthorizationAsync(
            SourceAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            if (request.ProfiledRevision is null ||
                Interlocked.Increment(ref finalAuthorizationCount) != 2)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(markerPath, Environment.ProcessId.ToString());
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                connectionString,
                sourceStorageRoot,
                userId,
                configurationOverrides: new Dictionary<string, string?>
                {
                    ["SourceStorage:OperationStaleAfterMinutes"] = staleMinutes,
                },
                authorizationWait: WaitAtFinalAuthorizationAsync);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult result = await application.AcquireAsync(
            repositoryId,
            RequireVariable(RetainedSourceSeparateProcessChild.RefVariable),
            cancellation.Token);
        throw new InvalidOperationException(
            $"The child capture returned before parent termination: {result.Status}/{result.Code}.");
    }

    [Trait("Suite", "PostgreSql")]
    [RetainedSourceChildOperationFact(
        RetainedSourceSeparateProcessChild.CaptureOperation)]
    public async Task CaptureAsync_FromFreshProductionComposition_CompletesAndRecordsIdentity()
    {
        RetainedSourceSeparateProcessChild.RequireEnabled();
        string connectionString = RequireVariable(
            RetainedSourceSeparateProcessChild.ConnectionVariable);
        string sourceStorageRoot = RequireVariable(
            RetainedSourceSeparateProcessChild.StorageRootVariable);
        string userId = RequireVariable(RetainedSourceSeparateProcessChild.UserVariable);
        Guid repositoryId = Guid.Parse(RequireVariable(
            RetainedSourceSeparateProcessChild.RepositoryVariable));
        string permittedRef = RequireVariable(RetainedSourceSeparateProcessChild.RefVariable);
        string resultPath = RequireVariable(RetainedSourceSeparateProcessChild.ResultVariable);

        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                connectionString,
                sourceStorageRoot,
                userId,
                createAuxiliaryRoots: false);
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        SourceCaptureResult result = await application.AcquireAsync(
            repositoryId,
            permittedRef);
        Assert.Equal(SourceCaptureStatus.Completed, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ProfiledRevision));
        OpenSourceReadContextResult originalContext = await application.OpenReadContextAsync(
            repositoryId,
            result.ProfiledRevision!,
            new SourceReadGrantRequest(maximumOperations: 4, maximumReturnedBytes: 1024));
        Assert.Equal(SourceReadStatus.Succeeded, originalContext.Status);
        await originalContext.Context!.DisposeAsync();
        RetainedSourceSeparateProcessChildTestsExtensions.WriteResult(
            resultPath,
            new RetainedSourceChildResult(
            RetainedSourceSeparateProcessChild.CaptureOperation,
            Environment.ProcessId,
            result.Status.ToString(),
            result.Code,
            result.ProfiledRevision,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    [Trait("Suite", "PostgreSql")]
    [RetainedSourceChildOperationFact(
        RetainedSourceSeparateProcessChild.ReopenOperation)]
    public async Task OpenReadContextAsync_FromFreshProductionComposition_ReadsRetainedBytesAfterRemoteUnavailable()
    {
        RetainedSourceSeparateProcessChild.RequireEnabled();
        string connectionString = RequireVariable(
            RetainedSourceSeparateProcessChild.ConnectionVariable);
        string sourceStorageRoot = RequireVariable(
            RetainedSourceSeparateProcessChild.StorageRootVariable);
        string userId = RequireVariable(RetainedSourceSeparateProcessChild.UserVariable);
        Guid repositoryId = Guid.Parse(RequireVariable(
            RetainedSourceSeparateProcessChild.RepositoryVariable));
        string profiledRevision = RequireVariable(RetainedSourceSeparateProcessChild.RefVariable);
        string expectedSha256 = RequireVariable(
            RetainedSourceSeparateProcessChild.ExpectedSha256Variable);
        int expectedByteLength = int.Parse(RequireVariable(
            RetainedSourceSeparateProcessChild.ExpectedByteLengthVariable));
        string resultPath = RequireVariable(RetainedSourceSeparateProcessChild.ResultVariable);

        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                connectionString,
                sourceStorageRoot,
                userId,
                createAuxiliaryRoots: false);
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            repositoryId,
            profiledRevision,
            new SourceReadGrantRequest(maximumOperations: 10, maximumReturnedBytes: 4096));
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
        byte[] content = bytes.Content.ToArray();
        string contentSha256 = Convert.ToHexString(SHA256.HashData(content));
        Assert.Equal(expectedSha256, contentSha256);
        Assert.Equal(expectedByteLength, content.Length);

        await RetainedSourceCaptureApplicationFixture.RevokeAuthorityAsync(
            connectionString,
            Guid.Parse("13000000-0000-4000-8000-000000000201"),
            userId);
        SourceBytesResult denied = await context.ReadBytesAsync(
            "src/retained-source.txt",
            0,
            1024);
        Assert.Equal(SourceReadStatus.Denied, denied.Status);
        Assert.Equal("source_denied", denied.Code);
        Assert.Empty(denied.Content.ToArray());
        OpenSourceReadContextResult deniedOpen = await application.OpenReadContextAsync(
            repositoryId,
            profiledRevision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.Denied, deniedOpen.Status);
        Assert.Equal("source_denied", deniedOpen.Code);
        Assert.Null(deniedOpen.Context);

        RetainedSourceSeparateProcessChildTestsExtensions.WriteResult(
            resultPath,
            new RetainedSourceChildResult(
            RetainedSourceSeparateProcessChild.ReopenOperation,
            Environment.ProcessId,
            bytes.Status.ToString(),
            bytes.Code,
            profiledRevision,
            contentSha256,
            content.Length,
            denied.Status.ToString(),
            denied.Code,
            deniedOpen.Status.ToString(),
            deniedOpen.Code));
    }

    [Trait("Suite", "PostgreSql")]
    [RetainedSourceChildOperationFact(
        RetainedSourceSeparateProcessChild.CorruptionOperation)]
    public async Task OpenReadContextAsync_WhenRetainedBlobIsCorrupt_DeniesWithoutContext()
    {
        RetainedSourceSeparateProcessChild.RequireEnabled();
        string connectionString = RequireVariable(
            RetainedSourceSeparateProcessChild.ConnectionVariable);
        string sourceStorageRoot = RequireVariable(
            RetainedSourceSeparateProcessChild.StorageRootVariable);
        string userId = RequireVariable(RetainedSourceSeparateProcessChild.UserVariable);
        Guid repositoryId = Guid.Parse(RequireVariable(
            RetainedSourceSeparateProcessChild.RepositoryVariable));
        string profiledRevision = RequireVariable(RetainedSourceSeparateProcessChild.RefVariable);
        string resultPath = RequireVariable(RetainedSourceSeparateProcessChild.ResultVariable);

        await using RetainedSourceAttachedApplication attached =
            RetainedSourceCaptureApplicationFixture.CreateAttached(
                connectionString,
                sourceStorageRoot,
                userId,
                createAuxiliaryRoots: false);
        IRetainedSourceApplication application = attached.Scope.ServiceProvider
            .GetRequiredService<IRetainedSourceApplication>();
        OpenSourceReadContextResult opened = await application.OpenReadContextAsync(
            repositoryId,
            profiledRevision,
            new SourceReadGrantRequest());
        Assert.Equal(SourceReadStatus.IntegrityFailure, opened.Status);
        Assert.Equal("source_integrity_failed", opened.Code);
        Assert.Null(opened.Context);
        RetainedSourceSeparateProcessChildTestsExtensions.WriteResult(
            resultPath,
            new RetainedSourceChildResult(
            RetainedSourceSeparateProcessChild.CorruptionOperation,
            Environment.ProcessId,
            opened.Status.ToString(),
            opened.Code,
            profiledRevision,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    private static string RequireVariable(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!
            : throw new InvalidOperationException(
                $"The retained-source child requires {name}.");
}

internal sealed class RetainedSourceChildFactAttribute : SuiteFactAttribute
{
    public RetainedSourceChildFactAttribute() : base("PostgreSql")
    {
        if (!RetainedSourceSeparateProcessChild.IsCrashEnabled())
        {
            Skip = "Retained-source child process qualification is opt-in.";
        }
    }
}

internal sealed class RetainedSourceChildOperationFactAttribute : SuiteFactAttribute
{
    public RetainedSourceChildOperationFactAttribute(string operation) : base("PostgreSql")
    {
        if (!RetainedSourceSeparateProcessChild.IsEnabled(operation))
        {
            Skip = "Retained-source child process qualification is opt-in.";
        }
    }
}

internal static class RetainedSourceSeparateProcessChildTestsExtensions
{
    internal static void WriteResult(string path, RetainedSourceChildResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(result));
    }
}
