using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>Additional format-correction allowance; never replaces admission budgets.</summary>
public sealed class OctoberRunCorrectionBudget
{
    private readonly Lock _sync = new();
    private readonly KnowledgeBinding _binding;
    private readonly Guid _runId;
    private readonly Dictionary<Guid, FileAllowance> _files = [];
    private int _reserved, _started;

    public OctoberRunCorrectionBudget(KnowledgeBinding binding, Guid runId)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (runId == Guid.Empty) throw new ArgumentException("An original run is required.", nameof(runId));
        _binding = binding;
        _runId = runId;
    }

    internal bool Register(OctoberFileWorkerRequest request)
    {
        lock (_sync)
        {
            if (request.Binding != _binding || request.RunId != _runId ||
                request.FileWorkstreamId == Guid.Empty || request.AdmissionId == Guid.Empty || request.AttemptId == Guid.Empty ||
                _files.ContainsKey(request.FileWorkstreamId)) return false;
            _files.Add(request.FileWorkstreamId, new(request.AdmissionId, request.AttemptId));
            return true;
        }
    }

    internal bool TryReserve(OctoberFileWorkerRequest request)
    {
        lock (_sync)
        {
            if (Find(request) is not { } file || file.Reserved >= 2 || _reserved >= 16) return false;
            file.Reserved++;
            _reserved++;
            return true;
        }
    }

    internal int Remaining(OctoberFileWorkerRequest request)
    {
        lock (_sync) return Find(request) is { } file ? Math.Min(2 - file.Reserved, 16 - _reserved) : 0;
    }

    internal void RecordProviderCallStarted(OctoberFileWorkerRequest request)
    {
        lock (_sync)
        {
            if (Find(request) is not { } file || file.Started >= file.Reserved)
                throw new InvalidOperationException("An exact reserved correction is required.");
            file.Started++;
            _started++;
        }
    }

    public OctoberCorrectionBudgetSnapshot Read()
    {
        lock (_sync) return new(_reserved, _started);
    }

    private FileAllowance? Find(OctoberFileWorkerRequest request) =>
        request.Binding == _binding && request.RunId == _runId &&
        _files.TryGetValue(request.FileWorkstreamId, out var file) &&
        file.AdmissionId == request.AdmissionId && file.AttemptId == request.AttemptId ? file : null;

    private sealed class FileAllowance(Guid admissionId, Guid attemptId)
    {
        internal Guid AdmissionId { get; } = admissionId;
        internal Guid AttemptId { get; } = attemptId;
        internal int Reserved { get; set; }
        internal int Started { get; set; }
    }
}

/// <summary>Started counts entry to the provider client, not confirmed HTTP transmission.</summary>
public sealed record OctoberCorrectionBudgetSnapshot(int ReservedAttempts, int ProviderCallsStarted);
