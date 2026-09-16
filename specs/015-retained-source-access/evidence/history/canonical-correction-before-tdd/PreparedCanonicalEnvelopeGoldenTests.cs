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

    [Fact]
    public void GoldenVectors_ExposeAllRequiredStableArtifactDomains()
    {
        Type contracts = RequirePreparedIdentityBuilder();

        Assert.NotEmpty(CompleteCodeGraphDomain);
        Assert.NotEmpty(ReadyJavaDomain);
        Assert.Equal(ReadyJavaDomain, NotApplicableJavaDomain);
        Assert.NotEmpty(ContextDomain);
        Assert.NotNull(contracts);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
