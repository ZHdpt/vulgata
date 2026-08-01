namespace Vulgata.Core.Entities;

/// <summary>
/// Closed failure-kind allowlist for a <see cref="ScanExecution"/> terminal
/// transition. Relational check constraint
/// <c>CK_ScanExecutions_TerminalReasonKind_Closed</c> permits only these
/// values.
/// </summary>
public enum ScanExecutionFailureKind
{
    /// <summary>The Worker Agent result failed schema
    /// validation.</summary>
    SchemaInvalid,

    /// <summary>The Worker Agent job returned an unhandled
    /// error.</summary>
    Error,

    /// <summary>The Worker Agent result exceeded a finite
    /// bound.</summary>
    Unbounded,

    /// <summary>A prerequisite for starting the Scan was missing (no approved
    /// handoff, missing Profiled Revision).</summary>
    MissingPrerequisite,

    /// <summary>An active Scan already exists for the Repository /
    /// ScanRun.</summary>
    DuplicateActiveScan,

    /// <summary>A prepared Code Unit identity was missing from the approved
    /// Pre-Scan Handoff.</summary>
    MissingCodeUnit,

    /// <summary>The typed job packet carried forbidden content (absolute host
    /// path, secret, raw-source carrier, unbounded
    /// diagnostic).</summary>
    ForbiddenContent,

    /// <summary>The typed job packet exceeded its finite UTF-8/count
    /// limits.</summary>
    OverLimitPacket,
}
