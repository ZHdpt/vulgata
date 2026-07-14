using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace SP._1.WorkerAgentExecutionAdapter;

/// <summary>
/// Headless Worker Agent Executor using Microsoft.Agents.AI packages
/// Validates that we can execute agents without conversational TurnContext
/// </summary>
public interface IWorkerAgentExecutor
{
    Task<WorkerAgentResult> ExecuteJobAsync(WorkerAgentJob job, CancellationToken ct = default);
}

/// <summary>
/// Implementation using IChatClient from Microsoft.Extensions.AI
/// This demonstrates headless execution without TurnContext dependency
/// </summary>
public class HeadlessWorkerAgentExecutor : IWorkerAgentExecutor
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<HeadlessWorkerAgentExecutor> _logger;

    public HeadlessWorkerAgentExecutor(IChatClient chatClient, ILogger<HeadlessWorkerAgentExecutor> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<WorkerAgentResult> ExecuteJobAsync(WorkerAgentJob job, CancellationToken ct = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogInformation("[Job {JobId}] Starting execution for {SymbolName} in {FilePath}",
            job.JobId, job.CodeUnit.SymbolName, job.CodeUnit.FilePath);

        try
        {
            // Simulate LLM prompt construction (would use _chatClient in real scenario)
            var prompt = BuildPrompt(job);
            _logger.LogDebug("[Job {JobId}] Prompt constructed: {PromptLength} chars",
                job.JobId, prompt.Length);

            // Simulate agent execution with mock response
            // In real implementation: var response = await _chatClient.GetResponseAsync(prompt, ct);
            var mockResponse = await SimulateAgentExecutionAsync(job, ct);

            var endTime = DateTimeOffset.UtcNow;
            var duration = endTime - startTime;

            _logger.LogInformation("[Job {JobId}] Execution completed in {Duration}ms",
                job.JobId, duration.TotalMilliseconds);

            return new WorkerAgentResult(
                JobId: job.JobId,
                SymbolName: job.CodeUnit.SymbolName,
                GeneratedDocument: mockResponse,
                ExecutionTime: duration,
                Success: true);
        }
        catch (Exception ex)
        {
            var endTime = DateTimeOffset.UtcNow;
            var duration = endTime - startTime;

            _logger.LogError(ex, "[Job {JobId}] Execution failed after {Duration}ms",
                job.JobId, duration.TotalMilliseconds);

            return new WorkerAgentResult(
                JobId: job.JobId,
                SymbolName: job.CodeUnit.SymbolName,
                GeneratedDocument: "",
                ExecutionTime: duration,
                Success: false,
                ErrorMessage: ex.Message);
        }
    }

    private static string BuildPrompt(WorkerAgentJob job)
    {
        return $"""
            You are a code analysis agent. Analyze the following code unit and generate a documentation document.
            
            File: {job.CodeUnit.FilePath}
            Symbol: {job.CodeUnit.SymbolName}
            
            Source Code:
            {job.CodeUnit.SourceContent}
            
            Communication Pattern Catalog Context:
            {job.CatalogContext}
            
            Generate a structured document with:
            1. Summary
            2. Key responsibilities
            3. Dependencies
            4. Communication patterns detected
            """;
    }

    private static async Task<string> SimulateAgentExecutionAsync(WorkerAgentJob job, CancellationToken ct)
    {
        // Simulate processing time (200-500ms)
        var delay = Random.Shared.Next(200, 500);
        await Task.Delay(delay, ct);

        // Simulate a failure for job-2 to test retry logic
        if (job.JobId == "job-2")
        {
            throw new InvalidOperationException("Simulated agent execution failure");
        }

        // Return mock document
        return $"""
            # Document for {job.CodeUnit.SymbolName}
            
            ## Summary
            This is a mock generated document for {job.CodeUnit.SymbolName} in {job.CodeUnit.FilePath}.
            
            ## Key Responsibilities
            - Processes code units
            - Generates documentation
            
            ## Dependencies
            - Depends on CodeGraph analysis
            - Uses Communication Pattern Catalog
            
            ## Communication Patterns
            No cross-repo communication detected.
            """;
    }
}
