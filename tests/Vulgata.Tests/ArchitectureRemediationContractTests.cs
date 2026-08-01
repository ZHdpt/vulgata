using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.FrameworkDetection;
using Vulgata.Web.Data;

namespace Vulgata.Tests;

public sealed class ArchitectureRemediationContractTests
{
    [Fact]
    public void FrameworkEvidenceDetector_PublicSurface_HasNoPathOrSnapshotSourceBypass()
    {
        MethodInfo[] pathBasedMethods = typeof(FrameworkEvidenceDetector)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method =>
                method.GetParameters().FirstOrDefault()?.ParameterType == typeof(string))
            .ToArray();
        ConstructorInfo[] snapshotSourceConstructors = typeof(FrameworkEvidenceDetector)
            .GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType.FullName ==
                    "Vulgata.Infrastructure.FrameworkDetection.ICommittedRepositorySnapshotSource"))
            .ToArray();

        Assert.Empty(pathBasedMethods);
        Assert.Empty(snapshotSourceConstructors);
    }

    [Fact]
    public void PublicConstructors_AllRequireCommittedSnapshotService()
    {
        ConstructorInfo[] constructors = typeof(RepositoryProfilingCoordinator)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.NotEmpty(constructors);
        Assert.All(
            constructors,
            constructor => Assert.Contains(
                constructor.GetParameters(),
                parameter =>
                    parameter.ParameterType == typeof(ICommittedRepositorySnapshotService)));
    }

    [Fact]
    public void PublicConstructors_AllRequirePreScanCommunicationDetector()
    {
        ConstructorInfo[] constructors = typeof(RepositoryProfilingCoordinator)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);

        Assert.NotEmpty(constructors);
        Assert.All(
            constructors,
            constructor => Assert.Contains(
                constructor.GetParameters(),
                parameter =>
                    parameter.ParameterType == typeof(IPreScanCommunicationDetector)));
    }

    [Fact]
    public void PublicConstructors_BypassAdapters_AreNotDiscoverable()
    {
        Type[] implementationTypes = typeof(RepositoryProfilingCoordinator)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);

        Assert.DoesNotContain(
            implementationTypes,
            type => type.Name is
                "EmptyPreScanCommunicationDetector" or
                "MissingCommittedRepositorySnapshotService");
    }

    [Fact]
    public void FinalPersistenceContract_OnlyCompositeCommitIsExposed()
    {
        MethodInfo[] finalPersistenceMethods = typeof(IScanRunRepository)
            .GetMethods()
            .Where(method => method.Name == nameof(IScanRunRepository.PersistCompletedProfileAsync))
            .ToArray();

        MethodInfo finalPersistenceMethod = Assert.Single(finalPersistenceMethods);
        ParameterInfo[] parameters = finalPersistenceMethod.GetParameters();
        Assert.Equal(typeof(Task<PreScanResult>), finalPersistenceMethod.ReturnType);
        Assert.Equal(typeof(RepositoryProfileCommitDraft), parameters[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }
}
