using Vulgata.Tests.TestInfrastructure;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Tests.PreparedPass1;

/// <summary>
/// T150 RED: AD-28 must project the existing FICC history into a bounded
/// terminal request. These tests are source-free and use only the approved
/// execution-local seams; the current compile scaffold is intentionally
/// nonfunctional, so the behavioral assertions must fail before T151.
/// </summary>
public sealed class Pass1DeepSeekTerminalContextProjectionTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_UsesDerivedInstructionExactPacketAndCanonicalServerContext()
    {
        byte[] packet = Encoding.UTF8.GetBytes("{\"jobId\":\"bounded-job\",\"sourceHash\":\"abc\"}");
        PreparedPass1TerminalContextProjectionResult projection =
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(packet, [CreateResult(1)], 10));

        Assert.True(projection.IsValid);
        Assert.Equal(
            "terminal preamble\n\nterminal common\n",
            projection.TerminalInstruction);
        Assert.Equal(3, projection.Messages.Count);
        Assert.Equal(ChatRole.System, projection.Messages[0].Role);
        Assert.Equal(
            projection.TerminalInstruction,
            projection.Messages[0].Text);
        Assert.Equal(ChatRole.User, projection.Messages[1].Role);
        Assert.Equal(
            packet,
            Encoding.UTF8.GetBytes(projection.Messages[1].Text));
        Assert.Equal(ChatRole.User, projection.Messages[2].Role);

        using JsonDocument context = JsonDocument.Parse(projection.ContextJson);
        Assert.Equal(
            "pass1-terminal-context/1",
            context.RootElement.GetProperty("schemaIdentity").GetString());
        Assert.Single(context.RootElement.GetProperty("evidenceResults").EnumerateArray());
        Assert.Equal(
            JsonValueKind.Null,
            context.RootElement.GetProperty("firstSubmissionCorrection").ValueKind);

        string messages = string.Join("\n", projection.Messages.Select(message => message.Text));
        Assert.DoesNotContain("assistant", messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tool_calls", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("navigate_code", messages, StringComparison.Ordinal);
        Assert.DoesNotContain("reasoning", messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("candidate", messages, StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_ProjectsOnlyClosedFirstSubmissionCorrectionCategory()
    {
        PreparedPass1TerminalContextProjectionResult projection =
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1), CreateResult(2)],
                    4,
                    new(
                        1,
                        PreparedPass1SubmissionRejectionKind.InvalidShape,
                        PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)));

        Assert.True(projection.IsValid);
        using JsonDocument context = JsonDocument.Parse(projection.ContextJson);
        Assert.Equal(
            ["schemaIdentity", "evidenceResults", "firstSubmissionCorrection"],
            context.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        JsonElement correction = context.RootElement.GetProperty("firstSubmissionCorrection");
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            correction.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, correction.GetProperty("attempt").GetInt32());
        Assert.Equal("InvalidShape", correction.GetProperty("category").GetString());
        Assert.Equal(
            "UseMinimalValidDocument",
            correction.GetProperty("repairCode").GetString());
        Assert.DoesNotContain("candidate", correction.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bytes", correction.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("arguments", correction.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validator", correction.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidShape,
        PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidSemantics,
        PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidBinding,
        PreparedPass1SubmissionRepairCode.CopyPacketBinding)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidEvidence,
        PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor)]
    public void Create_ProjectsExactCategoryDerivedRepairCodeInCanonicalOrder(
        PreparedPass1SubmissionRejectionKind category,
        PreparedPass1SubmissionRepairCode repairCode)
    {
        PreparedPass1TerminalContextProjectionResult projection =
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    4,
                    new(1, category, repairCode)));

        using JsonDocument context = JsonDocument.Parse(projection.ContextJson);
        JsonElement correction = context.RootElement.GetProperty("firstSubmissionCorrection");
        Assert.Equal(
            ["attempt", "category", "repairCode"],
            correction.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(1, correction.GetProperty("attempt").GetInt32());
        Assert.Equal(category.ToString(), correction.GetProperty("category").GetString());
        Assert.Equal(repairCode.ToString(), correction.GetProperty("repairCode").GetString());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidShape,
        PreparedPass1SubmissionRepairCode.CopyPacketBinding)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidSemantics,
        PreparedPass1SubmissionRepairCode.CopyPacketBinding)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidBinding,
        PreparedPass1SubmissionRepairCode.UseAuthorizedFullRangeAnchor)]
    [InlineData(
        PreparedPass1SubmissionRejectionKind.InvalidEvidence,
        PreparedPass1SubmissionRepairCode.UseMinimalValidDocument)]
    public void Create_RejectsMismatchedCategoryRepairCodeBeforeWireSend(
        PreparedPass1SubmissionRejectionKind category,
        PreparedPass1SubmissionRepairCode repairCode)
    {
        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    4,
                    new(1, category, repairCode))));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsUnknownRepairCodeBeforeWireSend()
    {
        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    4,
                    new(
                        1,
                        PreparedPass1SubmissionRejectionKind.InvalidShape,
                        (PreparedPass1SubmissionRepairCode)999))));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsMissingSubmissionAsTerminalCorrectionCategory()
    {
        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    4,
                    new(1, PreparedPass1SubmissionRejectionKind.MissingSubmission))));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsCorrectionAttemptsOtherThanTheFirstSubmission()
    {
        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    4,
                    new(2, PreparedPass1SubmissionRejectionKind.InvalidShape))));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_ProjectsTheFixedBudgetExhaustionResultAsAnExactString()
    {
        PreparedPass1TerminalContextProjectionResult projection =
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [PreparedPass1TerminalContextResult.FromNavigationBudgetExhausted(1)],
                    10));

        Assert.True(projection.IsValid);
        using JsonDocument context = JsonDocument.Parse(projection.ContextJson);
        JsonElement result = context.RootElement
            .GetProperty("evidenceResults")[0]
            .GetProperty("result");
        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("navigation-budget-exhausted", result.GetString());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(10, true)]
    [InlineData(4, true)]
    [InlineData(9, true)]
    public void ShouldProject_AlignsTurnTenAndEarlyCorrectionTriggers(
        int providerTurn,
        bool expected)
    {
        PreparedPass1TerminalContextCorrection? correction = providerTurn == 4
            ? new(1, PreparedPass1SubmissionRejectionKind.InvalidShape)
            : null;

        Assert.Equal(
            expected,
            PreparedPass1TerminalContextProjection.ShouldProject(providerTurn, correction));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SerializeInvokedResult_PreservesTypedNavigationResponseFields()
    {
        string serialized = PreparedPass1TerminalContextProjection.SerializeInvokedResult(
            CreateNavigationResponse());

        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.True(root.TryGetProperty("outcome", out _));
        Assert.True(root.TryGetProperty("receiptId", out _));
        Assert.True(root.TryGetProperty("results", out JsonElement results));
        Assert.Single(results.EnumerateArray());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_CapturesTypedInvokedResultUsingFunctionSerializerOptionsBeforeSourceMutation()
    {
        byte[] mutableSourceHash = Enumerable.Repeat((byte)0x11, 32).ToArray();
        PreparedPass1NavigationResponse response = new(
            PreparedPass1NavigationOutcome.DocumentEvidence,
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            PreparedPass1NavigationOperation.Definition,
            1,
            PreparedPass1NavigationBoundKind.Calls,
            1,
            32,
            [
                new PreparedPass1NavigationResult(
                    "src/Mutable.cs",
                    Guid.Parse("20000000-0000-4000-8000-000000000002"),
                    mutableSourceHash,
                    [Guid.Parse("30000000-0000-4000-8000-000000000003")],
                    [Guid.Parse("40000000-0000-4000-8000-000000000004")],
                    "Process",
                    new PreparedPass1LineRange(10, 12),
                    new PreparedPass1ByteRange(100, 140),
                    "src/Mutable.cs:10-12"),
            ]);
        AIFunction navigate = new PreparedNavigateCodeFunction(new ThrowingNavigationService()).Create(
            new(
                Guid.Parse("50000000-0000-4000-8000-000000000005"),
                1,
                Guid.Parse("60000000-0000-4000-8000-000000000006")));
        PreparedPass1TerminalContextResult capturedResult =
            PreparedPass1TerminalContextResult.FromNavigationResponse(1, response);
        JsonElement expected = JsonSerializer.SerializeToElement(
            response,
            navigate.JsonSerializerOptions);

        mutableSourceHash[0] = 0xEE;

        PreparedPass1TerminalContextProjectionResult projection =
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [capturedResult],
                    10));

        using JsonDocument context = JsonDocument.Parse(projection.ContextJson);
        JsonElement captured = context.RootElement.GetProperty("evidenceResults")[0]
            .GetProperty("result");
        string capturedBeforeMutation = captured.GetRawText();
        Assert.True(JsonElement.DeepEquals(expected, captured));
        Assert.Equal(
            JsonSerializer.Serialize(
                PreparedPass1NavigationOutcome.DocumentEvidence,
                navigate.JsonSerializerOptions),
            captured.GetProperty("outcome").GetRawText());
        Assert.Equal(
            Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray()),
            captured.GetProperty("results")[0].GetProperty("sourceHash").GetString());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsCanonicalTerminalContextAbove393216Utf8Bytes()
    {
        const int resultCount = 72;
        string oversizedCanonicalPath = "src/" + new string('a', 6000) + ".cs";
        Assert.True(resultCount * oversizedCanonicalPath.Length > 393216);
        PreparedPass1TerminalContextResult[] results = Enumerable.Range(1, resultCount)
            .Select(order => PreparedPass1TerminalContextResult.FromNavigationResponse(
                order,
                new(
                    PreparedPass1NavigationOutcome.DocumentEvidence,
                    Guid.Parse("10000000-0000-4000-8000-000000000001"),
                    PreparedPass1NavigationOperation.Definition,
                    1,
                    PreparedPass1NavigationBoundKind.Calls,
                    1,
                    32,
                    [
                        new PreparedPass1NavigationResult(
                            oversizedCanonicalPath,
                            Guid.Parse("20000000-0000-4000-8000-000000000002"),
                            new byte[32],
                            [Guid.Parse("30000000-0000-4000-8000-000000000003")],
                            [Guid.Parse("40000000-0000-4000-8000-000000000004")],
                            "Process",
                            new PreparedPass1LineRange(10, 12),
                            new PreparedPass1ByteRange(100, 140),
                            oversizedCanonicalPath + ":10-12"),
                    ])))
            .ToArray();

        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    results,
                    10)));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsCrInsideMarkerDerivedResourceEagerly()
    {
        string prompt = CreatePrompt().Replace(
            "terminal preamble",
            "terminal\r\npreamble",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(
                    Encoding.UTF8.GetBytes("{\"packet\":true}"),
                    [CreateResult(1)],
                    10) with
                {
                    PromptResource = prompt,
                }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void SerializeFixedResult_UsesOnlyTheExactBudgetExhaustionString()
    {
        Assert.Equal(
            "\"navigation-budget-exhausted\"",
            PreparedPass1TerminalContextProjection.SerializeFixedResult());
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("unrelated-result")]
    public void Create_RejectsUnsupportedStringEvidenceValues(string value)
    {
        PreparedPass1TerminalContextProjectionRequest request = CreateRequest(
            Encoding.UTF8.GetBytes("{\"packet\":true}"),
            [new(1, value)],
            10);

        Assert.Throws<InvalidDataException>(
            () => PreparedPass1TerminalContextProjection.Create(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsUnsupportedEvidenceValueTypes()
    {
        PreparedPass1TerminalContextProjectionRequest request = CreateRequest(
            Encoding.UTF8.GetBytes("{\"packet\":true}"),
            [new(1, 42)],
            10);

        Assert.Throws<InvalidDataException>(
            () => PreparedPass1TerminalContextProjection.Create(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void Accumulator_SupportsOnlyBoundedContiguousResults(int count)
    {
        using var accumulator = new PreparedPass1TerminalContextAccumulator();
        for (int order = 1; order <= count; order++)
        {
            accumulator.Add(CreateResult(order));
        }

        Assert.Equal(count, accumulator.Count);
        Assert.Equal(count, accumulator.Snapshot().Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Accumulator_RejectsSixtyFifthResultWithoutChangingBoundedState()
    {
        using var accumulator = new PreparedPass1TerminalContextAccumulator();
        for (int order = 1; order <= 64; order++)
        {
            accumulator.Add(CreateResult(order));
        }

        Assert.Throws<InvalidDataException>(() => accumulator.Add(CreateResult(65)));
        Assert.Equal(64, accumulator.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Accumulator_ReleaseClearsReferencesAndClosesTheExecutionLocalSeam()
    {
        var accumulator = new PreparedPass1TerminalContextAccumulator();
        accumulator.Add(CreateResult(1));

        accumulator.Release();

        Assert.True(accumulator.ReferenceReleased);
        Assert.Equal(0, accumulator.Count);
        Assert.Throws<ObjectDisposedException>(() => accumulator.Snapshot());
        accumulator.Dispose();
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Accumulator_RejectsNoncontiguousOrderingBeforeProjection()
    {
        using var accumulator = new PreparedPass1TerminalContextAccumulator();

        Assert.Throws<InvalidDataException>(() => accumulator.Add(CreateResult(2)));
        Assert.Equal(0, accumulator.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Accumulator_RejectsNullOrUnsupportedValuesWithoutChangingState()
    {
        using var accumulator = new PreparedPass1TerminalContextAccumulator();

        Assert.Throws<ArgumentNullException>(
            () => accumulator.Add(null!));
        Assert.Equal(0, accumulator.Count);

        Assert.Throws<InvalidDataException>(
            () => accumulator.Add(new(1, "unsupported")));
        Assert.Equal(0, accumulator.Count);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("out-of-order")]
    [InlineData("not-full-line")]
    [InlineData("forbidden-terminal-literal")]
    public void Create_RejectsMarkerAndTerminalInstructionDrift(string mutation)
    {
        string prompt = CreatePrompt();
        prompt = mutation switch
        {
            "missing" => prompt.Replace(
                "<!-- PASS1_TERMINAL_COMMON_END -->\n", string.Empty, StringComparison.Ordinal),
            "duplicate" => prompt.Replace(
                "<!-- PASS1_TERMINAL_COMMON_END -->\n",
                "<!-- PASS1_TERMINAL_COMMON_END -->\n<!-- PASS1_TERMINAL_COMMON_END -->\n",
                StringComparison.Ordinal),
            "out-of-order" => prompt.Replace(
                "<!-- PASS1_TERMINAL_PREAMBLE_END -->\nordinary prompt text containing navigate_code and reasoning\n<!-- PASS1_TERMINAL_COMMON_BEGIN -->",
                "<!-- PASS1_TERMINAL_COMMON_BEGIN -->\nordinary prompt text containing navigate_code and reasoning\n<!-- PASS1_TERMINAL_PREAMBLE_END -->",
                StringComparison.Ordinal),
            "not-full-line" => prompt.Replace(
                "<!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->",
                "prefix <!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->",
                StringComparison.Ordinal),
            "forbidden-terminal-literal" => prompt.Replace(
                "terminal preamble",
                "terminal preamble navigate_code",
                StringComparison.Ordinal),
            _ => throw new Xunit.Sdk.XunitException("Unknown marker mutation."),
        };

        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(Encoding.UTF8.GetBytes("{\"packet\":true}"), [CreateResult(1)], 10)
                    with { PromptResource = prompt }));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsPacketAndPromptIdentityBindingMismatch()
    {
        PreparedPass1TerminalContextProjectionRequest request =
            CreateRequest(Encoding.UTF8.GetBytes("{\"packet\":true}"), [CreateResult(1)], 10)
                with
                {
                    BoundPromptIdentity = "different-prompt",
                    BoundOriginalPacketUtf8 = Encoding.UTF8.GetBytes("{\"other\":true}"),
                };

        Assert.Throws<InvalidDataException>(
            () => PreparedPass1TerminalContextProjection.Create(request));
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public void Create_RejectsEagerProjectionBeforeForcedTurnNineFinalization()
    {
        Assert.Throws<InvalidDataException>(() =>
            PreparedPass1TerminalContextProjection.Create(
                CreateRequest(Encoding.UTF8.GetBytes("{\"packet\":true}"), [CreateResult(1)], 8)));
    }

    private static PreparedPass1TerminalContextProjectionRequest CreateRequest(
        ReadOnlyMemory<byte> packet,
        IReadOnlyList<PreparedPass1TerminalContextResult> results,
        int providerTurn,
        PreparedPass1TerminalContextCorrection? correction = null) =>
        new(
            CreatePrompt(),
            "story-12-3-prompt",
            packet,
            providerTurn,
            results,
            correction);

    private static string CreatePrompt() =>
        "before\n" +
        "<!-- PASS1_TERMINAL_PREAMBLE_BEGIN -->\n" +
        "terminal preamble\n" +
        "<!-- PASS1_TERMINAL_PREAMBLE_END -->\n" +
        "ordinary prompt text containing navigate_code and reasoning\n" +
        "<!-- PASS1_TERMINAL_COMMON_BEGIN -->\n" +
        "terminal common\n" +
        "<!-- PASS1_TERMINAL_COMMON_END -->\n" +
        "after";

    private static PreparedPass1TerminalContextResult CreateResult(int order) =>
        PreparedPass1TerminalContextResult.FromNavigationResponse(
            order,
            CreateNavigationResponse());

    private static PreparedPass1NavigationResponse CreateNavigationResponse() =>
        new(
            PreparedPass1NavigationOutcome.DocumentEvidence,
            Guid.Parse("10000000-0000-4000-8000-000000000001"),
            PreparedPass1NavigationOperation.Definition,
            1,
            PreparedPass1NavigationBoundKind.Calls,
            1,
            32,
            [
                new PreparedPass1NavigationResult(
                    "src/OrderService.cs",
                    Guid.Parse("20000000-0000-4000-8000-000000000002"),
                    new byte[32],
                    [Guid.Parse("30000000-0000-4000-8000-000000000003")],
                    [Guid.Parse("40000000-0000-4000-8000-000000000004")],
                    "Process",
                    new PreparedPass1LineRange(10, 12),
                    new PreparedPass1ByteRange(100, 140),
                    "src/OrderService.cs:10-12"),
            ]);

    private sealed class ThrowingNavigationService : IPass1NavigationService
    {
        public Task<PreparedPass1NavigationResponse> NavigateAsync(
            PreparedPass1NavigationRequestInput request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The serializer-options contract test must not invoke navigation.");
    }
}
