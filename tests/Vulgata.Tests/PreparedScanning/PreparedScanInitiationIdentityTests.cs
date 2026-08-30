using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T012 RED slice for the Core-owned Prepared Scan initiation execution
/// identity. Expected bytes and identifiers are independent literal vectors;
/// this test never reconstructs them with the production identity code.
/// </summary>
public sealed class PreparedScanInitiationIdentityTests
{
    private const string ExpectedEnvelopeHex =
        "5647434501060000003976756c676174612f73746f72792d31322e352f70726570617265642d7363616e2d696e6974696174696f6e2d657865637574696f6e2f312e30090000000a000104111111111111411181111111111111110002050000000d41646d696e6973747261746f72000304222222222222422282222222222222220004043333333333334333833333333333333300050600000016726566732f746167732f73746f72793132352d6f6e650006060000002830313233343536373839616263646566303132333435363738396162636465663031323334353637000706000000406161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616161616100080600000012636f64652d6c6f6769632f66696c652d76320009060000004d7b22616e616c7973697352756e74696d6556657273696f6e223a2231222c22636f6465477261706856657273696f6e223a22312e35222c226a64744c7356657273696f6e223a22312e3631227d000a0700000020b61f338bfebd542cd6f360583b7063f375eb2978fc45acd1c5ec67ec9eaf62b2";

    private const string ExpectedFingerprint =
        "c9e3241f8e4b4ed42539d87d53938360d04293dfa5c702a8529de9bc2c8482be";

    private const string ExpectedExecutionId = "e71c720d-1e9e-8a78-8b26-34788d6f67cb";
    private const string ExpectedSecondExecutionId = "c9074fec-6011-8b92-b902-551c2ebfbd2a";
    private const string ExpectedCoordinationId = "444950ba-c4f5-832d-acbd-71995d284230";

    [Fact]
    public void CreateExecution_UsesAcceptedDomainTagsAndLiteralGoldenIdentity()
    {
        PreparedCanonicalArtifact actual = Create(Baseline);

        Assert.Equal(ExpectedEnvelopeHex, Convert.ToHexString(actual.Envelope.Span).ToLowerInvariant());
        Assert.True(CanonicalEnvelope.Validate(actual.Envelope.Span));
        Assert.Equal(ExpectedFingerprint, actual.Fingerprint);
        Assert.Equal(
            Guid.Parse(ExpectedExecutionId),
            PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                "PreparedScanInitiationExecution",
                actual.Fingerprint));
    }

    [Fact]
    public void CreateExecution_RepositoryReversalPreservesCanonicalPairOrder()
    {
        PreparedCanonicalArtifact first = Create(Baseline);
        PreparedCanonicalArtifact second = Create(Baseline with
        {
            RepositoryId = SecondRepositoryId,
            RequestedRef = "refs/tags/story125-two",
            ProfiledRevision = "fedcba9876543210fedcba9876543210fedcba98",
        });
        Guid firstId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanInitiationExecution",
            first.Fingerprint);
        Guid secondId = PreparedScanIdentityBuilders.CreateDeterministicIdentity(
            "PreparedScanInitiationExecution",
            second.Fingerprint);

        ScanExecution firstExecution = ScanExecution.CreateNewPath(
            firstId,
            Baseline.RepositoryId,
            Baseline.SystemId,
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Baseline.ProfiledRevision,
            Baseline.PromptIdentity,
            Baseline.DocumentSchemaIdentity,
            Baseline.RuntimeIdentityJson,
            Baseline.RuntimeIdentityHash,
            InitiatedAt);
        ScanExecution secondExecution = ScanExecution.CreateNewPath(
            secondId,
            SecondRepositoryId,
            Baseline.SystemId,
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            "fedcba9876543210fedcba9876543210fedcba98",
            Baseline.PromptIdentity,
            Baseline.DocumentSchemaIdentity,
            Baseline.RuntimeIdentityJson,
            Baseline.RuntimeIdentityHash,
            InitiatedAt);

        Guid forward = TwoRepositoryScanCoordination.Create(
            firstExecution,
            secondExecution,
            InitiatedAt).Id;
        Guid reversed = TwoRepositoryScanCoordination.Create(
            secondExecution,
            firstExecution,
            InitiatedAt).Id;

        Assert.Equal(Guid.Parse(ExpectedSecondExecutionId), secondId);
        Assert.Equal(Guid.Parse(ExpectedCoordinationId), forward);
        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void CreateExecution_EachReplayBoundDimensionChangesTheIdentity()
    {
        PreparedCanonicalArtifact baseline = Create(Baseline);
        Func<PreparedScanInitiationIdentityInput, PreparedScanInitiationIdentityInput>[] mutations =
        [
            value => value with { UserId = Guid.Parse("77777777-7777-4777-8777-777777777777") },
            value => value with { EffectiveAuthorizationMode = "SystemOwner" },
            value => value with { SystemId = Guid.Parse("88888888-8888-4888-8888-888888888888") },
            value => value with { RepositoryId = SecondRepositoryId },
            value => value with { RequestedRef = "refs/heads/story125-one" },
            value => value with { ProfiledRevision = "abcdefabcdefabcdefabcdefabcdefabcdefabcd" },
            value => value with { PromptIdentity = new('b', 64) },
            value => value with { DocumentSchemaIdentity = "code-logic/file-v3" },
            value => value with { RuntimeIdentityJson = "{\"analysisRuntimeVersion\":\"2\"}" },
            value => value with { RuntimeIdentityHash = new('c', 64) },
        ];

        foreach (Func<PreparedScanInitiationIdentityInput, PreparedScanInitiationIdentityInput> mutate in mutations)
        {
            PreparedCanonicalArtifact changed = Create(mutate(Baseline));
            Assert.NotEqual(baseline.Fingerprint, changed.Fingerprint);
            Assert.NotEqual(
                PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                    "PreparedScanInitiationExecution",
                    baseline.Fingerprint),
                PreparedScanIdentityBuilders.CreateDeterministicIdentity(
                    "PreparedScanInitiationExecution",
                    changed.Fingerprint));
        }
    }

    [Fact]
    public void InitiationPort_CoreBoundary_HasNoEfOrOpenQueryDependency()
    {
        Assert.DoesNotContain(
            typeof(IPreparedScanInitiationStore).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("EntityFrameworkCore", StringComparison.Ordinal) == true);

        System.Reflection.MethodInfo method = Assert.Single(
            typeof(IPreparedScanInitiationStore).GetMethods());
        Assert.DoesNotContain("IQueryable", method.ReturnType.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DbSet", method.ReturnType.ToString(), StringComparison.Ordinal);
        Assert.All(method.GetParameters(), parameter =>
        {
            string typeName = parameter.ParameterType.ToString();
            Assert.DoesNotContain("EntityFrameworkCore", typeName, StringComparison.Ordinal);
            Assert.DoesNotContain("IQueryable", typeName, StringComparison.Ordinal);
            Assert.DoesNotContain("DbSet", typeName, StringComparison.Ordinal);
        });
    }

    private static readonly PreparedScanInitiationIdentityInput Baseline = new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "Administrator",
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        Guid.Parse("33333333-3333-4333-8333-333333333333"),
        "refs/tags/story125-one",
        "0123456789abcdef0123456789abcdef01234567",
        new('a', 64),
        "code-logic/file-v2",
        "{\"analysisRuntimeVersion\":\"1\",\"codeGraphVersion\":\"1.5\",\"jdtLsVersion\":\"1.61\"}",
        "b61f338bfebd542cd6f360583b7063f375eb2978fc45acd1c5ec67ec9eaf62b2");

    private static readonly Guid SecondRepositoryId =
        Guid.Parse("44444444-4444-4444-8444-444444444444");

    private static readonly DateTimeOffset InitiatedAt =
        new(2026, 8, 30, 0, 0, 0, TimeSpan.Zero);

    private static PreparedCanonicalArtifact Create(PreparedScanInitiationIdentityInput input) =>
        PreparedScanInitiationIdentityBuilder.CreateExecution(
            input.UserId,
            input.EffectiveAuthorizationMode,
            input.SystemId,
            input.RepositoryId,
            input.RequestedRef,
            input.ProfiledRevision,
            input.PromptIdentity,
            input.DocumentSchemaIdentity,
            input.RuntimeIdentityJson,
            input.RuntimeIdentityHash);
}

internal sealed record PreparedScanInitiationIdentityInput(
    Guid UserId,
    string EffectiveAuthorizationMode,
    Guid SystemId,
    Guid RepositoryId,
    string RequestedRef,
    string ProfiledRevision,
    string PromptIdentity,
    string DocumentSchemaIdentity,
    string RuntimeIdentityJson,
    string RuntimeIdentityHash);
