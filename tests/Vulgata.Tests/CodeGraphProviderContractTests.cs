using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Vulgata.Infrastructure.CodeGraph;

namespace Vulgata.Tests;

public sealed class CodeGraphProviderContractTests
{
    private const string _evidenceSchema = "cgc-provider-contract-evidence-v2";
    private const string _projectionAHash = "dbba4ff5ac4834ae2857307dad36c177d9655ac951744c646f9c6e54d3923f33";
    private const string _projectionBHash = "40d2da3b9b520b3c4eca3fd7c3bad4f4fb10643736e3a9e9b4ddf62c92adc096";
    private const string _providerLockHash = "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95";
    private const string _providerDistributionHash = "3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658";
    private static readonly HashSet<string> _safeNames = new(StringComparer.Ordinal)
    {
        "status", "message", "error", "data", "success", "query", "record_count", "results",
        "truncated", "result_limit", "record_kind", "provider_kind", "path", "name", "start_line",
        "end_line", "parser_language", "relationship_kind", "source_provider_kind", "source_path",
        "source_name", "source_start_line", "source_end_line", "target_provider_kind", "target_path",
        "target_name", "target_start_line", "target_end_line",
    };
    private const string _testIdentity =
        "Vulgata.Tests.CodeGraphProviderContractTests.FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract";
    private const string _reviewIdentity = "user-approved-story-4-2-plan-2026-07-20";
    private static readonly string[] _planArtifacts =
    [
        "specs/001-codegraph-extraction/plan.md",
        "specs/001-codegraph-extraction/research.md",
        "specs/001-codegraph-extraction/data-model.md",
        "specs/001-codegraph-extraction/quickstart.md",
        "specs/001-codegraph-extraction/contracts/repository-profiling-api.md",
        "specs/002-language-framework-detection/spec.md",
        "specs/002-language-framework-detection/plan.md",
        "specs/002-language-framework-detection/research.md",
        "specs/002-language-framework-detection/data-model.md",
        "specs/002-language-framework-detection/quickstart.md",
        "specs/002-language-framework-detection/contracts/framework-detection.md",
        "specs/002-language-framework-detection/contracts/provider-language-projection.md",
        "specs/002-language-framework-detection/contracts/repository-scan-overview.md",
    ];

    [Fact]
    public void AcceptedCorrectionManifest_IsCanonicalAndArtifactBound()
    {
        string repositoryRoot = FindRepositoryRoot();
        byte[] first = CreateAcceptedCorrectionManifest(repositoryRoot, "sha256:" + new string('a', 64));
        byte[] second = CreateAcceptedCorrectionManifest(repositoryRoot, "sha256:" + new string('a', 64));

        Assert.Equal(first, second);
        using JsonDocument manifest = JsonDocument.Parse(first);
        Assert.Equal(_reviewIdentity, manifest.RootElement.GetProperty("reviewIdentity").GetString());
        Assert.Equal(
            _planArtifacts,
            manifest.RootElement.GetProperty("artifacts")
                .EnumerateArray()
                .Select(item => item.GetProperty("path").GetString()));
        Assert.Equal(64, Hash(first).Length);
    }

    [Fact]
    public void ProviderFixture_EmptyFileAndManyRows_AreExactCompletionInputs()
    {
        string repositoryRoot = FindRepositoryRoot();
        string fixtureRoot = Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "CodeGraphProviderContract",
            "ProviderFixture");
        Assert.Empty(File.ReadAllBytes(Path.Combine(fixtureRoot, "empty_file.cs")));
        Assert.False(
            File.Exists(Path.Combine(fixtureRoot, "empty_file.py")),
            "The exceptional zero-byte Python fixture must not remain in the completion boundary.");

        string manyRowsPath = Path.Combine(fixtureRoot, "many_rows.py");
        Assert.True(File.Exists(manyRowsPath), "The >100-row provider fixture is absent.");
        Assert.True(
            File.ReadLines(manyRowsPath).Count(line => line.StartsWith("def row_", StringComparison.Ordinal)) > 100,
            "The provider fixture does not contain more than 100 structural declarations.");
    }

    [Fact]
    public void EvidenceSchema_LegacyRootNames_AreRejected()
    {
        byte[] evidence = CreateLegacyEvidence();

        Assert.Throws<InvalidDataException>(() => VerifyEvidence(evidence));
    }

    [Theory]
    [InlineData("unknown-root")]
    [InlineData("duplicate-root")]
    [InlineData("missing-root")]
    [InlineData("alternate-root")]
    [InlineData("wrong-typed-root")]
    [InlineData("unknown-nested")]
    [InlineData("duplicate-nested")]
    [InlineData("missing-nested")]
    [InlineData("alternate-nested")]
    [InlineData("wrong-typed-nested")]
    [InlineData("parallel-counts-and-completeness")]
    [InlineData("coverage-node-kinds")]
    [InlineData("legacy-observation-shape")]
    public void EvidenceSchema_DivergentFields_AreRejected(string caseId)
    {
        byte[] evidence = CreateDivergentLegacyEvidence(caseId);

        Assert.Throws<InvalidDataException>(() => VerifyEvidence(evidence));
    }

    [Fact]
    public void EvidenceSchema_ExactClosedGrammar_IsAccepted()
    {
        byte[] evidence = CreateCanonicalEvidence();

        VerifyEvidence(evidence);
        Assert.InRange(evidence.Length, 1, 16 * 1_024);
    }

    [Fact]
    public void CanonicalProviderContractConstants_ProductionRunnerVerifierAndStory41_AreIdentical()
    {
        string repositoryRoot = FindRepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "Vulgata.Tests",
            "CodeGraphProviderContract",
            "runner.py"));
        string provider = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "dotnet",
            "Vulgata.Infrastructure",
            "CodeGraph",
            "CodeGraphContextProvider.cs"));
        string contract = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "specs",
            "001-codegraph-extraction",
            "contracts",
            "repository-profiling-api.md"));

        Assert.Equal(_projectionAHash, Hash(CodeGraphContextProviderQueries.CodeUnits));
        Assert.Equal(_projectionBHash, Hash(CodeGraphContextProviderQueries.Relationships));
        Assert.Contains($"EXPECTED_A = \"{_projectionAHash}\"", runner, StringComparison.Ordinal);
        Assert.Contains($"EXPECTED_B = \"{_projectionBHash}\"", runner, StringComparison.Ordinal);
        Assert.Contains($"`{_projectionAHash}`", contract, StringComparison.Ordinal);
        Assert.Contains($"`{_projectionBHash}`", contract, StringComparison.Ordinal);

        int runnerNamesStart = runner.IndexOf("SAFE_NAMES = {", StringComparison.Ordinal);
        int runnerNamesEnd = runner.IndexOf("NODE_FIELDS = {", runnerNamesStart, StringComparison.Ordinal);
        HashSet<string> runnerNames = ExtractQuotedNames(runner[runnerNamesStart..runnerNamesEnd]);

        int parserStart = provider.IndexOf("internal static class CodeGraphHttpProjectionParser", StringComparison.Ordinal);
        int parserEnd = provider.IndexOf("internal static class CodeGraphContextProviderQueries", parserStart, StringComparison.Ordinal);
        string parser = provider[parserStart..parserEnd];
        HashSet<string> providerNames = ExtractQuotedNames(
            string.Join(
                '\n',
                Regex.Matches(
                    parser,
                    "(?:GetProperty|NameEquals)\\(\\\"([^\\\"]+)\\\"\\)|_(?:codeUnit|relationship)Fields\\s*=\\s*\\[(.*?)\\];",
                    RegexOptions.Singleline | RegexOptions.CultureInvariant)
                    .Select(match => match.Value)));

        const string contractMarker = "Raw `safeName` is permitted only for the exact case-sensitive ASCII set:";
        int contractNamesStart = contract.IndexOf('`', contract.IndexOf(contractMarker, StringComparison.Ordinal) + contractMarker.Length);
        int contractNamesEnd = contract.IndexOf('`', contractNamesStart + 1);
        var contractNames = new HashSet<string>(
            contract[(contractNamesStart + 1)..contractNamesEnd].Split(','),
            StringComparer.Ordinal);

        Assert.Equal(_safeNames.Order(StringComparer.Ordinal), runnerNames.Order(StringComparer.Ordinal));
        Assert.Equal(_safeNames.Order(StringComparer.Ordinal), providerNames.Order(StringComparer.Ordinal));
        Assert.Equal(_safeNames.Order(StringComparer.Ordinal), contractNames.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("duplicate-canonical-root")]
    [InlineData("unknown-canonical-nested")]
    [InlineData("duplicate-canonical-nested")]
    [InlineData("missing-canonical-nested")]
    [InlineData("alternate-canonical-nested")]
    [InlineData("non-integer-count")]
    [InlineData("non-boolean-completeness")]
    [InlineData("row-count-sum")]
    [InlineData("array-kind-sum")]
    [InlineData("false-pass-invariant")]
    [InlineData("projection-request-hash")]
    [InlineData("provider-hash")]
    [InlineData("attempted-alternative")]
    [InlineData("unsafe-raw-name")]
    public void EvidenceSchema_ClosedTypesBoundsAndPassInvariants_AreRejected(string caseId)
    {
        byte[] evidence = CreateInvalidCanonicalEvidence(caseId);

        Assert.Throws<InvalidDataException>(() => VerifyEvidence(evidence));
    }

    [Fact]
    public async Task RunnerObserver_ClosedBoundedHmacAndAllRowBehavior_IsExecutableWithoutDocker()
    {
        string runnerPath = Path.Combine(
            FindRepositoryRoot(),
            "tests",
            "Vulgata.Tests",
            "CodeGraphProviderContract",
            "runner.py");
        string script = """
            import ast,importlib.util,json,sys
            source=open(sys.argv[1],encoding="utf-8").read()
            tree=ast.parse(source)
            assignments={target.id:node.value for node in ast.walk(tree) if isinstance(node,ast.Assign) for target in node.targets if isinstance(target,ast.Name) and target.id in {"evidence","cleanup"}}
            evidence_keys={key.value for key in assignments["evidence"].keys}
            assert evidence_keys=={"schemaVersion","verdict","primaryFailureCategory","acceptedCorrectionSha256","claimSha256","sourceCommit","sourceTree","productionSourceManifestSha256","finalImageSha256","verifierBaseImageSha256","providerVersion","providerLockSha256","providerDistributionSha256","fixtureTreeSha256","projectionObservations","coverage","immutableSnapshot","cleanup","forbiddenRetention"}
            assert {key.value for key in assignments["cleanup"].keys}=={"gatewayStopped","contextDeleted","providerStateKind","providerStateRemoved","providerHomeRemoved","containerRemoved","tempImageTagsRemoved","failureCount"}
            spec=importlib.util.spec_from_file_location("provider_contract_runner",sys.argv[1])
            module=importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            query="RETURN fixture"
            row={"record_kind":"code_unit","provider_kind":"File","path":"fixture.cs","name":"fixture","start_line":None,"end_line":None,"parser_language":None}
            payload={"status":"ok","message":None,"data":{"success":True,"query":query,"record_count":2,"results":[row,row.copy()]},"error":None}
            raw=json.dumps(payload,separators=(",",":")).encode()
            observation,bounded,known=module.observe_json(raw,200,b"k"*32,module.EXPECTED_A)
            assert bounded and known
            assert set(observation)=={"attempted","httpStatus","bodyByteCount","bodySha256","requestSha256","rootValueKind","shapeComplete","containers","properties","arrays","aliasNames","coordinateBindings"}
            assert sum(1 for item in observation["containers"] if item["depth"]==3 and item["kind"]=="object")==1
            module.parse_envelope(raw,query,module.NODE_FIELDS)
            bad=json.loads(raw)
            bad["data"]["results"][1]["parser_language"]={}
            try:
                module.parse_envelope(json.dumps(bad,separators=(",",":")).encode(),query,module.NODE_FIELDS)
                raise AssertionError("all-row validation accepted a malformed second row")
            except RuntimeError:
                pass
            unknown_raw=b'{"unsafe_secret":1}'
            unknown,unknown_bounded,unknown_known=module.observe_json(unknown_raw,200,b"k"*32,module.EXPECTED_A)
            encoded=json.dumps(unknown,separators=(",",":"))
            assert unknown_bounded and not unknown_known
            assert "unsafe_secret" not in encoded and "hmacSha256" in encoded
            assert len(json.dumps(observation,separators=(",",":")).encode())<=5*1024
            print(len(json.dumps(observation,separators=(",",":")).encode()))
            """;

        ProcessResult result = await RunAsync(
            FindRepositoryRoot(),
            "python",
            ["-c", script, runnerPath]);

        EnsureSuccess(result);
        Assert.InRange(int.Parse(result.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture), 1, 5 * 1_024);
    }

    [Fact]
    public async Task FinalImage_ExactPinned051_ExactProductionProjections_MatchObservedContract()
    {
        string finalImage = RequireEnvironment("VULGATA_PROVIDER_CONTRACT_IMAGE");
        string acceptedCorrectionSha256 = RequireSha256Environment(
            "VULGATA_PROVIDER_CONTRACT_ACCEPTED_CORRECTION_SHA256");

        string repositoryRoot = FindRepositoryRoot();
        string resultsRoot = Path.Combine(repositoryRoot, "TestResults", "codegraph-provider-contract");
        Directory.CreateDirectory(resultsRoot);
        ProcessResult imageInspection = await RunAsync(
            repositoryRoot,
            "docker",
            ["image", "inspect", finalImage, "--format", "{{.Id}}"]);
        EnsureSuccess(imageInspection);
        string finalImageId = imageInspection.StandardOutput.Trim();
        Assert.Matches("^sha256:[0-9a-f]{64}$", finalImageId);

        byte[] manifestBytes = CreateAcceptedCorrectionManifest(repositoryRoot, finalImageId);
        string correctionRoot = Path.Combine(resultsRoot, acceptedCorrectionSha256);
        string claimPath = Path.Combine(correctionRoot, "claim.json");
        string evidencePath = Path.Combine(correctionRoot, "evidence.json");
        Assert.False(
            Directory.Exists(correctionRoot),
            $"Accepted correction {acceptedCorrectionSha256} already has an execution namespace.");
        Directory.CreateDirectory(correctionRoot);
        byte[] claimBytes = CreateClaim(manifestBytes, acceptedCorrectionSha256);
        WriteCreateNewDurable(claimPath, claimBytes);
        string claimSha256 = Hash(claimBytes);

        string contextRoot = Path.Combine(correctionRoot, "verifier-context");
        Directory.CreateDirectory(contextRoot);
        CopyDirectory(
            Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "CodeGraphProviderContract", "ProviderFixture"),
            Path.Combine(contextRoot, "fixture"));
        File.Copy(
            Path.Combine(repositoryRoot, "tests", "Vulgata.Tests", "CodeGraphProviderContract", "runner.py"),
            Path.Combine(contextRoot, "runner.py"));
        await File.WriteAllTextAsync(
            Path.Combine(contextRoot, "projection-a.cypher"),
            CodeGraphContextProviderQueries.CodeUnits,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await File.WriteAllTextAsync(
            Path.Combine(contextRoot, "projection-b.cypher"),
            CodeGraphContextProviderQueries.Relationships,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        string suffix = acceptedCorrectionSha256[..12];
        string exactBaseTag = $"vulgata-story42-exact-base:{suffix}";
        string verifierTag = $"vulgata-story42-contract-verifier:{suffix}";
        ProcessResult providerRun = new(-1, string.Empty, "provider-run-not-started");
        try
        {
            EnsureSuccess(await RunAsync(repositoryRoot, "docker", ["tag", finalImageId, exactBaseTag]));
            string dockerfile = """
                ARG BASE_IMAGE
                FROM ${BASE_IMAGE}
                USER root
                COPY runner.py /contract/runner.py
                COPY projection-a.cypher /contract/projection-a.cypher
                COPY projection-b.cypher /contract/projection-b.cypher
                COPY fixture /contract/fixture
                RUN chmod 0555 /contract/runner.py && chown -R app:app /contract
                USER app
                ENTRYPOINT ["python", "/contract/runner.py"]
                """;
            await File.WriteAllTextAsync(
                Path.Combine(contextRoot, "Dockerfile"),
                dockerfile,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            EnsureSuccess(await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "build", "--build-arg", $"BASE_IMAGE={exactBaseTag}",
                    "--label", $"vulgata.provider-contract.base={finalImageId}",
                    "--tag", verifierTag, contextRoot,
                ],
                TimeSpan.FromMinutes(5)));
            string verifierBase = (await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "image", "inspect", verifierTag,
                    "--format", "{{index .Config.Labels \"vulgata.provider-contract.base\"}}",
                ])).StandardOutput.Trim();
            Assert.Equal(finalImageId, verifierBase);

            providerRun = await RunAsync(
                repositoryRoot,
                "docker",
                [
                    "run", "--rm", "--network", "none",
                    "--env", $"FINAL_IMAGE_SHA256={finalImageId}",
                    "--env", $"VERIFIER_BASE_IMAGE_SHA256={verifierBase}",
                    "--env", $"ACCEPTED_CORRECTION_SHA256={acceptedCorrectionSha256}",
                    "--env", $"CLAIM_SHA256={claimSha256}",
                    "--volume", $"{correctionRoot}:/evidence",
                    verifierTag,
                ],
                TimeSpan.FromMinutes(5));

            Assert.True(File.Exists(evidencePath), $"Provider did not publish bounded evidence: {Bound(providerRun.StandardError)}");
            byte[] evidenceBytes = await File.ReadAllBytesAsync(evidencePath);
            Assert.InRange(evidenceBytes.Length, 1, 16 * 1_024);
            VerifyEvidence(evidenceBytes);
            using JsonDocument evidence = JsonDocument.Parse(evidenceBytes);
            JsonElement root = evidence.RootElement;
            Assert.Equal(_evidenceSchema, root.GetProperty("schemaVersion").GetString());
            Assert.Equal(acceptedCorrectionSha256, root.GetProperty("acceptedCorrectionSha256").GetString());
            Assert.Equal(claimSha256, root.GetProperty("claimSha256").GetString());
            Assert.Equal(finalImageId, root.GetProperty("finalImageSha256").GetString());
            Assert.Equal(finalImageId, root.GetProperty("verifierBaseImageSha256").GetString());
            Assert.Equal("0.5.1", root.GetProperty("providerVersion").GetString());
            Assert.Equal(
                _providerLockHash,
                root.GetProperty("providerLockSha256").GetString());
            Assert.Equal(
                _providerDistributionHash,
                root.GetProperty("providerDistributionSha256").GetString());
            JsonElement projectionObservations = root.GetProperty("projectionObservations");
            Assert.Equal(Hash(CodeGraphContextProviderQueries.CodeUnits), projectionObservations.GetProperty("A").GetProperty("requestSha256").GetString());
            Assert.Equal(Hash(CodeGraphContextProviderQueries.Relationships), projectionObservations.GetProperty("B").GetProperty("requestSha256").GetString());

            string verdict = root.GetProperty("verdict").GetString()!;
            Assert.True(
                providerRun.ExitCode == 0 && verdict == "pass",
                $"Provider contract {verdict}: {root.GetProperty("primaryFailureCategory").GetString()}");

            JsonElement rowCounts = root.GetProperty("coverage").GetProperty("rowCounts");
            Assert.True(rowCounts.GetProperty("projectionA").GetInt32() > 100);
            Assert.True(rowCounts.GetProperty("projectionB").GetInt32() > 100);
            Assert.InRange(rowCounts.GetProperty("combined").GetInt32(), 1, 250_000);

            JsonElement completeness = root.GetProperty("coverage").GetProperty("completeness");
            Assert.All(completeness.EnumerateObject(), property => Assert.True(property.Value.GetBoolean()));

            JsonElement parserLanguage = root.GetProperty("coverage").GetProperty("parserLanguage");
            Assert.True(parserLanguage.GetProperty("aliasObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("structuralBindingsObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("fileNullObserved").GetBoolean());
            Assert.True(parserLanguage.GetProperty("oneIdentityPerPath").GetBoolean());
            Assert.True(parserLanguage.GetProperty("allCodeUnitsAssigned").GetBoolean());
            Assert.True(parserLanguage.GetProperty("emptyFileUnknownLanguage").GetBoolean());
            Assert.Equal(0, parserLanguage.GetProperty("emptyFileRetainedCodeUnitCount").GetInt32());
            Assert.Equal(1, parserLanguage.GetProperty("unknownLanguageCount").GetInt32());

            JsonElement immutableSnapshot = root.GetProperty("immutableSnapshot");
            Assert.Equal(1, immutableSnapshot.GetProperty("indexCount").GetInt32());
            Assert.False(immutableSnapshot.GetProperty("watcherStarted").GetBoolean());
            Assert.False(immutableSnapshot.GetProperty("mutationBetweenProjections").GetBoolean());
            Assert.True(immutableSnapshot.GetProperty("snapshotEqual").GetBoolean());

            JsonElement cleanup = root.GetProperty("cleanup");
            Assert.True(cleanup.GetProperty("gatewayStopped").GetBoolean());
            Assert.True(cleanup.GetProperty("contextDeleted").GetBoolean());
            Assert.True(cleanup.GetProperty("providerStateRemoved").GetBoolean());
            Assert.True(cleanup.GetProperty("providerHomeRemoved").GetBoolean());
            Assert.All(root.GetProperty("forbiddenRetention").EnumerateObject(), property => Assert.False(property.Value.GetBoolean()));
        }
        finally
        {
            await RunAsync(repositoryRoot, "docker", ["image", "rm", "--force", verifierTag, exactBaseTag]);
            if (Directory.Exists(contextRoot))
            {
                Directory.Delete(contextRoot, recursive: true);
            }
        }
    }

    private static byte[] CreateAcceptedCorrectionManifest(string repositoryRoot, string finalImageId)
    {
        var artifacts = _planArtifacts.Select(path => new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["path"] = path,
            ["sha256"] = Hash(File.ReadAllBytes(Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))),
        }).ToArray();
        var manifest = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["artifacts"] = artifacts,
            ["evidenceSchema"] = _evidenceSchema,
            ["finalImageSha256"] = finalImageId,
            ["gate"] = "provider-contract-e2e",
            ["providerDistributionSha256"] = _providerDistributionHash,
            ["providerLockSha256"] = _providerLockHash,
            ["providerVersion"] = "0.5.1",
            ["projectionASha256"] = Hash(CodeGraphContextProviderQueries.CodeUnits),
            ["projectionBSha256"] = Hash(CodeGraphContextProviderQueries.Relationships),
            ["reviewIdentity"] = _reviewIdentity,
            ["story"] = "4-2-language-and-framework-detection",
            ["testIdentity"] = _testIdentity,
        };
        return JsonSerializer.SerializeToUtf8Bytes(manifest);
    }

    private static byte[] CreateLegacyEvidence()
    {
        string hash = new('a', 64);
        var evidence = new Dictionary<string, object?>
        {
            ["schema"] = _evidenceSchema,
            ["verdict"] = "pass",
            ["failureCategory"] = null,
            ["acceptedCorrectionSha256"] = hash,
            ["claimSha256"] = hash,
            ["finalImageSha256"] = $"sha256:{hash}",
            ["verifierBaseImageSha256"] = $"sha256:{hash}",
            ["providerVersion"] = "0.5.1",
            ["providerLockSha256"] = "10f09b78b02c886688c726bb93030380cfe54afc23798db24f6f4fc4cc4c7d95",
            ["providerDistributionSha256"] = "3cd79efe6c9525d69ae250c063404b500ef7595b6c63284d841c78a1ecf48658",
            ["fixtureTreeSha256"] = hash,
            ["projectionARequestSha256"] = Hash(CodeGraphContextProviderQueries.CodeUnits),
            ["projectionBRequestSha256"] = Hash(CodeGraphContextProviderQueries.Relationships),
            ["observations"] = Array.Empty<object>(),
            ["coverage"] = new Dictionary<string, object?>(),
            ["rowCounts"] = new Dictionary<string, int>
            {
                ["projectionA"] = 101,
                ["projectionB"] = 101,
                ["combined"] = 202,
            },
            ["completeness"] = new Dictionary<string, bool>
            {
                ["privateLimitExact"] = true,
            },
            ["immutableSnapshot"] = new Dictionary<string, object?>(),
            ["cleanup"] = new Dictionary<string, object?>(),
            ["forbiddenRetention"] = new Dictionary<string, object?>(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(evidence);
    }

    private static byte[] CreateCanonicalEvidence()
    {
        string hash = new('a', 64);
        string objectId = new('b', 40);
        Dictionary<string, object?> Projection(string requestHash) => new()
        {
            ["attempted"] = true,
            ["httpStatus"] = 200,
            ["bodyByteCount"] = 1,
            ["bodySha256"] = hash,
            ["requestSha256"] = requestHash,
            ["queryEchoSha256"] = requestHash,
            ["rootValueKind"] = "object",
            ["shapeComplete"] = true,
            ["containers"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["ordinal"] = 0,
                    ["kind"] = "object",
                    ["depth"] = 0,
                },
            },
            ["properties"] = Array.Empty<object>(),
            ["arrays"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["containerOrdinal"] = 0,
                    ["elementCount"] = 1,
                    ["nullCount"] = 0,
                    ["booleanCount"] = 0,
                    ["numberCount"] = 0,
                    ["stringCount"] = 1,
                    ["objectCount"] = 0,
                    ["arrayCount"] = 0,
                },
            },
            ["aliasNames"] = _safeNames
                .Order(StringComparer.Ordinal)
                .Select(name => new Dictionary<string, object?> { ["safeName"] = name })
                .ToArray(),
            ["coordinateBindings"] = new object[]
            {
                new[] { "path", "name" },
                new[] { "source_path", "source_name" },
                new[] { "target_path", "target_name" },
            },
        };

        var evidence = new Dictionary<string, object?>
        {
            ["schemaVersion"] = _evidenceSchema,
            ["verdict"] = "pass",
            ["primaryFailureCategory"] = "none",
            ["acceptedCorrectionSha256"] = hash,
            ["claimSha256"] = hash,
            ["sourceCommit"] = objectId,
            ["sourceTree"] = objectId,
            ["productionSourceManifestSha256"] = hash,
            ["finalImageSha256"] = $"sha256:{hash}",
            ["verifierBaseImageSha256"] = $"sha256:{hash}",
            ["providerVersion"] = "0.5.1",
            ["providerLockSha256"] = _providerLockHash,
            ["providerDistributionSha256"] = _providerDistributionHash,
            ["fixtureTreeSha256"] = hash,
            ["projectionObservations"] = new Dictionary<string, object?>
            {
                ["A"] = Projection(_projectionAHash),
                ["B"] = Projection(_projectionBHash),
            },
            ["coverage"] = new Dictionary<string, object?>
            {
                ["rowCounts"] = new Dictionary<string, int>
                {
                    ["projectionA"] = 101,
                    ["projectionB"] = 101,
                    ["combined"] = 202,
                },
                ["completeness"] = new Dictionary<string, bool>
                {
                    ["privateLimitExact"] = true,
                    ["injectedTruncationRejected"] = true,
                    ["queryDriftRejected"] = true,
                    ["languageConflictRejected"] = true,
                    ["combinedBoundRejected"] = true,
                },
                ["independentNodeKinds"] = new Dictionary<string, int>
                {
                    ["File"] = 1, ["Module"] = 1, ["Class"] = 1, ["Function"] = 1,
                },
                ["relationshipKinds"] = new Dictionary<string, int>
                {
                    ["CONTAINS"] = 1, ["IMPORTS"] = 1, ["CALLS"] = 1, ["INHERITS"] = 1,
                },
                ["filteredTargetUnresolvedOnly"] = true,
                ["projectionEndpointIdentityEqual"] = true,
                ["parserLanguage"] = new Dictionary<string, object?>
                {
                    ["aliasObserved"] = true,
                    ["structuralBindingsObserved"] = true,
                    ["fileNullObserved"] = true,
                    ["oneIdentityPerPath"] = true,
                    ["allCodeUnitsAssigned"] = true,
                    ["emptyFileUnknownLanguage"] = true,
                    ["emptyFileRetainedCodeUnitCount"] = 0,
                    ["unknownLanguageCount"] = 1,
                },
            },
            ["immutableSnapshot"] = new Dictionary<string, object?>
            {
                ["indexCount"] = 1,
                ["watcherStarted"] = false,
                ["mutationBetweenProjections"] = false,
                ["snapshotEqual"] = true,
            },
            ["cleanup"] = new Dictionary<string, object?>
            {
                ["gatewayStopped"] = true,
                ["contextDeleted"] = true,
                ["providerStateKind"] = "absent",
                ["providerStateRemoved"] = true,
                ["providerHomeRemoved"] = true,
                ["containerRemoved"] = true,
                ["tempImageTagsRemoved"] = true,
                ["failureCount"] = 0,
            },
            ["forbiddenRetention"] = new Dictionary<string, bool>
            {
                ["scalarValues"] = false,
                ["source"] = false,
                ["rows"] = false,
                ["rawJson"] = false,
                ["rawQuery"] = false,
                ["paths"] = false,
                ["symbols"] = false,
                ["credentials"] = false,
                ["tokens"] = false,
                ["endpointValues"] = false,
                ["diagnostics"] = false,
            },
        };
        return JsonSerializer.SerializeToUtf8Bytes(evidence);
    }

    private static byte[] CreateDivergentLegacyEvidence(string caseId)
    {
        string json = Encoding.UTF8.GetString(CreateLegacyEvidence());
        string replacement = caseId switch
        {
            "unknown-root" => json[..^1] + ",\"unexpected\":true}",
            "duplicate-root" => "{\"verdict\":\"pass\"," + json[1..],
            "missing-root" => json.Replace(",\"fixtureTreeSha256\":\"" + new string('a', 64) + "\"", string.Empty, StringComparison.Ordinal),
            "alternate-root" => json,
            "wrong-typed-root" => json.Replace("\"fixtureTreeSha256\":\"" + new string('a', 64) + "\"", "\"fixtureTreeSha256\":0", StringComparison.Ordinal),
            "unknown-nested" => json.Replace("\"coverage\":{}", "\"coverage\":{\"unexpected\":true}", StringComparison.Ordinal),
            "duplicate-nested" => json.Replace("\"coverage\":{}", "\"coverage\":{\"unexpected\":true,\"unexpected\":true}", StringComparison.Ordinal),
            "missing-nested" => json,
            "alternate-nested" => json.Replace("\"cleanup\":{}", "\"cleanup\":{\"databaseDeleted\":true,\"providerHomeDeleted\":true}", StringComparison.Ordinal),
            "wrong-typed-nested" => json.Replace("\"coverage\":{}", "\"coverage\":{\"rowCounts\":\"invalid\"}", StringComparison.Ordinal),
            "parallel-counts-and-completeness" => json,
            "coverage-node-kinds" => json.Replace("\"coverage\":{}", "\"coverage\":{\"nodeKinds\":{}}", StringComparison.Ordinal),
            "legacy-observation-shape" => json.Replace(
                "\"observations\":[]",
                "\"observations\":[{\"containers\":[],\"arrayKinds\":[]} ]",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(caseId), caseId, null),
        };
        return Encoding.UTF8.GetBytes(replacement);
    }

    private static byte[] CreateInvalidCanonicalEvidence(string caseId)
    {
        string json = Encoding.UTF8.GetString(CreateCanonicalEvidence());
        string replacement = caseId switch
        {
            "duplicate-canonical-root" => "{\"verdict\":\"pass\"," + json[1..],
            "unknown-canonical-nested" => json.Replace(
                "\"coverage\":{\"rowCounts\"",
                "\"coverage\":{\"unexpected\":true,\"rowCounts\"",
                StringComparison.Ordinal),
            "duplicate-canonical-nested" => json.Replace(
                "\"coverage\":{\"rowCounts\"",
                "\"coverage\":{\"rowCounts\":{},\"rowCounts\"",
                StringComparison.Ordinal),
            "missing-canonical-nested" => json.Replace(
                "\"privateLimitExact\":true,",
                string.Empty,
                StringComparison.Ordinal),
            "alternate-canonical-nested" => json.Replace(
                "\"providerStateRemoved\":true",
                "\"databaseDeleted\":true",
                StringComparison.Ordinal),
            "non-integer-count" => json.Replace(
                "\"projectionA\":101",
                "\"projectionA\":101.0",
                StringComparison.Ordinal),
            "non-boolean-completeness" => json.Replace(
                "\"privateLimitExact\":true",
                "\"privateLimitExact\":1",
                StringComparison.Ordinal),
            "row-count-sum" => json.Replace("\"combined\":202", "\"combined\":201", StringComparison.Ordinal),
            "array-kind-sum" => json.Replace(
                "\"stringCount\":1",
                "\"stringCount\":0",
                StringComparison.Ordinal),
            "false-pass-invariant" => json.Replace(
                "\"shapeComplete\":true",
                "\"shapeComplete\":false",
                StringComparison.Ordinal),
            "projection-request-hash" => json.Replace(
                $"\"requestSha256\":\"{_projectionAHash}\"",
                $"\"requestSha256\":\"{new string('c', 64)}\"",
                StringComparison.Ordinal),
            "provider-hash" => json.Replace(
                $"\"providerLockSha256\":\"{_providerLockHash}\"",
                $"\"providerLockSha256\":\"{new string('c', 64)}\"",
                StringComparison.Ordinal),
            "attempted-alternative" => json.Replace(
                "\"attempted\":true",
                "\"attempted\":false",
                StringComparison.Ordinal),
            "unsafe-raw-name" => json.Replace(
                "\"safeName\":\"data\"",
                "\"safeName\":\"unsafe_name\"",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(caseId), caseId, null),
        };
        return Encoding.UTF8.GetBytes(replacement);
    }

    private static void VerifyEvidence(byte[] evidenceBytes)
    {
        if (evidenceBytes.Length is < 1 or > 16 * 1_024)
        {
            throw new InvalidDataException("Evidence byte count is outside the closed bound.");
        }

        using JsonDocument evidence = JsonDocument.Parse(evidenceBytes);
        IReadOnlyDictionary<string, JsonElement> root = RequireObject(
            evidence.RootElement,
            "root",
            [
                "schemaVersion", "verdict", "primaryFailureCategory", "acceptedCorrectionSha256",
                "claimSha256", "sourceCommit", "sourceTree", "productionSourceManifestSha256",
                "finalImageSha256", "verifierBaseImageSha256", "providerVersion", "providerLockSha256",
                "providerDistributionSha256", "fixtureTreeSha256", "projectionObservations", "coverage",
                "immutableSnapshot", "cleanup", "forbiddenRetention",
            ]);
        RequireEqualString(root["schemaVersion"], _evidenceSchema, "schemaVersion");
        string verdict = RequireOneOf(root["verdict"], "verdict", ["pass", "fail"]);
        string failure = RequireOneOf(
            root["primaryFailureCategory"],
            "primaryFailureCategory",
            [
                "none", "preflight", "provider-lifecycle", "http-status", "invalid-json",
                "envelope-mismatch", "query-rejected", "projection-mismatch",
                "observation-bound-exceeded", "cleanup-failure", "publication-failure",
            ]);
        bool passed = verdict == "pass";
        if (passed != (failure == "none"))
        {
            throw new InvalidDataException("Verdict and primary failure category disagree.");
        }

        RequireSha256(root["acceptedCorrectionSha256"], "acceptedCorrectionSha256");
        RequireSha256(root["claimSha256"], "claimSha256");
        RequireGitObjectId(root["sourceCommit"], "sourceCommit");
        RequireGitObjectId(root["sourceTree"], "sourceTree");
        RequireSha256(root["productionSourceManifestSha256"], "productionSourceManifestSha256");
        RequireImageId(root["finalImageSha256"], "finalImageSha256");
        RequireImageId(root["verifierBaseImageSha256"], "verifierBaseImageSha256");
        RequireEqualString(root["providerVersion"], "0.5.1", "providerVersion");
        RequireEqualString(root["providerLockSha256"], _providerLockHash, "providerLockSha256");
        RequireEqualString(
            root["providerDistributionSha256"],
            _providerDistributionHash,
            "providerDistributionSha256");
        RequireSha256(root["fixtureTreeSha256"], "fixtureTreeSha256");

        IReadOnlyDictionary<string, JsonElement> projections = RequireObject(
            root["projectionObservations"],
            "projectionObservations",
            ["A", "B"]);
        bool projectionAComplete = ValidateProjection(projections["A"], "A", _projectionAHash, passed);
        bool projectionBComplete = ValidateProjection(projections["B"], "B", _projectionBHash, passed);
        ValidateCoverage(root["coverage"], passed);
        ValidateImmutableSnapshot(root["immutableSnapshot"], passed);
        ValidateCleanup(root["cleanup"], passed);
        ValidateForbiddenRetention(root["forbiddenRetention"]);
        if (passed && (!projectionAComplete || !projectionBComplete))
        {
            throw new InvalidDataException("Passing evidence requires two complete projections.");
        }
    }

    private static bool ValidateProjection(
        JsonElement element,
        string projection,
        string expectedRequestHash,
        bool passed)
    {
        IReadOnlyDictionary<string, JsonElement> value = RequireObject(
            element,
            $"projectionObservations.{projection}",
            [
                "attempted", "bodyByteCount", "requestSha256", "rootValueKind", "shapeComplete",
                "containers", "properties", "arrays", "aliasNames", "coordinateBindings",
            ],
            ["httpStatus", "bodySha256", "queryEchoSha256"]);
        bool attempted = RequireBoolean(value["attempted"], $"{projection}.attempted");
        long bodyByteCount = RequireInteger(value["bodyByteCount"], 0, 67_108_865, $"{projection}.bodyByteCount");
        RequireEqualString(value["requestSha256"], expectedRequestHash, $"{projection}.requestSha256");
        string rootKind = RequireOneOf(
            value["rootValueKind"],
            $"{projection}.rootValueKind",
            ["not-observed", "invalid-json", "null", "boolean", "number", "string", "object", "array"]);
        bool shapeComplete = RequireBoolean(value["shapeComplete"], $"{projection}.shapeComplete");

        if (!attempted)
        {
            if (value.ContainsKey("httpStatus") || value.ContainsKey("bodySha256")
                || value.ContainsKey("queryEchoSha256") || bodyByteCount != 0
                || rootKind != "not-observed" || shapeComplete)
            {
                throw new InvalidDataException($"Projection {projection} has an invalid not-attempted alternative.");
            }
        }
        else
        {
            if (!value.TryGetValue("httpStatus", out JsonElement httpStatus)
                || !value.TryGetValue("bodySha256", out JsonElement bodySha256))
            {
                throw new InvalidDataException($"Projection {projection} lacks attempted-response fields.");
            }

            RequireInteger(httpStatus, 100, 599, $"{projection}.httpStatus");
            RequireSha256(bodySha256, $"{projection}.bodySha256");
            if (value.TryGetValue("queryEchoSha256", out JsonElement queryEcho))
            {
                RequireSha256(queryEcho, $"{projection}.queryEchoSha256");
            }
        }

        bool hasUnknown = false;
        JsonElement[] containers = RequireArray(value["containers"], 0, 32, $"{projection}.containers");
        for (int index = 0; index < containers.Length; index++)
        {
            IReadOnlyDictionary<string, JsonElement> container = RequireObject(
                containers[index],
                $"{projection}.containers[{index}]",
                ["ordinal", "kind", "depth"],
                ["parentOrdinal", "safeViaPropertyName", "unknownViaName"]);
            long ordinal = RequireInteger(container["ordinal"], 0, 31, $"{projection}.containers[{index}].ordinal");
            if (ordinal != index)
            {
                throw new InvalidDataException($"Projection {projection} container ordinals are not contiguous.");
            }

            RequireOneOf(container["kind"], $"{projection}.containers[{index}].kind", ["object", "array"]);
            RequireInteger(container["depth"], 0, 4, $"{projection}.containers[{index}].depth");
            bool hasParent = container.TryGetValue("parentOrdinal", out JsonElement parentOrdinal);
            bool hasSafeName = container.TryGetValue("safeViaPropertyName", out JsonElement safeName);
            bool hasUnknownName = container.TryGetValue("unknownViaName", out JsonElement unknownName);
            if (index == 0)
            {
                if (hasParent || hasSafeName || hasUnknownName)
                {
                    throw new InvalidDataException($"Projection {projection} root container has parent metadata.");
                }
            }
            else
            {
                if (!hasParent || RequireInteger(parentOrdinal, 0, 31, $"{projection}.parentOrdinal") >= index
                    || (hasSafeName && hasUnknownName))
                {
                    throw new InvalidDataException($"Projection {projection} child container linkage is invalid.");
                }

                if (hasSafeName)
                {
                    RequireSafeName(safeName, $"{projection}.safeViaPropertyName");
                }

                if (hasUnknownName)
                {
                    ValidateUnknownName(unknownName, $"{projection}.unknownViaName");
                    hasUnknown = true;
                }
            }
        }

        JsonElement[] properties = RequireArray(value["properties"], 0, 128, $"{projection}.properties");
        foreach ((JsonElement propertyElement, int index) in properties.Select((item, index) => (item, index)))
        {
            IReadOnlyDictionary<string, JsonElement> property = RequireObject(
                propertyElement,
                $"{projection}.properties[{index}]",
                ["containerOrdinal", "valueKind", "occurrenceCount"],
                ["safeName", "unknownName"]);
            RequireContainerReference(property["containerOrdinal"], containers.Length, $"{projection}.property.containerOrdinal");
            RequireOneOf(
                property["valueKind"],
                $"{projection}.property.valueKind",
                ["null", "boolean", "number", "string", "object", "array"]);
            RequireInteger(property["occurrenceCount"], 1, 250_001, $"{projection}.property.occurrenceCount");
            bool hasSafeName = property.TryGetValue("safeName", out JsonElement safeName);
            bool hasUnknownName = property.TryGetValue("unknownName", out JsonElement unknownName);
            if (hasSafeName == hasUnknownName)
            {
                throw new InvalidDataException($"Projection {projection} property name alternative is invalid.");
            }

            if (hasSafeName)
            {
                RequireSafeName(safeName, $"{projection}.property.safeName");
            }
            else
            {
                ValidateUnknownName(unknownName, $"{projection}.property.unknownName");
                hasUnknown = true;
            }
        }

        JsonElement[] arrays = RequireArray(value["arrays"], 0, 32, $"{projection}.arrays");
        foreach ((JsonElement arrayElement, int index) in arrays.Select((item, index) => (item, index)))
        {
            IReadOnlyDictionary<string, JsonElement> array = RequireObject(
                arrayElement,
                $"{projection}.arrays[{index}]",
                [
                    "containerOrdinal", "elementCount", "nullCount", "booleanCount", "numberCount",
                    "stringCount", "objectCount", "arrayCount",
                ]);
            RequireContainerReference(array["containerOrdinal"], containers.Length, $"{projection}.array.containerOrdinal");
            long elementCount = RequireInteger(array["elementCount"], 0, 250_001, $"{projection}.array.elementCount");
            long kindSum = new[] { "nullCount", "booleanCount", "numberCount", "stringCount", "objectCount", "arrayCount" }
                .Sum(name => RequireInteger(array[name], 0, 250_001, $"{projection}.array.{name}"));
            if (kindSum != elementCount)
            {
                throw new InvalidDataException($"Projection {projection} array kind counts do not sum.");
            }
        }

        JsonElement[] aliases = RequireArray(value["aliasNames"], 0, 64, $"{projection}.aliasNames");
        string? priorAlias = null;
        foreach ((JsonElement aliasElement, int index) in aliases.Select((item, index) => (item, index)))
        {
            IReadOnlyDictionary<string, JsonElement> alias = RequireObject(
                aliasElement,
                $"{projection}.aliasNames[{index}]",
                [],
                ["safeName", "unknownName"]);
            bool hasSafeName = alias.TryGetValue("safeName", out JsonElement safeName);
            bool hasUnknownName = alias.TryGetValue("unknownName", out JsonElement unknownName);
            if (hasSafeName == hasUnknownName)
            {
                throw new InvalidDataException($"Projection {projection} alias alternative is invalid.");
            }

            string key;
            if (hasSafeName)
            {
                key = "s:" + RequireSafeName(safeName, $"{projection}.alias.safeName");
            }
            else
            {
                key = "u:" + ValidateUnknownName(unknownName, $"{projection}.alias.unknownName");
                hasUnknown = true;
            }

            if (priorAlias is not null && StringComparer.Ordinal.Compare(priorAlias, key) >= 0)
            {
                throw new InvalidDataException($"Projection {projection} aliases are not sorted and unique.");
            }

            priorAlias = key;
        }

        JsonElement[] bindings = RequireArray(value["coordinateBindings"], 0, 32, $"{projection}.coordinateBindings");
        string? priorBinding = null;
        foreach (JsonElement binding in bindings)
        {
            JsonElement[] pair = RequireArray(binding, 2, 2, $"{projection}.coordinateBinding");
            string key = RequireSafeName(pair[0], $"{projection}.coordinateBinding[0]")
                + "\0" + RequireSafeName(pair[1], $"{projection}.coordinateBinding[1]");
            if (priorBinding is not null && StringComparer.Ordinal.Compare(priorBinding, key) >= 0)
            {
                throw new InvalidDataException($"Projection {projection} coordinate bindings are not sorted and unique.");
            }

            priorBinding = key;
        }

        if (hasUnknown && (shapeComplete || passed))
        {
            throw new InvalidDataException($"Projection {projection} retains an unknown name in complete evidence.");
        }

        if (shapeComplete && (!attempted || bodyByteCount > 67_108_864 || rootKind != "object"
            || !value.TryGetValue("queryEchoSha256", out JsonElement completeQueryEcho)
            || RequireString(completeQueryEcho, $"{projection}.queryEchoSha256") != expectedRequestHash))
        {
            throw new InvalidDataException($"Projection {projection} shapeComplete invariant failed.");
        }

        return shapeComplete;
    }

    private static void ValidateCoverage(JsonElement element, bool passed)
    {
        IReadOnlyDictionary<string, JsonElement> coverage = RequireObject(
            element,
            "coverage",
            [
                "rowCounts", "completeness", "independentNodeKinds", "relationshipKinds",
                "filteredTargetUnresolvedOnly", "projectionEndpointIdentityEqual", "parserLanguage",
            ]);
        IReadOnlyDictionary<string, JsonElement> rowCounts = RequireObject(
            coverage["rowCounts"],
            "coverage.rowCounts",
            ["projectionA", "projectionB", "combined"]);
        long projectionA = RequireInteger(rowCounts["projectionA"], 0, 250_001, "coverage.rowCounts.projectionA");
        long projectionB = RequireInteger(rowCounts["projectionB"], 0, 250_001, "coverage.rowCounts.projectionB");
        long combined = RequireInteger(rowCounts["combined"], 0, 500_002, "coverage.rowCounts.combined");
        if (combined != projectionA + projectionB || (passed && (combined > 250_000 || projectionA <= 100 || projectionB <= 100)))
        {
            throw new InvalidDataException("Coverage row-count sum/pass invariant failed.");
        }

        string[] completenessNames =
        [
            "privateLimitExact", "injectedTruncationRejected", "queryDriftRejected",
            "languageConflictRejected", "combinedBoundRejected",
        ];
        IReadOnlyDictionary<string, JsonElement> completeness = RequireObject(
            coverage["completeness"],
            "coverage.completeness",
            completenessNames);
        foreach (string name in completenessNames)
        {
            bool value = RequireBoolean(completeness[name], $"coverage.completeness.{name}");
            if (passed && !value)
            {
                throw new InvalidDataException("Passing evidence has an incomplete completeness fact.");
            }
        }

        ValidatePositiveCountObject(
            coverage["independentNodeKinds"],
            "coverage.independentNodeKinds",
            ["File", "Module", "Class", "Function"],
            passed);
        ValidatePositiveCountObject(
            coverage["relationshipKinds"],
            "coverage.relationshipKinds",
            ["CONTAINS", "IMPORTS", "CALLS", "INHERITS"],
            passed);
        bool filteredTarget = RequireBoolean(
            coverage["filteredTargetUnresolvedOnly"],
            "coverage.filteredTargetUnresolvedOnly");
        bool identityEqual = RequireBoolean(
            coverage["projectionEndpointIdentityEqual"],
            "coverage.projectionEndpointIdentityEqual");
        if (passed && (!filteredTarget || !identityEqual))
        {
            throw new InvalidDataException("Passing coverage identity facts must be true.");
        }

        string[] parserBooleanNames =
        [
            "aliasObserved", "structuralBindingsObserved", "fileNullObserved", "oneIdentityPerPath",
            "allCodeUnitsAssigned", "emptyFileUnknownLanguage",
        ];
        IReadOnlyDictionary<string, JsonElement> parserLanguage = RequireObject(
            coverage["parserLanguage"],
            "coverage.parserLanguage",
            [.. parserBooleanNames, "emptyFileRetainedCodeUnitCount", "unknownLanguageCount"]);
        foreach (string name in parserBooleanNames)
        {
            bool value = RequireBoolean(parserLanguage[name], $"coverage.parserLanguage.{name}");
            if (passed && !value)
            {
                throw new InvalidDataException("Passing parser-language facts must be true.");
            }
        }

        long retainedCount = RequireInteger(
            parserLanguage["emptyFileRetainedCodeUnitCount"],
            0,
            250_000,
            "coverage.parserLanguage.emptyFileRetainedCodeUnitCount");
        long unknownCount = RequireInteger(
            parserLanguage["unknownLanguageCount"],
            0,
            10_000,
            "coverage.parserLanguage.unknownLanguageCount");
        if (passed && (retainedCount != 0 || unknownCount != 1))
        {
            throw new InvalidDataException("Passing parser-language counts are invalid.");
        }
    }

    private static void ValidatePositiveCountObject(
        JsonElement element,
        string path,
        string[] names,
        bool passed)
    {
        IReadOnlyDictionary<string, JsonElement> value = RequireObject(element, path, names);
        foreach (string name in names)
        {
            long count = RequireInteger(value[name], 0, 250_001, $"{path}.{name}");
            if (passed && count == 0)
            {
                throw new InvalidDataException($"Passing count at {path} must be positive.");
            }
        }
    }

    private static void ValidateImmutableSnapshot(JsonElement element, bool passed)
    {
        IReadOnlyDictionary<string, JsonElement> snapshot = RequireObject(
            element,
            "immutableSnapshot",
            ["indexCount", "watcherStarted", "mutationBetweenProjections", "snapshotEqual"]);
        long indexCount = RequireInteger(snapshot["indexCount"], 0, 1, "immutableSnapshot.indexCount");
        bool watcherStarted = RequireBoolean(snapshot["watcherStarted"], "immutableSnapshot.watcherStarted");
        bool mutation = RequireBoolean(
            snapshot["mutationBetweenProjections"],
            "immutableSnapshot.mutationBetweenProjections");
        bool equal = RequireBoolean(snapshot["snapshotEqual"], "immutableSnapshot.snapshotEqual");
        if (passed && (indexCount != 1 || watcherStarted || mutation || !equal))
        {
            throw new InvalidDataException("Passing immutable snapshot facts are invalid.");
        }
    }

    private static void ValidateCleanup(JsonElement element, bool passed)
    {
        string[] booleanNames =
        [
            "gatewayStopped", "contextDeleted", "providerStateRemoved", "providerHomeRemoved",
            "containerRemoved", "tempImageTagsRemoved",
        ];
        IReadOnlyDictionary<string, JsonElement> cleanup = RequireObject(
            element,
            "cleanup",
            [.. booleanNames, "providerStateKind", "failureCount"]);
        foreach (string name in booleanNames)
        {
            bool value = RequireBoolean(cleanup[name], $"cleanup.{name}");
            if (passed && !value)
            {
                throw new InvalidDataException("Passing cleanup facts must be true.");
            }
        }

        RequireOneOf(
            cleanup["providerStateKind"],
            "cleanup.providerStateKind",
            ["absent", "regular-file", "directory", "link-or-reparse", "special", "unknown"]);
        long failureCount = RequireInteger(cleanup["failureCount"], 0, 8, "cleanup.failureCount");
        if (passed && failureCount != 0)
        {
            throw new InvalidDataException("Passing cleanup evidence has failures.");
        }
    }

    private static void ValidateForbiddenRetention(JsonElement element)
    {
        string[] names =
        [
            "scalarValues", "source", "rows", "rawJson", "rawQuery", "paths", "symbols",
            "credentials", "tokens", "endpointValues", "diagnostics",
        ];
        IReadOnlyDictionary<string, JsonElement> retention = RequireObject(element, "forbiddenRetention", names);
        foreach (string name in names)
        {
            if (RequireBoolean(retention[name], $"forbiddenRetention.{name}"))
            {
                throw new InvalidDataException("Evidence reports forbidden retained content.");
            }
        }
    }

    private static IReadOnlyDictionary<string, JsonElement> RequireObject(
        JsonElement element,
        string path,
        string[] required,
        string[]? optional = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }

        var allowed = new HashSet<string>(required, StringComparer.Ordinal);
        if (optional is not null)
        {
            allowed.UnionWith(optional);
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name) || !properties.TryAdd(property.Name, property.Value))
            {
                throw new InvalidDataException($"{path} has an unknown or duplicate property.");
            }
        }

        if (required.Any(name => !properties.ContainsKey(name)))
        {
            throw new InvalidDataException($"{path} lacks a required property.");
        }

        return properties;
    }

    private static JsonElement[] RequireArray(JsonElement element, int minimum, int maximum, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path} must be an array.");
        }

        JsonElement[] values = element.EnumerateArray().ToArray();
        if (values.Length < minimum || values.Length > maximum)
        {
            throw new InvalidDataException($"{path} is outside its item bound.");
        }

        return values;
    }

    private static bool RequireBoolean(JsonElement element, string path)
    {
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"{path} must be a strict JSON boolean.");
        }

        return element.GetBoolean();
    }

    private static long RequireInteger(JsonElement element, long minimum, long maximum, string path)
    {
        string raw = element.GetRawText();
        if (element.ValueKind != JsonValueKind.Number
            || raw.IndexOfAny(['.', 'e', 'E']) >= 0
            || !element.TryGetInt64(out long value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidDataException($"{path} must be a bounded JSON integer.");
        }

        return value;
    }

    private static string RequireString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{path} must be a string.");
        }

        return element.GetString()!;
    }

    private static void RequireEqualString(JsonElement element, string expected, string path)
    {
        if (!string.Equals(RequireString(element, path), expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{path} does not equal the canonical value.");
        }
    }

    private static string RequireOneOf(JsonElement element, string path, string[] values)
    {
        string value = RequireString(element, path);
        if (!values.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{path} is outside the closed value set.");
        }

        return value;
    }

    private static void RequireSha256(JsonElement element, string path)
    {
        string value = RequireString(element, path);
        if (!IsLowerHex(value, 64))
        {
            throw new InvalidDataException($"{path} must be lower-case SHA-256.");
        }
    }

    private static void RequireGitObjectId(JsonElement element, string path)
    {
        string value = RequireString(element, path);
        if (!IsLowerHex(value, 40) && !IsLowerHex(value, 64))
        {
            throw new InvalidDataException($"{path} must be a lower-case Git object ID.");
        }
    }

    private static void RequireImageId(JsonElement element, string path)
    {
        string value = RequireString(element, path);
        if (!value.StartsWith("sha256:", StringComparison.Ordinal) || !IsLowerHex(value[7..], 64))
        {
            throw new InvalidDataException($"{path} must be an immutable image ID.");
        }
    }

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string RequireSafeName(JsonElement element, string path)
    {
        string value = RequireString(element, path);
        if (!_safeNames.Contains(value))
        {
            throw new InvalidDataException($"{path} is not a raw safe name.");
        }

        return value;
    }

    private static string ValidateUnknownName(JsonElement element, string path)
    {
        IReadOnlyDictionary<string, JsonElement> value = RequireObject(
            element,
            path,
            ["hmacSha256", "utf8Length", "firstSeenOrdinal"]);
        RequireSha256(value["hmacSha256"], $"{path}.hmacSha256");
        RequireInteger(value["utf8Length"], 0, 67_108_864, $"{path}.utf8Length");
        RequireInteger(value["firstSeenOrdinal"], 0, 127, $"{path}.firstSeenOrdinal");
        return RequireString(value["hmacSha256"], $"{path}.hmacSha256");
    }

    private static void RequireContainerReference(JsonElement element, int containerCount, string path)
    {
        long value = RequireInteger(element, 0, 31, path);
        if (value >= containerCount)
        {
            throw new InvalidDataException($"{path} does not reference an observed container.");
        }
    }

    private static HashSet<string> ExtractQuotedNames(string value) =>
        Regex.Matches(value, "\\\"([a-z][a-z0-9_]*)\\\"", RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static byte[] CreateClaim(byte[] manifestBytes, string acceptedCorrectionSha256)
    {
        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        var claim = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["acceptedCorrectionManifest"] = manifest.RootElement.Clone(),
            ["acceptedCorrectionSha256"] = acceptedCorrectionSha256,
            ["evidenceSchema"] = _evidenceSchema,
            ["executionOrdinal"] = 1,
            ["schema"] = "cgc-provider-contract-claim-v1",
        };
        return JsonSerializer.SerializeToUtf8Bytes(claim);
    }

    private static void WriteCreateNewDurable(string path, byte[] bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hash(byte[] value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));

    private static string RequireEnvironment(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"Required completion-lane input {name} is absent.");
        return value!;
    }

    private static string RequireSha256Environment(string name)
    {
        string value = RequireEnvironment(name);
        Assert.Matches("^[0-9a-f]{64}$", value);
        return value;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Vulgata.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static async Task<ProcessResult> RunAsync(
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var cancellation = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            throw;
        }

        return new ProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    private static void EnsureSuccess(ProcessResult result)
    {
        Assert.True(result.ExitCode == 0, $"Process exited {result.ExitCode}: {Bound(result.StandardError)}");
    }

    private static string Bound(string value)
    {
        string normalized = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 1_024 ? normalized : normalized[..1_024];
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
