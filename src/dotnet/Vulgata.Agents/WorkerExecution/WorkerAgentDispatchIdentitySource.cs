using System.Security.Cryptography;
using System.Text;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Agents.WorkerExecution;

public sealed class WorkerAgentDispatchIdentitySource : IScanRecoveryDispatchIdentitySource
{
    private const string _pass1IdentityPrefix = "pass1-code-logic-worker.v1/sha256/";

    public Task<ScanRecoveryDispatchIdentity?> GetAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        if (systemId == Guid.Empty)
        {
            throw new ArgumentException("System identity is required.", nameof(systemId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string prompt = WorkerAgentDispatcher.LoadPass1Prompt(
            typeof(WorkerAgentDispatcher).Assembly);
        string promptHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(prompt)));
        return Task.FromResult<ScanRecoveryDispatchIdentity?>(new(
            _pass1IdentityPrefix + promptHash,
            KnowledgeDocumentLimits.SchemaIdentity));
    }
}
