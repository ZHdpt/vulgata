using Microsoft.Extensions.Logging;

namespace SP._1.WorkerAgentExecutionAdapter;

/// <summary>
/// Deterministic Orchestrator that manages superstep dispatch
/// Validates the architecture pattern: BackgroundService -> Orchestrator -> WorkerAgent executor
/// </summary>
public class OrchestratorHarness
{
    private readonly IWorkerAgentExecutor _executor;
    private readonly ILogger<OrchestratorHarness> _logger;
    private readonly List<JobTelemetry> _telemetry = [];

    public OrchestratorHarness(IWorkerAgentExecutor executor, ILogger<OrchestratorHarness> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public IReadOnlyList<JobTelemetry> Telemetry => _telemetry;

    /// <summary>
    /// Execute a superstep with multiple jobs in parallel, with retry logic
    /// </summary>
    public async Task<IReadOnlyList<WorkerAgentResult>> ExecuteSuperstepAsync(
        IReadOnlyList<WorkerAgentJob> jobs,
        int maxRetries = 1,
        CancellationToken ct = default)
    {
        _logger.LogInformation("[Orchestrator] Starting superstep with {JobCount} jobs", jobs.Count);

        var results = new List<WorkerAgentResult>();
        var tasks = jobs.Select(job => ExecuteWithRetryAsync(job, maxRetries, ct)).ToList();

        // Wait for all jobs to complete (parallel execution)
        var completedResults = await Task.WhenAll(tasks);
        results.AddRange(completedResults);

        _logger.LogInformation("[Orchestrator] Superstep completed. {SuccessCount}/{TotalCount} jobs succeeded",
            results.Count(r => r.Success), results.Count);

        return results;
    }

    private async Task<WorkerAgentResult> ExecuteWithRetryAsync(
        WorkerAgentJob job,
        int maxRetries,
        CancellationToken ct)
    {
        var attempt = 0;
        WorkerAgentResult? result = null;

        while (attempt <= maxRetries)
        {
            var startTime = DateTimeOffset.UtcNow;

            try
            {
                if (attempt > 0)
                {
                    _logger.LogWarning("[Job {JobId}] Retry attempt {Attempt}/{MaxRetries}",
                        job.JobId, attempt, maxRetries);
                }

                result = await _executor.ExecuteJobAsync(job, ct);

                if (result.Success)
                {
                    RecordTelemetry(job, startTime, result.ExecutionTime, "Succeeded");
                    return result;
                }

                // Job failed but didn't throw - record and retry
                _logger.LogWarning("[Job {JobId}] Execution returned failure: {Error}",
                    job.JobId, result.ErrorMessage);

                if (attempt == maxRetries)
                {
                    RecordTelemetry(job, startTime, result.ExecutionTime, "Failed", result.ErrorMessage);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Job {JobId}] Execution threw exception on attempt {Attempt}",
                    job.JobId, attempt + 1);

                if (attempt == maxRetries)
                {
                    // Final attempt failed
                    var endTime = DateTimeOffset.UtcNow;
                    RecordTelemetry(job, startTime, endTime - startTime, "Failed", ex.Message);

                    return new WorkerAgentResult(
                        JobId: job.JobId,
                        SymbolName: job.CodeUnit.SymbolName,
                        GeneratedDocument: "",
                        ExecutionTime: endTime - startTime,
                        Success: false,
                        ErrorMessage: $"Failed after {attempt + 1} attempts: {ex.Message}");
                }
            }

            attempt++;

            // Exponential backoff before retry
            if (attempt <= maxRetries)
            {
                var backoff = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
                _logger.LogDebug("[Job {JobId}] Backing off for {Backoff}ms before retry",
                    job.JobId, backoff.TotalMilliseconds);
                await Task.Delay(backoff, ct);
            }
        }

        // Should never reach here, but compiler needs it
        throw new InvalidOperationException($"Job {job.JobId} exhausted all retries");
    }

    private void RecordTelemetry(
        WorkerAgentJob job,
        DateTimeOffset startTime,
        TimeSpan duration,
        string status,
        string? errorMessage = null)
    {
        var telemetry = new JobTelemetry(
            JobId: job.JobId,
            FilePath: job.CodeUnit.FilePath,
            SymbolName: job.CodeUnit.SymbolName,
            StartTime: startTime,
            EndTime: startTime + duration,
            Duration: duration,
            Status: status,
            ErrorMessage: errorMessage);

        _telemetry.Add(telemetry);

        _logger.LogInformation(
            "[Telemetry] Job {JobId} | {SymbolName} | {Status} | {Duration}ms",
            telemetry.JobId,
            telemetry.SymbolName,
            telemetry.Status,
            telemetry.Duration.TotalMilliseconds);
    }
}
