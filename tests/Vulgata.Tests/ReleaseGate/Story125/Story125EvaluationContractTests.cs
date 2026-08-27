using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Reflection;

namespace Vulgata.Tests.ReleaseGate;

public sealed class Story125EvaluationContractTests
{
    private const string ContractDirectory =
        "specs/014-grounded-llm-wiki-chat-release-evidence/contracts";

    public static IEnumerable<object[]> AcceptedSchemas()
    {
        yield return ["chat-evidence-packet-v2.schema.json", "Grounded Chat Evidence Packet 2.0"];
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

    [Theory]
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

    [Theory]
    [MemberData(nameof(ValidContractDocuments))]
    public void AcceptedSchema_ValidKnownGoodDocument_Passes(string fileName, string json)
    {
        using JsonDocument schema = LoadSchema(fileName);
        using JsonDocument document = JsonDocument.Parse(json);

        IReadOnlyList<string> errors = SchemaValidator.Validate(document.RootElement, schema.RootElement);

        Assert.Empty(errors);
    }

    [Theory]
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

    [Fact]
    public void Story125QuestionCorpus_IsSchemaValidAndContainsTwelveToTwentyCases()
    {
        using JsonDocument corpus = LoadQuestionCorpusFixture();
        using JsonDocument schema = LoadSchema("story125-evaluation-case.schema.json");

        IReadOnlyList<string> errors = SchemaValidator.Validate(corpus.RootElement, schema.RootElement);

        Assert.Empty(errors);
        JsonElement cases = corpus.RootElement.GetProperty("cases");
        Assert.InRange(cases.GetArrayLength(), 12, 20);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

        MethodInfo? validator = typeof(Story125ReleaseHarness).GetMethod(
            "ValidateCrossArtifactConsistency",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        Assert.NotNull(
            validator);

        object? rawResult = validator!.Invoke(null, [root]);
        IReadOnlyList<string> errors = rawResult switch
        {
            IReadOnlyList<string> diagnostics => diagnostics,
            IEnumerable<string> diagnostics => diagnostics.ToArray(),
            bool valid => valid ? [] : ["cross-artifact consistency rejected"],
            _ => throw new InvalidOperationException(
                "ValidateCrossArtifactConsistency must return errors or a validity boolean."),
        };

        Assert.Empty(errors);
    }

    [Fact]
    public void Story125EvaluationScoring_RejectsAcceptedTechnicalClaimCitationCoverageBelowOneHundredPercent()
    {
        JsonObject envelope = CreatePassedScoringEnvelope();
        ((JsonObject)envelope["releaseReceipt"]!["chat"]!)!["acceptedTechnicalClaimCitationPercent"] = 99;

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.Contains(
            errors,
            error => error.Contains("citation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
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

    [Fact]
    public void Story125EvaluationScoring_UsesExactNonnegativeHalfUpUsefulPercentage()
    {
        JsonObject envelope = CreatePassedScoringEnvelope(usefulCount: 10, usefulPercent: 83.33);

        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(envelope);

        Assert.DoesNotContain(
            errors,
            error => error.Contains("useful percentage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Story125SemanticValidation_KnownGoodPlannedEnvelope_RemainsGreen()
    {
        IReadOnlyList<string> errors = Story125SemanticHarnessProbe.Validate(CreateValidReleaseEnvelope());

        Assert.Empty(errors);
    }

    [Theory]
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
            ["schemaVersion"] = "story125-release-evidence/2.1",
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
            ["schemaVersion"] = "grounded-chat-evidence/2.0",
            ["requestId"] = "request-story125-01",
            ["mode"] = "business",
            ["systemId"] = systemId,
            ["repositoryIds"] = new JsonArray(repositoryA, repositoryB),
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
            ["flowSteps"] = new JsonArray(),
            ["advisoryContext"] = "",
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
            ["schemaVersion"] = "story125-release-evidence/2.1",
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
