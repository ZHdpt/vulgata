using System.Reflection;
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
        "pass1-code-logic-worker.v2/sha256/5b1e48e50b83c5d12b7916d72d1973f4984db8bf60d1fa62df5cb9f4bcd27e7b";

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task LegacyRecoveryIdentity_RemainsV1AndKnowledgeDocumentSchemaAsync()
    {
        WorkerAgentDispatchIdentitySource source = new();
        ScanRecoveryDispatchIdentity? identity = await source.GetAsync(
            Guid.Parse("10000000-0000-0000-0000-000000000001"));

        Assert.NotNull(identity);
        Assert.Equal(
            "pass1-code-logic-worker.v1/sha256/4f7afc804335ac7e7cc2c10aa6cb034af9d4cdad224e64493dd3f1589ebd6673",
            identity.PromptIdentity);
        Assert.Equal(KnowledgeDocumentLimits.SchemaIdentity, identity.DocumentSchemaIdentity);
    }

    [Fact]
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
