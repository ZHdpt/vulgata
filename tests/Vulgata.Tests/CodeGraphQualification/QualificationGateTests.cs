using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vulgata.Tests.CodeGraphQualification;

[Trait("Category", "CodeGraphQualificationSynthetic")]
public sealed class QualificationGateTests
{
    [Fact]
    public async Task QualificationSchema_WhenHistoricalSchema10ResultClaimsPass_RejectsItAsync()
    {
        JsonObject historicalPass = CreateSchema11Result();
        historicalPass["schemaVersion"] = "1.0";
        historicalPass.Remove("structuralEvidence");

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(historicalPass);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenFailureClaimsQualifiedCategory_RejectsItAsync()
    {
        JsonObject failedAsQualified = CreateSchema11Result();
        failedAsQualified["status"] = "fail";
        failedAsQualified["nextAction"] = "corrective-planning";

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(failedAsQualified);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenHistoricalSchema10FailureIsReadFromEvidence_AcceptsItAsync()
    {
        string historicalResultPath = QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Fixtures", "historical-qualification-result.schema1.0.json");
        JsonObject historicalFailure = JsonNode.Parse(await File.ReadAllTextAsync(historicalResultPath))!.AsObject();

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(historicalFailure);

        Assert.True(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenRetiredSchema11ResultIsPresented_RejectsItAsync()
    {
        JsonObject retired = CreateSchema11Result();
        retired["schemaVersion"] = "1.1";
        retired["nextAction"] = "return-to-plan";

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(retired);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenSchema11OmitsStructuralEvidence_RejectsItAsync()
    {
        JsonObject result = CreateSchema11Result();
        result.Remove("structuralEvidence");

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenSafeSchema11ProjectionUsesOnlyApprovedFacts_AcceptsItAsync()
    {
        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(CreateSchema11Result());

        Assert.True(validation.IsValid, validation.Diagnostic);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("symbol")]
    [InlineData("source")]
    [InlineData("absolutePath")]
    [InlineData("provider")]
    [InlineData("rawOutput")]
    public async Task QualificationSchema_WhenStructuralEvidenceContainsRawOrProviderProperty_RejectsItAsync(string propertyName)
    {
        JsonObject result = CreateSchema11Result();
        result["structuralEvidence"]!.AsObject()[propertyName] = "synthetic-unretained-value";

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenStructuralEvidenceContainsArbitraryRelationshipKind_RejectsItAsync()
    {
        JsonObject result = CreateSchema11Result();
        result["structuralEvidence"]!.AsObject()["relationshipKinds"] =
            new JsonArray("CALLS", "IMPORTS", "INHERITS", "UNAPPROVED");

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Theory]
    [MemberData(nameof(InvalidPassCases))]
    public async Task QualificationSchema_WhenPassMissesLifecycleCleanupOrStreamingProof_RejectsItAsync(
        string scenario,
        Action<JsonObject> mutate)
    {
        JsonObject result = CreateSchema11Result();
        mutate(result);

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, $"{scenario}: {validation.Diagnostic}");
    }

    [Fact]
    public async Task QualificationSchema_WhenOutputIsExactlyAtSharedCap_RemainsEligibleAsync()
    {
        JsonObject result = CreateSchema11Result();
        JsonObject outputCap = GetStructuralEvidence(result)["outputCap"]!.AsObject();
        JsonObject stdout = GetStructuralEvidence(result)["streams"]!.AsObject()["stdout"]!.AsObject();
        outputCap["observedTotalBytes"] = 65536;
        stdout["bytes"] = 65536;

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.True(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenOutputReachesBoundedOverflowSentinel_RequiresFailureAndForcedCleanupAsync()
    {
        JsonObject result = CreateSchema11Result();
        result["status"] = "fail";
        result["category"] = "unsafe-evidence";
        result["nextAction"] = "corrective-planning";
        JsonObject structuralEvidence = GetStructuralEvidence(result);
        JsonObject outputCap = structuralEvidence["outputCap"]!.AsObject();
        JsonObject stdout = structuralEvidence["streams"]!.AsObject()["stdout"]!.AsObject();
        outputCap["observedTotalBytes"] = 65537;
        outputCap["overflowed"] = true;
        outputCap["terminationRequested"] = true;
        outputCap["containerCleanupForced"] = true;
        stdout["bytes"] = 65537;

        SchemaValidationResult overflowValidation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.True(overflowValidation.IsValid, overflowValidation.Diagnostic);

        outputCap["overflowed"] = false;
        SchemaValidationResult overCapWithoutOverflow = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(overCapWithoutOverflow.IsValid, overCapWithoutOverflow.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenMarkerBoundaryContractDropsStrictUtf8OrStdoutProof_RejectsItAsync()
    {
        JsonObject result = CreateSchema11Result();
        JsonObject structuralEvidence = GetStructuralEvidence(result);
        structuralEvidence["streamingGrammar"]!.AsObject()["strictIncrementalUtf8"] = false;

        SchemaValidationResult nonStrictUtf8 = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(nonStrictUtf8.IsValid, nonStrictUtf8.Diagnostic);

        result = CreateSchema11Result();
        structuralEvidence = GetStructuralEvidence(result);
        structuralEvidence["markers"]!.AsObject()["stdoutOrderedPair"] = false;
        SchemaValidationResult unorderedStdoutMarkers = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(unorderedStdoutMarkers.IsValid, unorderedStdoutMarkers.Diagnostic);

        result = CreateSchema11Result();
        GetStructuralEvidence(result)["streamingGrammar"]!.AsObject()["maximumRecordBytes"] = 4097;
        SchemaValidationResult oversizedRecordAllowance = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(oversizedRecordAllowance.IsValid, oversizedRecordAllowance.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenRawOutputWouldBeRetained_RejectsItAsync()
    {
        JsonObject result = CreateSchema11Result();
        GetStructuralEvidence(result)["rawOutputRetained"] = true;

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenCleanupFails_RequiresCleanupFailurePrecedenceAsync()
    {
        JsonObject result = CreateSchema11Result();
        result["status"] = "fail";
        result["category"] = "cleanup-failure";
        result["nextAction"] = "corrective-planning";
        result["cleanup"]!.AsObject()["succeeded"] = false;

        SchemaValidationResult precedenceValidation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.True(precedenceValidation.IsValid, precedenceValidation.Diagnostic);

        result["category"] = "unsafe-evidence";
        SchemaValidationResult ignoredPrecedence = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(ignoredPrecedence.IsValid, ignoredPrecedence.Diagnostic);
    }

    [Fact]
    public async Task QualificationSchema_WhenResultRequestsProviderMapping_RejectsItAsync()
    {
        JsonObject result = CreateSchema11Result();
        result["nextAction"] = "provider-mapping";

        SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);

        Assert.False(validation.IsValid, validation.Diagnostic);
    }

    public static IEnumerable<object[]> InvalidPassCases()
    {
        yield return
        [
            "missing baseline lifecycle command",
            (Action<JsonObject>)(result => result["commands"]!.AsArray().RemoveAt(4)),
        ];
        yield return
        [
            "cleanup was not attempted",
            (Action<JsonObject>)(result => result["cleanup"]!.AsObject()["attempted"] = false),
        ];
        yield return
        [
            "cleanup did not succeed",
            (Action<JsonObject>)(result => result["cleanup"]!.AsObject()["succeeded"] = false),
        ];
        yield return
        [
            "shared cap overflowed",
            (Action<JsonObject>)(result => GetStructuralEvidence(result)["outputCap"]!.AsObject()["overflowed"] = true),
        ];
        yield return
        [
            "observed bytes exceed the inclusive cap",
            (Action<JsonObject>)(result => GetStructuralEvidence(result)["outputCap"]!.AsObject()["observedTotalBytes"] = 65537),
        ];
        yield return
        [
            "process-tree termination was requested",
            (Action<JsonObject>)(result => GetStructuralEvidence(result)["outputCap"]!.AsObject()["terminationRequested"] = true),
        ];
        yield return
        [
            "forced container cleanup was needed",
            (Action<JsonObject>)(result => GetStructuralEvidence(result)["outputCap"]!.AsObject()["containerCleanupForced"] = true),
        ];
        yield return
        [
            "query completion predicate is not proved",
            (Action<JsonObject>)(result => GetStructuralEvidence(result)["predicateFacts"]!.AsObject()["queryCompleted"] = false),
        ];
    }

    [Fact]
    public async Task QualificationGate_WhenExactQualifiedEvidenceIsSafe_WritesCompletePassResultAsync()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"codegraph-qualification-{Guid.NewGuid():N}.json");

        try
        {
            await QualificationResultWriter.WriteQualifiedAsync(
                outputPath,
                new QualifiedResultInput(
                    ImageReference: "vulgata-codegraph-qualification:0.5.1",
                    ImageDigest: "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    PythonVersion: "3.12.13",
                    RequirementsLockSha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                    FixtureManifestSha256: "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                    ExecutionUser: "app",
                    Commands:
                    [
                        new QualificationCommandEvidence("cgc-version", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-doctor", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-context-create", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-context-mode", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-context-default", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-index", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-api-start", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-api-health", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-api-query", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("gateway-stop", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                        new QualificationCommandEvidence("cgc-context-delete", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                    ],
                    EvidencePaths: ["evidence/red.md", "evidence/verification.md"],
                    ElapsedMilliseconds: 123_456,
                    CleanupSucceeded: true,
                    StructuralEvidence: CreateQualifiedStructuralEvidence()));

            using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            JsonElement root = document.RootElement;

            Assert.Equal("pass", root.GetProperty("status").GetString());
            Assert.Equal("1.2", root.GetProperty("schemaVersion").GetString());
            Assert.Equal("qualified", root.GetProperty("category").GetString());
            Assert.Equal("parser-planning", root.GetProperty("nextAction").GetString());
            Assert.Equal("vulgata-codegraph-qualification:0.5.1", root.GetProperty("image").GetProperty("reference").GetString());
            Assert.Equal("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", root.GetProperty("image").GetProperty("digest").GetString());
            Assert.Equal("3.12.13", root.GetProperty("python").GetProperty("version").GetString());
            Assert.Equal("0.5.1", root.GetProperty("packages").GetProperty("codegraphcontextVersion").GetString());
            Assert.Matches("^[a-f0-9]{64}$", root.GetProperty("packages").GetProperty("lockSha256").GetString());
            Assert.Matches("^[a-f0-9]{64}$", root.GetProperty("fixtureManifestSha256").GetString());
            Assert.Equal("app", root.GetProperty("executionIdentity").GetProperty("user").GetString());
            Assert.True(root.GetProperty("executionIdentity").GetProperty("nonRoot").GetBoolean());
            Assert.Equal(123_456, root.GetProperty("elapsedMilliseconds").GetInt32());
            Assert.True(root.GetProperty("cleanup").GetProperty("attempted").GetBoolean());
            Assert.True(root.GetProperty("cleanup").GetProperty("succeeded").GetBoolean());
            Assert.True(root.GetProperty("evidence").GetProperty("bounded").GetBoolean());
            Assert.True(root.GetProperty("evidence").GetProperty("redacted").GetBoolean());
            Assert.False(root.GetProperty("structuralEvidence").GetProperty("rawOutputRetained").GetBoolean());
            Assert.Equal(
                "vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2",
                root.GetProperty("structuralEvidence").GetProperty("grammarId").GetString());

            JsonElement[] commands = root.GetProperty("commands").EnumerateArray().ToArray();
            Assert.Equal(11, commands.Length);
            Assert.All(commands, command =>
            {
                Assert.Equal(0, command.GetProperty("exitCode").GetInt32());
                Assert.Matches("^[a-f0-9]{64}$", command.GetProperty("stdoutSha256").GetString());
                Assert.Matches("^[a-f0-9]{64}$", command.GetProperty("stderrSha256").GetString());
            });
            Assert.Equal(
                ["cgc-version", "cgc-doctor", "cgc-context-create", "cgc-context-mode", "cgc-context-default", "cgc-index", "cgc-api-start", "cgc-api-health", "cgc-api-query", "gateway-stop", "cgc-context-delete"],
                commands.Select(command => command.GetProperty("name").GetString()!).ToArray());
            Assert.Equal(
                ["evidence/red.md", "evidence/verification.md"],
                root.GetProperty("evidence").GetProperty("paths").EnumerateArray().Select(path => path.GetString()!).ToArray());
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task QualificationGate_WhenQualifiedEvidenceIsUnsafeOrCleanupFailed_DoesNotWritePartialPassAsync()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"codegraph-qualification-{Guid.NewGuid():N}.json");
        QualifiedResultInput unsafeInput = new(
            ImageReference: "vulgata-codegraph-qualification:0.5.1",
            ImageDigest: "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            PythonVersion: "3.12.13",
            RequirementsLockSha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            FixtureManifestSha256: "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            ExecutionUser: "app",
            Commands:
            [
                new QualificationCommandEvidence("cgc-version", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-doctor", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-context-create", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-context-mode", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-context-default", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-index", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-api-start", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-api-health", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-api-query", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("gateway-stop", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
                new QualificationCommandEvidence("cgc-context-delete", 0, "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"),
            ],
            EvidencePaths: ["C:\\private\\checkout\\synthetic-token.md"],
            ElapsedMilliseconds: 123_456,
            CleanupSucceeded: false,
            StructuralEvidence: CreateQualifiedStructuralEvidence());

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => QualificationResultWriter.WriteQualifiedAsync(outputPath, unsafeInput));

            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidQualifiedInputMutations))]
    public async Task QualificationGate_WhenWouldBePassViolatesLifecycleOrStreamingInvariant_DoesNotWritePartialPassAsync(
        string scenario,
        object mutation)
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"codegraph-qualification-{Guid.NewGuid():N}.json");

        try
        {
            Func<QualifiedResultInput, QualifiedResultInput> mutate = Assert.IsType<Func<QualifiedResultInput, QualifiedResultInput>>(mutation);
            QualifiedResultInput invalidInput = mutate(CreateQualifiedResultInput());

            await Assert.ThrowsAsync<ArgumentException>(
                () => QualificationResultWriter.WriteQualifiedAsync(outputPath, invalidInput));

            Assert.False(File.Exists(outputPath), scenario);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task QualificationGate_WhenCleanupFails_WritesOnlySchema11CleanupFailureWithCorrectivePlanningAsync()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"codegraph-qualification-{Guid.NewGuid():N}.json");
        QualifiedResultInput qualified = CreateQualifiedResultInput();
        CgcStreamingEvidence cleanupFailure = qualified.StructuralEvidence with { CleanupSucceeded = false };
        UnqualifiedResultInput input = new(
            qualified.ImageReference,
            qualified.ImageDigest,
            qualified.PythonVersion,
            qualified.RequirementsLockSha256,
            qualified.FixtureManifestSha256,
            qualified.ExecutionUser,
            true,
            qualified.Commands,
            qualified.EvidencePaths,
            qualified.ElapsedMilliseconds,
            "unsafe-evidence",
            cleanupFailure);

        try
        {
            await QualificationResultWriter.WriteCorrectiveAsync(outputPath, input);

            JsonObject result = JsonNode.Parse(await File.ReadAllTextAsync(outputPath))!.AsObject();
            Assert.Equal("fail", result["status"]!.GetValue<string>());
            Assert.Equal("cleanup-failure", result["category"]!.GetValue<string>());
            Assert.Equal("corrective-planning", result["nextAction"]!.GetValue<string>());
            Assert.False(result["cleanup"]!.AsObject()["succeeded"]!.GetValue<bool>());

            SchemaValidationResult validation = await ValidateAgainstQualificationSchemaAsync(result);
            Assert.True(validation.IsValid, validation.Diagnostic);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task QualificationGate_WhenDockerEvidenceContainsUnsafeText_WritesRedactedCorrectiveStopAsync()
    {
        string outputPath = Path.Combine(Path.GetTempPath(), $"codegraph-qualification-{Guid.NewGuid():N}.json");
        string manifestPath = QualificationTestPaths.FromRepositoryRoot(
            "tests", "Vulgata.Tests", "CodeGraphQualification", "Fixtures", "qualification-fixture-manifest.json");

        try
        {
            await QualificationResultWriter.WriteDockerDaemonUnavailableAsync(
                outputPath,
                manifestPath,
                "stdout /private/checkout/token=synthetic-token",
                "stderr C:\\private\\checkout\\credential=synthetic-password",
                exitCode: 1);

            string result = await File.ReadAllTextAsync(outputPath);
            using JsonDocument document = JsonDocument.Parse(result);

            Assert.Equal("fail", document.RootElement.GetProperty("status").GetString());
            Assert.Equal("docker-daemon-unavailable", document.RootElement.GetProperty("category").GetString());
            Assert.Equal("corrective-planning", document.RootElement.GetProperty("nextAction").GetString());
            Assert.Equal(
                "0.5.1",
                document.RootElement.GetProperty("packages").GetProperty("codegraphcontextVersion").GetString());
            Assert.DoesNotContain("synthetic-token", result, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-password", result, StringComparison.Ordinal);
            Assert.DoesNotContain("C:\\private\\checkout", result, StringComparison.Ordinal);
            Assert.True(document.RootElement.GetProperty("evidence").GetProperty("bounded").GetBoolean());
            Assert.True(document.RootElement.GetProperty("evidence").GetProperty("redacted").GetBoolean());
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    public static IEnumerable<object[]> InvalidQualifiedInputMutations()
    {
        yield return
        [
            "a required lifecycle command is absent",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with { Commands = input.Commands.Take(10).ToArray() }),
        ];
        yield return
        [
            "a lifecycle command is duplicated",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                Commands = [.. input.Commands.Take(10), input.Commands[0]],
            }),
        ];
        yield return
        [
            "an unapproved lifecycle command is substituted",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                Commands = input.Commands
                    .Select(command => command.Name == "cgc-api-query"
                        ? command with { Name = "cgc-unapproved" }
                        : command)
                    .ToArray(),
            }),
        ];
        yield return
        [
            "a lifecycle command exits non-zero",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                Commands = input.Commands
                    .Select(command => command.Name == "cgc-api-query" ? command with { ExitCode = 23 } : command)
                    .ToArray(),
            }),
        ];
        yield return
        [
            "cleanup was not attempted",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with { CleanupAttempted = false },
            }),
        ];
        yield return
        [
            "cleanup did not succeed",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                CleanupSucceeded = false,
                StructuralEvidence = input.StructuralEvidence with { CleanupSucceeded = false },
            }),
        ];
        yield return
        [
            "a pass claims termination",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with { TerminationRequested = true },
            }),
        ];
        yield return
        [
            "stream byte conservation fails",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with
                {
                    StandardOutput = input.StructuralEvidence.StandardOutput with
                    {
                        Bytes = input.StructuralEvidence.StandardOutput.Bytes + 1,
                    },
                },
            }),
        ];
        yield return
        [
            "the canonical safe summary does not match the finite facts",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with { CanonicalSummarySha256 = new string('0', 64) },
            }),
        ];
        yield return
        [
            "the stdout end marker is absent",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with
                {
                    StdoutEndCount = 0,
                    HasExactlyOneOrderedStdoutMarkerPair = false,
                },
            }),
        ];
        yield return
        [
            "the filtered-target predicate is absent",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with { HasFilteredTargetCandidate = false },
            }),
        ];
        yield return
        [
            "an unknown relationship kind is present",
            (Func<QualifiedResultInput, QualifiedResultInput>)(input => input with
            {
                StructuralEvidence = input.StructuralEvidence with
                {
                    RelationshipKindCounts = new Dictionary<string, int>(input.StructuralEvidence.RelationshipKindCounts, StringComparer.Ordinal)
                    {
                        ["UNAPPROVED"] = 1,
                    },
                },
            }),
        ];
    }

    private static JsonObject CreateSchema11Result()
    {
        const string hashA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string hashB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        return new JsonObject
        {
            ["schemaVersion"] = "1.2",
            ["status"] = "pass",
            ["category"] = "qualified",
            ["nextAction"] = "parser-planning",
            ["image"] = new JsonObject
            {
                ["reference"] = "vulgata-codegraph-qualification:0.5.1",
                ["digest"] = $"sha256:{hashA}",
            },
            ["python"] = new JsonObject
            {
                ["lockPath"] = "docker/codegraph/python-version.txt",
                ["version"] = "3.12.13",
            },
            ["packages"] = new JsonObject
            {
                ["lockPath"] = "docker/codegraph/requirements.lock",
                ["lockSha256"] = hashA,
                ["codegraphcontextVersion"] = "0.5.1",
            },
            ["executionIdentity"] = new JsonObject
            {
                ["user"] = "app",
                ["nonRoot"] = true,
            },
            ["fixtureManifestSha256"] = hashB,
            ["commands"] = new JsonArray
            {
                CreateCommand("cgc-version", hashA, hashB),
                CreateCommand("cgc-doctor", hashA, hashB),
                CreateCommand("cgc-context-create", hashA, hashB),
                CreateCommand("cgc-context-mode", hashA, hashB),
                CreateCommand("cgc-context-default", hashA, hashB),
                CreateCommand("cgc-index", hashA, hashB),
                CreateCommand("cgc-api-start", hashA, hashB),
                CreateCommand("cgc-api-health", hashA, hashB),
                CreateCommand("cgc-api-query", hashA, hashB),
                CreateCommand("gateway-stop", hashA, hashB),
                CreateCommand("cgc-context-delete", hashA, hashB),
            },
            ["evidence"] = new JsonObject
            {
                ["bounded"] = true,
                ["redacted"] = true,
                ["paths"] = new JsonArray("evidence/final-gate.md"),
            },
            ["structuralEvidence"] = new JsonObject
            {
                ["rawOutputRetained"] = false,
                ["grammarId"] = "vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2",
                ["streamingGrammar"] = new JsonObject
                {
                    ["observer"] = "vulgata-cgc-streaming-evidence-v2",
                    ["queryStdoutRegion"] = "vulgata-cgc-http-json-v1",
                    ["structuralRecord"] = "vulgata-cgc-http-json-v1",
                    ["strictIncrementalUtf8"] = true,
                    ["maximumRecordBytes"] = 4096,
                },
                ["outputCap"] = new JsonObject
                {
                    ["limitBytes"] = 65536,
                    ["observedTotalBytes"] = 64,
                    ["overflowed"] = false,
                    ["terminationRequested"] = false,
                    ["containerCleanupForced"] = false,
                },
                ["streams"] = new JsonObject
                {
                    ["stdout"] = new JsonObject { ["bytes"] = 64, ["sha256"] = hashA },
                    ["stderr"] = new JsonObject { ["bytes"] = 0, ["sha256"] = hashB },
                },
                ["canonicalSummarySha256"] = hashA,
                ["markers"] = new JsonObject
                {
                    ["stdoutBeginCount"] = 1,
                    ["stdoutEndCount"] = 1,
                    ["stdoutOrderedPair"] = true,
                    ["stderrExcludedFromStructuralEvidence"] = true,
                },
                ["recordCounts"] = new JsonObject
                {
                    ["valid"] = 4,
                    ["invalidOrOversized"] = 0,
                },
                ["relationshipKinds"] = new JsonArray("CALLS", "IMPORTS", "INHERITS", "CONTAINS"),
                ["relationshipKindCounts"] = new JsonObject
                {
                    ["CALLS"] = 1,
                    ["IMPORTS"] = 1,
                    ["INHERITS"] = 1,
                    ["CONTAINS"] = 1,
                },
                ["predicateFacts"] = new JsonObject
                {
                    ["queryCompleted"] = true,
                    ["hasValidRecordGrammar"] = true,
                    ["allRequiredRelationshipKindsObserved"] = true,
                    ["hasFilteredTargetCandidate"] = true,
                    ["hasPositiveOrderedRange"] = true,
                    ["hasNonEmptyEndpointIdentity"] = true,
                    ["allStructuralFactsCorrelatedWithinValidRecords"] = true,
                },
            },
            ["elapsedMilliseconds"] = 123_456,
            ["cleanup"] = new JsonObject
            {
                ["attempted"] = true,
                ["succeeded"] = true,
            },
        };
    }

    private static JsonObject CreateCommand(string name, string stdoutSha256, string stderrSha256) => new()
    {
        ["name"] = name,
        ["exitCode"] = 0,
        ["stdoutSha256"] = stdoutSha256,
        ["stderrSha256"] = stderrSha256,
    };

    private static CgcStreamingEvidence CreateQualifiedStructuralEvidence()
    {
        const string stdoutSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string stderrSha256 = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        Dictionary<string, int> relationshipKindCounts = new(StringComparer.Ordinal)
        {
            ["CALLS"] = 1,
            ["CONTAINS"] = 1,
            ["IMPORTS"] = 1,
            ["INHERITS"] = 1,
        };
        string predicateCounts = string.Join(
            ',',
            "valid=4",
            "invalidOrOversized=0",
            "CALLS=1",
            "CONTAINS=1",
            "IMPORTS=1",
            "INHERITS=1",
            "allRequiredRelationshipKindsObserved=true",
            "allStructuralFactsCorrelatedWithinValidRecords=true",
            "hasFilteredTargetCandidate=true",
            "hasNonEmptyEndpointIdentity=true",
            "hasPositiveOrderedRange=true",
            "hasValidRecordGrammar=true",
            "queryCompleted=true");
        string canonicalSummary = string.Join(
            '|',
            "1.2",
            "64",
            "0",
            "64",
            "false",
            "false",
            "false",
            stdoutSha256,
            stderrSha256,
            "vulgata-cgc-http-json-v1,vulgata-cgc-streaming-evidence-v2",
            "CALLS,CONTAINS,IMPORTS,INHERITS",
            predicateCounts,
            "true",
            "true");

        return new CgcStreamingEvidence(
            IsValidUtf8: true,
            Overflowed: false,
            TerminationRequested: false,
            ObservedTotalBytes: 64,
            StandardOutput: new CgcStreamEvidence(64, stdoutSha256),
            StandardError: new CgcStreamEvidence(0, stderrSha256),
            CanonicalSummarySha256: Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalSummary))),
            StdoutBeginCount: 1,
            StdoutEndCount: 1,
            HasExactlyOneOrderedStdoutMarkerPair: true,
            RanAsNonRoot: true,
            IndexedWholeRepository: true,
            CleanupSucceeded: true,
            CleanupAttempted: true,
            ContainerCleanupForced: false,
            HasValidRecordGrammar: true,
            HasValidStructuralRecord: true,
            ValidRecordCount: 4,
            InvalidOrOversizedRecordCount: 0,
            RelationshipKindCounts: relationshipKindCounts,
            HasAllRequiredRelationshipKinds: true,
            HasFilteredTargetCandidate: true,
            HasPositiveOrderedRange: true,
            HasNonEmptyEndpointIdentity: true,
            AllStructuralFactsCorrelatedWithinValidRecords: true,
            Commands:
            [
                new QualificationCommandEvidence("cgc-version", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-doctor", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-context-create", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-context-mode", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-context-default", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-index", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-api-start", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-api-health", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-api-query", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("gateway-stop", 0, stdoutSha256, stderrSha256),
                new QualificationCommandEvidence("cgc-context-delete", 0, stdoutSha256, stderrSha256),
            ],
            CommandEvidenceOverflowed: false);
    }

    private static QualifiedResultInput CreateQualifiedResultInput()
    {
        const string hashA = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string hashB = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        return new QualifiedResultInput(
            ImageReference: "vulgata-codegraph-qualification:0.5.1",
            ImageDigest: $"sha256:{hashA}",
            PythonVersion: "3.12.13",
            RequirementsLockSha256: hashA,
            FixtureManifestSha256: hashB,
            ExecutionUser: "app",
            Commands:
            [
                new QualificationCommandEvidence("cgc-version", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-doctor", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-context-create", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-context-mode", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-context-default", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-index", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-api-start", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-api-health", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-api-query", 0, hashA, hashB),
                new QualificationCommandEvidence("gateway-stop", 0, hashA, hashB),
                new QualificationCommandEvidence("cgc-context-delete", 0, hashA, hashB),
            ],
            EvidencePaths: ["evidence/final-gate.md"],
            ElapsedMilliseconds: 123_456,
            CleanupSucceeded: true,
            StructuralEvidence: CreateQualifiedStructuralEvidence());
    }

    private static JsonObject GetStructuralEvidence(JsonObject result) => result["structuralEvidence"]!.AsObject();

    private static async Task<SchemaValidationResult> ValidateAgainstQualificationSchemaAsync(JsonObject result)
    {
        string temporaryResultPath = Path.Combine(Path.GetTempPath(), $"codegraph-schema-{Guid.NewGuid():N}.json");
        string schemaPath = QualificationTestPaths.FromRepositoryRoot(
            "specs", "001-codegraph-extraction", "qualification", "qualification-result.schema.json");

        try
        {
            await File.WriteAllTextAsync(temporaryResultPath, result.ToJsonString());

            ProcessStartInfo startInfo = new(GetPowerShellExecutable())
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(
                $"$valid = Test-Json -Path {ToPowerShellLiteral(temporaryResultPath)} -SchemaFile {ToPowerShellLiteral(schemaPath)}; if ($valid) {{ exit 0 }}; exit 1");

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start the local JSON-schema validator.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return new SchemaValidationResult(
                process.ExitCode == 0,
                $"Test-Json exit={process.ExitCode}; stdout={await standardOutput}; stderr={await standardError}");
        }
        finally
        {
            File.Delete(temporaryResultPath);
        }
    }

    private static string GetPowerShellExecutable() => "pwsh";

    private static string ToPowerShellLiteral(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private sealed record SchemaValidationResult(bool IsValid, string Diagnostic);
}
