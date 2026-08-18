namespace Vulgata.Tests.AnalysisRuntime;

internal sealed record TestProcessRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment);

internal sealed record TestProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class RecordingProcessLauncherDouble
{
    private readonly Queue<TestProcessResult> _results = new();
    private readonly List<TestProcessRequest> _requests = [];

    public IReadOnlyList<TestProcessRequest> Requests => _requests;

    public bool ObservedCancellation { get; private set; }

    public void Enqueue(TestProcessResult result) => _results.Enqueue(result);

    public Task<TestProcessResult> LaunchAsync(TestProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObserveCancellation(cancellationToken);

        _requests.Add(request);
        TestProcessResult result = _results.Count == 0
            ? new TestProcessResult(0, string.Empty, string.Empty)
            : _results.Dequeue();
        return Task.FromResult(result);
    }

    private void ObserveCancellation(CancellationToken cancellationToken)
    {
        ObservedCancellation = cancellationToken.IsCancellationRequested;
        cancellationToken.ThrowIfCancellationRequested();
    }
}
