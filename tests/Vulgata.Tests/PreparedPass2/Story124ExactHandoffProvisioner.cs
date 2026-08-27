using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.CrossRepository;
using Vulgata.Infrastructure.Data;
using Vulgata.Tests.PreparedPass1;
using Vulgata.Tests.PreparedScanning;

namespace Vulgata.Tests.PreparedPass2;

/// <summary>
/// Creates the exact, accepted Story 12.3 handoff needed by the opt-in Story 12.4
/// production qualification. Every Source Anchor is bound to bytes from the two
/// explicitly approved repository roots; no generated source is used as authority.
/// </summary>
internal static class Story124ExactHandoffProvisioner
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private const string ConsumerPath =
        "src/main/java/cn/com/yitong/actions/mobile/UpdateChannelSessionOp.java";
    private const string ProviderXmlPath =
        "src/main/resources/disgram/in/cust/updateSession.xml";
    private const string ProviderImplementationPath =
        "src/main/java/cn/com/yitong/actions/domain/login/UpdateSessionOp.java";

    internal static async Task<T062QualificationInputs> ProvisionAsync(string connectionString)
    {
        string consumerRoot = Required("VULGATA_T062_REPOSITORY_ONE_ROOT");
        string providerRoot = Required("VULGATA_T062_REPOSITORY_TWO_ROOT");
        var endpoint = new Uri(Required("VULGATA_T062_WORKER_PROVIDER_ENDPOINT"));
        string model = Required("VULGATA_T062_WORKER_PROVIDER_MODEL");
        ExactResolutionProfile profile = new ExactResolutionProfileCatalog().Load();
        string consumerRevision = Commit(profile, "mbs_account");
        string providerRevision = Commit(profile, "mbs_channel");
        RequireApprovedRevision(consumerRoot, consumerRevision);
        RequireApprovedRevision(providerRoot, providerRevision);

        await using ExactCrossRepositoryTestFixture fixture =
            await ExactCrossRepositoryTestFixture.CreateAsync();
        await CopyApprovedSourceAsync(consumerRoot, ConsumerPath,
            fixture.ConsumerRepository.RootPath);
        await CopyApprovedSourceAsync(providerRoot, ProviderXmlPath,
            fixture.ProviderRepository.RootPath);
        await CopyApprovedSourceAsync(providerRoot, ProviderImplementationPath,
            fixture.ProviderRepository.RootPath);

        await using VulgataDbContext context =
            PreparedScanPostgreSqlFixture.CreateContext(connectionString);
        Guid pairId = ExactCrossRepositoryTestValues.Id(12_001);
        if (await context.Pass1PairExecutions.AnyAsync(value => value.Id == pairId))
        {
            throw new Xunit.Sdk.XunitException(
                "The retained Story 12.4 source already contains the provisioned exact handoff; " +
                "use it directly for subsequent inspection or clone a pre-Pass-2 source.");
        }

        ExactPass1DocumentFixture consumer = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Consumer, consumerRevision);
        ExactPass1DocumentFixture provider = fixture.CreatePass1Document(
            ExactPass1DocumentKind.Provider, providerRevision);
        BindProductionDocumentIdentity(consumer);
        BindProductionDocumentIdentity(provider);
        await BindApprovedSourceIdentityAsync(consumerRoot, consumer);
        await BindApprovedSourceIdentityAsync(providerRoot, provider);
        await ExactCrossRepositoryResolutionTests.SeedAcceptedV2PairAsync(
            context, fixture, consumer, provider);

        ExactPass1DocumentFixture providerImplementation = await AddProviderImplementationAsync(
            context, providerRoot, providerRevision, provider);
        await AddOrdinaryMemberAuthoritiesAsync(
            context, consumer, provider, providerImplementation);
        context.Pass1Outcomes.Add(Pass1Outcome.Create(
            pairId,
            Pass1OutcomeStatus.Succeeded,
            dispatchedCount: 2,
            completedCount: 2,
            ordinaryFailureCount: 0,
            highValueSeedCount: 0,
            succeededHighValueSeedCount: 0,
            reconciliationFingerprint: null,
            reconciledEdgeCount: 0,
            failureInventory: [],
            completedAt: ExactCrossRepositoryTestValues.Pass1CompletedAt));
        await context.SaveChangesAsync();

        T062RepositoryQualification[] repositories =
        [
            new(
                ExactCrossRepositoryTestValues.Id(11_001),
                ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                consumerRoot,
                consumerRevision),
            new(
                ExactCrossRepositoryTestValues.Id(11_002),
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                providerRoot,
                providerRevision),
        ];
        await ResolveExactHandoffAsync(context, profile, repositories, consumer);

        Guid[] manifests =
        [
            ExactCrossRepositoryTestValues.Id(11_001),
            ExactCrossRepositoryTestValues.Id(11_002),
        ];
        Assert.Equal(2, await context.PreparedScanManifests.CountAsync(
            value => manifests.Contains(value.Id) &&
                value.ApprovalState == PreparedScanManifestApprovalState.Approved));
        Assert.Equal(3, await context.DocumentVersions.CountAsync(value =>
            value.SystemId == ExactCrossRepositoryTestValues.SystemId &&
            value.SchemaIdentity == "code-logic/file-v2"));

        return new T062QualificationInputs(
            connectionString,
            ExactCrossRepositoryTestValues.SystemId,
            manifests,
            repositories,
            endpoint,
            model);
    }

    private static async Task ResolveExactHandoffAsync(
        VulgataDbContext context,
        ExactResolutionProfile profile,
        IReadOnlyList<T062RepositoryQualification> repositories,
        ExactPass1DocumentFixture consumer)
    {
        ExactResolutionRuntimeRequest request = ExactResolutionRuntimeRequest.Create(
            ExactCrossRepositoryTestValues.SystemId,
            profile,
            [
                new(
                    "mbs_account",
                    "mbs_account",
                    ExactCrossRepositoryTestValues.ConsumerRepositoryId,
                    ExactCrossRepositoryTestValues.ConsumerScanRunId,
                    ExactCrossRepositoryTestValues.ConsumerScanExecutionId),
                new(
                    "mbs_channel",
                    "mbs_channel",
                    ExactCrossRepositoryTestValues.ProviderRepositoryId,
                    ExactCrossRepositoryTestValues.ProviderScanRunId,
                    ExactCrossRepositoryTestValues.ProviderScanExecutionId),
            ]);
        var scope = new RepositoryAccessScope(
        [
            ExactCrossRepositoryTestValues.ConsumerRepositoryId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
        ]);
        CurrentCommunicationCandidateSet candidates = await new CurrentCommunicationCandidateSource(
            context,
            new Story124QualificationSourceReader(repositories)).ReconstructAsync(request, scope);
        UnsupportedObservation unsupportedProfile = Assert.Single(profile.UnsupportedObservations);
        var unsupported = new ExactResolutionDecision(
            unsupportedProfile.ObservationId,
            ExactResolutionDecisionKind.UnsupportedObservation,
            new ExactResolutionOwner(
                consumer.DocumentVersion.RepositoryId,
                consumer.DocumentVersion.ScanRunId,
                consumer.DocumentVersion.Id,
                consumer.SourceAnchor.Id,
                consumer.DocumentVersion.ProfiledRevision),
            null,
            "unsupported_kind",
            consumer.SourceAnchor.EvidenceLocator,
            ConsumerEntryStableKey: consumer.CodeUnit.SourceHash);
        ExactResolutionDecisionBatch batch = await new ExactCrossRepositoryResolver().ResolveAsync(
            profile,
            candidates.Consumers,
            candidates.Providers,
            [unsupported]);
        ExactResolutionBatchOutcome outcome = await new ExactCrossRepositoryResolutionStore(context)
            .ApplyAsync(batch, scope);
        Assert.Equal(ExactResolutionOutcomeKind.Applied, outcome.Kind);
        Assert.Equal(2, await context.CrossRepositoryResolutionDecisions.CountAsync(value =>
            value.Pass1PairExecutionId == ExactCrossRepositoryTestValues.Id(12_001)));
    }

    private static async Task<ExactPass1DocumentFixture> AddProviderImplementationAsync(
        VulgataDbContext context,
        string providerRoot,
        string revision,
        ExactPass1DocumentFixture providerXml)
    {
        string fullPath = FullPath(providerRoot, ProviderImplementationPath);
        byte[] bytes = await File.ReadAllBytesAsync(fullPath);
        string sourceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Guid codeUnitId = ExactCrossRepositoryTestValues.Id(404);
        Guid logicalDocumentId = ExactCrossRepositoryTestValues.Id(504);
        Guid documentVersionId = OrdinaryPass1JobIdentity.CreateDocumentVersionId(
            OrdinaryPass1JobIdentity.Create(
                providerXml.ScanExecution.Id,
                providerXml.ScanRun.Id,
                ExactCrossRepositoryTestValues.ProviderRepositoryId,
                codeUnitId,
                revision).JobId);
        Guid sourceAnchorId = ExactCrossRepositoryTestValues.Id(704);
        Guid edgeId = ExactCrossRepositoryTestValues.Id(1_404);
        var codeUnit = new CodeUnit(
            providerXml.ScanRun.Id,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ProviderImplementationPath,
            sourceHash,
            sourceHash,
            25,
            25,
            "cn.com.yitong.actions.domain.login.UpdateSessionOp",
            CodeUnitType.Class,
            RangeConfidence.Exact,
            "java");
        ExactCrossRepositoryReflection.SetId(codeUnit, codeUnitId);
        context.CodeUnits.Add(codeUnit);

        LogicalDocument logical = LogicalDocument.Create(
            logicalDocumentId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            DocumentType.CodeLogic,
            "exact-pass1/provider-implementation",
            ExactCrossRepositoryTestValues.CreatedAt);
        const string json = "{\"schemaIdentity\":\"code-logic/file-v2\",\"sourceGrounded\":true}";
        string markdown = "# cn.com.yitong.actions.domain.login.UpdateSessionOp\n\n" +
            "Source-grounded provider implementation for the exact qualification profile.";
        (long byteStart, long byteEnd) = ExactLineByteRange(bytes, 25, 25);
        DocumentVersion document = DocumentVersion.Create(
            documentVersionId,
            logicalDocumentId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            ExactCrossRepositoryTestValues.SystemId,
            providerXml.ScanRun.Id,
            revision,
            1,
            "cn.com.yitong.actions.domain.login.UpdateSessionOp",
            ProviderImplementationPath,
            "code-logic/file-v2",
            json,
            markdown,
            Hash(json),
            Hash(markdown),
            ExactCrossRepositoryTestValues.Pass1CompletedAt);
        document.SourceAnchors.Add(new SourceAnchor(
            sourceAnchorId,
            documentVersionId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            providerXml.ScanRun.Id,
            codeUnitId,
            revision,
            ProviderImplementationPath,
            ProviderImplementationPath + "#line=25;ordinal=0",
            25,
            25,
            sourceHash,
            byteStart,
            byteEnd,
            ExactCrossRepositoryTestValues.Pass1CompletedAt));
        context.LogicalDocuments.Add(logical);
        await context.SaveChangesAsync();
        context.DocumentVersions.Add(document);
        context.DocumentEdges.Add(new DocumentEdge(
            edgeId,
            providerXml.DocumentVersion.Id,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            providerXml.ScanRun.Id,
            documentVersionId,
            ExactCrossRepositoryTestValues.ProviderRepositoryId,
            providerXml.ScanRun.Id,
            DocumentEdgeType.References,
            ExactCrossRepositoryTestValues.Pass1CompletedAt));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"LogicalDocuments\" SET \"CurrentVersionId\" = {documentVersionId} WHERE \"Id\" = {logicalDocumentId}");
        context.ChangeTracker.Clear();
        return new(
            providerXml.ScanRun,
            providerXml.ScanExecution,
            codeUnit,
            logical,
            document,
            Assert.Single(document.SourceAnchors));
    }

    private static void BindProductionDocumentIdentity(ExactPass1DocumentFixture document)
    {
        Guid derived = OrdinaryPass1JobIdentity.CreateDocumentVersionId(
            OrdinaryPass1JobIdentity.Create(
                document.ScanExecution.Id,
                document.ScanRun.Id,
                document.DocumentVersion.RepositoryId,
                document.CodeUnit.Id,
                document.DocumentVersion.ProfiledRevision).JobId);
        ExactCrossRepositoryReflection.SetId(document.DocumentVersion, derived);
        Set(document.SourceAnchor, nameof(SourceAnchor.DocumentVersionId), derived);
    }

    private static async Task BindApprovedSourceIdentityAsync(
        string sourceRoot,
        ExactPass1DocumentFixture document)
    {
        byte[] bytes = await File.ReadAllBytesAsync(
            FullPath(sourceRoot, document.DocumentVersion.RepositoryRelativePath!));
        string sourceHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Set(document.CodeUnit, nameof(CodeUnit.FileIdentityHash), sourceHash);
        Set(document.CodeUnit, nameof(CodeUnit.SourceHash), sourceHash);
        Set(document.SourceAnchor, nameof(SourceAnchor.SourceHash), sourceHash);
        if (document.DocumentVersion.RepositoryRelativePath!.EndsWith(
                ".xml", StringComparison.OrdinalIgnoreCase))
        {
            string normalized = _strictUtf8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal);
            int lineCount = normalized.Split('\n').Length;
            lineCount = Math.Max(1, lineCount);
            Set(document.CodeUnit, nameof(CodeUnit.LineStart), 1);
            Set(document.CodeUnit, nameof(CodeUnit.LineEnd), lineCount);
            Set(document.SourceAnchor, nameof(SourceAnchor.LineStart), 1);
            Set(document.SourceAnchor, nameof(SourceAnchor.LineEnd), lineCount);
        }
        (long byteStart, long byteEnd) = ExactLineByteRange(
            bytes,
            document.SourceAnchor.LineStart,
            document.SourceAnchor.LineEnd);
        Set(document.SourceAnchor, nameof(SourceAnchor.ByteStart), (long?)byteStart);
        Set(document.SourceAnchor, nameof(SourceAnchor.ByteEnd), (long?)byteEnd);
    }

    private static (long Start, long End) ExactLineByteRange(
        byte[] bytes,
        int lineStart,
        int lineEnd)
    {
        var starts = new List<int> { 0 };
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\n')
                starts.Add(index + 1);
        }
        if (lineStart < 1 || lineEnd < lineStart || lineEnd > starts.Count)
            throw new InvalidDataException("The approved Source Anchor line range is outside its file.");
        int start = starts[lineStart - 1];
        int end = lineEnd < starts.Count ? starts[lineEnd] : bytes.Length;
        if (end <= start)
            throw new InvalidDataException("The approved Source Anchor byte range is empty.");
        return (start, end);
    }

    private static async Task AddOrdinaryMemberAuthoritiesAsync(
        VulgataDbContext context,
        params ExactPass1DocumentFixture[] documents)
    {
        Pass1PairExecution pair = await context.Pass1PairExecutions.SingleAsync(value =>
            value.Id == ExactCrossRepositoryTestValues.Id(12_001));
        TwoRepositoryScanCoordination coordination = await context.TwoRepositoryScanCoordinations
            .SingleAsync(value => value.Id == pair.CoordinationId);
        var manifestOrdinals = new Dictionary<Guid, int>();
        int pairSelectionOrdinal = 0;
        foreach (ExactPass1DocumentFixture document in documents)
        {
            int manifestOrdinal = manifestOrdinals.TryGetValue(
                document.DocumentVersion.RepositoryId, out int prior)
                ? prior + 1
                : 1;
            manifestOrdinals[document.DocumentVersion.RepositoryId] = manifestOrdinal;
            PreparedScanManifest manifest = await context.PreparedScanManifests.SingleAsync(value =>
                value.CoordinationId == coordination.Id &&
                value.RepositoryId == document.DocumentVersion.RepositoryId);
            var entry = (PreparedScanManifestEntry)Activator.CreateInstance(
                typeof(PreparedScanManifestEntry),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: null,
                culture: null)!;
            Set(entry, nameof(PreparedScanManifestEntry.FileCodeUnitId), document.CodeUnit.Id);
            Set(entry, nameof(PreparedScanManifestEntry.PreparedScanManifestId), manifest.Id);
            Set(entry, nameof(PreparedScanManifestEntry.CoordinationId), coordination.Id);
            Set(entry, nameof(PreparedScanManifestEntry.FileCodeUnitIdentityFingerprint),
                document.CodeUnit.SourceHash);
            Set(entry, nameof(PreparedScanManifestEntry.DirectBusinessScore), 500);
            Set(entry, nameof(PreparedScanManifestEntry.FinalBusinessScore), 500);
            Set(entry, nameof(PreparedScanManifestEntry.SelectionReason),
                PreparedSelectionReason.DirectBusinessEvidence);
            Set(entry, nameof(PreparedScanManifestEntry.ManifestOrdinal), manifestOrdinal);
            Set(entry, nameof(PreparedScanManifestEntry.PairSelectionOrdinal), ++pairSelectionOrdinal);
            context.PreparedScanManifestEntries.Add(entry);

            ScanJobExecution scanJob = ScanJobExecution.Create(
                document.ScanExecution.Id,
                document.DocumentVersion.RepositoryId,
                document.ScanRun.Id,
                document.CodeUnit.Id,
                manifestOrdinal,
                ExactCrossRepositoryTestValues.CreatedAt);
            Guid lease = Guid.NewGuid();
            scanJob.AcquireLease(
                lease,
                ExactCrossRepositoryTestValues.Pass1CompletedAt.AddMinutes(5),
                ExactCrossRepositoryTestValues.CreatedAt.AddSeconds(20));
            scanJob.Complete(
                scanJob.StateVersion,
                lease,
                document.DocumentVersion.Id,
                ExactCrossRepositoryTestValues.Pass1CompletedAt);
            context.ScanJobExecutions.Add(scanJob);
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task CopyApprovedSourceAsync(
        string approvedRoot,
        string relativePath,
        string fixtureRoot)
    {
        byte[] bytes = await File.ReadAllBytesAsync(FullPath(approvedRoot, relativePath));
        string target = FullPath(fixtureRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllBytesAsync(target, bytes);
    }

    private static void RequireApprovedRevision(string root, string expected)
    {
        string head = RunGit(root, "rev-parse", "HEAD").Trim();
        if (!StringComparer.Ordinal.Equals(head, expected))
            throw new Xunit.Sdk.XunitException(
                $"Approved source root is not at the exact profile revision; root_hash={Hash(root)}.");
    }

    private static string RunGit(string root, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new Xunit.Sdk.XunitException("Unable to inspect the approved repository revision.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Xunit.Sdk.XunitException(
                $"Unable to inspect the approved repository revision; diagnostic_hash={Hash(error)}.");
        return output;
    }

    private static string Commit(ExactResolutionProfile profile, string alias) =>
        profile.Repositories.Single(value => StringComparer.Ordinal.Equals(value.Alias, alias)).ExpectedCommit;

    private static string FullPath(string root, string relativePath)
    {
        string canonicalRoot = Path.GetFullPath(root);
        string candidate = Path.GetFullPath(Path.Combine(
            canonicalRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string boundary = canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? canonicalRoot
            : canonicalRoot + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
            throw new Xunit.Sdk.XunitException(
                $"Exact qualification source is unavailable; path_hash={Hash(relativePath)}.");
        return candidate;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Required(string variable) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable))
            ? Environment.GetEnvironmentVariable(variable)!
            : throw new Xunit.Sdk.XunitException($"T048 requires {variable}.");

    private static void Set<T>(object target, string propertyName, T value) =>
        target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
}

internal sealed class Story124QualificationSourceReader(
    IReadOnlyList<T062RepositoryQualification> repositories) : IRevisionBoundSourceReader
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private readonly IReadOnlyDictionary<Guid, T062RepositoryQualification> _repositories =
        repositories.ToDictionary(value => value.RepositoryId);

    public async Task<SourceExcerptResult> ReadAsync(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        CancellationToken cancellationToken)
    {
        if (!_repositories.TryGetValue(scope.RepositoryId, out T062RepositoryQualification? repository) ||
            !StringComparer.Ordinal.Equals(scope.ProfiledRevision, repository.ProfiledRevision))
            return Failed(scope, request, AnalysisPortFailureReason.Rejected,
                "source_binding_refused");
        string root = Path.GetFullPath(repository.SourceRoot);
        string path = Path.GetFullPath(Path.Combine(
            root, request.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string boundary = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            return Failed(scope, request, AnalysisPortFailureReason.Unavailable, "source_not_found");
        byte[] bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(hash, request.ExpectedSourceSha256))
            return Failed(scope, request, AnalysisPortFailureReason.IntegrityMismatch,
                "source_integrity_mismatch");

        string excerpt;
        if (request.ByteStart is long byteStart &&
            request.ByteEndExclusive is long byteEndExclusive)
        {
            if (byteEndExclusive > bytes.LongLength)
                return Failed(scope, request, AnalysisPortFailureReason.Rejected,
                    "source_range_rejected");
            excerpt = _strictUtf8.GetString(bytes.AsSpan(
                checked((int)byteStart),
                checked((int)(byteEndExclusive - byteStart))));
        }
        else
        {
            string source = _strictUtf8.GetString(bytes);
            string[] lines = source.Split('\n');
            if (request.Range.EndLine > lines.Length)
                return Failed(scope, request, AnalysisPortFailureReason.Rejected,
                    "source_range_rejected");
            string[] selected = lines[(request.Range.StartLine - 1)..request.Range.EndLine];
            if (selected.Length > 0 && selected[^1].EndsWith('\r'))
                selected[^1] = selected[^1][..^1];
            excerpt = string.Join('\n', selected);
        }

        int byteCount = _strictUtf8.GetByteCount(excerpt);
        if (string.IsNullOrWhiteSpace(excerpt) || byteCount > request.ByteLimit)
            return Failed(scope, request, AnalysisPortFailureReason.Rejected,
                "source_range_rejected");
        return new SourceExcerptResult(
            AnalysisOperationStatus.Succeeded,
            scope.RepositoryId,
            scope.ProfiledRevision,
            request.RelativePath,
            request.Range,
            hash,
            excerpt,
            byteCount,
            null);
    }

    private static SourceExcerptResult Failed(
        AnalysisScope scope,
        RevisionBoundSourceRequest request,
        AnalysisPortFailureReason reason,
        string code) => new(
            AnalysisOperationStatus.Failed,
            scope.RepositoryId,
            scope.ProfiledRevision,
            request.RelativePath,
            request.Range,
            request.ExpectedSourceSha256,
            string.Empty,
            0,
            new AnalysisPortFailure(reason, code, code));
}
