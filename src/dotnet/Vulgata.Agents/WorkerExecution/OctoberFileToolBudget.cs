namespace Vulgata.Agents.WorkerExecution;

/// <summary>Host-owned accounting shared by all attempts of one primary file.</summary>
public sealed class OctoberFileToolBudget
{
    private readonly Lock _sync = new();
    private readonly Guid _runId, _workstreamId, _admissionId;
    private readonly int? _advisory;
    private readonly int _maximum;
    private int _requested, _admitted, _executed;

    public OctoberFileToolBudget(Guid runId, Guid fileWorkstreamId, Guid admissionId, int? advisory, int maximum)
    {
        if (runId == Guid.Empty || fileWorkstreamId == Guid.Empty || admissionId == Guid.Empty ||
            maximum is < 0 or > 1000 || advisory is { } threshold && (threshold <= 0 || threshold >= maximum))
            throw new ArgumentException("A bounded primary-file tool budget is required.");
        _runId = runId; _workstreamId = fileWorkstreamId; _admissionId = admissionId;
        _advisory = advisory; _maximum = maximum;
    }

    internal bool Matches(OctoberFileWorkerRequest request)
        => request.RunId == _runId && request.FileWorkstreamId == _workstreamId && request.AdmissionId == _admissionId &&
            request.Limits.MaximumToolCalls == _maximum && request.Limits.AdvisoryToolCalls == _advisory;

    internal bool TryAdmit()
    {
        lock (_sync)
        {
            _requested++;
            if (_admitted >= _maximum) return false;
            _admitted++;
            return true;
        }
    }

    internal void RecordExecution()
    {
        lock (_sync) _executed++;
    }

    public OctoberFileToolBudgetSnapshot Read()
    {
        lock (_sync) return new(_advisory, _maximum, _requested, _admitted, _executed,
            _maximum - _admitted, _advisory is { } threshold && _admitted >= threshold);
    }
}

public sealed record OctoberFileToolBudgetSnapshot(int? AdvisoryLimit, int MaximumLimit,
    int RequestedCalls, int AdmittedCalls, int ExecutedCalls, int RemainingCalls, bool AdvisoryReached);
