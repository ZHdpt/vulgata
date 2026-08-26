namespace Vulgata.Core.DomainServices;

public sealed record PreparedPass2WorkerContractAuthority(
    string PromptIdentity,
    string PromptFingerprint,
    string SubmissionSchemaIdentity,
    string SubmissionSchemaFingerprint,
    string ToolDeclarationIdentity,
    string ToolDeclarationFingerprint);

public interface IPreparedPass2WorkerContractAuthority
{
    PreparedPass2WorkerContractAuthority GetCurrent();
}
