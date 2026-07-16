using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulgata.Tests.CodeGraphQualification;

/// <summary>
/// Writes the qualification decision from finite observer facts only. This is a
/// test-only gate; it is not a provider-result parser or production contract.
/// </summary>
internal static class QualificationResultWriter
{
    private const string _qualifiedImageReference = "vulgata-codegraph-qualification:0.5.1";
    private const string _qualifiedExecutionUser = "app";
    private const string _observerGrammar = "vulgata-cgc-streaming-evidence-v2";
    private const string _queryGrammar = "vulgata-cgc-http-json-v1";
    private const string _structuralGrammar = "vulgata-cgc-http-json-v1";
    private const string _grammarId = "vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2";
    private static readonly IReadOnlyList<string> _qualifiedCommandNames = Array.AsReadOnly(
    [
        "cgc-version",
        "cgc-doctor",
        "cgc-context-create",
        "cgc-context-mode",
        "cgc-context-default",
        "cgc-index",
        "cgc-api-start",
        "cgc-api-health",
        "cgc-api-query",
        "gateway-stop",
        "cgc-context-delete",
    ]);
    private static readonly IReadOnlyList<string> _requiredRelationshipKinds = Array.AsReadOnly(
    [
        "CALLS",
        "CONTAINS",
        "IMPORTS",
        "INHERITS",
    ]);
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal static async Task WriteQualifiedAsync(
        string outputPath,
        QualifiedResultInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(input);

        ValidateQualifiedInput(input);
        StructuralEvidence structuralEvidence = CreateStructuralEvidence(input.StructuralEvidence);
        QualificationResult result = new(
            SchemaVersion: "1.2",
            Status: "pass",
            Category: "qualified",
            NextAction: "return-to-plan",
            Image: new ImageIdentity(_qualifiedImageReference, input.ImageDigest),
            Python: new PythonIdentity("docker/codegraph/python-version.txt", input.PythonVersion),
            Packages: new PackageIdentity("docker/codegraph/requirements.lock", input.RequirementsLockSha256, "0.5.1"),
            ExecutionIdentity: new ExecutionIdentity(_qualifiedExecutionUser, true),
            FixtureManifestSha256: input.FixtureManifestSha256,
            Commands: _qualifiedCommandNames
                .Select(name => input.Commands.Single(command => command.Name == name))
                .Select(command => new CommandEvidence(command.Name, command.ExitCode, command.StdoutSha256, command.StderrSha256))
                .ToArray(),
            Evidence: new EvidenceIdentity(true, true, input.EvidencePaths.ToArray()),
            StructuralEvidence: structuralEvidence,
            ElapsedMilliseconds: input.ElapsedMilliseconds,
            Cleanup: new CleanupIdentity(true, true));

        await WriteAsync(outputPath, result, cancellationToken);
    }

    internal static async Task WriteDockerDaemonUnavailableAsync(
        string outputPath,
        string fixtureManifestPath,
        string standardOutput,
        string standardError,
        int exitCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureManifestPath);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        byte[] fixtureManifest = await File.ReadAllBytesAsync(fixtureManifestPath, cancellationToken);
        HistoricalQualificationResult result = new(
            SchemaVersion: "1.0",
            Status: "fail",
            Category: "docker-daemon-unavailable",
            NextAction: "corrective-planning",
            Image: new ImageIdentity("vulgata-codegraph-qualification", null),
            Python: new PythonIdentity("docker/codegraph/python-version.txt", null),
            Packages: new PackageIdentity("docker/codegraph/requirements.lock", null, "0.5.1"),
            ExecutionIdentity: new ExecutionIdentity(null, false),
            FixtureManifestSha256: Hash(fixtureManifest),
            Commands:
            [
                new CommandEvidence(
                    "docker-version",
                    exitCode,
                    Hash(Encoding.UTF8.GetBytes(standardOutput)),
                    Hash(Encoding.UTF8.GetBytes(standardError))),
            ],
            Evidence: new EvidenceIdentity(true, true, ["evidence/red.md"]),
            ElapsedMilliseconds: 0,
            Cleanup: new CleanupIdentity(false, true));

        await WriteAsync(outputPath, result, cancellationToken);
    }

    internal static async Task WriteCorrectiveAsync(
        string outputPath,
        UnqualifiedResultInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(input);
        ValidateUnqualifiedInput(input);

        CgcStreamingEvidence structuralEvidence = input.StructuralEvidence;
        string category = structuralEvidence.CleanupSucceeded ? input.RequestedCategory : "cleanup-failure";
        QualificationResult result = new(
            SchemaVersion: "1.2",
            Status: "fail",
            Category: category,
            NextAction: "corrective-planning",
            Image: new ImageIdentity(input.ImageReference, input.ImageDigest),
            Python: new PythonIdentity("docker/codegraph/python-version.txt", input.PythonVersion),
            Packages: new PackageIdentity("docker/codegraph/requirements.lock", input.RequirementsLockSha256, "0.5.1"),
            ExecutionIdentity: new ExecutionIdentity(input.ExecutionUser, input.RanAsNonRoot),
            FixtureManifestSha256: input.FixtureManifestSha256,
            Commands: input.Commands
                .Where(command => _qualifiedCommandNames.Contains(command.Name, StringComparer.Ordinal))
                .Take(_qualifiedCommandNames.Count)
                .Select(command => new CommandEvidence(command.Name, command.ExitCode, command.StdoutSha256, command.StderrSha256))
                .ToArray(),
            Evidence: new EvidenceIdentity(true, true, input.EvidencePaths.ToArray()),
            StructuralEvidence: CreateCorrectiveStructuralEvidence(structuralEvidence),
            ElapsedMilliseconds: input.ElapsedMilliseconds,
            Cleanup: new CleanupIdentity(structuralEvidence.CleanupAttempted, structuralEvidence.CleanupSucceeded));

        await WriteAsync(outputPath, result, cancellationToken);
    }

    private static async Task WriteAsync<T>(string outputPath, T result, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(result, _serializerOptions) + Environment.NewLine,
            Encoding.UTF8,
            cancellationToken);
    }

    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static void ValidateQualifiedInput(QualifiedResultInput input)
    {
        if (!string.Equals(input.ImageReference, _qualifiedImageReference, StringComparison.Ordinal))
        {
            throw new ArgumentException("A qualified result requires the exact qualified image reference.", nameof(input));
        }

        if (!IsSha256Digest(input.ImageDigest))
        {
            throw new ArgumentException("A qualified result requires an image SHA-256 digest.", nameof(input));
        }

        if (!IsPython312Version(input.PythonVersion))
        {
            throw new ArgumentException("A qualified result requires a CPython 3.12 version.", nameof(input));
        }

        if (!IsSha256(input.RequirementsLockSha256) || !IsSha256(input.FixtureManifestSha256))
        {
            throw new ArgumentException("A qualified result requires lower-case SHA-256 evidence identities.", nameof(input));
        }

        if (!string.Equals(input.ExecutionUser, _qualifiedExecutionUser, StringComparison.Ordinal))
        {
            throw new ArgumentException("A qualified result requires the non-root app user.", nameof(input));
        }

        ValidateLifecycle(input.Commands);
        ValidateEvidencePaths(input.EvidencePaths);

        if (input.ElapsedMilliseconds is < 0 or > 1_800_000)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Qualification duration must be within the 1,800,000-millisecond cap.");
        }

        ValidateStructuralEvidence(input.StructuralEvidence);
        if (!input.CleanupSucceeded || !input.StructuralEvidence.CleanupSucceeded)
        {
            throw new ArgumentException("A qualified result requires successful private-context cleanup.", nameof(input));
        }
    }

    private static void ValidateLifecycle(IReadOnlyList<QualificationCommandEvidence>? commands)
    {
        if (commands is null || commands.Count != _qualifiedCommandNames.Count)
        {
            throw new ArgumentException("A qualified result requires evidence for every approved cgc command.", nameof(commands));
        }

        if (commands.Select(command => command.Name).Distinct(StringComparer.Ordinal).Count() != _qualifiedCommandNames.Count ||
            !_qualifiedCommandNames.All(expected => commands.Any(command => command.Name == expected)))
        {
            throw new ArgumentException("Qualified command evidence must contain every approved command exactly once.", nameof(commands));
        }

        foreach (QualificationCommandEvidence command in commands)
        {
            if (!_qualifiedCommandNames.Contains(command.Name, StringComparer.Ordinal) ||
                command.ExitCode != 0 ||
                !IsSha256(command.StdoutSha256) ||
                !IsSha256(command.StderrSha256))
            {
                throw new ArgumentException("Qualified command evidence must be approved, successful, and hash-only.", nameof(commands));
            }
        }
    }

    private static void ValidateEvidencePaths(IReadOnlyList<string>? evidencePaths)
    {
        if (evidencePaths is null ||
            evidencePaths.Count is 0 or > 8 ||
            evidencePaths.Any(path => !IsSafeRelativeEvidencePath(path)))
        {
            throw new ArgumentException("Qualified result evidence must be bounded, redacted, and relative.", nameof(evidencePaths));
        }
    }

    private static void ValidateUnqualifiedInput(UnqualifiedResultInput input)
    {
        if (string.IsNullOrWhiteSpace(input.ImageReference) || input.ImageReference.Length > 256 ||
            (input.ImageDigest is not null && !IsSha256Digest(input.ImageDigest)) ||
            (input.PythonVersion is not null && !IsPython312Version(input.PythonVersion)) ||
            (input.RequirementsLockSha256 is not null && !IsSha256(input.RequirementsLockSha256)) ||
            !IsSha256(input.FixtureManifestSha256) ||
            input.ElapsedMilliseconds is < 0 or > 1_800_000 ||
            input.StructuralEvidence is null)
        {
            throw new ArgumentException("A corrective result requires bounded qualification identities and observer facts.", nameof(input));
        }

        if (input.RequestedCategory is "qualified" or "cleanup-failure" ||
            input.RequestedCategory is not ("docker-daemon-unavailable" or "image-build-failure" or "provider-contract-failure" or "timeout" or "unsafe-evidence"))
        {
            throw new ArgumentException("A corrective result requires an approved non-qualified category.", nameof(input));
        }

        ValidateEvidencePaths(input.EvidencePaths);
    }

    private static void ValidateStructuralEvidence(CgcStreamingEvidence? evidence)
    {
        if (evidence is null ||
            !evidence.IsValidUtf8 ||
            evidence.Overflowed ||
            evidence.TerminationRequested ||
            evidence.ContainerCleanupForced ||
            !evidence.CleanupAttempted ||
            !evidence.CleanupSucceeded ||
            evidence.CommandEvidenceOverflowed)
        {
            throw new ArgumentException("A qualified result requires an unforced, safe, completed streaming observation.", nameof(evidence));
        }

        if (evidence.ObservedTotalBytes is < 0 or > CgcStreamingEvidenceObserver.MaximumObservedBytes ||
            evidence.StandardOutput.Bytes < 0 ||
            evidence.StandardError.Bytes < 0 ||
            evidence.StandardOutput.Bytes + evidence.StandardError.Bytes != evidence.ObservedTotalBytes ||
            !IsSha256(evidence.StandardOutput.Sha256) ||
            !IsSha256(evidence.StandardError.Sha256))
        {
            throw new ArgumentException("Streaming evidence must conserve bounded per-stream byte identities.", nameof(evidence));
        }

        if (evidence.StdoutBeginCount != 1 ||
            evidence.StdoutEndCount != 1 ||
            !evidence.HasExactlyOneOrderedStdoutMarkerPair ||
            !evidence.HasValidRecordGrammar ||
            !evidence.HasValidStructuralRecord ||
            evidence.ValidRecordCount is < 1 or > 256 ||
            evidence.InvalidOrOversizedRecordCount != 0 ||
            !evidence.HasAllRequiredRelationshipKinds ||
            !evidence.HasFilteredTargetCandidate ||
            !evidence.HasPositiveOrderedRange ||
            !evidence.HasNonEmptyEndpointIdentity ||
            !evidence.AllStructuralFactsCorrelatedWithinValidRecords)
        {
            throw new ArgumentException("A qualified result requires all stdout-only structural predicates.", nameof(evidence));
        }

        if (evidence.RelationshipKindCounts.Count != _requiredRelationshipKinds.Count ||
            _requiredRelationshipKinds.Any(kind => !evidence.RelationshipKindCounts.TryGetValue(kind, out int count) || count is < 1 or > 256))
        {
            throw new ArgumentException("Structural evidence may contain only the approved relationship kinds.", nameof(evidence));
        }

        string expectedSummary = CreateCanonicalSummarySha256(evidence);
        if (!string.Equals(evidence.CanonicalSummarySha256, expectedSummary, StringComparison.Ordinal))
        {
            throw new ArgumentException("The streaming canonical summary does not match the fixed qualification serialization.", nameof(evidence));
        }
    }

    private static StructuralEvidence CreateStructuralEvidence(CgcStreamingEvidence evidence) =>
        new(
            RawOutputRetained: false,
            GrammarId: _grammarId,
            StreamingGrammar: new StreamingGrammar(_observerGrammar, _queryGrammar, _structuralGrammar, true, CgcStreamingEvidenceObserver.MaximumStructuralRecordBytes),
            OutputCap: new OutputCap(
                CgcStreamingEvidenceObserver.MaximumObservedBytes,
                evidence.ObservedTotalBytes,
                evidence.Overflowed,
                evidence.TerminationRequested,
                evidence.ContainerCleanupForced),
            Streams: new Streams(
                new StreamEvidence(evidence.StandardOutput.Bytes, evidence.StandardOutput.Sha256),
                new StreamEvidence(evidence.StandardError.Bytes, evidence.StandardError.Sha256)),
            CanonicalSummarySha256: evidence.CanonicalSummarySha256,
            Markers: new MarkerEvidence(
                evidence.StdoutBeginCount,
                evidence.StdoutEndCount,
                evidence.HasExactlyOneOrderedStdoutMarkerPair,
                true),
            RecordCounts: new RecordCounts(evidence.ValidRecordCount, evidence.InvalidOrOversizedRecordCount),
            RelationshipKinds: _requiredRelationshipKinds
                .Where(kind => evidence.RelationshipKindCounts[kind] > 0)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray(),
            RelationshipKindCounts: new RelationshipKindCounts(
                evidence.RelationshipKindCounts["CALLS"],
                evidence.RelationshipKindCounts["IMPORTS"],
                evidence.RelationshipKindCounts["INHERITS"],
                evidence.RelationshipKindCounts["CONTAINS"]),
            PredicateFacts: new PredicateFacts(
                evidence.HasExactlyOneOrderedStdoutMarkerPair,
                evidence.HasValidRecordGrammar,
                evidence.HasAllRequiredRelationshipKinds,
                evidence.HasFilteredTargetCandidate,
                evidence.HasPositiveOrderedRange,
                evidence.HasNonEmptyEndpointIdentity,
                evidence.AllStructuralFactsCorrelatedWithinValidRecords));

    private static StructuralEvidence CreateCorrectiveStructuralEvidence(CgcStreamingEvidence evidence) =>
        new(
            RawOutputRetained: false,
            GrammarId: _grammarId,
            StreamingGrammar: new StreamingGrammar(_observerGrammar, _queryGrammar, _structuralGrammar, true, CgcStreamingEvidenceObserver.MaximumStructuralRecordBytes),
            OutputCap: new OutputCap(
                CgcStreamingEvidenceObserver.MaximumObservedBytes,
                Math.Clamp(evidence.ObservedTotalBytes, 0, CgcStreamingEvidenceObserver.OverflowSentinelBytes),
                evidence.Overflowed,
                evidence.TerminationRequested,
                evidence.ContainerCleanupForced),
            Streams: new Streams(
                new StreamEvidence(Math.Clamp(evidence.StandardOutput.Bytes, 0, CgcStreamingEvidenceObserver.OverflowSentinelBytes), evidence.StandardOutput.Sha256),
                new StreamEvidence(Math.Clamp(evidence.StandardError.Bytes, 0, CgcStreamingEvidenceObserver.OverflowSentinelBytes), evidence.StandardError.Sha256)),
            CanonicalSummarySha256: evidence.CanonicalSummarySha256,
            Markers: new MarkerEvidence(
                Math.Clamp(evidence.StdoutBeginCount, 0, 1),
                Math.Clamp(evidence.StdoutEndCount, 0, 1),
                evidence.HasExactlyOneOrderedStdoutMarkerPair,
                true),
            RecordCounts: new RecordCounts(
                Math.Clamp(evidence.ValidRecordCount, 0, 256),
                Math.Clamp(evidence.InvalidOrOversizedRecordCount, 0, 256)),
            RelationshipKinds: _requiredRelationshipKinds
                .Where(kind => evidence.RelationshipKindCounts.TryGetValue(kind, out int count) && count > 0)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray(),
            RelationshipKindCounts: new RelationshipKindCounts(
                GetBoundedRelationshipCount(evidence, "CALLS"),
                GetBoundedRelationshipCount(evidence, "IMPORTS"),
                GetBoundedRelationshipCount(evidence, "INHERITS"),
                GetBoundedRelationshipCount(evidence, "CONTAINS")),
            PredicateFacts: new PredicateFacts(
                evidence.HasExactlyOneOrderedStdoutMarkerPair,
                evidence.HasValidRecordGrammar,
                evidence.HasAllRequiredRelationshipKinds,
                evidence.HasFilteredTargetCandidate,
                evidence.HasPositiveOrderedRange,
                evidence.HasNonEmptyEndpointIdentity,
                evidence.AllStructuralFactsCorrelatedWithinValidRecords));

    private static int GetBoundedRelationshipCount(CgcStreamingEvidence evidence, string kind) =>
        evidence.RelationshipKindCounts.TryGetValue(kind, out int count) ? Math.Clamp(count, 0, 256) : 0;

    private static string CreateCanonicalSummarySha256(CgcStreamingEvidence evidence)
    {
        string relationshipKinds = string.Join(
            ',',
            _requiredRelationshipKinds
                .Where(kind => evidence.RelationshipKindCounts[kind] > 0)
                .OrderBy(kind => kind, StringComparer.Ordinal));
        string predicateCounts = string.Join(
            ',',
            $"valid={evidence.ValidRecordCount.ToString(CultureInfo.InvariantCulture)}",
            $"invalidOrOversized={evidence.InvalidOrOversizedRecordCount.ToString(CultureInfo.InvariantCulture)}",
            $"CALLS={evidence.RelationshipKindCounts["CALLS"].ToString(CultureInfo.InvariantCulture)}",
            $"CONTAINS={evidence.RelationshipKindCounts["CONTAINS"].ToString(CultureInfo.InvariantCulture)}",
            $"IMPORTS={evidence.RelationshipKindCounts["IMPORTS"].ToString(CultureInfo.InvariantCulture)}",
            $"INHERITS={evidence.RelationshipKindCounts["INHERITS"].ToString(CultureInfo.InvariantCulture)}",
            $"allRequiredRelationshipKindsObserved={ToLowerBoolean(evidence.HasAllRequiredRelationshipKinds)}",
            $"allStructuralFactsCorrelatedWithinValidRecords={ToLowerBoolean(evidence.AllStructuralFactsCorrelatedWithinValidRecords)}",
            $"hasFilteredTargetCandidate={ToLowerBoolean(evidence.HasFilteredTargetCandidate)}",
            $"hasNonEmptyEndpointIdentity={ToLowerBoolean(evidence.HasNonEmptyEndpointIdentity)}",
            $"hasPositiveOrderedRange={ToLowerBoolean(evidence.HasPositiveOrderedRange)}",
            $"hasValidRecordGrammar={ToLowerBoolean(evidence.HasValidRecordGrammar)}",
            $"queryCompleted={ToLowerBoolean(evidence.HasExactlyOneOrderedStdoutMarkerPair)}");
        string summary = string.Join(
            '|',
            "1.2",
            evidence.StandardOutput.Bytes.ToString(CultureInfo.InvariantCulture),
            evidence.StandardError.Bytes.ToString(CultureInfo.InvariantCulture),
            evidence.ObservedTotalBytes.ToString(CultureInfo.InvariantCulture),
            ToLowerBoolean(evidence.Overflowed),
            ToLowerBoolean(evidence.TerminationRequested),
            ToLowerBoolean(evidence.ContainerCleanupForced),
            evidence.StandardOutput.Sha256,
            evidence.StandardError.Sha256,
            _grammarId,
            relationshipKinds,
            predicateCounts,
            ToLowerBoolean(evidence.CleanupAttempted),
            ToLowerBoolean(evidence.CleanupSucceeded));
        return Hash(Encoding.UTF8.GetBytes(summary));
    }

    private static string ToLowerBoolean(bool value) => value ? "true" : "false";

    private static bool IsSha256Digest(string? value) =>
        value is not null &&
        value.StartsWith("sha256:", StringComparison.Ordinal) &&
        IsSha256(value["sha256:".Length..]);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' || character is >= 'a' and <= 'f');

    private static bool IsPython312Version(string? value)
    {
        if (value is null || !value.StartsWith("3.12.", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(value["3.12.".Length..], out int patch) && patch >= 0;
    }

    private static bool IsSafeRelativeEvidencePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 256 || !path.StartsWith("evidence/", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.Contains('\\') &&
            !path.Contains(':') &&
            path.Split('/').All(segment =>
                segment.Length > 0 &&
                segment is not "." and not ".." &&
                segment.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'));
    }

    private sealed record QualificationResult(
        string SchemaVersion,
        string Status,
        string Category,
        string NextAction,
        ImageIdentity Image,
        PythonIdentity Python,
        PackageIdentity Packages,
        ExecutionIdentity ExecutionIdentity,
        string FixtureManifestSha256,
        IReadOnlyList<CommandEvidence> Commands,
        EvidenceIdentity Evidence,
        StructuralEvidence StructuralEvidence,
        int ElapsedMilliseconds,
        CleanupIdentity Cleanup);

    private sealed record HistoricalQualificationResult(
        string SchemaVersion,
        string Status,
        string Category,
        string NextAction,
        ImageIdentity Image,
        PythonIdentity Python,
        PackageIdentity Packages,
        ExecutionIdentity ExecutionIdentity,
        string FixtureManifestSha256,
        IReadOnlyList<CommandEvidence> Commands,
        EvidenceIdentity Evidence,
        int ElapsedMilliseconds,
        CleanupIdentity Cleanup);

    private sealed record ImageIdentity(string Reference, string? Digest);

    private sealed record PythonIdentity(string LockPath, string? Version);

    private sealed record PackageIdentity(string LockPath, string? LockSha256, string CodegraphcontextVersion);

    private sealed record ExecutionIdentity(string? User, bool NonRoot);

    private sealed record CommandEvidence(string Name, int? ExitCode, string? StdoutSha256, string? StderrSha256);

    private sealed record EvidenceIdentity(bool Bounded, bool Redacted, IReadOnlyList<string> Paths);

    private sealed record StructuralEvidence(
        bool RawOutputRetained,
        string GrammarId,
        StreamingGrammar StreamingGrammar,
        OutputCap OutputCap,
        Streams Streams,
        string CanonicalSummarySha256,
        MarkerEvidence Markers,
        RecordCounts RecordCounts,
        IReadOnlyList<string> RelationshipKinds,
        RelationshipKindCounts RelationshipKindCounts,
        PredicateFacts PredicateFacts);

    private sealed record StreamingGrammar(string Observer, string QueryStdoutRegion, string StructuralRecord, bool StrictIncrementalUtf8, int MaximumRecordBytes);

    private sealed record OutputCap(int LimitBytes, long ObservedTotalBytes, bool Overflowed, bool TerminationRequested, bool ContainerCleanupForced);

    private sealed record Streams(StreamEvidence Stdout, StreamEvidence Stderr);

    private sealed record StreamEvidence(long Bytes, string Sha256);

    private sealed record MarkerEvidence(int StdoutBeginCount, int StdoutEndCount, bool StdoutOrderedPair, bool StderrExcludedFromStructuralEvidence);

    private sealed record RecordCounts(int Valid, int InvalidOrOversized);

    private sealed record RelationshipKindCounts(
        [property: JsonPropertyName("CALLS")] int CALLS,
        [property: JsonPropertyName("IMPORTS")] int IMPORTS,
        [property: JsonPropertyName("INHERITS")] int INHERITS,
        [property: JsonPropertyName("CONTAINS")] int CONTAINS);

    private sealed record PredicateFacts(
        bool QueryCompleted,
        bool HasValidRecordGrammar,
        bool AllRequiredRelationshipKindsObserved,
        bool HasFilteredTargetCandidate,
        bool HasPositiveOrderedRange,
        bool HasNonEmptyEndpointIdentity,
        bool AllStructuralFactsCorrelatedWithinValidRecords);

    private sealed record CleanupIdentity(bool Attempted, bool Succeeded);
}

internal sealed record QualifiedResultInput(
    string ImageReference,
    string ImageDigest,
    string PythonVersion,
    string RequirementsLockSha256,
    string FixtureManifestSha256,
    string ExecutionUser,
    IReadOnlyList<QualificationCommandEvidence> Commands,
    IReadOnlyList<string> EvidencePaths,
    int ElapsedMilliseconds,
    bool CleanupSucceeded,
    CgcStreamingEvidence StructuralEvidence);

internal sealed record UnqualifiedResultInput(
    string ImageReference,
    string? ImageDigest,
    string? PythonVersion,
    string? RequirementsLockSha256,
    string FixtureManifestSha256,
    string? ExecutionUser,
    bool RanAsNonRoot,
    IReadOnlyList<QualificationCommandEvidence> Commands,
    IReadOnlyList<string> EvidencePaths,
    int ElapsedMilliseconds,
    string RequestedCategory,
    CgcStreamingEvidence StructuralEvidence);

internal sealed record QualificationCommandEvidence(
    string Name,
    int ExitCode,
    string StdoutSha256,
    string StderrSha256);
