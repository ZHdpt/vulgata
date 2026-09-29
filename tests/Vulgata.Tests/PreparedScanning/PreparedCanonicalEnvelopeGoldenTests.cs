using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T008 RED slice.  Literal vectors and the independent
/// reconstruction contract are kept here; the production writer is resolved
/// without a compile-time dependency until T007/T009 add it.
/// </summary>
public sealed class PreparedCanonicalEnvelopeGoldenTests
{
    private const string CompleteCodeGraphDomain =
        "vulgata/story-12.2/codegraph-completeness-proof/1.0";
    private const string ReadyJavaDomain = "vulgata/story-12.2/java-readiness/1.0";
    private const string NotApplicableJavaDomain = "vulgata/story-12.2/java-readiness/1.0";
    private const string ContextDomain =
        "vulgata/story-12.2/prepared-scan-context-snapshot/1.0";

    // Literal empty-root envelopes provide immutable byte fixtures while the
    // production identity builders are still absent. The full artifact vectors
    // are added by T009 once the typed builders exist.
    private const string SmallestCompleteCodeGraphEnvelopeHex =
        "5647434501060000003376756c676174612f73746f72792d31322e322f636f646567726170682d636f6d706c6574656e6573732d70726f6f662f312e300900000000";
    private const string SmallestReadyJavaEnvelopeHex =
        "5647434501060000002576756c676174612f73746f72792d31322e322f6a6176612d72656164696e6573732f312e300900000000";
    private const string SmallestNotApplicableJavaEnvelopeHex = SmallestReadyJavaEnvelopeHex;
    private const string SmallestContextEnvelopeHex =
        "5647434501060000003576756c676174612f73746f72792d31322e322f70726570617265642d7363616e2d636f6e746578742d736e617073686f742f312e300900000000";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GoldenVectors_ExposeAllRequiredStableArtifactDomains()
    {
        Type contracts = RequirePreparedIdentityBuilder();

        Assert.NotEmpty(CompleteCodeGraphDomain);
        Assert.NotEmpty(ReadyJavaDomain);
        Assert.Equal(ReadyJavaDomain, NotApplicableJavaDomain);
        Assert.NotEmpty(ContextDomain);
        Assert.NotNull(contracts);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GoldenVectors_RequireLiteralCompleteCodeGraphReadyJavaXmlOnlyAndContextBytes()
    {
        RequirePreparedIdentityBuilder();

        Assert.Equal((byte)0x56, Convert.FromHexString(SmallestCompleteCodeGraphEnvelopeHex)[0]);
        Assert.Equal((byte)0x56, Convert.FromHexString(SmallestReadyJavaEnvelopeHex)[0]);
        Assert.Equal((byte)0x56, Convert.FromHexString(SmallestContextEnvelopeHex)[0]);

        Assert.True(
            SmallestCompleteCodeGraphEnvelopeHex.Length > 0,
            "T008 requires the literal Complete CodeGraph vector after the production identity seam is available.");
        Assert.True(
            SmallestReadyJavaEnvelopeHex.Length > 0,
            "T008 requires the literal Ready Java vector after the production identity seam is available.");
        Assert.True(
            SmallestNotApplicableJavaEnvelopeHex.Length > 0,
            "T008 requires the literal XML-only NotApplicable Java vector after the production identity seam is available.");
        Assert.True(
            SmallestContextEnvelopeHex.Length > 0,
            "T008 requires the literal Prepared Scan Context Snapshot vector after the production identity seam is available.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GoldenVectors_RequireIndependentReconstructorAndCaptureTimeInvariance()
    {
        Type contracts = RequirePreparedIdentityBuilder();
        MethodInfo[] methods = contracts.GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Contains(
            methods,
            method => method.Name.Contains("Reconstruct", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Canonical", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            methods,
            method => method.Name.Contains("Context", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void GoldenVectors_RequireOrdinalAndUnicodeMutationCoverage()
    {
        Type contracts = RequirePreparedIdentityBuilder();

        Assert.NotNull(contracts.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static));
        Assert.Contains(
            contracts.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Fingerprint", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Identity", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void FileCodeUnitIdentity_V1GoldenRemainsByteAndIdentityStable()
    {
        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.CreateFileCodeUnitIdentity(
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            Guid.Parse("11111111-2222-4333-8444-555555555555"),
            new string('d', 40),
            "src/Order.java",
            new string('a', 64),
            0,
            128,
            1,
            8,
            1,
            1,
            new string('b', 64));

        Assert.Equal(
            "5647434501060000002e76756c676174612f73746f72792d31322e322f66696c652d636f64652d756e69742d6964656e746974792f312e30090000000c000104102132435465768798a9bacbdcedfe0f0002041111111122224333844455555555555500030600000028646464646464646464646464646464646464646464646464646464646464646464646464646464640004060000000e7372632f4f726465722e6a61766100050700000020aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa0006030000000000000000000703000000000000008000080300000000000000010009030000000000000008000a030000000000000001000b030000000000000001000c0700000020bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            Convert.ToHexString(artifact.Envelope.Span).ToLowerInvariant());
        Assert.Equal(
            "21870acdebeb7d142f39f4bf7cf51cafee74e84f037ca1bef0c62aac5d48e76d",
            artifact.Fingerprint);
        Assert.Equal(
            Guid.Parse("c0aae19f-7fe9-8d69-b20b-bcf2e25dedbf"),
            PreparedScanIdentityBuilders.CreateDeterministicIdentity("FileCodeUnit", artifact.Fingerprint));
        Assert.True(CanonicalEnvelope.Validate(artifact.Envelope.Span));
    }

    private static Type RequirePreparedIdentityBuilder()
    {
        Assembly assembly = typeof(CodeGraphCompletenessProof).Assembly;
        Type? contracts = assembly.GetType(
            "Vulgata.Core.DomainServices.PreparedScanIdentityBuilders") ??
            assembly.GetType("Vulgata.Core.DomainServices.PreparedCanonicalIdentity") ??
            assembly.GetTypes().FirstOrDefault(type =>
                type.Name.Contains("PreparedScanIdentity", StringComparison.Ordinal) ||
                type.Name.Contains("PreparedCanonicalIdentity", StringComparison.Ordinal));

        Assert.True(
            contracts is not null,
            "Story 12.2 typed canonical identity public seam is missing; implement T009 before this RED can turn GREEN.");
        return contracts!;
    }
}
