namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed allowlist reserved for the payload of a <see cref="WorkerAgentResultEnvelope"/>
/// with <see cref="WorkerAgentResultOutcome.Valid"/>. Story 11.2 validates shape only
/// and does not consume any payload field; Story 11.3 supplies the LLM-backed content.
/// Ad-hoc fields are rejected; adding fields in Story 11.3 must reopen the allowlist
/// deliberately.
/// </summary>
public sealed class WorkerAgentResultPayload
{
    private WorkerAgentResultPayload()
    {
    }

    /// <summary>
    /// Reserved marker. Story 11.2 sets this to a fixed empty shape; Story 11.3
    /// will introduce typed payload fields by reopening the closed allowlist.
    /// </summary>
    public static WorkerAgentResultPayload Reserved { get; } = new();
}
