using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// Story 12.2 T036 RED slice.  These tests bind the US4 planner to its
/// public construction/expansion seams.  They deliberately fail while the
/// File Code Unit factory and bounded related-file expansion are absent;
/// the already-green pure selection planner is not used as a substitute.
/// </summary>
public sealed class PreparedFileCodeUnitAndSelectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void FileCodeUnitFactory_ExposesExactBoundedConstructionSeam()
    {
        Type planner = typeof(PreparedScanPlanner);
        MethodInfo? factory = planner.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(method => method.Name.Contains("FileCodeUnit", StringComparison.Ordinal));

        Assert.True(
            factory is not null,
            "T037 must expose a public File Code Unit construction seam from Core; " +
            "the existing candidate-only planner is not sufficient.");

        Assert.DoesNotContain(
            factory!.GetParameters(),
            parameter => parameter.Name is "absolutePath" or "sourceContent" or "hostPath");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void FileCodeUnitConstruction_ExposesWholeFileAndExactSplitBoundChecks()
    {
        Type factory = typeof(PreparedFileCodeUnitDraft);
        string[] names = factory.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();
        Assert.Contains("Create", names);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void RelatedFileExpansion_UsesBoundedCallsImportsInheritsAndTimeoutWarning()
    {
        Assembly infrastructure = typeof(Vulgata.Infrastructure.Git.PreparedScanCandidateEnumerator)
            .Assembly;
        Type? expansion = infrastructure.GetType(
            "Vulgata.Infrastructure.Analysis.CodeGraph.PreparedRelatedFileExpansion",
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(
            expansion);
        MethodInfo[] methods = expansion!.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        Assert.Contains(
            methods,
            method => method.Name.Contains("Expand", StringComparison.Ordinal));
        Assert.Contains(
            methods.SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType.Name.Contains("Related", StringComparison.Ordinal));
        Assert.Contains(
            expansion.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            property => property.Name.Contains("Warning", StringComparison.Ordinal) ||
                property.Name.Contains("Timeout", StringComparison.Ordinal));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Selection_RequiresBothNamedOrdinalDomainsAndOrdinalIndependentIdentity()
    {
        PropertyInfo[] draftProperties = typeof(PreparedFileCodeUnitDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(draftProperties, property => property.Name == "ManifestOrdinal");
        Assert.Contains(draftProperties, property => property.Name == "PairSelectionOrdinal");

        MethodInfo identity = typeof(PreparedScanPlanner).GetMethod(
            "FingerprintIdentity",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "The planner must expose the File Code Unit identity projection.");

        PreparedFileCodeUnitDraft first = CreateDraft(1, 1);
        PreparedFileCodeUnitDraft second = first with
        {
            ManifestOrdinal = 2,
            PairSelectionOrdinal = 9,
        };

        string firstFingerprint = (string)identity.Invoke(null, [first])!;
        string secondFingerprint = (string)identity.Invoke(null, [second])!;
        Assert.Equal(firstFingerprint, secondFingerprint);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void MatchedOwner_FileCodeUnitSeamCarriesServerOwnedSymbolName()
    {
        PropertyInfo[] inputProperties = typeof(PreparedFileCodeUnitInput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo[] draftProperties = typeof(PreparedFileCodeUnitDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            inputProperties,
            property => property.Name == "SymbolName" && property.PropertyType == typeof(string));
        Assert.Contains(
            draftProperties,
            property => property.Name == "SymbolName" && property.PropertyType == typeof(string));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SemanticOwner_UsesSeparateV2IdentityBuilderWithOrdinalFqnBinding()
    {
        MethodInfo[] identityMethods = typeof(PreparedScanIdentityBuilders)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name.Contains("FileCodeUnit", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(
            identityMethods,
            method => method.Name.Contains("Semantic", StringComparison.Ordinal) &&
                method.ReturnType == typeof(PreparedCanonicalArtifact) &&
                method.GetParameters().Any(parameter =>
                    parameter.Name is "symbolName" or "fullyQualifiedSymbol"));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void CanonicalEnvelope_UnknownDomainAndVersion_AreRejectedByCreateAndValidate()
    {
        Assert.Throws<ArgumentException>(() => CanonicalEnvelope.Create(
            "vulgata/story-12.2/file-code-unit-identity/9.9",
            Array.Empty<CanonicalField>()));
        Assert.Throws<ArgumentException>(() => CanonicalEnvelope.Create(
            "vulgata/example/unregistered/1.0",
            Array.Empty<CanonicalField>()));

        byte[] validControl = CanonicalEnvelope.Create(
            "vulgata/story-12.2/file-code-unit-identity/1.0",
            Array.Empty<CanonicalField>());
        Assert.True(CanonicalEnvelope.Validate(validControl));
        Assert.False(CanonicalEnvelope.Validate(
            Convert.FromHexString("564743450106000000046576696c0900000000")));
        Assert.False(CanonicalEnvelope.Validate(
            Convert.FromHexString("5647434501060000002e76756c676174612f73746f72792d31322e322f66696c652d636f64652d756e69742d6964656e746974792f392e390900000000")));
    }

    private static PreparedFileCodeUnitDraft CreateDraft(int manifestOrdinal, int pairOrdinal) =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
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
            new string('b', 64),
            new string('c', 64),
            800,
            800,
            PreparedSelectionReason.DirectBusinessEvidence,
            manifestOrdinal,
            pairOrdinal);
}
