namespace Vulgata.Infrastructure.Data.October.Knowledge;

/// <summary>Host-owned authenticated protection with distinct dependency and consumed-content purposes.</summary>
public interface IOctoberKnowledgeDisclosureProtector
{
    string ProtectDependency(string plaintext);
    string UnprotectDependency(string protectedPayload);
    string ProtectConsumedContent(string plaintext);
    string UnprotectConsumedContent(string protectedPayload);
}
