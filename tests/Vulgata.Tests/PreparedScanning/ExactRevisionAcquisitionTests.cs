using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using Vulgata.Infrastructure.Git;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T013 RED coverage for the Story 12.2 revision-acquisition seam.
/// Reflection is intentional: the adapter is the production work owned by
/// T014 and is not present while this public-seam test is authored.
/// </summary>
public sealed class ExactRevisionAcquisitionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedRevisionAcquirer_ExposesOnePublicExactAcquisitionPort()
    {
        Assembly assembly = typeof(ExactGitRevisionSnapshotService).Assembly;
        Type acquirer = RequirePublicType(
            assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRevisionAcquirer");

        MethodInfo[] acquisitionMethods = acquirer
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "AcquireAsync")
            .ToArray();

        MethodInfo method = Assert.Single(acquisitionMethods);
        Assert.True(
            typeof(Task).IsAssignableFrom(method.ReturnType),
            "Exact acquisition must be asynchronous and cancellation-aware.");
        ParameterInfo[] parameters = method.GetParameters();
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(CancellationToken));
        Assert.Contains(parameters, parameter => parameter.ParameterType == typeof(string));
        Assert.True(
            parameters.Count(parameter => parameter.ParameterType == typeof(string)) >= 2,
            "The port must receive both the full Git reference and exact commit.");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void PreparedRevisionAcquirer_DoesNotExposeOrdinaryCheckoutOrUnboundedRevisionPorts()
    {
        Type acquirer = RequirePublicType(
            typeof(ExactGitRevisionSnapshotService).Assembly,
            "Vulgata.Infrastructure.Git.PreparedScanRevisionAcquirer");

        Assert.DoesNotContain(
            acquirer.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("WorkingCopy", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            acquirer.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(string) &&
                parameter.Name is "revision" or "objectId"));
    }

    private static Type RequirePublicType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        Assert.True(type!.IsPublic, $"{fullName} must be a public production seam.");
        Assert.False(type.IsAbstract, $"{fullName} must be constructible by the preparation composition root.");
        return type;
    }
}
