using System.Reflection;
using System.Security.Cryptography;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public sealed class PreparedPass2WorkerContractAuthoritySource(
    PreparedRetrieveCodeLogicFunction retrieve,
    PreparedSubmitBusinessLogicFunction submit) : IPreparedPass2WorkerContractAuthority
{
    private const string PromptResource =
        "Vulgata.Agents.Prompts.pass2-business-logic-worker.v1.md";
    private const string SchemaResource =
        "Vulgata.Agents.Contracts.business-logic-flow-v1.schema.json";

    public PreparedPass2WorkerContractAuthority GetCurrent()
    {
        Assembly assembly = typeof(PreparedPass2WorkerContractAuthoritySource).Assembly;
        byte[] prompt = ReadSingle(assembly, PromptResource);
        byte[] schema = ReadSingle(assembly, SchemaResource);
        Pass2StrictDeclarationSet declarations = Pass2StrictToolDeclarations.Create(
            retrieve.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            submit.Create(static (_, _) => Task.FromResult(
                PreparedPass2CandidateDecision.Reject(
                    PreparedPass2SubmissionDisposition.InvalidSemantics))));
        return new(
            "pass2-business-logic-worker.v1",
            Fingerprint(prompt),
            "business-logic/flow-v1",
            Fingerprint(schema),
            Pass2StrictToolDeclarations.Identity,
            Fingerprint(declarations.ProjectedUtf8));
    }

    private static byte[] ReadSingle(Assembly assembly, string name)
    {
        string resource = assembly.GetManifestResourceNames().Single(value =>
            StringComparer.Ordinal.Equals(value, name));
        using Stream stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("The accepted Pass 2 artifact is unavailable.");
        using var destination = new MemoryStream();
        stream.CopyTo(destination);
        return destination.ToArray();
    }

    private static string Fingerprint(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
