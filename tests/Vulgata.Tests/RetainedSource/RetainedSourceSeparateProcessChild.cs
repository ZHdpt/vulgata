using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.RetainedSource;

/// <summary>
/// Marker and mode contract for the later child-process test. No test method is
/// declared in the scaffolding phase, so invoking the filter cannot produce a
/// passing placeholder or be mistaken for reopen qualification.
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

    internal static bool IsEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable(ModeVariable),
            ModeValue,
            StringComparison.Ordinal);

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

    private static string RequireVariable(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
            ? Environment.GetEnvironmentVariable(name)!
            : throw new InvalidOperationException(
                $"The retained-source child requires {name}.");
}

internal sealed class RetainedSourceChildFactAttribute : FactAttribute
{
    public RetainedSourceChildFactAttribute()
    {
        if (!RetainedSourceSeparateProcessChild.IsEnabled())
        {
            Skip = "Retained-source child process qualification is opt-in.";
        }
    }
}
