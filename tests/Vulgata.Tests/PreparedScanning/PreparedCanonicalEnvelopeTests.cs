using System.Reflection;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T006 RED slice.  These tests deliberately bind through the
/// public Core seam so that a missing canonical writer is reported as a
/// behavioral RED (rather than making the test project fail to compile).
/// </summary>
public sealed class PreparedCanonicalEnvelopeTests
{
    [Fact]
    public void CanonicalEnvelope_ExposesVersionedWriterPublicSeam()
    {
        Type writer = RequireCanonicalWriter();

        Assert.Contains(
            writer.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name is "Create" or "Encode" or "Write" or "Derive");
    }

    [Fact]
    public void CanonicalEnvelope_ContractRequiresScalarAndNestedValueSupport()
    {
        Type writer = RequireCanonicalWriter();
        MethodInfo[] methods = writer.GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Contains(methods, method => method.Name.Contains("Null", StringComparison.Ordinal));
        Assert.Contains(methods, method => method.Name.Contains("String", StringComparison.Ordinal));
        Assert.Contains(methods, method => method.Name.Contains("Object", StringComparison.Ordinal));
        Assert.Contains(methods, method => method.Name.Contains("List", StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalEnvelope_ContractIncludesValidationAndUuidDerivation()
    {
        Type writer = RequireCanonicalWriter();
        MethodInfo[] methods = writer.GetMethods(BindingFlags.Public | BindingFlags.Static);

        Assert.Contains(methods, method =>
            method.Name.Contains("Validate", StringComparison.Ordinal) ||
            method.Name.Contains("Decode", StringComparison.Ordinal));
        Assert.Contains(methods, method => method.Name.Contains("Uuid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanonicalEnvelope_ContractMustBeIndependentOfLegacyIdentityEnvelope()
    {
        Type writer = RequireCanonicalWriter();

        Assert.DoesNotContain(
            writer.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.DeclaringType == typeof(OrdinaryPass1JobIdentity));
    }

    [Fact]
    public void SemanticFileCodeUnitIdentity_ApprovedV2Domain_ProducesValidCanonicalEnvelope()
    {
        PreparedCanonicalArtifact artifact = PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            new string('c', 40),
            "src/Order.java",
            new string('d', 64),
            0,
            128,
            1,
            8,
            1,
            1,
            new string('e', 64),
            "com.example.Order");

        Assert.True(CanonicalEnvelope.Validate(artifact.Envelope.Span));
    }

    [Fact]
    public void SemanticFileCodeUnitIdentity_RepeatedInputsAreStable_AndSymbolsDifferentiate()
    {
        PreparedCanonicalArtifact Create(string symbol) =>
            PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity(
                Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
                new string('c', 40),
                "src/Order.java",
                new string('d', 64),
                0,
                128,
                1,
                8,
                1,
                1,
                new string('e', 64),
                symbol);

        PreparedCanonicalArtifact first = Create("com.example.Order");
        PreparedCanonicalArtifact replay = Create("com.example.Order");
        PreparedCanonicalArtifact differentSymbol = Create("com.example.Other");

        Assert.Equal(first.Envelope.ToArray(), replay.Envelope.ToArray());
        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Guid firstIdentity = PreparedScanIdentityBuilders.CreateDeterministicIdentity("FileCodeUnit", first.Fingerprint);
        Guid replayIdentity = PreparedScanIdentityBuilders.CreateDeterministicIdentity("FileCodeUnit", replay.Fingerprint);
        Guid differentSymbolIdentity = PreparedScanIdentityBuilders.CreateDeterministicIdentity("FileCodeUnit", differentSymbol.Fingerprint);
        Assert.Equal(firstIdentity, replayIdentity);
        Assert.NotEqual(firstIdentity, differentSymbolIdentity);
        Assert.False(first.Envelope.Span.SequenceEqual(differentSymbol.Envelope.Span));
        Assert.NotEqual(first.Fingerprint, differentSymbol.Fingerprint);
    }

    [Fact]
    public void SemanticFileCodeUnitIdentity_InvalidSymbolsAreRejected()
    {
        foreach (string symbol in new[] { "", "Order", "com.example. bad", " com.example.Order" })
        {
            Assert.Throws<ArgumentException>(() =>
                PreparedScanIdentityBuilders.CreateSemanticFileCodeUnitIdentity(
                    Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                    Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
                    new string('c', 40),
                    "src/Order.java",
                    new string('d', 64),
                    0,
                    128,
                    1,
                    8,
                    1,
                    1,
                    new string('e', 64),
                    symbol));
        }
    }

    private static Type RequireCanonicalWriter()
    {
        Type? writer = typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
            "Vulgata.Core.DomainServices.CanonicalEnvelope") ??
            typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
                "Vulgata.Core.DomainServices.CanonicalEnvelopeWriter") ??
            typeof(OrdinaryPass1JobIdentity).Assembly.GetType(
                "Vulgata.Core.DomainServices.PreparedCanonicalEnvelope");

        Assert.True(
            writer is not null,
            "Story 12.2 canonical-envelope public seam is missing; implement T007 before this RED can turn GREEN.");
        return writer!;
    }
}
