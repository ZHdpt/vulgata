using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class CgcStreamingEvidenceObserverTests
{
    [Fact]
    public void Complete_WithChunkedHttpJson_ProducesOnlySchema12StructuralPredicates()
    {
        using CgcStreamingEvidenceObserver observer = new();
        byte[] health = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
        byte[] payload = Encoding.UTF8.GetBytes(CreateHttpResponse(
            CreateValidRecord("CALLS"),
            CreateValidRecord("CONTAINS"),
            CreateValidRecord("IMPORTS", sourceStart: 5, sourceEnd: 5, targetStart: 7, targetEnd: 7),
            CreateValidRecord("INHERITS"),
            CreateValidRecord(
                "CALLS",
                targetPath: "src/Filtered/GeneratedClient.g.cs",
                targetStart: null,
                targetEnd: null,
                filteredTarget: true)));

        observer.Observe(CgcOutputStream.HttpHealthResponse, health.AsSpan(0, 3));
        observer.Observe(CgcOutputStream.HttpHealthResponse, health.AsSpan(3));
        for (int offset = 0; offset < payload.Length; offset += 17)
        {
            observer.Observe(CgcOutputStream.HttpQueryResponse, payload.AsSpan(offset, Math.Min(17, payload.Length - offset)));
        }

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.True(evidence.IsValidUtf8);
        Assert.True(evidence.HasExactlyOneOrderedStdoutMarkerPair, observer.HttpDiagnostic);
        Assert.True(evidence.HasValidStructuralRecord);
        Assert.True(evidence.HasFilteredTargetCandidate);
        Assert.True(evidence.HasPositiveOrderedRange);
        Assert.True(evidence.HasNonEmptyEndpointIdentity);
        Assert.True(evidence.HasAllRequiredRelationshipKinds);
        Assert.Equal(2, evidence.RelationshipKindCounts["CALLS"]);
        Assert.Equal(1, evidence.RelationshipKindCounts["CONTAINS"]);
        Assert.Equal(1, evidence.RelationshipKindCounts["IMPORTS"]);
        Assert.Equal(1, evidence.RelationshipKindCounts["INHERITS"]);
        Assert.Equal(payload.Length, evidence.HttpResponse.Bytes);
        Assert.Equal(health.Length, evidence.HealthResponse.Bytes);
        Assert.Equal(payload.Length + health.Length, evidence.ObservedTotalBytes);
        Assert.Equal(Sha256(payload), evidence.HttpResponse.Sha256);
        Assert.Equal(Sha256(health), evidence.HealthResponse.Sha256);
        Assert.False(evidence.Overflowed);

        string projection = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain("src/Caller.cs", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("Caller", projection, StringComparison.Ordinal);
        Assert.DoesNotContain(CgcQualificationRunner.StaticStructuralQuery, projection, StringComparison.Ordinal);
    }

    [Fact]
    public void Complete_AtInclusiveSharedByteCap_AccountsEveryLogicalChannelByteAndHash()
    {
        using CgcStreamingEvidenceObserver observer = new();
        byte[] stdout = Enumerable.Repeat((byte)'a', 1_000).ToArray();
        byte[] stderr = Enumerable.Repeat((byte)'b', 2_000).ToArray();
        byte[] health = Enumerable.Repeat((byte)'c', 3_000).ToArray();
        byte[] query = Enumerable.Repeat(
            (byte)' ',
            CgcStreamingEvidenceObserver.MaximumObservedBytes - stdout.Length - stderr.Length - health.Length).ToArray();

        observer.Observe(CgcOutputStream.StandardOutput, stdout);
        observer.Observe(CgcOutputStream.StandardError, stderr);
        observer.Observe(CgcOutputStream.HttpHealthResponse, health);
        CgcObservationOutcome outcome = observer.Observe(CgcOutputStream.HttpQueryResponse, query);
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(outcome.TerminationRequired);
        Assert.False(evidence.Overflowed);
        Assert.Equal(CgcStreamingEvidenceObserver.MaximumObservedBytes, evidence.ObservedTotalBytes);
        Assert.Equal(evidence.ObservedTotalBytes,
            evidence.StandardOutput.Bytes + evidence.StandardError.Bytes +
            evidence.HealthResponse.Bytes + evidence.HttpResponse.Bytes);
        Assert.Equal(Sha256(stdout), evidence.StandardOutput.Sha256);
        Assert.Equal(Sha256(stderr), evidence.StandardError.Sha256);
        Assert.Equal(Sha256(health), evidence.HealthResponse.Sha256);
        Assert.Equal(Sha256(query), evidence.HttpResponse.Sha256);
    }

    [Fact]
    public void Observe_On65537thSharedByte_AccountsSentinelInTriggeringChannelThenTerminates()
    {
        int terminationRequests = 0;
        int forcedCleanupRequests = 0;
        CgcOverflowTerminationSignal overflowSignal = new(
            () => terminationRequests++,
            () => forcedCleanupRequests++);
        using CgcStreamingEvidenceObserver observer = new(overflowSignal.Signal);
        observer.Observe(
            CgcOutputStream.StandardOutput,
            Enumerable.Repeat((byte)'a', CgcStreamingEvidenceObserver.MaximumObservedBytes).ToArray());

        CgcObservationOutcome outcome = observer.Observe(CgcOutputStream.HttpQueryResponse, [(byte)'b', (byte)'c']);
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.True(outcome.TerminationRequired);
        Assert.Equal(1, terminationRequests);
        Assert.Equal(1, forcedCleanupRequests);
        Assert.True(evidence.Overflowed);
        Assert.True(evidence.TerminationRequested);
        Assert.Equal(CgcStreamingEvidenceObserver.OverflowSentinelBytes, evidence.ObservedTotalBytes);
        Assert.Equal(1, evidence.HttpResponse.Bytes);
        Assert.Equal(Sha256([(byte)'b']), evidence.HttpResponse.Sha256);
    }

    [Fact]
    public void Complete_WithInvalidUtf8OrTrailingJson_RejectsStructuralEvidence()
    {
        using CgcStreamingEvidenceObserver invalidUtf8 = new();
        invalidUtf8.Observe(CgcOutputStream.HttpQueryResponse, [0xC3]);

        using CgcStreamingEvidenceObserver trailing = new();
        trailing.Observe(
            CgcOutputStream.HttpQueryResponse,
            Encoding.UTF8.GetBytes(CreateHttpResponse(CreateValidRecord("CALLS")) + "{}"));

        Assert.False(invalidUtf8.Complete().IsValidUtf8);
        Assert.False(invalidUtf8.Complete().HasValidStructuralRecord);
        Assert.False(trailing.Complete().HasValidStructuralRecord);
    }

    [Theory]
    [MemberData(nameof(InvalidHttpResponses))]
    public void Complete_WithRejectedEnvelopeOrRecordShape_FailsClosed(string response)
    {
        using CgcStreamingEvidenceObserver observer = new();
        observer.Observe(CgcOutputStream.HttpQueryResponse, Encoding.UTF8.GetBytes(response));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.HasValidStructuralRecord, observer.HttpDiagnostic);
        Assert.False(evidence.HasExactlyOneOrderedStdoutMarkerPair, observer.HttpDiagnostic);
    }

    public static TheoryData<string> InvalidHttpResponses()
    {
        string validRecord = JsonSerializer.Serialize(CreateValidRecord("CALLS"));
        string valid = CreateHttpResponse(CreateValidRecord("CALLS"));
        TheoryData<string> data = new();
        data.Add(valid.Replace("\"status\":\"ok\"", "\"status\":\"failed\"", StringComparison.Ordinal));
        data.Add(valid.Replace("\"error\":null", "\"error\":\"unsafe\"", StringComparison.Ordinal));
        data.Add(valid.Replace("\"query\":", "\"query\":\"different\",\"ignored_query\":", StringComparison.Ordinal));
        data.Add(valid.Replace("\"record_count\":1", "\"record_count\":2", StringComparison.Ordinal));
        data.Add(valid.Replace("\"relationship_kind\":\"CALLS\"", "\"relationship_kind\":null", StringComparison.Ordinal));
        data.Add(valid.Replace("\"source_path\":\"src/Caller.cs\"", "\"source_path\":{\"source\":\"secret\"}", StringComparison.Ordinal));
        data.Add(valid.Replace("\"source_symbol\":\"Caller\"", "\"source\":\"secret\",\"source_symbol\":\"Caller\"", StringComparison.Ordinal));
        data.Add(valid.Replace("\"source_symbol\":\"Caller\"", "\"source_symbol\":\"Caller\",\"source_symbol\":\"Duplicate\"", StringComparison.Ordinal));
        data.Add(valid.Replace("\"source_symbol\":\"Caller\",", string.Empty, StringComparison.Ordinal));
        data.Add(valid.Replace("\"source_start_line\":1", "\"source_start_line\":\"1\"", StringComparison.Ordinal));
        data.Add(valid.Replace("\"filtered_target\":false", "\"filtered_target\":null", StringComparison.Ordinal));
        data.Add("{\"status\":\"ok\",\"error\":null,\"data\":{\"success\":true,\"query\":" +
            JsonSerializer.Serialize(CgcQualificationRunner.StaticStructuralQuery) +
            ",\"record_count\":1,\"results\":[{" +
            validRecord[..(validRecord.IndexOf("\"target_path\"", StringComparison.Ordinal) - 1)].TrimStart('{') +
            "},{" + validRecord[(validRecord.IndexOf("\"target_path\"", StringComparison.Ordinal))..].TrimEnd('}') + "}]}}" );
        return data;
    }

    [Fact]
    public void Complete_WithRecordOver4096RawBytes_RejectsAndCountsIt()
    {
        using CgcStreamingEvidenceObserver observer = new();
        object oversized = CreateValidRecord("CALLS", sourceSymbol: new string('x', 4_096));
        observer.Observe(CgcOutputStream.HttpQueryResponse, Encoding.UTF8.GetBytes(CreateHttpResponse(oversized)));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.HasValidStructuralRecord);
        Assert.Equal(1, evidence.InvalidOrOversizedRecordCount);
    }

    [Theory]
    [InlineData("IMPORTS", 3, 4, 5, 5)]
    [InlineData("IMPORTS", 3, 3, 5, 6)]
    [InlineData("CONTAINS", 3, 3, null, null)]
    [InlineData("INHERITS", 3, 3, null, null)]
    public void Complete_WithRelationshipSpecificPredicateViolation_RejectsRecord(
        string kind,
        int sourceStart,
        int sourceEnd,
        int? targetStart,
        int? targetEnd)
    {
        using CgcStreamingEvidenceObserver observer = new();
        observer.Observe(
            CgcOutputStream.HttpQueryResponse,
            Encoding.UTF8.GetBytes(CreateHttpResponse(CreateValidRecord(
                kind,
                sourceStart: sourceStart,
                sourceEnd: sourceEnd,
                targetStart: targetStart,
                targetEnd: targetEnd))));

        Assert.False(observer.Complete().HasValidStructuralRecord);
    }

    [Fact]
    public void RunnerContract_ContainsNoRejectedWholeDocumentOrCliTableBridge()
    {
        MethodInfo createScript = typeof(CgcQualificationRunner).GetMethod(
            "CreateQualificationScript",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        string script = (string)createScript.Invoke(null, ["context", "/db", "/home"])!;

        Assert.DoesNotContain("bridge.py", script, StringComparison.Ordinal);
        Assert.DoesNotContain("python", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VULGATA_CGC_QUERY_OUTPUT", script, StringComparison.Ordinal);
        Assert.DoesNotContain("key=value", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cgc query", script, StringComparison.OrdinalIgnoreCase);
        string runnerSource = File.ReadAllText(QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "CodeGraphQualificationRunner.cs"));
        Assert.DoesNotContain("ReadAsString", runnerSource, StringComparison.Ordinal);
    }

    private static string CreateHttpResponse(params object[] records) => JsonSerializer.Serialize(new
    {
        status = "ok",
        error = (string?)null,
        data = new
        {
            success = true,
            query = CgcQualificationRunner.StaticStructuralQuery,
            record_count = records.Length,
            results = records,
        },
    });

    private static object CreateValidRecord(
        string relationshipKind,
        string sourceSymbol = "Caller",
        int sourceStart = 1,
        int sourceEnd = 2,
        string targetPath = "src/Target.cs",
        int? targetStart = 3,
        int? targetEnd = 4,
        bool filteredTarget = false) => new
        {
            relationship_kind = relationshipKind,
            source_path = "src/Caller.cs",
            source_symbol = sourceSymbol,
            source_start_line = sourceStart,
            source_end_line = sourceEnd,
            source_identity = "Caller",
            target_path = targetPath,
            target_symbol = "Target",
            target_start_line = targetStart,
            target_end_line = targetEnd,
            target_identity = "Target",
            filtered_target = filteredTarget,
        };

    private static string Sha256(ReadOnlySpan<byte> value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
