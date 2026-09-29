using Vulgata.Tests.TestInfrastructure;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125EvaluationContractTests
{
    private const string ContractDirectory =
        "specs/014-grounded-llm-wiki-chat-release-evidence/contracts";

    public static IEnumerable<object[]> AcceptedSchemas()
    {
        yield return ["chat-evidence-packet-v2.schema.json", "Grounded Chat Evidence Packet 2.2"];
        yield return ["story125-evaluation-case.schema.json", "Story 12.5 Chat Evaluation Corpus"];
        yield return ["story125-release-evidence.schema.json", "Story 12.5 Release Evidence Run Envelope"];
    }

    public static IEnumerable<object[]> ValidContractDocuments()
    {
        yield return ["chat-evidence-packet-v2.schema.json", CreateValidChatPacket().ToJsonString()];
        yield return ["story125-evaluation-case.schema.json", CreateValidEvaluationCorpus().ToJsonString()];
        yield return ["story125-release-evidence.schema.json", CreateValidReleaseEnvelope().ToJsonString()];
    }

    public static IEnumerable<object[]> InvalidContractDocuments()
    {
        JsonObject packet = CreateValidChatPacket();
        packet.Remove("fingerprint");
        yield return ["chat-evidence-packet-v2.schema.json", packet.ToJsonString(), "missing packet fingerprint"];

        JsonObject corpus = CreateValidEvaluationCorpus();
        corpus["cases"]!.AsArray().RemoveAt(11);
        yield return ["story125-evaluation-case.schema.json", corpus.ToJsonString(), "corpus has fewer than twelve cases"];

        JsonObject envelope = CreateValidReleaseEnvelope();
        envelope["checks"] = new JsonArray();
        yield return ["story125-release-evidence.schema.json", envelope.ToJsonString(), "planned envelope has no check inventory"];
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [MemberData(nameof(AcceptedSchemas))]
    public void AcceptedSchema_LoadsAsClosedDraft202012Document(string fileName, string title)
    {
        using JsonDocument schema = LoadSchema(fileName);
        JsonElement root = schema.RootElement;

        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());
        Assert.Equal(title, root.GetProperty("title").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.False, root.GetProperty("additionalProperties").ValueKind);
        Assert.True(root.GetProperty("required").GetArrayLength() > 0);
        Assert.True(root.GetProperty("$defs").GetProperty("sha256").ValueKind is JsonValueKind.Object);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [MemberData(nameof(ValidContractDocuments))]
    public void AcceptedSchema_ValidKnownGoodDocument_Passes(string fileName, string json)
    {
        using JsonDocument schema = LoadSchema(fileName);
        using JsonDocument document = JsonDocument.Parse(json);

        IReadOnlyList<string> errors = SchemaValidator.Validate(document.RootElement, schema.RootElement);

        Assert.Empty(errors);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [MemberData(nameof(InvalidContractDocuments))]
    public void AcceptedSchema_IndependentKnownBadDocument_Fails(string fileName, string json, string reason)
    {
        using JsonDocument schema = LoadSchema(fileName);
        using JsonDocument document = JsonDocument.Parse(json);

        IReadOnlyList<string> errors = SchemaValidator.Validate(document.RootElement, schema.RootElement);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.Length > 0);
        Assert.True(errors.Count > 0, reason);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125QuestionCorpus_IsSchemaValidAndContainsTwelveToTwentyCases()
    {
        using JsonDocument corpus = LoadQuestionCorpusFixture();
        using JsonDocument schema = LoadSchema("story125-evaluation-case.schema.json");

        IReadOnlyList<string> errors = SchemaValidator.Validate(corpus.RootElement, schema.RootElement);

        Assert.Empty(errors);
        JsonElement cases = corpus.RootElement.GetProperty("cases");
        Assert.InRange(cases.GetArrayLength(), 12, 20);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125QuestionCorpus_HasStableUniqueCaseIds()
    {
        using JsonDocument corpus = LoadQuestionCorpusFixture();
        JsonElement cases = corpus.RootElement.GetProperty("cases");
        string[] ids = cases.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Matches("^q[0-9]{2}$", id));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125QuestionCorpus_FollowUpsReferenceExistingAcyclicChainsOfAtMostThreeQuestions()
    {
        using JsonDocument corpus = LoadQuestionCorpusFixture();
        JsonElement[] cases = corpus.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Dictionary<string, string?> parents = cases.ToDictionary(
            item => item.GetProperty("id").GetString() ?? string.Empty,
            item => item.GetProperty("followUpTo").ValueKind == JsonValueKind.Null
                ? null
                : item.GetProperty("followUpTo").GetString(),
            StringComparer.Ordinal);

        foreach ((string id, string? parent) in parents)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { id };
            string? current = parent;
            int depth = 0;
            while (current is not null)
            {
                depth++;
                Assert.True(parents.ContainsKey(current), $"Case {id} references missing follow-up parent {current}.");
                Assert.True(visited.Add(current), $"Case {id} participates in a cyclic follow-up chain.");
                Assert.InRange(depth, 1, 2);
                current = parents[current];
            }
        }
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125QuestionCorpus_DoesNotEmbedDesiredProviderAnswers()
    {
        using JsonDocument corpus = LoadQuestionCorpusFixture();

        foreach (JsonElement item in corpus.RootElement.GetProperty("cases").EnumerateArray())
        {
            Assert.DoesNotContain("expectedAnswer", item.EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain("providerAnswer", item.EnumerateObject().Select(property => property.Name));
            Assert.DoesNotContain("expectedResponse", item.EnumerateObject().Select(property => property.Name));
        }
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125CrossArtifactConsistency_UsesOneClosedStreamingVocabularyAndByteDomain()
    {
        string root = FindRepositoryRoot();
        string plan = File.ReadAllText(Path.Combine(
            root, "specs", "014-grounded-llm-wiki-chat-release-evidence", "plan.md"));
        string model = File.ReadAllText(Path.Combine(
            root, "specs", "014-grounded-llm-wiki-chat-release-evidence", "data-model.md"));
        string streaming = File.ReadAllText(Path.Combine(
            root, "specs", "014-grounded-llm-wiki-chat-release-evidence", "contracts", "chat-provider-streaming-v1.md"));
        string release = File.ReadAllText(Path.Combine(
            root, "specs", "014-grounded-llm-wiki-chat-release-evidence", "contracts", "release-evidence-contract.md"));
        string schema = File.ReadAllText(Path.Combine(
            root, "specs", "014-grounded-llm-wiki-chat-release-evidence", "contracts", "story125-release-evidence.schema.json"));

        foreach (string term in new[] { "16,384", "65,536", "1,048,576", "131,072" })
        {
            Assert.Contains(term, plan, StringComparison.Ordinal);
            Assert.Contains(term, model, StringComparison.Ordinal);
            Assert.Contains(term, streaming, StringComparison.Ordinal);
        }

        Assert.Contains("streaming", release, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("screenshot", release, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("receipt", release, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transportOutcome", model, StringComparison.Ordinal);
        Assert.Contains("rejectionCategory", model, StringComparison.Ordinal);
        Assert.Contains("validationOutcome", model, StringComparison.Ordinal);

        using JsonDocument schemaDocument = JsonDocument.Parse(schema);
        JsonElement streamAttempt = schemaDocument.RootElement
            .GetProperty("$defs")
            .GetProperty("streamAttemptReceipt");
        JsonElement transportOutcome = streamAttempt.GetProperty("properties")
            .GetProperty("transportOutcome").GetProperty("enum");
        JsonElement rejectionCategory = streamAttempt.GetProperty("properties")
            .GetProperty("rejectionCategory").GetProperty("oneOf")[0].GetProperty("enum");
        JsonElement validationOutcome = streamAttempt.GetProperty("properties")
            .GetProperty("validationOutcome").GetProperty("enum");
        Assert.Contains("completed", transportOutcome.EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("invalid-wire-utf8", rejectionCategory.EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("rejected-semantic", validationOutcome.EnumerateArray().Select(value => value.GetString()));

        string[] acceptedTransportOutcomes =
        [
            "completed", "cancelled", "disconnect", "inactivity", "overall-deadline",
            "raw-body-overflow", "sse-line-overflow", "sse-event-overflow",
            "received-provider-chunk-overflow", "received-provider-response-overflow",
            "decoded-delta-overflow", "final-answer-overflow", "invalid-wire-utf8",
            "invalid-sse", "invalid-json-event", "invalid-json-escape", "invalid-answer-utf8",
            "missing-terminal", "duplicate-terminal", "contradictory-terminal",
            "post-terminal-data", "provider-failure",
        ];
        Assert.Equal(
            acceptedTransportOutcomes.Order(StringComparer.Ordinal),
            transportOutcome.EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));
        Assert.Equal(
            acceptedTransportOutcomes.Where(value => value != "completed").Order(StringComparer.Ordinal),
            rejectionCategory.EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125EvaluationScoring_RejectsAcceptedTechnicalClaimCitationCoverageBelowOneHundredPercent()
    {
        JsonObject envelope = CreatePassedScoringEnvelope();
        ((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["acceptedTechnicalClaimCitationPercent"] = 99;

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Contains(
            errors,
            error => error.Contains("citation", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125EvaluationScoring_RejectsAnyUnauthorizedDisclosureAutomatically()
    {
        JsonObject envelope = CreatePassedScoringEnvelope();
        ((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["unauthorizedDisclosureCount"] = 1;

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Contains(
            errors,
            error => error.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("disclosure", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125EvaluationScoring_UsesExactNonnegativeHalfUpUsefulPercentage()
    {
        JsonObject envelope = CreatePassedScoringEnvelope(usefulCount: 10, usefulPercent: 83.33);

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.DoesNotContain(
            errors,
            error => error.Contains("useful percentage", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125EvaluationScoring_RejectsAcceptedAnswerAtOrAboveThirtySeconds()
    {
        JsonObject envelope = CreatePassedScoringEnvelope();
        ((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["overThirtySecondAcceptedAnswerCount"] = 1;
        ((JsonArray)((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["caseReceipts"]!)[0]!["totalAcceptedAnswerDurationMilliseconds"] = 30000;

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Contains(
            errors,
            error => error.Contains("30", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("duration", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("timing", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125EvaluationScoring_RejectsProviderDispositionOutsideClosedVocabulary()
    {
        JsonObject envelope = CreatePassedScoringEnvelope();
        ((JsonArray)((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["caseReceipts"]!)[0]!["providerDisposition"] = "provider-called";

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Contains(
            errors,
            error => error.Contains("providerDisposition", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("provider disposition", StringComparison.OrdinalIgnoreCase));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125SemanticValidation_KnownGoodPlannedEnvelope_RemainsGreen()
    {
        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(CreateValidReleaseEnvelope());

        Assert.Empty(errors);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [InlineData("dangling-command-reference")]
    [InlineData("unresolved-observation-reference")]
    [InlineData("foreign-artifact")]
    [InlineData("missing-artifact")]
    [InlineData("nonzero-command-exit")]
    [InlineData("altered-artifact-hash")]
    [InlineData("mismatched-corpus-result-receipt-packet-sets")]
    public void Story125SemanticValidation_EvidenceBindingMutation_IsRejected(string mutation)
    {
        JsonObject envelope = CreateValidReleaseEnvelope();
        envelope["status"] = "attempted";
        envelope["result"] = new JsonObject { ["kind"] = "attempted" };
        envelope["commands"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "cmd-valid",
                ["redactedExactCommand"] = "dotnet test --filter Story125",
                ["commandSha256"] = new string('c', 64),
                ["exitCode"] = 0,
                ["startedAtUtc"] = "2026-08-27T12:00:00Z",
                ["endedAtUtc"] = "2026-08-27T12:00:01Z",
                ["durationMilliseconds"] = 1000,
                ["stdoutArtifact"] = new JsonObject
                {
                    ["path"] = "evidence/observation.txt",
                    ["sha256"] = new string('a', 64),
                    ["mediaType"] = "text/plain",
                },
                ["stderrArtifact"] = null,
            },
        };
        envelope["attemptedObservations"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "obs-valid",
                ["stage"] = "release-test",
                ["observedAtUtc"] = "2026-08-27T12:00:01Z",
                ["summary"] = "Release test completed.",
                ["commandIds"] = new JsonArray("cmd-valid"),
                ["artifacts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["path"] = "evidence/observation.txt",
                        ["sha256"] = new string('a', 64),
                        ["mediaType"] = "text/plain",
                    },
                },
            },
        };
        ((JsonObject)envelope["checks"]![0]!)!["status"] = "attempted";
        ((JsonObject)envelope["checks"]![0]!)!["commandIds"] = new JsonArray("cmd-valid");
        ((JsonObject)envelope["checks"]![0]!)!["artifacts"] = new JsonArray
        {
            new JsonObject
            {
                ["path"] = "evidence/observation.txt",
                ["sha256"] = new string('a', 64),
                ["mediaType"] = "text/plain",
            },
        };
        ((JsonObject)envelope["checks"]![0]!)!["attemptedObservationIds"] = new JsonArray("obs-valid");

        IReadOnlyList<string> baselineErrors = Story125SemanticHarnessProbe.Validate(envelope);
        Assert.Empty(baselineErrors);

        switch (mutation)
        {
            case "dangling-command-reference":
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["commandIds"] =
                    new JsonArray("cmd-missing");
                break;
            case "unresolved-observation-reference":
                ((JsonObject)envelope["checks"]![0]!)!["attemptedObservationIds"] =
                    new JsonArray("obs-missing");
                break;
            case "foreign-artifact":
                ((JsonObject)((JsonObject)envelope["attemptedObservations"]![0]!)!["artifacts"]![0]!)!["path"] =
                    "foreign/evidence.txt";
                break;
            case "missing-artifact":
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["artifacts"] = new JsonArray();
                break;
            case "nonzero-command-exit":
                ((JsonObject)envelope["commands"]![0]!)!["exitCode"] = 7;
                break;
            case "altered-artifact-hash":
                ((JsonObject)((JsonObject)envelope["attemptedObservations"]![0]!)!["artifacts"]![0]!)!["sha256"] =
                    new string('b', 64);
                break;
            case "mismatched-corpus-result-receipt-packet-sets":
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["corpusCaseIds"] =
                    new JsonArray("q01", "q02");
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["resultCaseIds"] =
                    new JsonArray("q01");
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["receiptCaseIds"] =
                    new JsonArray("q02");
                ((JsonObject)envelope["attemptedObservations"]![0]!)!["packetFingerprints"] =
                    new JsonArray(new string('c', 64));
                break;
        }

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.NotEmpty(errors);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125PassedEnvelope_EvidenceBindingGraph_IsSchemaValidAndSemanticallyGreen()
    {
        JsonObject envelope = CreateSchemaValidPassedEnvelope();
        using JsonDocument schema = LoadSchema("story125-release-evidence.schema.json");
        using JsonDocument document = JsonDocument.Parse(envelope.ToJsonString());

        IReadOnlyList<string> schemaErrors = SchemaValidator.Validate(document.RootElement, schema.RootElement);
        IReadOnlyList<string> semanticErrors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Empty(schemaErrors);
        Assert.Empty(semanticErrors);
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [InlineData("dangling-chat-command-reference")]
    [InlineData("foreign-results-artifact")]
    [InlineData("altered-results-artifact-hash")]
    [InlineData("missing-results-artifact")]
    [InlineData("foreign-packet-result-artifact")]
    [InlineData("missing-packet-result-artifact")]
    [InlineData("foreign-streaming-observations-artifact")]
    [InlineData("missing-streaming-observations-artifact")]
    [InlineData("duplicate-case-receipt-id")]
    [InlineData("missing-case-receipt-id")]
    [InlineData("foreign-case-receipt-id")]
    [InlineData("mismatched-corpus-result-ids")]
    [InlineData("reordered-packet-fingerprints")]
    [InlineData("foreign-packet-fingerprint")]
    public void Story125PassedEnvelope_EvidenceBindingMutation_IsRejected(string mutation)
    {
        JsonObject envelope = CreateSchemaValidPassedEnvelope();
        using JsonDocument schema = LoadSchema("story125-release-evidence.schema.json");
        using JsonDocument baselineDocument = JsonDocument.Parse(envelope.ToJsonString());
        Assert.Empty(SchemaValidator.Validate(baselineDocument.RootElement, schema.RootElement));
        Assert.Empty(Story125SemanticHarnessProbe.Validate(envelope));

        JsonObject chat = (JsonObject)envelope["releaseReceipt"]!["chat"]!;
        JsonArray caseReceipts = (JsonArray)chat["caseReceipts"]!;
        JsonArray packetFingerprints = (JsonArray)chat["packetFingerprints"]!;

        switch (mutation)
        {
            case "dangling-chat-command-reference":
                chat["commandIds"] = new JsonArray("cmd-missing");
                break;
            case "foreign-results-artifact":
            case "altered-results-artifact-hash":
            case "missing-results-artifact":
                MutateArtifact((JsonObject)chat, "resultsArtifact", mutation);
                break;
            case "foreign-packet-result-artifact":
            case "missing-packet-result-artifact":
                MutateArtifact((JsonObject)chat, "packetResultArtifact", mutation);
                break;
            case "foreign-streaming-observations-artifact":
            case "missing-streaming-observations-artifact":
                MutateArtifact((JsonObject)chat, "streamingObservationsArtifact", mutation);
                break;
            case "duplicate-case-receipt-id":
                ((JsonObject)caseReceipts[1]!)!["caseId"] = "q01";
                break;
            case "missing-case-receipt-id":
                ((JsonObject)caseReceipts[0]!)!["caseId"] = "q00";
                break;
            case "foreign-case-receipt-id":
                ((JsonObject)caseReceipts[0]!)!["caseId"] = "q99";
                break;
            case "mismatched-corpus-result-ids":
                ((JsonObject)caseReceipts[0]!)!["caseId"] = "q00";
                break;
            case "reordered-packet-fingerprints":
                JsonNode? first = packetFingerprints[0];
                packetFingerprints[0] = packetFingerprints[1]!.DeepClone();
                packetFingerprints[1] = first!.DeepClone();
                break;
            case "foreign-packet-fingerprint":
                packetFingerprints[0] = new string('f', 64);
                break;
        }

        using JsonDocument mutatedDocument = JsonDocument.Parse(envelope.ToJsonString());
        IReadOnlyList<string> schemaErrors = SchemaValidator.Validate(mutatedDocument.RootElement, schema.RootElement);
        IReadOnlyList<string> semanticErrors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.NotEmpty(schemaErrors.Concat(semanticErrors));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteFact("HistoricalBoundary")]
    public void Story125PassedEnvelope_HashVerifiedCorpusAndResultGraph_RemainsGreen()
    {
        Dictionary<string, byte[]> artifactContents = CreateFixedCorpusResultArtifactContents();
        JsonObject envelope = CreateSchemaValidPassedEnvelope(artifactContents);
        using JsonDocument schema = LoadSchema("story125-release-evidence.schema.json");
        using JsonDocument document = JsonDocument.Parse(envelope.ToJsonString());

        Assert.Empty(SchemaValidator.Validate(document.RootElement, schema.RootElement));
        Assert.Empty(Story125SemanticHarnessProbe.Validate(envelope, artifactContents));
    }

    [Trait("Suite", "HistoricalBoundary")]
    [SuiteTheory("HistoricalBoundary")]
    [InlineData("coordinated-foreign-case-identity")]
    [InlineData("altered-result-artifact-content")]
    [InlineData("missing-result-artifact-content")]
    public void Story125PassedEnvelope_HashVerifiedCorpusAndResultMutation_IsRejected(string mutation)
    {
        Dictionary<string, byte[]> artifactContents = CreateFixedCorpusResultArtifactContents();
        JsonObject envelope = CreateSchemaValidPassedEnvelope(artifactContents);
        using JsonDocument schema = LoadSchema("story125-release-evidence.schema.json");
        using JsonDocument baselineDocument = JsonDocument.Parse(envelope.ToJsonString());
        Assert.Empty(SchemaValidator.Validate(baselineDocument.RootElement, schema.RootElement));
        Assert.Empty(Story125SemanticHarnessProbe.Validate(envelope, artifactContents));

        JsonArray caseReceipts = (JsonArray)envelope["releaseReceipt"]!["chat"]!["caseReceipts"]!;
        switch (mutation)
        {
            case "coordinated-foreign-case-identity":
                JsonObject foreignReceipt = (JsonObject)caseReceipts[0]!;
                foreignReceipt["caseId"] = "q99";
                ((JsonObject)foreignReceipt["streamAttempts"]![0]!)!["caseId"] = "q99";
                break;
            case "altered-result-artifact-content":
                artifactContents["evidence/chat-results.json"] = Encoding.UTF8.GetBytes(
                    "{\"corpusCaseIds\":[\"q01\"],\"resultCaseIds\":[\"q01\"]}");
                break;
            case "missing-result-artifact-content":
                artifactContents.Remove("evidence/chat-results.json");
                break;
        }

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope, artifactContents);

        Assert.NotEmpty(errors);
    }

    private static void MutateArtifact(JsonObject chat, string propertyName, string mutation)
    {
        if (mutation.StartsWith("missing-", StringComparison.Ordinal))
        {
            chat[propertyName] = CreateArtifact("evidence/unbound.json", 'd');
        }
        else if (mutation.StartsWith("foreign-", StringComparison.Ordinal))
        {
            chat[propertyName] = CreateArtifact("foreign/evidence.json", 'a');
        }
        else
        {
            chat[propertyName] = CreateArtifact("evidence/results.json", 'b');
        }
    }

    private static JsonObject CreateSchemaValidPassedEnvelope(
        IReadOnlyDictionary<string, byte[]>? artifactContents = null)
    {
        const string story125Commit = "cccccccccccccccccccccccccccccccccccccccc";
        const string story124Commit = "dddddddddddddddddddddddddddddddddddddddd";
        const string processObservationId = "process-release01";
        const string commandId = "cmd-chat";
        const string packetCommandId = "cmd-packets";
        const string streamingCommandId = "cmd-stream";
        const string restartCommandId = "cmd-restart";
        const string observationId = "obs-release";
        const string observedAtUtc = "2026-08-27T12:00:01Z";

        string[] mandatoryChecks =
        [
            "tier1-contract-ranking-relationship",
            "tier1-conversation-synthesis-security",
            "tier1-production-ui-corpus-evidence",
            "tier1-build-full-tests",
            "tier2-postgresql-restart-resume",
            "tier3-candidate-tool-database-preflight",
            "tier3-real-scan",
            "tier3-grounded-chat-corpus",
            "tier3-restart-continuity",
            "tier3-screenshot-release-integrity",
        ];

        var checks = new JsonArray();
        foreach (string id in mandatoryChecks)
        {
            int tier = id.StartsWith("tier1-", StringComparison.Ordinal)
                ? 1
                : id.StartsWith("tier2-", StringComparison.Ordinal) ? 2 : 3;
            checks.Add(new JsonObject
            {
                ["id"] = id,
                ["tier"] = tier,
                ["status"] = "passed",
                ["commandIds"] = new JsonArray(commandId, packetCommandId, streamingCommandId, restartCommandId),
                ["artifacts"] = new JsonArray
                {
                    CreateArtifact("evidence/chat-results.json", 'a'),
                    CreateArtifact("evidence/packets.json", 'b'),
                    CreateArtifact("evidence/streaming.json", 'c'),
                },
                ["attemptedObservationIds"] = new JsonArray(observationId),
                ["reason"] = null,
            });
        }

        var packetFingerprints = new JsonArray();
        var caseReceipts = new JsonArray();
        for (int index = 1; index <= 12; index++)
        {
            packetFingerprints.Add(CaseFingerprint(index));
            caseReceipts.Add(CreateCaseReceipt(index, story125Commit));
        }

        var screenshots = new JsonArray();
        foreach ((string viewKind, string fileName) in new[]
        {
            ("scan-state", "scan-state.png"),
            ("business-answer", "business-answer.png"),
            ("it-answer", "it-answer.png"),
            ("restart", "restart.png"),
            ("release-summary", "release-summary.png"),
        })
        {
            screenshots.Add(new JsonObject
            {
                ["path"] = $"evidence/screenshots/{fileName}",
                ["sha256"] = new string('e', 64),
                ["viewKind"] = viewKind,
                ["candidateBound"] = true,
                ["productionPersistedState"] = true,
                ["redactionPassed"] = true,
            });
        }

        JsonObject aggregateResultsArtifact = CreateArtifact("evidence/chat-results.json", 'a');
        JsonObject aggregatePacketsArtifact = CreateArtifact("evidence/packets.json", 'b');
        JsonObject aggregateStreamingArtifact = CreateArtifact("evidence/streaming.json", 'c');

        JsonObject envelope = new JsonObject
        {
            ["schemaVersion"] = "story125-release-evidence/2.2",
            ["runId"] = "story125-20260827t120000z-abcdef12",
            ["supersedesRunId"] = null,
            ["status"] = "passed",
            ["candidate"] = new JsonObject
            {
                ["story125Commit"] = story125Commit,
                ["story124IntegratedCommit"] = story124Commit,
                ["cleanWorktree"] = true,
            },
            ["environment"] = new JsonObject
            {
                ["databaseName"] = "vulgata_story125_abcdef12",
                ["postgresqlMajor"] = 17,
                ["topology"] = "windows-host-app-tools-postgresql-only-docker",
                ["repositoryRevisions"] = new JsonArray
                {
                    new JsonObject { ["repositoryAlias"] = "mbs-account", ["profiledRevision"] = story125Commit },
                    new JsonObject { ["repositoryAlias"] = "mbs-channel", ["profiledRevision"] = story124Commit },
                },
                ["promptVersions"] = new JsonArray("story125-chat-v1"),
                ["schemaVersions"] = new JsonArray("story125-release-evidence/2.2"),
                ["toolVersions"] = new JsonArray
                {
                    new JsonObject { ["name"] = "dotnet", ["version"] = "10.0", ["checksum"] = new string('1', 64) },
                    new JsonObject { ["name"] = "postgres", ["version"] = "17", ["checksum"] = new string('2', 64) },
                    new JsonObject { ["name"] = "playwright", ["version"] = "1", ["checksum"] = new string('3', 64) },
                },
                ["provider"] = new JsonObject
                {
                    ["providerAlias"] = "release-provider",
                    ["apiType"] = "openai-compatible",
                    ["model"] = "release-model",
                    ["nonSecretConfigurationIdentity"] = "release-provider-config",
                    ["configurationFingerprint"] = new string('4', 64),
                },
                ["streamAttestationAuthority"] = new JsonObject
                {
                    ["authorityKind"] = "story125-trackc-sealed-raw-stream-attestor/1.0",
                    ["processObservationId"] = processObservationId,
                    ["processPublicKeyFingerprint"] = new string('5', 64),
                    ["candidateCommit"] = story125Commit,
                    ["adapterImplementation"] = "Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport",
                    ["adapterVersion"] = "1",
                    ["adapterAssemblySha256"] = new string('6', 64),
                    ["attestationSinkImplementation"] = "Vulgata.Agents.ChatExecution.Story125StreamAttestationSink",
                    ["attestationSinkVersion"] = "1",
                    ["attestationSinkAssemblySha256"] = new string('7', 64),
                    ["preflightArtifact"] = CreateArtifact("evidence/stream-preflight.json", '7'),
                },
            },
            ["commands"] = new JsonArray
            {
                CreateCommand(commandId, "evidence/chat-results.json", 'a'),
                CreateCommand(packetCommandId, "evidence/packets.json", 'b'),
                CreateCommand(streamingCommandId, "evidence/streaming.json", 'c'),
                CreateCommand(restartCommandId, "evidence/restart.json", '8'),
            },
            ["checks"] = checks,
            ["attemptedObservations"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = observationId,
                    ["stage"] = "release-evidence",
                    ["observedAtUtc"] = observedAtUtc,
                    ["summary"] = "Release evidence graph observed.",
                    ["commandIds"] = new JsonArray(commandId, packetCommandId, streamingCommandId),
                    ["artifacts"] = new JsonArray
                    {
                        aggregateResultsArtifact.DeepClone(),
                        aggregatePacketsArtifact.DeepClone(),
                        aggregateStreamingArtifact.DeepClone(),
                    },
                },
            },
            ["result"] = new JsonObject
            {
                ["kind"] = "passed",
                ["completedAtUtc"] = "2026-08-27T12:00:02Z",
            },
            ["releaseReceipt"] = new JsonObject
            {
                ["scan"] = new JsonObject
                {
                    ["fileCodeUnitCount"] = 50,
                    ["highValueSeedCount"] = 1,
                    ["failedHighValueSeedCount"] = 0,
                    ["ordinaryFailureCount"] = 0,
                    ["ordinaryFailureBudget"] = 1,
                    ["exactCrossRepositoryReferencesEdgeCount"] = 1,
                    ["honestUncertaintyCount"] = 1,
                    ["businessLogicDocumentCount"] = 3,
                    ["crossRepositoryBusinessFlowCount"] = 1,
                    ["countsReceipt"] = CreateClaimReceipt("evidence/scan-counts.json", '9', commandId),
                    ["exactEdgeReceipt"] = CreateClaimReceipt("evidence/scan-edges.json", 'a', commandId),
                    ["uncertaintyReceipt"] = CreateClaimReceipt("evidence/scan-uncertainty.json", 'b', commandId),
                    ["businessLogicDocumentsReceipt"] = CreateClaimReceipt("evidence/scan-documents.json", 'c', commandId),
                    ["crossRepositoryFlowReceipt"] = CreateClaimReceipt("evidence/scan-flows.json", 'd', commandId),
                },
                ["chat"] = new JsonObject
                {
                    ["corpusId"] = "story125-release-corpus",
                    ["caseCount"] = 12,
                    ["corpusResultCount"] = 12,
                    ["usefulCount"] = 10,
                    ["usefulPercent"] = 83.33,
                    ["acceptedTechnicalClaimCitationPercent"] = 100,
                    ["unauthorizedDisclosureCount"] = 0,
                    ["overThirtySecondAcceptedAnswerCount"] = 0,
                    ["streamedProviderResponseCount"] = 12,
                    ["nonStreamingAcceptedAnswerCount"] = 0,
                    ["caseReceipts"] = caseReceipts,
                    ["packetFingerprints"] = packetFingerprints,
                    ["resultsArtifact"] = aggregateResultsArtifact,
                    ["packetResultArtifact"] = aggregatePacketsArtifact,
                    ["streamingObservationsArtifact"] = aggregateStreamingArtifact,
                    ["commandIds"] = new JsonArray(commandId, packetCommandId, streamingCommandId),
                },
                ["restart"] = new JsonObject
                {
                    ["sameDatabase"] = true,
                    ["authenticatedWikiAccessible"] = true,
                    ["authenticatedChatAccessible"] = true,
                    ["duplicatePublishedDocumentCount"] = 0,
                    ["dataLossDetected"] = false,
                    ["beforeArtifact"] = CreateArtifact("evidence/restart-before.json", 'e'),
                    ["afterArtifact"] = CreateArtifact("evidence/restart-after.json", 'f'),
                    ["duplicateQueryReceipt"] = CreateClaimReceipt("evidence/restart-query.json", '1', commandId),
                    ["commandIds"] = new JsonArray(commandId, restartCommandId),
                },
                ["databaseOwnership"] = new JsonObject
                {
                    ["databaseName"] = "vulgata_story125_abcdef12",
                    ["forbiddenDatabaseAccessCount"] = 0,
                    ["preflightArtifact"] = CreateArtifact("evidence/db-preflight.json", '2'),
                    ["ownershipArtifact"] = CreateArtifact("evidence/db-ownership.json", '3'),
                    ["cleanupArtifact"] = CreateArtifact("evidence/db-cleanup.json", '4'),
                    ["commandIds"] = new JsonArray(commandId, restartCommandId),
                },
                ["screenshots"] = screenshots,
                ["limitations"] = new JsonArray(),
            },
        };

        if (artifactContents is not null)
        {
            ApplyArtifactContentHashes(envelope, artifactContents);
        }

        return envelope;
    }

    private static Dictionary<string, byte[]> CreateFixedCorpusResultArtifactContents()
    {
        string[] caseIds = Enumerable.Range(1, 12).Select(index => $"q{index:00}").ToArray();
        string caseIdJson = string.Join("\",\"", caseIds);
        string fingerprintsJson = string.Join("\",\"", Enumerable.Range(1, 12).Select(CaseFingerprint));

        return new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["evidence/chat-results.json"] = Encoding.UTF8.GetBytes(
                $"{{\"corpusCaseIds\":[\"{caseIdJson}\"],\"resultCaseIds\":[\"{caseIdJson}\"]}}"),
            ["evidence/packets.json"] = Encoding.UTF8.GetBytes(
                $"{{\"caseIds\":[\"{caseIdJson}\"],\"packetFingerprints\":[\"{fingerprintsJson}\"]}}"),
            ["evidence/streaming.json"] = Encoding.UTF8.GetBytes(
                $"{{\"caseIds\":[\"{caseIdJson}\"]}}"),
        };
    }

    private static void ApplyArtifactContentHashes(
        JsonNode node,
        IReadOnlyDictionary<string, byte[]> artifactContents)
    {
        if (node is JsonObject obj)
        {
            if (obj["path"] is JsonValue pathValue &&
                pathValue.TryGetValue(out string? path) &&
                path is not null &&
                artifactContents.TryGetValue(path, out byte[]? content))
            {
                obj["sha256"] = ComputeSha256(content);
            }

            foreach (JsonNode? child in obj.Select(property => property.Value).ToArray())
            {
                if (child is not null)
                {
                    ApplyArtifactContentHashes(child, artifactContents);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    ApplyArtifactContentHashes(child, artifactContents);
                }
            }
        }
    }

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static JsonObject CreateCommand(string id, string stdoutPath, char stdoutHash)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["redactedExactCommand"] = "dotnet test --filter Story125",
            ["commandSha256"] = new string('8', 64),
            ["exitCode"] = 0,
            ["startedAtUtc"] = "2026-08-27T12:00:00Z",
            ["endedAtUtc"] = "2026-08-27T12:00:01Z",
            ["durationMilliseconds"] = 1000,
            ["stdoutArtifact"] = CreateArtifact(stdoutPath, stdoutHash),
            ["stderrArtifact"] = null,
        };
    }

    private static JsonObject CreateCaseReceipt(int index, string candidateCommit)
    {
        string caseId = $"q{index:00}";
        string fingerprint = CaseFingerprint(index);
        return new JsonObject
        {
            ["caseId"] = caseId,
            ["providerDisposition"] = "invoked",
            ["packetFingerprint"] = fingerprint,
            ["packetArtifact"] = CreateArtifact($"evidence/packets/{caseId}.json", fingerprint),
            ["resultArtifact"] = CreateArtifact($"evidence/results/{caseId}.json", 'e'),
            ["streamAttempts"] = new JsonArray { CreateStreamAttempt(caseId, candidateCommit, fingerprint) },
            ["syntaxRepairUsed"] = false,
            ["validationOutcome"] = "accepted",
            ["totalAcceptedAnswerDurationMilliseconds"] = 1000,
        };
    }

    private static JsonObject CreateStreamAttempt(string caseId, string candidateCommit, string fingerprint)
    {
        const string timestamp = "2026-08-27T12:00:01Z";
        return new JsonObject
        {
            ["attemptKind"] = "primary",
            ["candidateCommit"] = candidateCommit,
            ["caseId"] = caseId,
            ["requestId"] = $"stream-{caseId}-00000001",
            ["runId"] = "story125-20260827t120000z-abcdef12",
            ["attestationAuthority"] = "story125-trackc-sealed-raw-stream-attestor/1.0",
            ["runtimeObservationId"] = $"runtime-stream-{caseId}-00000001",
            ["completionSummaryId"] = $"completion-stream-{caseId}-00000001",
            ["processObservationId"] = "process-release01",
            ["processPublicKeyFingerprint"] = new string('5', 64),
            ["observationSequence"] = 1,
            ["adapterImplementation"] = "Vulgata.Agents.ChatExecution.OpenAiCompatibleChatProviderStreamingTransport",
            ["adapterVersion"] = "1",
            ["adapterAssemblySha256"] = new string('6', 64),
            ["attestationSinkImplementation"] = "Vulgata.Agents.ChatExecution.Story125StreamAttestationSink",
            ["attestationSinkVersion"] = "1",
            ["attestationSinkAssemblySha256"] = new string('7', 64),
            ["providerApi"] = "openai-compatible",
            ["providerConfigurationFingerprint"] = new string('4', 64),
            ["canonicalStreamingRequestFingerprint"] = fingerprint,
            ["requestStreamMode"] = true,
            ["responseHeaderDisposition"] = "received",
            ["responseStatusCode"] = 200,
            ["responseMediaType"] = "text/event-stream",
            ["negotiatedProtocol"] = "HTTP/1.1",
            ["responseHeadersAtUtc"] = timestamp,
            ["firstPhysicalReadDisposition"] = "observed",
            ["firstPhysicalReadAtUtc"] = timestamp,
            ["physicalEofAtUtc"] = timestamp,
            ["physicalEofDisposition"] = "observed-after-terminal",
            ["streamingTransport"] = true,
            ["streamStartedAtUtc"] = timestamp,
            ["firstDataAtUtc"] = timestamp,
            ["terminalAtUtc"] = timestamp,
            ["timeToFirstDataMilliseconds"] = 10,
            ["rawReadCount"] = 1,
            ["maximumRawReadByteCount"] = 10,
            ["rawBodyByteCount"] = 100,
            ["maximumSseLineByteCount"] = 10,
            ["maximumSseEventByteCount"] = 10,
            ["providerChunkCount"] = 1,
            ["maximumReceivedProviderChunkByteCount"] = 10,
            ["assembledReceivedProviderResponseByteCount"] = 10,
            ["answerDeltaCount"] = 1,
            ["maximumAnswerDeltaByteCount"] = 10,
            ["assembledAnswerByteCount"] = 10,
            ["terminalEventCount"] = 1,
            ["inactivityLimitMilliseconds"] = 10000,
            ["overallDeadlineAtUtc"] = "2026-08-27T12:00:29Z",
            ["transportOutcome"] = "completed",
            ["rejectionCategory"] = null,
            ["validationOutcome"] = "accepted",
            ["runtimeObservationArtifact"] = CreateArtifact($"evidence/runtime-stream-{caseId}.json", fingerprint),
            ["completionSummaryArtifact"] = CreateArtifact($"evidence/completion-stream-{caseId}.json", fingerprint),
            ["authenticatedAttestationArtifact"] = CreateArtifact($"evidence/attestation-stream-{caseId}.json", fingerprint),
            ["attestationSignature"] = new string('A', 80),
        };
    }

    private static JsonObject CreateClaimReceipt(string path, char hash, string commandId)
    {
        return new JsonObject
        {
            ["observedAtUtc"] = "2026-08-27T12:00:01Z",
            ["queryFingerprint"] = new string('9', 64),
            ["commandIds"] = new JsonArray(commandId),
            ["artifacts"] = new JsonArray { CreateArtifact(path, hash) },
        };
    }

    private static JsonObject CreateArtifact(string path, char hash) =>
        CreateArtifact(path, new string(hash, 64));

    private static JsonObject CreateArtifact(string path, string sha256) => new()
    {
        ["path"] = path,
        ["sha256"] = sha256,
        ["mediaType"] = "application/json",
    };

    private static string CaseFingerprint(int index) => index.ToString("x64");

    private static JsonObject CreatePassedScoringEnvelope(int usefulCount = 10, double usefulPercent = 83.33)
    {
        string[] mandatoryChecks =
        [
            "tier1-contract-ranking-relationship",
            "tier1-conversation-synthesis-security",
            "tier1-production-ui-corpus-evidence",
            "tier1-build-full-tests",
            "tier2-postgresql-restart-resume",
            "tier3-candidate-tool-database-preflight",
            "tier3-real-scan",
            "tier3-grounded-chat-corpus",
            "tier3-restart-continuity",
            "tier3-screenshot-release-integrity",
        ];
        var checks = new JsonArray();
        foreach (string id in mandatoryChecks)
        {
            int tier = id.StartsWith("tier1-", StringComparison.Ordinal)
                ? 1
                : id.StartsWith("tier2-", StringComparison.Ordinal) ? 2 : 3;
            checks.Add(new JsonObject
            {
                ["id"] = id,
                ["tier"] = tier,
                ["status"] = "passed",
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = "story125-release-evidence/2.2",
            ["runId"] = "story125-20260827t120000z-abcdef12",
            ["supersedesRunId"] = null,
            ["status"] = "passed",
            ["candidate"] = null,
            ["environment"] = null,
            ["commands"] = new JsonArray(),
            ["checks"] = checks,
            ["attemptedObservations"] = new JsonArray(),
            ["result"] = new JsonObject { ["kind"] = "passed" },
            ["releaseReceipt"] = new JsonObject
            {
                ["screenshots"] = new JsonArray
                {
                    new JsonObject { ["viewKind"] = "scan-state" },
                    new JsonObject { ["viewKind"] = "business-answer" },
                    new JsonObject { ["viewKind"] = "it-answer" },
                    new JsonObject { ["viewKind"] = "restart" },
                    new JsonObject { ["viewKind"] = "release-summary" },
                },
                ["chat"] = new JsonObject
                {
                    ["caseCount"] = 12,
                    ["corpusResultCount"] = 12,
                    ["usefulCount"] = usefulCount,
                    ["usefulPercent"] = usefulPercent,
                    ["acceptedTechnicalClaimCitationPercent"] = 100,
                    ["unauthorizedDisclosureCount"] = 0,
                    ["overThirtySecondAcceptedAnswerCount"] = 0,
                    ["caseReceipts"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["caseId"] = "q01",
                            ["providerDisposition"] = "invoked",
                            ["totalAcceptedAnswerDurationMilliseconds"] = 1000,
                        },
                    },
                },
            },
        };
    }

    private static JsonObject CreateValidChatPacket()
    {
        const string systemId = "11111111-1111-1111-1111-111111111111";
        const string repositoryA = "22222222-2222-2222-2222-222222222222";
        const string repositoryB = "33333333-3333-3333-3333-333333333333";
        const string documentId = "44444444-4444-4444-4444-444444444444";
        const string versionId = "55555555-5555-5555-5555-555555555555";
        const string sectionId = "66666666-6666-6666-6666-666666666666";
        const string anchorId = "77777777-7777-7777-7777-777777777777";

        return new JsonObject
        {
            ["schemaVersion"] = "grounded-chat-evidence/2.2",
            ["requestId"] = "request-story125-01",
            ["mode"] = "business",
            ["systemId"] = systemId,
            ["repositoryIds"] = new JsonArray(repositoryA, repositoryB),
            ["selection"] = new JsonObject
            {
                ["policy"] = "lexical-current-v2/1.0",
                ["candidateWindow"] = 256,
                ["primarySectionTarget"] = 12,
                ["primaryDocumentHandles"] = new JsonArray("doc-account"),
                ["primarySectionHandles"] = new JsonArray("section-account-summary"),
                ["expansionSeedDocumentHandles"] = new JsonArray("doc-account"),
            },
            ["documents"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "doc-account",
                    ["logicalDocumentId"] = documentId,
                    ["documentVersionId"] = versionId,
                    ["repositoryId"] = repositoryA,
                    ["documentType"] = "businessLogic",
                    ["profiledRevision"] = "abcdef1",
                    ["title"] = "Account authorization flow"
                }
            },
            ["sections"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "section-account-summary",
                    ["documentHandle"] = "doc-account",
                    ["stableSectionId"] = sectionId,
                    ["order"] = 1,
                    ["kind"] = "summary",
                    ["heading"] = "Authorization decision",
                    ["contentMarkdown"] = "The account service checks the current authorization decision.",
                    ["anchorHandles"] = new JsonArray("anchor-account-01"),
                    ["supportingDocumentHandles"] = new JsonArray()
                }
            },
            ["sourceAnchors"] = new JsonArray
            {
                new JsonObject
                {
                    ["handle"] = "anchor-account-01",
                    ["anchorId"] = anchorId,
                    ["documentHandle"] = "doc-account",
                    ["repositoryId"] = repositoryA,
                    ["repositoryRelativePath"] = "src/account/Authorization.cs",
                    ["lineStart"] = 10,
                    ["lineEnd"] = 18,
                    ["sourceHash"] = new string('a', 64)
                }
            },
            ["relationships"] = new JsonArray(),
            ["flowGroups"] = new JsonArray(),
            ["flowSteps"] = new JsonArray(),
            ["advisoryContext"] = "",
            ["advisoryContextAuthorityFingerprint"] = new string('c', 64),
            ["conversation"] = new JsonObject { ["turns"] = new JsonArray() },
            ["fingerprint"] = new string('b', 64)
        };
    }

    private static JsonObject CreateValidEvaluationCorpus()
    {
        var cases = new JsonArray();
        for (int i = 1; i <= 12; i++)
        {
            string id = $"q{i:00}";
            cases.Add(new JsonObject
            {
                ["id"] = id,
                ["mode"] = i % 2 == 0 ? "it" : "business",
                ["systemAlias"] = "mbs",
                ["repositoryAliases"] = new JsonArray("mbs-account", "mbs-channel"),
                ["question"] = $"What is the release decision for case {i}?",
                ["followUpTo"] = null,
                ["expectedEvidenceThemes"] = new JsonArray("authorization", "release-decision"),
                ["expectedRelationshipKinds"] = new JsonArray("references"),
                ["usefulAnswerCriteria"] = new JsonArray("Explains the decision and cites current evidence."),
                ["citationExpectation"] = "required",
                ["authorizationExpectation"] = "authorized",
                ["observation"] = new JsonObject
                {
                    ["status"] = "planned",
                    ["durationMilliseconds"] = null,
                    ["packetFingerprint"] = null,
                    ["answerFingerprint"] = null,
                    ["useful"] = null,
                    ["allAcceptedTechnicalClaimsCited"] = null,
                    ["unauthorizedDisclosure"] = null,
                    ["limitation"] = "Not executed; prepared for the release run."
                }
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = "story125-chat-evaluation/1.0",
            ["corpusId"] = "story125-release-corpus",
            ["sourceAuthorities"] = new JsonArray(
                "docs/bmad/epics.md",
                "specs/014-grounded-llm-wiki-chat-release-evidence/spec.md"),
            ["cases"] = cases
        };
    }

    private static JsonObject CreateValidReleaseEnvelope()
    {
        return new JsonObject
        {
            ["schemaVersion"] = "story125-release-evidence/2.2",
            ["runId"] = "story125-20260827t120000z-abcdef12",
            ["supersedesRunId"] = null,
            ["status"] = "planned",
            ["candidate"] = null,
            ["environment"] = null,
            ["commands"] = new JsonArray(),
            ["checks"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "tier1-contract-ranking-relationship",
                    ["tier"] = 1,
                    ["status"] = "planned",
                    ["commandIds"] = new JsonArray(),
                    ["artifacts"] = new JsonArray(),
                    ["attemptedObservationIds"] = new JsonArray(),
                    ["reason"] = null
                }
            },
            ["attemptedObservations"] = new JsonArray(),
            ["result"] = new JsonObject { ["kind"] = "planned" },
            ["releaseReceipt"] = null
        };
    }

    private JsonDocument LoadSchema(string fileName)
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, ContractDirectory, fileName);
        Assert.True(File.Exists(path), $"Accepted contract schema is missing: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private JsonDocument LoadQuestionCorpusFixture()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "Vulgata.Tests",
            "ReleaseGate",
            "Story125",
            "Fixtures",
            "story125-question-corpus.json");
        Assert.True(
            File.Exists(path),
            $"T006 RED: the Story 12.5 question corpus fixture is intentionally absent until T007 creates it: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vulgata.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The Vulgata repository root could not be located.");
    }

    private static class SchemaValidator
    {
        public static List<string> Validate(JsonElement instance, JsonElement schema)
        {
            var errors = new List<string>();
            ValidateNode(instance, schema, schema, "$", errors);
            return errors;
        }

        private static void ValidateNode(
            JsonElement instance,
            JsonElement schema,
            JsonElement rootSchema,
            string path,
            ICollection<string> errors)
        {
            if (schema.TryGetProperty("$ref", out JsonElement reference))
            {
                string pointer = reference.GetString() ?? string.Empty;
                if (!pointer.StartsWith("#/$defs/", StringComparison.Ordinal))
                {
                    errors.Add($"{path}: unsupported schema reference");
                    return;
                }

                string definition = pointer[8..];
                if (!rootSchema.GetProperty("$defs").TryGetProperty(definition, out JsonElement definitionSchema))
                {
                    errors.Add($"{path}: unresolved schema reference");
                    return;
                }

                ValidateNode(instance, definitionSchema, rootSchema, path, errors);
                return;
            }

            if (schema.TryGetProperty("oneOf", out JsonElement oneOf))
            {
                int validAlternatives = 0;
                foreach (JsonElement alternative in oneOf.EnumerateArray())
                {
                    var alternativeErrors = new List<string>();
                    ValidateNode(instance, alternative, rootSchema, path, alternativeErrors);
                    if (alternativeErrors.Count == 0)
                    {
                        validAlternatives++;
                    }
                }

                if (validAlternatives != 1)
                {
                    errors.Add($"{path}: expected exactly one matching schema alternative");
                }
            }

            if (schema.TryGetProperty("allOf", out JsonElement allOf))
            {
                foreach (JsonElement subschema in allOf.EnumerateArray())
                {
                    ValidateNode(instance, subschema, rootSchema, path, errors);
                }
            }

            if (schema.TryGetProperty("if", out JsonElement condition))
            {
                var conditionErrors = new List<string>();
                ValidateNode(instance, condition, rootSchema, path, conditionErrors);
                string branch = conditionErrors.Count == 0 ? "then" : "else";
                if (schema.TryGetProperty(branch, out JsonElement branchSchema))
                {
                    ValidateNode(instance, branchSchema, rootSchema, path, errors);
                }
            }

            if (schema.TryGetProperty("const", out JsonElement constant) &&
                !JsonEquals(instance, constant))
            {
                errors.Add($"{path}: value does not match const");
            }

            if (schema.TryGetProperty("enum", out JsonElement enumValues) &&
                !enumValues.EnumerateArray().Any(value => JsonEquals(instance, value)))
            {
                errors.Add($"{path}: value is not in enum");
            }

            if (schema.TryGetProperty("type", out JsonElement type))
            {
                string? expectedType = type.ValueKind == JsonValueKind.String ? type.GetString() : null;
                if (expectedType is not null && !MatchesType(instance, expectedType))
                {
                    errors.Add($"{path}: expected {expectedType}");
                    return;
                }
            }

            if (instance.ValueKind == JsonValueKind.Object)
            {
                ValidateObject(instance, schema, rootSchema, path, errors);
            }
            else if (instance.ValueKind == JsonValueKind.Array)
            {
                ValidateArray(instance, schema, rootSchema, path, errors);
            }
            else if (instance.ValueKind == JsonValueKind.String)
            {
                ValidateString(instance, schema, path, errors);
            }
            else if (instance.ValueKind == JsonValueKind.Number)
            {
                ValidateNumber(instance, schema, path, errors);
            }
        }

        private static void ValidateObject(
            JsonElement instance,
            JsonElement schema,
            JsonElement rootSchema,
            string path,
            ICollection<string> errors)
        {
            if (schema.TryGetProperty("required", out JsonElement required))
            {
                foreach (JsonElement property in required.EnumerateArray())
                {
                    string name = property.GetString() ?? string.Empty;
                    if (!instance.TryGetProperty(name, out _))
                    {
                        errors.Add($"{path}: missing required property {name}");
                    }
                }
            }

            if (schema.TryGetProperty("additionalProperties", out JsonElement additional) &&
                additional.ValueKind == JsonValueKind.False &&
                schema.TryGetProperty("properties", out JsonElement properties))
            {
                foreach (JsonProperty property in instance.EnumerateObject())
                {
                    if (!properties.TryGetProperty(property.Name, out _))
                    {
                        errors.Add($"{path}: unexpected property {property.Name}");
                    }
                }
            }

            if (!schema.TryGetProperty("properties", out JsonElement definedProperties))
            {
                return;
            }

            foreach (JsonProperty property in definedProperties.EnumerateObject())
            {
                if (instance.TryGetProperty(property.Name, out JsonElement value))
                {
                    ValidateNode(value, property.Value, rootSchema, $"{path}.{property.Name}", errors);
                }
            }
        }

        private static void ValidateArray(
            JsonElement instance,
            JsonElement schema,
            JsonElement rootSchema,
            string path,
            ICollection<string> errors)
        {
            int count = instance.GetArrayLength();
            if (schema.TryGetProperty("minItems", out JsonElement min) && count < min.GetInt32())
            {
                errors.Add($"{path}: fewer than minItems");
            }

            if (schema.TryGetProperty("maxItems", out JsonElement max) && count > max.GetInt32())
            {
                errors.Add($"{path}: more than maxItems");
            }

            if (schema.TryGetProperty("uniqueItems", out JsonElement unique) && unique.GetBoolean())
            {
                string[] values = instance.EnumerateArray().Select(value => value.GetRawText()).ToArray();
                if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                {
                    errors.Add($"{path}: duplicate array items");
                }
            }

            if (schema.TryGetProperty("items", out JsonElement itemSchema))
            {
                int index = 0;
                foreach (JsonElement item in instance.EnumerateArray())
                {
                    ValidateNode(item, itemSchema, rootSchema, $"{path}[{index}]", errors);
                    index++;
                }
            }
        }

        private static void ValidateString(
            JsonElement instance,
            JsonElement schema,
            string path,
            ICollection<string> errors)
        {
            string value = instance.GetString() ?? string.Empty;
            if (schema.TryGetProperty("minLength", out JsonElement min) && value.Length < min.GetInt32())
            {
                errors.Add($"{path}: shorter than minLength");
            }

            if (schema.TryGetProperty("maxLength", out JsonElement max) && value.Length > max.GetInt32())
            {
                errors.Add($"{path}: longer than maxLength");
            }

            if (schema.TryGetProperty("pattern", out JsonElement pattern) &&
                !Regex.IsMatch(value, pattern.GetString() ?? string.Empty, RegexOptions.CultureInvariant))
            {
                errors.Add($"{path}: value does not match pattern");
            }

            if (schema.TryGetProperty("format", out JsonElement format) &&
                format.GetString() == "uuid" &&
                !Guid.TryParse(value, out _))
            {
                errors.Add($"{path}: invalid uuid");
            }
        }

        private static void ValidateNumber(
            JsonElement instance,
            JsonElement schema,
            string path,
            ICollection<string> errors)
        {
            if (!instance.TryGetDecimal(out decimal value))
            {
                errors.Add($"{path}: invalid number");
                return;
            }

            if (schema.TryGetProperty("minimum", out JsonElement minimum) && value < minimum.GetDecimal())
            {
                errors.Add($"{path}: below minimum");
            }

            if (schema.TryGetProperty("maximum", out JsonElement maximum) && value > maximum.GetDecimal())
            {
                errors.Add($"{path}: above maximum");
            }

            if (schema.TryGetProperty("multipleOf", out JsonElement multipleOf) &&
                value % multipleOf.GetDecimal() != 0)
            {
                errors.Add($"{path}: not a multipleOf value");
            }
        }

        private static bool MatchesType(JsonElement value, string expectedType) => expectedType switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };

        private static bool JsonEquals(JsonElement left, JsonElement right) =>
            string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }
}
