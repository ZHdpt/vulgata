using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Web.Data;

namespace Vulgata.Tests.PreparedScanning;

/// <summary>
/// T025 RED coverage for the dedicated Prepared Pass 1 contract-identity
/// authority. The expected v2 identity is a literal contract value; it is not
/// reconstructed by a production helper under test.
/// </summary>
public sealed class PreparedPass1ContractIdentitySourceTests
{
    private const string PreparedPass1SourceTypeName =
        "Vulgata.Agents.WorkerExecution.EmbeddedPreparedPass1ContractIdentitySource";

    private const string PreparedPass1PortTypeName =
        "Vulgata.Core.DomainServices.IPreparedPass1ContractIdentitySource";

    private const string ExpectedPreparedPass1PromptIdentity =
        "pass1-code-logic-worker.v2/sha256/010247bbb9d8c07c2bd60a17353e5a088f7b7ea5ab490647712027d762b30937";

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void DefaultPreparedPass1Composition_ExposesV2PromptAndDocumentSchemaIdentity()
    {
        Type corePort = RequireType(typeof(IPreparedScanInitiationStore).Assembly, PreparedPass1PortTypeName);
        Type sourceType = RequireType(typeof(WorkerAgentDispatchIdentitySource).Assembly, PreparedPass1SourceTypeName);
        Assert.True(corePort.IsInterface);
        Assert.NotNull(sourceType.GetInterface(PreparedPass1PortTypeName));

        object source = Activator.CreateInstance(sourceType)!;
        MethodInfo getCurrent = Assert.Single(sourceType.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "GetCurrent");
        object? identity = getCurrent.Invoke(source, null);
        Assert.NotNull(identity);

        Assert.Equal(
            ExpectedPreparedPass1PromptIdentity,
            identity!.GetType().GetProperty("PromptIdentity")?.GetValue(identity));
        Assert.Equal(
            "code-logic/file-v2",
            identity.GetType().GetProperty("DocumentSchemaIdentity")?.GetValue(identity));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void InitiationCoordinator_UsesDedicatedPreparedPass1PortAndNotLegacyRecoveryPort()
    {
        Type coordinator = typeof(PreparedScanCoordinator).Assembly.GetType(
            "Vulgata.Web.Data.PreparedScanInitiationCoordinator")!;
        ConstructorInfo constructor = Assert.Single(
            coordinator.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        string[] parameterNames = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
            .ToArray();

        Assert.Contains(PreparedPass1PortTypeName, parameterNames);
        Assert.DoesNotContain(
            "Vulgata.Core.DomainServices.IScanRecoveryDispatchIdentitySource",
            parameterNames);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LegacyRecoveryIdentity_RemainsV1AndKnowledgeDocumentSchemaAsync()
    {
        WorkerAgentDispatchIdentitySource source = new();
        ScanRecoveryDispatchIdentity? identity = await source.GetAsync(
            Guid.Parse("10000000-0000-0000-0000-000000000001"));

        Assert.NotNull(identity);
        Assert.Equal(
            "pass1-code-logic-worker.v1/sha256/ad32930ab6e0c0b4e05e4e8c1f3e4298082b2a648198015d31687b53ac8ac37f",
            identity.PromptIdentity);
        Assert.Equal(KnowledgeDocumentLimits.SchemaIdentity, identity.DocumentSchemaIdentity);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void AuthorizationRechecker_UsesWebIdentityAndMaterializedRepositorySeams()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "dotnet",
            "Vulgata.Web",
            "Data",
            "PreparedScanInitiationAuthorizationRechecker.cs"),
            Encoding.UTF8);

        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Vulgata.Infrastructure.Data", source, StringComparison.Ordinal);
        Assert.DoesNotContain("VulgataDbContext", source, StringComparison.Ordinal);
        Assert.Contains("UserManager<ApplicationUser>", source, StringComparison.Ordinal);
        Assert.Contains("ISystemRepository", source, StringComparison.Ordinal);
        Assert.Contains("IRepositoryRepository", source, StringComparison.Ordinal);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("invalid-utf8")]
    [InlineData("schema-inconsistent")]
    public void EmbeddedPreparedPass1Source_HostileResource_IsRejected(string scenario)
    {
        string resourceName = FindPreparedPass1ResourceName();
        string[] names = scenario switch
        {
            "missing" => [],
            "duplicate" => [resourceName, resourceName],
            _ => [resourceName],
        };
        byte[] content = scenario switch
        {
            "empty" => [],
            "invalid-utf8" => [0xC3, 0x28],
            "schema-inconsistent" => Encoding.UTF8.GetBytes(
                "`schemaIdentity` is exactly `knowledge-document/file-v1`.") ,
            _ => ReadPreparedPass1Resource(),
        };

        object source = InvokeWithResourceProvider(
            () => names,
            _ => new MemoryStream(content, writable: false));
        Exception exception = Assert.ThrowsAny<Exception>(() => InvokeGetCurrent(source));

        Assert.True(
            exception is InvalidOperationException or InvalidDataException or DecoderFallbackException,
            $"Unexpected hostile-resource exception: {exception.GetType().FullName}: {exception.Message}");
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void EmbeddedPreparedPass1Source_BindsExactDecodedTextSha256()
    {
        string resourceName = FindPreparedPass1ResourceName();
        object source = InvokeWithResourceProvider(
            () => [resourceName],
            _ => new MemoryStream(ReadPreparedPass1Resource(), writable: false));

        object identity = InvokeGetCurrent(source);
        Assert.Equal(
            ExpectedPreparedPass1PromptIdentity,
            identity.GetType().GetProperty("PromptIdentity")?.GetValue(identity));

        string decodedText = Encoding.UTF8.GetString(ReadPreparedPass1Resource());
        string expectedHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(decodedText)));
        Assert.Equal(
            $"pass1-code-logic-worker.v2/sha256/{expectedHash}",
            identity.GetType().GetProperty("PromptIdentity")?.GetValue(identity));
    }

    private static object InvokeWithResourceProvider(
        Func<string[]> names,
        Func<string, Stream?> stream)
    {
        Type sourceType = RequireType(
            typeof(WorkerAgentDispatchIdentitySource).Assembly,
            PreparedPass1SourceTypeName);
        ConstructorInfo? constructor = sourceType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(Func<string[]>), typeof(Func<string, Stream>)],
            modifiers: null);
        Assert.NotNull(
            constructor);
        try
        {
            return constructor!.Invoke([names, stream]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object InvokeGetCurrent(object source)
    {
        MethodInfo getCurrent = Assert.Single(
            source.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name == "GetCurrent");
        try
        {
            return getCurrent.Invoke(source, null)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static string FindPreparedPass1ResourceName()
    {
        string[] names = typeof(WorkerAgentDispatchIdentitySource).Assembly
            .GetManifestResourceNames()
            .Where(name => name.EndsWith(
                "pass1-code-logic-worker.v2.md",
                StringComparison.Ordinal))
            .ToArray();
        return Assert.Single(names);
    }

    private static byte[] ReadPreparedPass1Resource()
    {
        string resourceName = FindPreparedPass1ResourceName();
        using Stream stream = typeof(WorkerAgentDispatchIdentitySource).Assembly
            .GetManifestResourceStream(resourceName)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        Type? type = assembly.GetType(fullName);
        Assert.NotNull(type);
        return type!;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException(
            "Could not locate the repository root.");
    }
}
