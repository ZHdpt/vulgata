using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

/// <summary>
/// Closed, sanitized rejection categories observable only during the current
/// Worker execution.
/// </summary>
public enum PreparedPass1SubmissionRejectionKind
{
    MissingSubmission,
    InvalidShape,
    InvalidBinding,
    InvalidEvidence,
    InvalidSemantics,
}

/// <summary>
/// Observes at most one closed submission-rejection category per execution.
/// Implementations must remain in memory and must not retain provider content.
/// </summary>
public interface IPreparedPass1SubmissionDiagnosticObserver
{
    IReadOnlyList<PreparedPass1SubmissionRejectionKind> Categories =>
        [];

    void Observe(PreparedPass1SubmissionRejectionKind category);
}

/// <summary>
/// Execution-scope in-memory observer used by provider qualification.
/// </summary>
public sealed class PreparedPass1SubmissionDiagnosticObserver
    : IPreparedPass1SubmissionDiagnosticObserver
{
    private readonly object _sync = new();
    private readonly List<PreparedPass1SubmissionRejectionKind> _categories = [];

    public IReadOnlyList<PreparedPass1SubmissionRejectionKind> Categories
    {
        get
        {
            lock (_sync)
            {
                return [.. _categories];
            }
        }
    }

    public void Observe(PreparedPass1SubmissionRejectionKind category)
    {
        lock (_sync)
        {
            _categories.Add(category);
        }
    }
}

/// <summary>
/// Creates the execution-local, non-evidence finalization function for one
/// prepared Pass 1 Worker execution.
/// </summary>
public sealed class PreparedSubmitCodeLogicFunction(
    IPreparedPass1SubmissionDiagnosticObserver? diagnosticObserver = null)
{
    public PreparedSubmitCodeLogicSession CreateSession(PreparedPass1WorkerPacket packet) =>
        CreateSession(packet, new());

    internal PreparedSubmitCodeLogicSession CreateSession(
        PreparedPass1WorkerPacket packet,
        PreparedPass1ReceiptProvenanceLedger receiptProvenance) =>
        new(packet, diagnosticObserver, receiptProvenance);
}

/// <summary>
/// Owns the bounded submission attempts and the sole canonical result captured
/// during one provider execution. No state from this session is durable.
/// </summary>
public sealed class PreparedSubmitCodeLogicSession
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly object _sync = new();
    private readonly PreparedPass1WorkerPacket _packet;
    private readonly IPreparedPass1SubmissionDiagnosticObserver? _diagnosticObserver;
    private readonly PreparedPass1ReceiptProvenanceLedger _receiptProvenance;
    private byte[]? _capturedCanonicalJson;
    private int _submissionCount;
    private bool _diagnosticReported;

    internal PreparedSubmitCodeLogicSession(
        PreparedPass1WorkerPacket packet,
        IPreparedPass1SubmissionDiagnosticObserver? diagnosticObserver,
        PreparedPass1ReceiptProvenanceLedger receiptProvenance)
    {
        _packet = packet ?? throw new ArgumentNullException(nameof(packet));
        _diagnosticObserver = diagnosticObserver;
        _receiptProvenance = receiptProvenance ??
            throw new ArgumentNullException(nameof(receiptProvenance));
        Function = CreateFunction();
    }

    public AIFunction Function { get; }

    public bool HasSubmission
    {
        get
        {
            lock (_sync)
            {
                return _submissionCount > 0;
            }
        }
    }

    public bool NavigationAllowed
    {
        get
        {
            lock (_sync)
            {
                return _submissionCount == 0;
            }
        }
    }

    public bool ShouldTerminate
    {
        get
        {
            lock (_sync)
            {
                return _capturedCanonicalJson is not null || _submissionCount >= 2;
            }
        }
    }

    public byte[]? TakeCapturedCanonicalJson()
    {
        lock (_sync)
        {
            if (_capturedCanonicalJson is null)
            {
                return null;
            }

            byte[] result = [.. _capturedCanonicalJson];
            CryptographicOperations.ZeroMemory(_capturedCanonicalJson);
            _capturedCanonicalJson = null;
            return result;
        }
    }

    internal void ReportMissingSubmission()
    {
        lock (_sync)
        {
            ReportRejection(PreparedPass1SubmissionRejectionKind.MissingSubmission);
        }
    }

    private AIFunction CreateFunction()
    {
        string Submit(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _submissionCount++;
                arguments.TryGetValue("document", out object? value);
                if (_submissionCount > 2 || _capturedCanonicalJson is not null ||
                    arguments.Count != 1 ||
                    value is not JsonElement document ||
                    document.ValueKind != JsonValueKind.Object)
                {
                    return Reject(PreparedPass1SubmissionRejectionKind.InvalidShape);
                }

                byte[] candidate = _strictUtf8.GetBytes(document.GetRawText());
                try
                {
                    PreparedPass1ValidationResult validation =
                        PreparedPass1ResultParser.Validate(_packet, candidate);
                    if (!validation.IsValid)
                    {
                        PreparedPass1SubmissionRejectionKind rejectionKind =
                            validation.FailureKind switch
                        {
                            PreparedPass1ValidationFailureKind.Shape =>
                                PreparedPass1SubmissionRejectionKind.InvalidShape,
                            PreparedPass1ValidationFailureKind.Binding =>
                                PreparedPass1SubmissionRejectionKind.InvalidBinding,
                            PreparedPass1ValidationFailureKind.Evidence =>
                                PreparedPass1SubmissionRejectionKind.InvalidEvidence,
                            PreparedPass1ValidationFailureKind.Semantics =>
                                PreparedPass1SubmissionRejectionKind.InvalidSemantics,
                            _ => throw new InvalidOperationException(
                                "Prepared Pass 1 validation returned an unknown failure kind."),
                        };
                        return Reject(rejectionKind);
                    }

                    if (!CandidateReceiptsBelongToExecution(validation.CanonicalJson!.Value))
                    {
                        return Reject(PreparedPass1SubmissionRejectionKind.InvalidEvidence);
                    }

                    _capturedCanonicalJson = validation.CanonicalJson!.Value.ToArray();
                    return "accepted";
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(candidate);
                }
            }
        }

        Func<AIFunctionArguments, CancellationToken, string> invoker = Submit;
        AIFunction inner = AIFunctionFactory.Create(
            invoker,
            new AIFunctionFactoryOptions
            {
                Name = "submit_code_logic",
                Description = "Submit the final Code Logic Document for complete local validation.",
                AdditionalProperties = new Dictionary<string, object?>
                {
                    ["strict"] = true,
                },
                ExcludeResultSchema = true,
            });
        return new ProjectedSubmitFunction(inner, DeepSeekStrictSchemaProjector.Build());
    }

    private bool CandidateReceiptsBelongToExecution(ReadOnlyMemory<byte> canonicalJson)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(canonicalJson);
            JsonElement root = document.RootElement;
            IEnumerable<Guid> receiptIds = root.GetProperty("communicationEvidence")
                .EnumerateArray()
                .SelectMany(value => value.GetProperty("navigationReceiptIds").EnumerateArray())
                .Concat(root.GetProperty("knownReferences").EnumerateArray()
                    .Select(value => value.GetProperty("navigationReceiptId")))
                .Concat(root.GetProperty("deferredObservations").EnumerateArray()
                    .SelectMany(value => value.GetProperty("navigationReceiptIds").EnumerateArray()))
                .Select(value => value.GetGuid());
            return receiptIds.All(_receiptProvenance.Contains);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return false;
        }
    }

    private string Reject(PreparedPass1SubmissionRejectionKind rejectionKind)
    {
        ReportRejection(rejectionKind);
        return rejectionKind switch
        {
            PreparedPass1SubmissionRejectionKind.InvalidShape => "invalid-shape",
            PreparedPass1SubmissionRejectionKind.InvalidBinding => "invalid-binding",
            PreparedPass1SubmissionRejectionKind.InvalidEvidence => "invalid-evidence",
            PreparedPass1SubmissionRejectionKind.InvalidSemantics => "invalid-semantics",
            _ => throw new InvalidOperationException(
                "Prepared Pass 1 submission rejection kind is not tool-reportable."),
        };
    }

    private void ReportRejection(
        PreparedPass1SubmissionRejectionKind rejectionKind)
    {
        if (_diagnosticReported)
        {
            return;
        }

        _diagnosticReported = true;
        _diagnosticObserver?.Observe(rejectionKind);
    }

    private sealed class ProjectedSubmitFunction(
        AIFunction inner,
        DeepSeekStrictSchemaProjection projection) : AIFunction
    {
        private readonly JsonElement _jsonSchema =
            JsonDocument.Parse(projection.ProjectedSchemaJson).RootElement.Clone();

        public override string Name => inner.Name;
        public override string Description => inner.Description;
        public override IReadOnlyDictionary<string, object?> AdditionalProperties =>
            inner.AdditionalProperties;
        public override System.Reflection.MethodInfo? UnderlyingMethod => inner.UnderlyingMethod;
        public override JsonSerializerOptions JsonSerializerOptions => inner.JsonSerializerOptions;
        public override JsonElement JsonSchema => _jsonSchema;
        public override JsonElement? ReturnJsonSchema => null;

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken) =>
            inner.InvokeAsync(arguments, cancellationToken);
    }
}
