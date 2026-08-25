using System.Reflection;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Tests.PreparedPass1.Fixtures;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T013 RED boundary for the prepared Pass 1 dispatcher.
///
/// The dispatcher and its packet DTO are intentionally obtained through the
/// accepted public seam.  This keeps the RED test compilable before T014 adds
/// the new prepared path and prevents it from coupling to a private provider
/// or to the legacy SequentialScanRunner dispatcher.
/// </summary>
public sealed class PreparedPass1WorkerDispatcherTests
{
    private const string DispatcherPortName =
        "Vulgata.Core.DomainServices.IPreparedPass1WorkerDispatcher";

    private const string DispatcherImplementationName =
        "Vulgata.Agents.WorkerExecution.PreparedPass1WorkerDispatcher";

    [Fact]
    public void PreparedDispatcher_ExposesDedicatedPortAndServerOwnedDispatchLoop()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        Type implementation = RequireType(
            DispatcherImplementationName,
            LoadAgentsAssembly());

        Assert.True(
            port.IsInterface,
            "The prepared Pass 1 dispatcher must be a dedicated Core port.");
        Assert.True(
            port.IsAssignableFrom(implementation),
            "The Agents implementation must implement the dedicated prepared port.");

        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Assert.Equal(typeof(Task<>), dispatch.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(
            dispatch.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));

        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;
        Assert.NotEqual(
            typeof(WorkerAgentJobPacket),
            requestType);
        Assert.DoesNotContain(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedDispatcher_RequestBindsPreparedUnitsSnapshotAndClassificationProjection()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;
        PropertyInfo[] properties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "PreparedFileCodeUnits" or "FileCodeUnits");
        Assert.Contains(
            properties,
            property => property.Name.Contains("ContextSnapshot", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Classification", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("ProfiledRevision", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Repository", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("ScanRun", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Concurrency", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedDispatcher_PacketProjectionCarriesExactUnitIdentityAndNoCompletionOrder()
    {
        ApprovedPreparedManifestPairSeed pair =
            PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        int preparedUnitCount = pair.Manifests.Sum(manifest => manifest.FileCodeUnits.Count);
        Assert.Equal(4, preparedUnitCount);
        Assert.Equal(
            pair.Manifests.SelectMany(manifest => manifest.FileCodeUnits)
                .Select(unit => unit.IdentityFingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            preparedUnitCount);

        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type resultType = dispatch.ReturnType.GetGenericArguments().Single();
        PropertyInfo packetCollection = Assert.Single(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Packets" or "Jobs" or "DispatchPackets");
        Type packetType = packetCollection.PropertyType
            .GetInterfaces()
            .Append(packetCollection.PropertyType)
            .Where(type => type.IsGenericType)
            .SelectMany(type => type.GetGenericArguments())
            .FirstOrDefault(type => type != typeof(Guid))
            ?? packetCollection.PropertyType.GetElementType()
            ?? throw new InvalidOperationException(
                "The prepared dispatch result must expose a typed packet collection.");

        PropertyInfo[] packetProperties = packetType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(packetProperties, property => property.Name == "JobId");
        Assert.Contains(packetProperties, property => property.Name == "FileCodeUnitId");
        Assert.Contains(packetProperties, property => property.Name == "RepositoryId");
        Assert.Contains(packetProperties, property => property.Name == "ScanRunId");
        Assert.Contains(packetProperties, property => property.Name == "ProfiledRevision");
        Assert.Contains(packetProperties, property => property.Name.Contains("SourceHash", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("FileHash", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("ManifestOrdinal", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("PairSelectionOrdinal", StringComparison.Ordinal));
        Assert.DoesNotContain(
            packetProperties,
            property => property.Name.Contains("Completion", StringComparison.Ordinal) ||
                property.Name.Contains("FinishedAt", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedDispatcher_UsesSharedCapacityAndDoesNotExposeNavigationScheduling()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;

        PropertyInfo[] requestProperties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo concurrency = Assert.Single(
            requestProperties,
            property => property.Name.Contains("Concurrency", StringComparison.Ordinal));
        Assert.True(
            concurrency.PropertyType == typeof(int) || concurrency.PropertyType == typeof(int?),
            "The shared dispatch capacity must be a bounded integer setting.");

        Type implementation = RequireType(
            DispatcherImplementationName,
            LoadAgentsAssembly());
        Assert.DoesNotContain(
            implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Navigate", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DispatchAsync_InvokesEachExecutorConversationOnceAndReturnsItsFirstResultAsync()
    {
        ScriptedProviderExecutor executor = new(turnsBeforeFinalResult: 9);
        IPreparedPass1WorkerDispatcher dispatcher = CreateDispatcher(executor);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            CreateLoopRequest(),
            CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.All(result.Results, item =>
        {
            Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, item.Outcome);
            Assert.Equal("invalid-or-repeated-tool-call", item.ReasonCode);
            Assert.Null(item.ParsedResult);
        });
        Assert.All(executor.TotalTurns, turns => Assert.Equal(1, turns.Value));
        Assert.All(executor.ToolAttemptCounts, attempts => Assert.Equal(1, attempts.Value));
    }

    [Fact]
    public async Task DispatchAsync_RepeatedDurableClaimsDoNotUseDispatcherLocalRetryOrBudgetAsync()
    {
        ScriptedProviderExecutor executor = new(turnsBeforeFinalResult: 8);
        IPreparedPass1WorkerDispatcher dispatcher = CreateDispatcher(executor);
        PreparedPass1DispatchRequest request = CreateLoopRequest();

        PreparedPass1DispatchResult first = await dispatcher.DispatchAsync(
            request,
            CancellationToken.None);
        PreparedPass1DispatchResult replay = await dispatcher.DispatchAsync(
            request,
            CancellationToken.None);

        Assert.Equal(
            first.Results.Select(item => item.JobId),
            replay.Results.Select(item => item.JobId));
        Assert.All(executor.TotalTurns, turns => Assert.Equal(2, turns.Value));
        Assert.All(executor.ToolAttemptCounts, attempts => Assert.Equal(2, attempts.Value));
        Assert.All(first.Results.Concat(replay.Results), item =>
        {
            Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, item.Outcome);
            Assert.Equal("invalid-or-repeated-tool-call", item.ReasonCode);
            Assert.Null(item.ParsedResult);
        });

        Type packetType = resultPacketType();
        Assert.NotNull(
            packetType.GetProperty("RemainingNavigationBudget", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public async Task DispatchAsync_InvokesExecutorExactlyOncePerDurableClaimAsync()
    {
        OneShotRejectedExecutor executor = new();
        IPreparedPass1WorkerDispatcher dispatcher = CreateDispatcher(executor);
        PreparedPass1DispatchRequest request = CreateLoopRequest();
        request = new(
            [request.PreparedFileCodeUnits[0]],
            [request.ContextSnapshots[0]],
            [],
            1);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            request,
            CancellationToken.None);

        Assert.Equal(1, executor.CallCount);
        PreparedPass1JobDispatchResult item = Assert.Single(result.Results);
        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, item.Outcome);
        Assert.Equal("invalid-or-repeated-tool-call", item.ReasonCode);
    }

    [Fact]
    public void PreparedDispatcher_DoesNotOwnConversationRetryOrBudgetState()
    {
        FieldInfo[] fields = typeof(PreparedPass1WorkerDispatcher)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.DoesNotContain(
            fields,
            field => field.FieldType.Name.Contains("ConversationState", StringComparison.Ordinal) ||
                field.Name.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
                field.Name.Contains("budget", StringComparison.OrdinalIgnoreCase) ||
                field.Name.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DispatchAsync_InvalidCompletedPayload_ReportsOneSanitizedCategoryAndClosedFailureAsync()
    {
        RecordingSubmissionDiagnosticObserver observer = new();
        InvalidCompletedResultExecutor executor = new();
        IPreparedPass1WorkerDispatcher dispatcher =
            new PreparedPass1WorkerDispatcher(executor, observer);
        PreparedPass1DispatchRequest request = CreateLoopRequest();
        request = new(
            [request.PreparedFileCodeUnits[0]],
            [request.ContextSnapshots[0]],
            [],
            1);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            request,
            CancellationToken.None);
        PreparedPass1JobDispatchResult item = Assert.Single(result.Results);

        Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, item.Outcome);
        Assert.Equal("worker-result-rejected", item.ReasonCode);
        Assert.Null(item.ParsedResult);
        Assert.Equal(
            [PreparedPass1SubmissionRejectionKind.InvalidShape],
            observer.Categories);
    }

    [Fact]
    public async Task DispatchAsync_EightRefusedToolAttemptsRemainExecutorOwnedAndDoNotLoopAsync()
    {
        ScriptedProviderExecutor executor = new(turnsBeforeFinalResult: 8);
        IPreparedPass1WorkerDispatcher dispatcher = CreateDispatcher(executor);

        PreparedPass1DispatchResult result = await dispatcher.DispatchAsync(
            CreateLoopRequest(),
            CancellationToken.None);

        Assert.All(result.Results, item =>
        {
            Assert.Equal(PreparedPass1RawWorkerOutcome.Rejected, item.Outcome);
            Assert.Equal("invalid-or-repeated-tool-call", item.ReasonCode);
            Assert.Null(item.ParsedResult);
        });
        Assert.All(executor.TotalTurns, turns => Assert.Equal(1, turns.Value));
        Assert.All(executor.ToolAttemptCounts, attempts => Assert.Equal(1, attempts.Value));
    }

    private static Type RequireType(string fullName, Assembly assembly) =>
        assembly.GetType(fullName, throwOnError: false, ignoreCase: false)
        ?? throw new InvalidOperationException(
            $"T014 production seam is missing: {fullName}.");

    private static Assembly LoadAgentsAssembly() =>
        Assembly.Load("Vulgata.Agents");

    private static IPreparedPass1WorkerDispatcher CreateDispatcher(
        IPreparedPass1JobExecutor executor)
    {
        Type implementation = RequireType(
            DispatcherImplementationName,
            LoadAgentsAssembly());
        return Assert.IsAssignableFrom<IPreparedPass1WorkerDispatcher>(
            Activator.CreateInstance(implementation, executor));
    }

    private static Type resultPacketType()
    {
        MethodInfo dispatch = Assert.Single(
            typeof(IPreparedPass1WorkerDispatcher).GetMethods(),
            method => method.Name == "DispatchAsync");
        Type resultType = dispatch.ReturnType.GetGenericArguments().Single();
        PropertyInfo packetCollection = Assert.Single(
            resultType.GetProperties(),
            property => property.Name is "Packets" or "Jobs" or "DispatchPackets");
        return packetCollection.PropertyType
            .GetInterfaces()
            .Append(packetCollection.PropertyType)
            .Where(type => type.IsGenericType)
            .SelectMany(type => type.GetGenericArguments())
            .FirstOrDefault(type => type != typeof(Guid))
            ?? packetCollection.PropertyType.GetElementType()
            ?? throw new InvalidOperationException("The dispatch result must expose packets.");
    }

    private static PreparedPass1DispatchRequest CreateLoopRequest()
    {
        ApprovedPreparedManifestPairSeed pair =
            PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        Guid systemId = Guid.Parse("50000000-0000-4000-8000-000000000005");
        Guid pairExecutionId = Guid.Parse("60000000-0000-4000-8000-000000000006");
        List<PreparedPass1FileCodeUnitAssignment> assignments = [];
        List<PreparedPass1ContextSnapshotProjection> snapshots = [];

        foreach (PreparedManifestSeed manifest in pair.Manifests)
        {
            PreparedFileCodeUnitSeed file = manifest.FileCodeUnits[0];
            Guid scanExecutionId = GuidFromHash($"execution:{manifest.RepositoryId:D}");
            Guid manifestId = GuidFromHash($"manifest:{manifest.RepositoryId:D}");
            Guid contextId = GuidFromHash($"context:{manifest.RepositoryId:D}");
            byte[] source = Encoding.UTF8.GetBytes(file.Source);
            string contextHash = Sha256($"context:{manifest.RepositoryId:D}");
            snapshots.Add(new(
                contextId,
                manifest.RepositoryId,
                manifest.ScanRunId,
                manifest.ProfiledRevision,
                contextHash,
                contextHash,
                null,
                null,
                null));

            Guid fileCodeUnitId = GuidFromHash($"unit:{manifest.RepositoryId:D}:{file.RelativePath}");
            Guid jobId = ScanJobExecution.Create(
                scanExecutionId,
                manifest.RepositoryId,
                manifest.ScanRunId,
                fileCodeUnitId,
                file.ManifestOrdinal,
                DateTimeOffset.UnixEpoch).Id;
            assignments.Add(new(
                pairExecutionId,
                jobId,
                scanExecutionId,
                systemId,
                manifest.RepositoryId,
                manifest.ScanRunId,
                manifest.ProfiledRevision,
                fileCodeUnitId,
                file.IdentityFingerprint,
                manifestId,
                manifest.ManifestFingerprint,
                pair.PairFingerprint,
                file.RelativePath,
                0,
                source.Length,
                1,
                Math.Max(1, file.Source.Count(value => value == '\n') + 1),
                1,
                1,
                file.FileHash,
                file.SourceHash,
                "story-12-3-loop-prompt",
                "code-logic/file-v2",
                Sha256("runtime:story-12-3"),
                contextId,
                file.ManifestOrdinal,
                file.PairSelectionOrdinal,
                source));
        }

        return new(
            assignments,
            snapshots,
            [],
            2);
    }

    private static Guid GuidFromHash(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class ScriptedProviderExecutor(int turnsBeforeFinalResult)
        : IPreparedPass1JobExecutor
    {
        private readonly ConcurrentDictionary<Guid, int> _turns = new();
        private readonly ConcurrentDictionary<Guid, int> _toolAttempts = new();

        public IReadOnlyDictionary<Guid, int> TotalTurns => _turns;

        public IReadOnlyDictionary<Guid, int> ToolAttemptCounts => _toolAttempts;

        public int TurnCount(Guid jobId) => _turns[jobId];

        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            int turn = _turns.AddOrUpdate(packet.JobId, 1, (_, previous) => previous + 1);
            if (turn <= turnsBeforeFinalResult)
            {
                _toolAttempts.AddOrUpdate(packet.JobId, 1, (_, previous) => previous + 1);
                return Task.FromResult(new PreparedPass1RawWorkerResult(
                    PreparedPass1RawWorkerOutcome.Rejected,
                    ReadOnlyMemory<byte>.Empty,
                    "invalid-or-repeated-tool-call"));
            }

            return Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Completed,
                DeferredFallbackJson(packet),
                null));
        }

        private static ReadOnlyMemory<byte> DeferredFallbackJson(
            PreparedPass1WorkerPacket packet) =>
            JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaIdentity = "code-logic/file-v2",
                binding = new
                {
                    jobId = packet.JobId.ToString("D"),
                    scanExecutionId = packet.ScanExecutionId.ToString("D"),
                    systemId = packet.SystemId.ToString("D"),
                    repositoryId = packet.RepositoryId.ToString("D"),
                    scanRunId = packet.ScanRunId.ToString("D"),
                    profiledRevision = packet.ProfiledRevision,
                    fileCodeUnitId = packet.FileCodeUnitId.ToString("D"),
                    fileCodeUnitIdentityFingerprint = packet.FileCodeUnitIdentityFingerprint,
                    preparedScanManifestId = packet.PreparedScanManifestId.ToString("D"),
                    preparedScanManifestFingerprint = packet.PreparedScanManifestFingerprint,
                    pairPackageFingerprint = packet.PairPackageFingerprint,
                    repositoryRelativePath = packet.RepositoryRelativePath,
                    byteStart = packet.ByteStart,
                    byteEnd = packet.ByteEnd,
                    lineStart = packet.LineStart,
                    lineEnd = packet.LineEnd,
                    segmentOrdinal = packet.SegmentOrdinal,
                    segmentCount = packet.SegmentCount,
                    fileHash = packet.FileHash,
                    sourceHash = packet.SourceHash,
                    promptIdentity = packet.PromptIdentity,
                    documentSchemaIdentity = packet.DocumentSchemaIdentity,
                    analysisRuntimeIdentityHash = packet.AnalysisRuntimeIdentityHash,
                },
                sourceAnchors = Array.Empty<object>(),
                filePurpose = new { summary = "deferred navigation bound" },
                internalUnits = Array.Empty<object>(),
                executionFlow = Array.Empty<object>(),
                dataFlow = Array.Empty<object>(),
                rules = Array.Empty<object>(),
                dataAccess = Array.Empty<object>(),
                communicationEvidence = Array.Empty<object>(),
                knownReferences = Array.Empty<object>(),
                deferredObservations = new[] { new { outcome = "BoundExceeded", boundKind = "Calls" } },
                assumptions = Array.Empty<object>() });
        }

    private sealed class InvalidCompletedResultExecutor : IPreparedPass1JobExecutor
    {
        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Completed,
                Encoding.UTF8.GetBytes(
                    "{\"schemaIdentity\":\"code-logic/file-v2\",\"schemaIdentity\":\"code-logic/file-v2\"}"),
                null));
    }

    private sealed class OneShotRejectedExecutor : IPreparedPass1JobExecutor
    {
        public int CallCount { get; private set; }

        public Task<PreparedPass1RawWorkerResult> ExecuteAsync(
            PreparedPass1WorkerPacket packet,
            string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new PreparedPass1RawWorkerResult(
                PreparedPass1RawWorkerOutcome.Rejected,
                ReadOnlyMemory<byte>.Empty,
                "invalid-or-repeated-tool-call"));
        }
    }

    private sealed class RecordingSubmissionDiagnosticObserver
        : IPreparedPass1SubmissionDiagnosticObserver
    {
        public List<PreparedPass1SubmissionRejectionKind> Categories { get; } = [];

        public void Observe(PreparedPass1SubmissionRejectionKind category) =>
            Categories.Add(category);
    }
}
