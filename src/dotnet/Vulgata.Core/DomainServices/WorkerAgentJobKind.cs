namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed allowlist of typed Worker Agent job kinds dispatched by the
/// Sequential Scan Runner. The accepted demo supports exactly the Pass 1 and
/// prepared Pass 2 variants. Unknown kinds are rejected before dispatch.
/// </summary>
public enum WorkerAgentJobKind
{
    /// <summary>
    /// Pass 1 Code Logic Document generation job. The only kind the
    /// deadline runner dispatches in Story 11.2.
    /// </summary>
    Pass1CodeLogicDocument,

    /// <summary>
    /// Prepared Pass 2 Business Logic Document generation job.
    /// </summary>
    Pass2BusinessLogicDocument,
}
