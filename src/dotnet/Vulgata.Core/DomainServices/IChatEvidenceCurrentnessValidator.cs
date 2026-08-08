namespace Vulgata.Core.DomainServices;

public interface IChatEvidenceCurrentnessValidator
{
    Task<ChatCurrentnessValidationOutcome> ValidateCurrentAsync(
        ChatCurrentnessValidationRequest request,
        RepositoryAccessScope accessScope,
        CancellationToken cancellationToken = default);
}
