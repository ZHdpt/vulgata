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
