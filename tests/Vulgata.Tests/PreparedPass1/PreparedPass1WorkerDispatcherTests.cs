using System.Reflection;
using Vulgata.Core.DomainServices;
using Vulgata.Tests.PreparedPass1.Fixtures;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// Story 12.3 T013 RED boundary for the prepared Pass 1 dispatcher.
///
/// The dispatcher and its packet DTO are intentionally obtained through the
/// accepted public seam.  This keeps the RED test compilable before T014 adds
/// the new prepared path and prevents it from coupling to a private provider
/// or to the legacy SequentialScanRunner dispatcher.
/// </summary>
public sealed class PreparedPass1WorkerDispatcherTests
{
    private const string DispatcherPortName =
        "Vulgata.Core.DomainServices.IPreparedPass1WorkerDispatcher";

    private const string DispatcherImplementationName =
        "Vulgata.Agents.WorkerExecution.PreparedPass1WorkerDispatcher";

    [Fact]
    public void PreparedDispatcher_ExposesDedicatedPortAndServerOwnedDispatchLoop()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        Type implementation = RequireType(
            DispatcherImplementationName,
            LoadAgentsAssembly());

        Assert.True(
            port.IsInterface,
            "The prepared Pass 1 dispatcher must be a dedicated Core port.");
        Assert.True(
            port.IsAssignableFrom(implementation),
            "The Agents implementation must implement the dedicated prepared port.");

        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Assert.Equal(typeof(Task<>), dispatch.ReturnType.GetGenericTypeDefinition());
        Assert.Contains(
            dispatch.GetParameters(),
            parameter => parameter.ParameterType == typeof(CancellationToken));

        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;
        Assert.NotEqual(
            typeof(WorkerAgentJobPacket),
            requestType);
        Assert.DoesNotContain(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreparedDispatcher_RequestBindsPreparedUnitsSnapshotAndClassificationProjection()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;
        PropertyInfo[] properties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Contains(
            properties,
            property => property.Name is "PreparedFileCodeUnits" or "FileCodeUnits");
        Assert.Contains(
            properties,
            property => property.Name.Contains("ContextSnapshot", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Classification", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("ProfiledRevision", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Repository", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("ScanRun", StringComparison.Ordinal));
        Assert.Contains(
            properties,
            property => property.Name.Contains("Concurrency", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedDispatcher_PacketProjectionCarriesExactUnitIdentityAndNoCompletionOrder()
    {
        ApprovedPreparedManifestPairSeed pair =
            PreparedPass1ManifestPairFixtures.CreateApprovedPair();
        int preparedUnitCount = pair.Manifests.Sum(manifest => manifest.FileCodeUnits.Count);
        Assert.Equal(4, preparedUnitCount);
        Assert.Equal(
            pair.Manifests.SelectMany(manifest => manifest.FileCodeUnits)
                .Select(unit => unit.IdentityFingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            preparedUnitCount);

        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type resultType = dispatch.ReturnType.GetGenericArguments().Single();
        PropertyInfo packetCollection = Assert.Single(
            resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name is "Packets" or "Jobs" or "DispatchPackets");
        Type packetType = packetCollection.PropertyType
            .GetInterfaces()
            .Append(packetCollection.PropertyType)
            .Where(type => type.IsGenericType)
            .SelectMany(type => type.GetGenericArguments())
            .FirstOrDefault(type => type != typeof(Guid))
            ?? packetCollection.PropertyType.GetElementType()
            ?? throw new InvalidOperationException(
                "The prepared dispatch result must expose a typed packet collection.");

        PropertyInfo[] packetProperties = packetType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        Assert.Contains(packetProperties, property => property.Name == "JobId");
        Assert.Contains(packetProperties, property => property.Name == "FileCodeUnitId");
        Assert.Contains(packetProperties, property => property.Name == "RepositoryId");
        Assert.Contains(packetProperties, property => property.Name == "ScanRunId");
        Assert.Contains(packetProperties, property => property.Name == "ProfiledRevision");
        Assert.Contains(packetProperties, property => property.Name.Contains("SourceHash", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("FileHash", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("ManifestOrdinal", StringComparison.Ordinal));
        Assert.Contains(packetProperties, property => property.Name.Contains("PairSelectionOrdinal", StringComparison.Ordinal));
        Assert.DoesNotContain(
            packetProperties,
            property => property.Name.Contains("Completion", StringComparison.Ordinal) ||
                property.Name.Contains("FinishedAt", StringComparison.Ordinal));
    }

    [Fact]
    public void PreparedDispatcher_UsesSharedCapacityAndDoesNotExposeNavigationScheduling()
    {
        Type port = RequireType(DispatcherPortName, typeof(WorkerAgentJobPacket).Assembly);
        MethodInfo dispatch = Assert.Single(
            port.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "DispatchAsync");
        Type requestType = dispatch.GetParameters()
            .Single(parameter => parameter.ParameterType != typeof(CancellationToken))
            .ParameterType;

        PropertyInfo[] requestProperties = requestType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo concurrency = Assert.Single(
            requestProperties,
            property => property.Name.Contains("Concurrency", StringComparison.Ordinal));
        Assert.True(
            concurrency.PropertyType == typeof(int) || concurrency.PropertyType == typeof(int?),
            "The shared dispatch capacity must be a bounded integer setting.");

        Type implementation = RequireType(
            DispatcherImplementationName,
            LoadAgentsAssembly());
        Assert.DoesNotContain(
            implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Schedule", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Navigate", StringComparison.OrdinalIgnoreCase) ||
                method.Name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase));
    }

    private static Type RequireType(string fullName, Assembly assembly) =>
        assembly.GetType(fullName, throwOnError: false, ignoreCase: false)
        ?? throw new InvalidOperationException(
            $"T014 production seam is missing: {fullName}.");

    private static Assembly LoadAgentsAssembly() =>
        Assembly.Load("Vulgata.Agents");
}
