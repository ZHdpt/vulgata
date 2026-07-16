using System.Text;
using System.Text.Json;
using System.Reflection;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class CgcStreamingEvidenceObserverTests
{
    [Fact]
    public void Complete_WithUtf8SplitAcrossChunksAndOneOrderedStdoutPair_ProducesSafeStructuralFacts()
    {
        CgcStreamingEvidenceObserver observer = new();
        byte[] payload = Encoding.UTF8.GetBytes(
            "VULGATA_CGC_QUERY_OUTPUT=BEGIN\n" +
            "relationship_kind=CALLS|source_path=src/Caller.cs|source_symbol=Café|source_start_line=1|source_end_line=2|source_identity=Caller|target_path=src/Filtered/GeneratedClient.g.cs|target_symbol=Client|target_start_line=3|target_end_line=4|target_identity=FilteredClient\n" +
            "VULGATA_CGC_QUERY_OUTPUT=END\n");

        int markerSplit = 10;
        int utf8Split = Array.IndexOf(payload, (byte)0xC3) + 1;
        observer.Observe(CgcOutputStream.StandardOutput, payload.AsSpan(0, markerSplit));
        observer.Observe(CgcOutputStream.StandardOutput, payload.AsSpan(markerSplit, utf8Split - markerSplit));
        observer.Observe(CgcOutputStream.StandardOutput, payload.AsSpan(utf8Split));
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.True(evidence.IsValidUtf8);
        Assert.True(evidence.HasExactlyOneOrderedStdoutMarkerPair);
        Assert.True(evidence.HasValidStructuralRecord);
        Assert.True(evidence.HasFilteredTargetCandidate);
        Assert.True(evidence.HasPositiveOrderedRange);
        Assert.True(evidence.HasNonEmptyEndpointIdentity);
        Assert.Equal(1, evidence.RelationshipKindCounts["CALLS"]);
        Assert.Equal(payload.Length, evidence.ObservedTotalBytes);
        Assert.False(evidence.Overflowed);
    }

    [Fact]
    public void Complete_AtInclusiveSharedByteCap_AccountsEveryByteWithoutOverflow()
    {
        CgcStreamingEvidenceObserver observer = new();
        byte[] output = Enumerable.Repeat((byte)'x', CgcStreamingEvidenceObserver.MaximumObservedBytes).ToArray();

        CgcObservationOutcome outcome = observer.Observe(CgcOutputStream.StandardOutput, output);
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(outcome.TerminationRequired);
        Assert.False(evidence.Overflowed);
        Assert.Equal(CgcStreamingEvidenceObserver.MaximumObservedBytes, evidence.ObservedTotalBytes);
        Assert.Equal(CgcStreamingEvidenceObserver.MaximumObservedBytes, evidence.StandardOutput.Bytes);
        Assert.Equal(0, evidence.StandardError.Bytes);
    }

    [Fact]
    public void Observe_On65537thSharedByte_AccountsTriggeringStreamThenSignalsImmediateTermination()
    {
        int terminationRequests = 0;
        int forcedCleanupRequests = 0;
        CgcOverflowTerminationSignal overflowSignal = new(
            () => terminationRequests++,
            () => forcedCleanupRequests++);
        CgcStreamingEvidenceObserver observer = new(overflowSignal.Signal);
        observer.Observe(
            CgcOutputStream.StandardOutput,
            Enumerable.Repeat((byte)'a', CgcStreamingEvidenceObserver.MaximumObservedBytes).ToArray());

        CgcObservationOutcome outcome = observer.Observe(CgcOutputStream.StandardError, [(byte)'b', (byte)'c']);
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.True(outcome.TerminationRequired);
        Assert.Equal(1, terminationRequests);
        Assert.Equal(1, forcedCleanupRequests);
        Assert.True(evidence.Overflowed);
        Assert.True(evidence.TerminationRequested);
        Assert.Equal(CgcStreamingEvidenceObserver.OverflowSentinelBytes, evidence.ObservedTotalBytes);
        Assert.Equal(CgcStreamingEvidenceObserver.MaximumObservedBytes, evidence.StandardOutput.Bytes);
        Assert.Equal(1, evidence.StandardError.Bytes);
        Assert.Equal(evidence.ObservedTotalBytes, evidence.StandardOutput.Bytes + evidence.StandardError.Bytes);
    }

    [Fact]
    public void Complete_WhenEndMarkerArrivesAfterSharedCap_RefusesStructuralProof()
    {
        CgcStreamingEvidenceObserver observer = new();
        byte[] begin = Encoding.UTF8.GetBytes("VULGATA_CGC_QUERY_OUTPUT=BEGIN\n");
        byte[] filler = Enumerable.Repeat(
            (byte)'x',
            CgcStreamingEvidenceObserver.MaximumObservedBytes - begin.Length).ToArray();
        byte[] end = Encoding.UTF8.GetBytes("VULGATA_CGC_QUERY_OUTPUT=END\n");

        observer.Observe(CgcOutputStream.StandardOutput, begin);
        observer.Observe(CgcOutputStream.StandardOutput, filler);
        observer.Observe(CgcOutputStream.StandardOutput, end);
        CgcStreamingEvidence evidence = observer.Complete();

        Assert.True(evidence.Overflowed);
        Assert.Equal(0, evidence.StdoutEndCount);
        Assert.False(evidence.HasExactlyOneOrderedStdoutMarkerPair);
        Assert.False(evidence.HasValidStructuralRecord);
    }

    [Fact]
    public void Complete_WithInvalidOrSplitUtf8_FailsSafely()
    {
        CgcStreamingEvidenceObserver observer = new();
        observer.Observe(CgcOutputStream.StandardError, [0xC3]);

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.IsValidUtf8);
        Assert.False(evidence.HasExactlyOneOrderedStdoutMarkerPair);
    }

    [Fact]
    public void Complete_WithStderrOnlyMarkerAndStructuralRow_ExcludesItFromProof()
    {
        CgcStreamingEvidenceObserver observer = new();
        observer.Observe(CgcOutputStream.StandardError, Encoding.UTF8.GetBytes(CreateFramedOutput(CreateValidRow())));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.Equal(0, evidence.StdoutBeginCount);
        Assert.Equal(0, evidence.StdoutEndCount);
        Assert.False(evidence.HasExactlyOneOrderedStdoutMarkerPair);
        Assert.False(evidence.HasValidStructuralRecord);
    }

    [Theory]
    [InlineData("VULGATA_CGC_QUERY_OUTPUT=END\nVULGATA_CGC_QUERY_OUTPUT=BEGIN\n")]
    [InlineData("VULGATA_CGC_QUERY_OUTPUT=BEGIN\nVULGATA_CGC_QUERY_OUTPUT=BEGIN\nVULGATA_CGC_QUERY_OUTPUT=END\n")]
    [InlineData("VULGATA_CGC_QUERY_OUTPUT=BEGIN\nrelationship_kind=CALLS\n")]
    public void Complete_WithReversedDuplicateOrMissingMarkers_FailsFraming(string output)
    {
        CgcStreamingEvidenceObserver observer = new();
        observer.Observe(CgcOutputStream.StandardOutput, Encoding.UTF8.GetBytes(output));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.HasExactlyOneOrderedStdoutMarkerPair);
        Assert.False(evidence.HasValidStructuralRecord);
    }

    [Fact]
    public void Complete_With4097ByteStructuralRow_FailsWithoutRetainingIt()
    {
        CgcStreamingEvidenceObserver observer = new();
        string oversizedRow = new('x', CgcStreamingEvidenceObserver.MaximumStructuralRecordBytes + 1);
        observer.Observe(CgcOutputStream.StandardOutput, Encoding.UTF8.GetBytes(CreateFramedOutput(oversizedRow)));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.HasValidStructuralRecord);
        Assert.Equal(1, evidence.InvalidOrOversizedRecordCount);
    }

    [Fact]
    public void Complete_WithRelationshipAndTupleFragmentsInDifferentRows_RefusesCrossRowCorrelation()
    {
        CgcStreamingEvidenceObserver observer = new();
        string firstRow = "relationship_kind=CALLS|source_path=src/Caller.cs|source_symbol=Caller|source_start_line=1|source_end_line=2|source_identity=Caller";
        string secondRow = "target_path=src/Filtered/GeneratedClient.g.cs|target_symbol=Client|target_start_line=3|target_end_line=4|target_identity=FilteredClient";
        observer.Observe(CgcOutputStream.StandardOutput, Encoding.UTF8.GetBytes(CreateFramedOutput(firstRow, secondRow)));

        CgcStreamingEvidence evidence = observer.Complete();

        Assert.False(evidence.HasValidStructuralRecord);
        Assert.False(evidence.AllStructuralFactsCorrelatedWithinValidRecords);
        Assert.False(evidence.HasFilteredTargetCandidate);
    }

    [Fact]
    public void Complete_ProjectsHashesAndFactsWithoutRawProviderOutput()
    {
        const string secret = "synthetic-provider-secret-keep-out";
        CgcStreamingEvidenceObserver observer = new();
        observer.Observe(CgcOutputStream.StandardOutput, Encoding.UTF8.GetBytes(CreateFramedOutput($"relationship_kind=CALLS|source_path=src/{secret}.cs")));

        CgcStreamingEvidence evidence = observer.Complete();
        string projection = JsonSerializer.Serialize(evidence);

        Assert.DoesNotContain(secret, projection, StringComparison.Ordinal);
        Assert.DoesNotContain("rawOutput", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("merged", projection, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            typeof(CgcStreamingEvidenceObserver).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(StringBuilder));
        Assert.DoesNotContain(
            typeof(CgcQualificationRunner).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType == typeof(StringBuilder));
        Assert.Matches("^[a-f0-9]{64}$", evidence.StandardOutput.Sha256);
        Assert.Matches("^[a-f0-9]{64}$", evidence.StandardError.Sha256);
    }

    private static string CreateFramedOutput(params string[] rows) =>
        $"VULGATA_CGC_QUERY_OUTPUT=BEGIN\n{string.Join("\n", rows)}\nVULGATA_CGC_QUERY_OUTPUT=END\n";

    private static string CreateValidRow() =>
        "relationship_kind=CALLS|source_path=src/Caller.cs|source_symbol=Caller|source_start_line=1|source_end_line=2|source_identity=Caller|target_path=src/Filtered/GeneratedClient.g.cs|target_symbol=Client|target_start_line=3|target_end_line=4|target_identity=FilteredClient";
}
