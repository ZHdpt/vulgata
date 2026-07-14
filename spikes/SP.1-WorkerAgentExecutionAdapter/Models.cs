namespace SP._1.WorkerAgentExecutionAdapter;

/// <summary>
/// Mock Code Unit for spike validation
/// </summary>
public record CodeUnit(string FilePath, string SymbolName, string SourceContent);

/// <summary>
/// Mock Worker Agent Job
/// </summary>
public record WorkerAgentJob(string JobId, CodeUnit CodeUnit, string CatalogContext);

/// <summary>
/// Mock Worker Agent Result
/// </summary>
public record WorkerAgentResult(
    string JobId,
    string SymbolName,
    string GeneratedDocument,
    TimeSpan ExecutionTime,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Execution telemetry for a single job
/// </summary>
public record JobTelemetry(
    string JobId,
    string FilePath,
    string SymbolName,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    TimeSpan Duration,
    string Status,
    string? ErrorMessage = null);
