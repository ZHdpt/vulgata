using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices;

namespace Vulgata.Agents.WorkerExecution;

public enum PreparedPass2SubmissionDisposition
{
    Accepted,
    InvalidShape,
    InvalidBinding,
    InvalidEvidence,
    InvalidSemantics,
}

public sealed record PreparedPass2CandidateDecision(
    PreparedPass2SubmissionDisposition Disposition,
    ReadOnlyMemory<byte> CanonicalUtf8)
{
    public static PreparedPass2CandidateDecision Reject(
        PreparedPass2SubmissionDisposition disposition)
    {
        if (disposition == PreparedPass2SubmissionDisposition.Accepted)
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        return new(disposition, ReadOnlyMemory<byte>.Empty);
    }
}

/// <summary>
/// Creates the non-evidence finalization function. Candidate validation is a
/// local callback; the provider argument remains exactly one closed document.
/// </summary>
public sealed class PreparedSubmitBusinessLogicFunction
{
    public Type SessionContract => typeof(PreparedSubmitBusinessLogicSession);

    public PreparedSubmitBusinessLogicSession CreateSession(
        Func<ReadOnlyMemory<byte>, CancellationToken, Task<PreparedPass2CandidateDecision>>
            validateCandidateAsync) =>
        new(validateCandidateAsync);

    public AIFunction Create(
        Func<ReadOnlyMemory<byte>, CancellationToken, Task<PreparedPass2CandidateDecision>>
            validateCandidateAsync) =>
        CreateSession(validateCandidateAsync).Function;
}

public sealed class PreparedSubmitBusinessLogicSession
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly object _sync = new();
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken,
        Task<PreparedPass2CandidateDecision>> _validateCandidateAsync;
    private byte[]? _acceptedCanonicalUtf8;
    private int _submissionCount;
    private PreparedPass2SubmissionDisposition? _lastDisposition;

    internal PreparedSubmitBusinessLogicSession(
        Func<ReadOnlyMemory<byte>, CancellationToken,
            Task<PreparedPass2CandidateDecision>> validateCandidateAsync)
    {
        _validateCandidateAsync = validateCandidateAsync ??
            throw new ArgumentNullException(nameof(validateCandidateAsync));
        Function = CreateFunction();
    }

    public AIFunction Function { get; }

    public PreparedPass2SubmissionDisposition? LastDisposition
    {
        get
        {
            lock (_sync)
            {
                return _lastDisposition;
            }
        }
    }

    public bool IsAccepted
    {
        get
        {
            lock (_sync)
            {
                return _acceptedCanonicalUtf8 is not null;
            }
        }
    }

    public ReadOnlyMemory<byte> TakeAcceptedCanonicalUtf8()
    {
        lock (_sync)
        {
            if (_acceptedCanonicalUtf8 is null)
            {
                return ReadOnlyMemory<byte>.Empty;
            }

            byte[] result = [.. _acceptedCanonicalUtf8];
            CryptographicOperations.ZeroMemory(_acceptedCanonicalUtf8);
            _acceptedCanonicalUtf8 = null;
            return result;
        }
    }

    private AIFunction CreateFunction()
    {
        async Task<string> SubmitAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JsonElement document;
            lock (_sync)
            {
                _submissionCount++;
                if (_submissionCount > 2 || _acceptedCanonicalUtf8 is not null ||
                    arguments.Count != 1 ||
                    !arguments.TryGetValue("document", out object? value) ||
                    value is not JsonElement candidate ||
                    candidate.ValueKind != JsonValueKind.Object)
                {
                    return "invalid-shape";
                }

                document = candidate.Clone();
            }

            byte[] candidateUtf8 = _strictUtf8.GetBytes(document.GetRawText());
            try
            {
                PreparedPass2CandidateDecision decision =
                    await _validateCandidateAsync(candidateUtf8, cancellationToken);
                if (!Enum.IsDefined(decision.Disposition))
                {
                    return "invalid-semantics";
                }

                lock (_sync)
                {
                    _lastDisposition = decision.Disposition;
                }

                if (decision.Disposition != PreparedPass2SubmissionDisposition.Accepted ||
                    decision.CanonicalUtf8.IsEmpty)
                {
                    return decision.Disposition switch
                    {
                        PreparedPass2SubmissionDisposition.InvalidShape => "invalid-shape",
                        PreparedPass2SubmissionDisposition.InvalidBinding => "invalid-binding",
                        PreparedPass2SubmissionDisposition.InvalidEvidence => "invalid-evidence",
                        _ => "invalid-semantics",
                    };
                }

                lock (_sync)
                {
                    _acceptedCanonicalUtf8 = decision.CanonicalUtf8.ToArray();
                }

                return "accepted";
            }
            finally
            {
                CryptographicOperations.ZeroMemory(candidateUtf8);
            }
        }

        Func<AIFunctionArguments, CancellationToken, Task<string>> invoker = SubmitAsync;
        AIFunction inner = AIFunctionFactory.Create(
            invoker,
            new AIFunctionFactoryOptions
            {
                Name = "submit_business_logic",
                Description = "Submit the final Business Logic Document for complete local validation.",
                AdditionalProperties = new Dictionary<string, object?>
                {
                    ["strict"] = true,
                },
                ExcludeResultSchema = true,
            });
        return new SchemaBoundSubmitFunction(
            inner,
            Pass2BusinessLogicSchemaContract.LoadDocumentParametersSchema());
    }

    private sealed class SchemaBoundSubmitFunction(
        AIFunction inner,
        JsonElement jsonSchema) : AIFunction
    {
        public override string Name => inner.Name;
        public override string Description => inner.Description;
        public override IReadOnlyDictionary<string, object?> AdditionalProperties =>
            inner.AdditionalProperties;
        public override MethodInfo? UnderlyingMethod => inner.UnderlyingMethod;
        public override JsonSerializerOptions JsonSerializerOptions =>
            inner.JsonSerializerOptions;
        public override JsonElement JsonSchema { get; } = jsonSchema.Clone();
        public override JsonElement? ReturnJsonSchema => null;

        protected override ValueTask<object?> InvokeCoreAsync(
            AIFunctionArguments arguments,
            CancellationToken cancellationToken) =>
            inner.InvokeAsync(arguments, cancellationToken);
    }
}
