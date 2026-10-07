using Microsoft.AspNetCore.DataProtection;
using Vulgata.Infrastructure.Data.October.Knowledge;

namespace Vulgata.Web.Data;

/// <summary>Uses the existing host key provider; it creates no key ring or persistence configuration.</summary>
public sealed class OctoberKnowledgeDisclosureDataProtectionProtector : IOctoberKnowledgeDisclosureProtector
{
    private readonly IDataProtector _dependencies;
    private readonly IDataProtector _consumedContent;

    public OctoberKnowledgeDisclosureDataProtectionProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _dependencies = provider.CreateProtector("Vulgata.October.Knowledge.Disclosure.v1", "dependencies");
        _consumedContent = provider.CreateProtector("Vulgata.October.Knowledge.Disclosure.v1", "consumed-content");
    }

    public string ProtectDependency(string plaintext) => _dependencies.Protect(plaintext);
    public string UnprotectDependency(string protectedPayload) => _dependencies.Unprotect(protectedPayload);
    public string ProtectConsumedContent(string plaintext) => _consumedContent.Protect(plaintext);
    public string UnprotectConsumedContent(string protectedPayload) => _consumedContent.Unprotect(protectedPayload);
}
