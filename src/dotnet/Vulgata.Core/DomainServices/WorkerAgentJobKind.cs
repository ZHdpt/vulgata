namespace Vulgata.Core.DomainServices;

/// <summary>
/// Closed allowlist of typed Worker Agent job kinds dispatched by the
/// Sequential Scan Runner. Story 11.2 defines only
/// <see cref="Pass1CodeLogicDocument"/>; Story 11.3 may add
/// <c>Pass2BusinessLogicDocument</c>. Unknown kinds are rejected before
/// dispatch.
/// </summary>
public enum WorkerAgentJobKind
{
    /// <summary>
    /// Pass 1 Code Logic Document generation job. The only kind the
    /// deadline runner dispatches in Story 11.2.
    /// </summary>
    Pass1CodeLogicDocument,
}
