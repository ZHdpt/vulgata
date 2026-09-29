using Vulgata.Tests.TestInfrastructure;
using System.Reflection;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2SchemaResourceTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void BusinessLogicFlowSchema_IsOneEmbeddedResource_AndMatchesAcceptedContract()
    {
        Assembly assembly = typeof(Pass2BusinessLogicSchemaContract).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("business-logic-flow-v1.schema.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(resources);
        Assert.Equal(Pass2BusinessLogicSchemaContract.ResourceName, resources[0]);
        using Stream stream = assembly.GetManifestResourceStream(resources[0])!;
        byte[] embedded = SHA256.HashData(ReadAll(stream));
        string contractPath = FindAcceptedContractPath();
        Assert.True(File.Exists(contractPath), $"Accepted contract not found: {contractPath}");
        byte[] accepted = SHA256.HashData(File.ReadAllBytes(contractPath));
        Assert.Equal(accepted, embedded);
    }

    private static string FindAcceptedContractPath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "specs", "013-exact-cross-repository-flow-pass2", "contracts",
                "business-logic-flow-v1.schema.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            "specs", "013-exact-cross-repository-flow-pass2", "contracts",
            "business-logic-flow-v1.schema.json");
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
