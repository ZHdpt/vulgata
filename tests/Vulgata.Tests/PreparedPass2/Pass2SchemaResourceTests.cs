using System.Reflection;
using System.Security.Cryptography;

namespace Vulgata.Tests.PreparedPass2;

public sealed class Pass2SchemaResourceTests
{
    private const string ResourceName =
        "Vulgata.Agents.Contracts.business-logic-flow-v1.schema.json";

    [Fact]
    public void BusinessLogicFlowSchema_IsOneEmbeddedResource_AndMatchesAcceptedContract()
    {
        Assembly assembly = typeof(Vulgata.Agents.WorkerExecution.WorkerAgentDispatcher).Assembly;
        string[] resources = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith("business-logic-flow-v1.schema.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Single(resources);
        Assert.Equal(ResourceName, resources[0]);
        using Stream stream = assembly.GetManifestResourceStream(resources[0])!;
        byte[] embedded = SHA256.HashData(ReadAll(stream));
        string contractPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "specs", "013-exact-cross-repository-flow-pass2", "contracts",
            "business-logic-flow-v1.schema.json"));
        Assert.True(File.Exists(contractPath), $"Accepted contract not found: {contractPath}");
        byte[] accepted = SHA256.HashData(File.ReadAllBytes(contractPath));
        Assert.Equal(accepted, embedded);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
