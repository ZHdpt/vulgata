using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Vulgata.Tests.ReleaseGate;

internal sealed record Story125RunIdentity(string DatabaseName);

internal static partial class Story125ReleaseHarness
{
    private const string _databasePrefix = "vulgata_story125_";
    private static readonly (string Id, int Tier)[] _mandatoryChecks =
    [
        ("tier1-contract-ranking-relationship", 1),
        ("tier1-conversation-synthesis-security", 1),
        ("tier1-production-ui-corpus-evidence", 1),
        ("tier1-build-full-tests", 1),
        ("tier2-postgresql-restart-resume", 2),
        ("tier3-candidate-tool-database-preflight", 3),
        ("tier3-real-scan", 3),
        ("tier3-grounded-chat-corpus", 3),
        ("tier3-restart-continuity", 3),
        ("tier3-screenshot-release-integrity", 3),
    ];

    private static readonly HashSet<string> _transportOutcomes =
    [
        "completed", "cancelled", "disconnect", "inactivity", "overall-deadline",
        "raw-body-overflow", "sse-line-overflow", "sse-event-overflow", "chunk-overflow",
        "total-overflow", "invalid-wire-utf8", "invalid-sse", "invalid-json-event",
        "invalid-json-escape", "invalid-answer-utf8", "missing-terminal", "duplicate-terminal",
        "contradictory-terminal", "post-terminal-data", "provider-failure",
    ];

    private static readonly HashSet<string> _validationOutcomes =
    [
        "syntax-repair-required", "accepted", "rejected-before-parse", "rejected-syntax",
        "rejected-semantic", "cancelled",
    ];

    internal static Story125RunIdentity CreateRunIdentity() =>
        new(_databasePrefix + Guid.NewGuid().ToString("N"));

    internal static bool IsOwnedDatabaseName(string? databaseName) =>
        databaseName is not null && OwnedDatabaseNameRegex().IsMatch(databaseName);

    internal static IReadOnlyList<string> ValidateSemanticEnvelope(string json)
    {
        var errors = new List<string>();
        JsonObject? envelope;
        try
        {
            envelope = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException exception)
        {
            return [$"Envelope is not valid JSON: {exception.Message}"];
        }

        if (envelope is null)
        {
            return ["Envelope root must be an object."];
        }

        string? status = StringValue(envelope, "status");
        string? resultKind = StringValue(envelope["result"] as JsonObject, "kind");
        bool passed = string.Equals(status, "passed", StringComparison.Ordinal);
        if (!string.Equals(status, resultKind, StringComparison.Ordinal))
        {
            errors.Add("Envelope status and result kind must agree exactly.");
        }

        if (passed != (envelope["releaseReceipt"] is JsonObject))
        {
            errors.Add("A release receipt is required only for a passed envelope.");
        }

        ValidateSupersession(envelope, errors);
        ValidateChecks(envelope["checks"] as JsonArray, passed, errors);
        ValidateEvidenceBindings(envelope, errors);

        JsonArray? observations = envelope["attemptedObservations"] as JsonArray;
        if (status is "planned" or "not-run" && observations is { Count: > 0 })
        {
            errors.Add($"A {status} envelope cannot contain attempted observations.");
        }

        if (observations is not null)
        {
            foreach (JsonNode? observation in observations)
            {
                if (observation is JsonObject item)
                {
                    ValidateContentDarkObservation(item, envelope, errors);
                    ValidateCaseAttempts(item, errors);
                    ValidateStreamAttempt(item, errors);
                }
            }
        }

        if (envelope["releaseReceipt"] is JsonObject receipt)
        {
            ValidatePassedReceipt(receipt, errors);
        }

        return errors;
    }

    private static void ValidateEvidenceBindings(JsonObject envelope, List<string> errors)
    {
        Dictionary<string, JsonObject> commands = IndexById(
            envelope["commands"] as JsonArray,
            "command",
            errors);
        Dictionary<string, JsonObject> observations = IndexById(
            envelope["attemptedObservations"] as JsonArray,
            "observation",
            errors);

        var commandArtifacts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string commandId, JsonObject command) in commands)
        {
            var artifacts = new HashSet<string>(StringComparer.Ordinal);
            AddArtifact(command["stdoutArtifact"] as JsonObject, artifacts, errors);
            AddArtifact(command["stderrArtifact"] as JsonObject, artifacts, errors);
            commandArtifacts[commandId] = artifacts;
        }

        var observationArtifacts = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string observationId, JsonObject observation) in observations)
        {
            string[] referencedCommandIds = StringArray(observation, "commandIds", errors);
            HashSet<string> permittedArtifacts = ResolveCommandArtifacts(
                referencedCommandIds,
                commands,
                commandArtifacts,
                errors);
            HashSet<string> artifacts = ArtifactSet(observation["artifacts"] as JsonArray, errors);
            if (artifacts.Count == 0)
            {
                errors.Add($"Observation {observationId} must carry at least one evidence artifact.");
            }
            else if (!artifacts.IsSubsetOf(permittedArtifacts))
            {
                errors.Add($"Observation {observationId} contains an artifact not reproduced by its same-envelope commands.");
            }

            observationArtifacts[observationId] = artifacts;
            ValidateRecordedCaseBindings(observation, errors);
        }

        if (envelope["checks"] is not JsonArray checks)
        {
            return;
        }

        foreach (JsonObject check in checks.OfType<JsonObject>())
        {
            string[] referencedCommandIds = StringArray(check, "commandIds", errors);
            HashSet<string> permittedArtifacts = ResolveCommandArtifacts(
                referencedCommandIds,
                commands,
                commandArtifacts,
                errors);

            foreach (string observationId in StringArray(check, "attemptedObservationIds", errors))
            {
                if (!observations.TryGetValue(observationId, out _))
                {
                    errors.Add($"Check references unresolved observation {observationId}.");
                    continue;
                }

                permittedArtifacts.UnionWith(observationArtifacts[observationId]);
            }

            HashSet<string> artifacts = ArtifactSet(check["artifacts"] as JsonArray, errors);
            if (!artifacts.IsSubsetOf(permittedArtifacts))
            {
                errors.Add("Check contains an artifact not reproduced by its referenced same-envelope command or observation.");
            }
        }
    }

    private static Dictionary<string, JsonObject> IndexById(
        JsonArray? items,
        string kind,
        List<string> errors)
    {
        var indexed = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (items is null)
        {
            return indexed;
        }

        foreach (JsonObject item in items.OfType<JsonObject>())
        {
            string? id = StringValue(item, "id");
            if (id is null)
            {
                errors.Add($"Every {kind} must have an ID.");
            }
            else if (!indexed.TryAdd(id, item))
            {
                errors.Add($"Duplicate {kind} ID {id}.");
            }
        }

        return indexed;
    }

    private static HashSet<string> ResolveCommandArtifacts(
        IEnumerable<string> commandIds,
        IReadOnlyDictionary<string, JsonObject> commands,
        IReadOnlyDictionary<string, HashSet<string>> commandArtifacts,
        List<string> errors)
    {
        var artifacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (string commandId in commandIds)
        {
            if (!commands.TryGetValue(commandId, out JsonObject? command))
            {
                errors.Add($"Evidence references unresolved command {commandId}.");
                continue;
            }

            if (IntValue(command, "exitCode") != 0)
            {
                errors.Add($"Referenced command {commandId} must exit with code zero.");
            }

            artifacts.UnionWith(commandArtifacts[commandId]);
        }

        return artifacts;
    }

    private static HashSet<string> ArtifactSet(JsonArray? artifacts, List<string> errors)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (artifacts is null)
        {
            return result;
        }

        foreach (JsonObject artifact in artifacts.OfType<JsonObject>())
        {
            AddArtifact(artifact, result, errors);
        }

        return result;
    }

    private static void AddArtifact(JsonObject? artifact, HashSet<string> artifacts, List<string> errors)
    {
        if (artifact is null)
        {
            return;
        }

        string? path = StringValue(artifact, "path");
        string? sha256 = StringValue(artifact, "sha256");
        string? mediaType = StringValue(artifact, "mediaType");
        if (path is null || sha256 is null || mediaType is null)
        {
            errors.Add("Artifact binding requires path, SHA-256, and media type.");
            return;
        }

        artifacts.Add(string.Concat(path, "\n", sha256, "\n", mediaType));
    }

    private static string[] StringArray(JsonObject value, string propertyName, List<string> errors)
    {
        if (value[propertyName] is not JsonArray values)
        {
            return [];
        }

        var result = new List<string>(values.Count);
        foreach (JsonNode? node in values)
        {
            if (node is JsonValue item && item.TryGetValue(out string? text) && text is not null)
            {
                result.Add(text);
            }
            else
            {
                errors.Add($"{propertyName} must contain only string IDs.");
            }
        }

        return [.. result];
    }

    private static void ValidateRecordedCaseBindings(JsonObject observation, List<string> errors)
    {
        string[] names = ["corpusCaseIds", "resultCaseIds", "receiptCaseIds"];
        if (!names.Any(observation.ContainsKey))
        {
            return;
        }

        HashSet<string>[] sets = names
            .Select(name => StringArray(observation, name, errors).ToHashSet(StringComparer.Ordinal))
            .ToArray();
        if (sets.Any(set => set.Count == 0) || !sets[0].SetEquals(sets[1]) || !sets[0].SetEquals(sets[2]))
        {
            errors.Add("Corpus, result, and receipt case ID sets must agree exactly.");
        }

        string[] packetFingerprints = StringArray(observation, "packetFingerprints", errors);
        if (packetFingerprints.Length > sets[0].Count ||
            packetFingerprints.Distinct(StringComparer.Ordinal).Count() != packetFingerprints.Length)
        {
            errors.Add("Packet fingerprints must be a unique bounded invoked-case binding.");
        }
    }

    internal static IReadOnlyList<string> ValidateCrossArtifactConsistency(string repositoryRoot)
    {
        var errors = new List<string>();
        string featureRoot = Path.Combine(
            repositoryRoot,
            "specs",
            "014-grounded-llm-wiki-chat-release-evidence");
        string[] prosePaths =
        [
            Path.Combine(featureRoot, "plan.md"),
            Path.Combine(featureRoot, "data-model.md"),
            Path.Combine(featureRoot, "contracts", "chat-provider-streaming-v1.md"),
        ];
        string releasePath = Path.Combine(featureRoot, "contracts", "release-evidence-contract.md");
        string schemaPath = Path.Combine(featureRoot, "contracts", "story125-release-evidence.schema.json");

        foreach (string path in prosePaths.Append(releasePath).Append(schemaPath))
        {
            if (!File.Exists(path))
            {
                errors.Add($"Required contract artifact is missing: {path}");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        string[] requiredCaps = ["16,384", "65,536", "1,048,576", "131,072"];
        foreach (string path in prosePaths)
        {
            string text = File.ReadAllText(path);
            foreach (string cap in requiredCaps)
            {
                if (!text.Contains(cap, StringComparison.Ordinal))
                {
                    errors.Add($"{Path.GetFileName(path)} omits accepted byte cap {cap}.");
                }
            }
        }

        string release = File.ReadAllText(releasePath);
        foreach (string term in new[] { "streaming", "screenshot", "receipt" })
        {
            if (!release.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Release evidence contract omits {term}.");
            }
        }

        string model = File.ReadAllText(prosePaths[1]);
        foreach (string vocabularyName in new[] { "transportOutcome", "rejectionCategory", "validationOutcome" })
        {
            if (!model.Contains(vocabularyName, StringComparison.Ordinal))
            {
                errors.Add($"Data model omits {vocabularyName}.");
            }
        }

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        JsonElement attempt = schema.RootElement.GetProperty("$defs").GetProperty("streamAttemptReceipt");
        JsonElement properties = attempt.GetProperty("properties");
        ValidateEnum(properties.GetProperty("transportOutcome").GetProperty("enum"), _transportOutcomes, "transportOutcome", errors);
        ValidateEnum(
            properties.GetProperty("rejectionCategory").GetProperty("oneOf")[0].GetProperty("enum"),
            _transportOutcomes.Where(value => value != "completed").ToHashSet(StringComparer.Ordinal),
            "rejectionCategory",
            errors);
        ValidateEnum(properties.GetProperty("validationOutcome").GetProperty("enum"), _validationOutcomes, "validationOutcome", errors);

        return errors;
    }

    private static void ValidateSupersession(JsonObject envelope, List<string> errors)
    {
        string? runId = StringValue(envelope, "runId");
        string? supersedes = StringValue(envelope, "supersedesRunId");
        if (supersedes is null)
        {
            return;
        }

        if (string.Equals(runId, supersedes, StringComparison.Ordinal))
        {
            errors.Add("A run cannot supersede itself.");
            return;
        }

        Match current = RunIdRegex().Match(runId ?? string.Empty);
        Match previous = RunIdRegex().Match(supersedes);
        if (!current.Success || !previous.Success ||
            !string.Equals(current.Groups[2].Value, previous.Groups[2].Value, StringComparison.Ordinal))
        {
            errors.Add("The supersession target is not in this immutable run lineage.");
            return;
        }

        if (string.CompareOrdinal(previous.Groups[1].Value, current.Groups[1].Value) >= 0)
        {
            errors.Add("A supersession target must be older than the correcting run.");
        }
    }

    private static void ValidateChecks(JsonArray? checks, bool passed, List<string> errors)
    {
        if (!passed)
        {
            return;
        }

        if (checks is null || checks.Count != _mandatoryChecks.Length)
        {
            errors.Add("A passed envelope must contain exactly the ten mandatory checks.");
            return;
        }

        for (int index = 0; index < _mandatoryChecks.Length; index++)
        {
            if (checks[index] is not JsonObject check)
            {
                errors.Add($"Mandatory check {index + 1} must be an object.");
                continue;
            }

            (string expectedId, int expectedTier) = _mandatoryChecks[index];
            if (
                !string.Equals(StringValue(check, "id"), expectedId, StringComparison.Ordinal) ||
                IntValue(check, "tier") != expectedTier ||
                !string.Equals(StringValue(check, "status"), "passed", StringComparison.Ordinal))
            {
                errors.Add($"Mandatory check {index + 1} must be {expectedId} at Tier {expectedTier} in fixed order and passed.");
            }
        }
    }

    private static void ValidatePassedReceipt(JsonObject receipt, List<string> errors)
    {
        if (receipt["screenshots"] is JsonArray screenshots)
        {
            string[] categories = ["scan-state", "business-answer", "it-answer", "restart", "release-summary"];
            var actual = screenshots
                .OfType<JsonObject>()
                .Select(item => StringValue(item, "viewKind"))
                .Where(value => value is not null)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string category in categories)
            {
                if (!actual.Contains(category))
                {
                    errors.Add($"Passed release receipt lacks screenshot category {category}.");
                }
            }
        }
        else
        {
            errors.Add("Passed release receipt lacks screenshots.");
        }

        if (receipt["chat"] is not JsonObject chat)
        {
            return;
        }

        if (IntValue(chat, "caseCount") is int caseCount && caseCount > 0 &&
            IntValue(chat, "usefulCount") is int usefulCount &&
            DoubleValue(chat, "usefulPercent") is double recorded)
        {
            decimal exact = decimal.Floor((10000m * usefulCount / caseCount) + 0.5m) / 100m;
            if ((decimal)recorded != exact)
            {
                errors.Add($"Useful percentage must use exact half-up scoring; expected {exact.ToString("0.00", CultureInfo.InvariantCulture)}.");
            }
        }

        if (IntValue(chat, "acceptedTechnicalClaimCitationPercent") != 100)
        {
            errors.Add("Accepted material technical claim citation coverage must be exactly 100 percent.");
        }

        if (IntValue(chat, "unauthorizedDisclosureCount") != 0)
        {
            errors.Add("Any unauthorized evidence or existence disclosure is an automatic failure.");
        }

        if (IntValue(chat, "overThirtySecondAcceptedAnswerCount") != 0)
        {
            errors.Add("Every accepted complete answer must finish in under 30 seconds.");
        }

        if (chat["caseReceipts"] is JsonArray caseReceipts)
        {
            foreach (JsonObject caseReceipt in caseReceipts.OfType<JsonObject>())
            {
                string? disposition = StringValue(caseReceipt, "providerDisposition");
                if (disposition is not ("invoked" or "not-invoked-insufficient-evidence" or "not-invoked-authorization-boundary"))
                {
                    errors.Add($"Unknown providerDisposition {disposition ?? "<missing>"}.");
                }

                if (IntValue(caseReceipt, "totalAcceptedAnswerDurationMilliseconds") is int duration && duration >= 30000)
                {
                    errors.Add("Accepted answer duration must be under 30 seconds.");
                }
            }
        }
    }

    private static void ValidateContentDarkObservation(JsonObject observation, JsonObject envelope, List<string> errors)
    {
        string[] forbiddenContentFields =
        [
            "content", "answer", "partialAnswer", "claim", "citation", "sourceAnchor",
            "connectionString", "credential", "secret",
        ];
        if (observation.Any(property => forbiddenContentFields.Contains(property.Key, StringComparer.OrdinalIgnoreCase)))
        {
            errors.Add("Attempted observations must remain content-dark and secret-free.");
        }

        if (observation["unauthorizedDisclosure"]?.GetValue<bool>() is true)
        {
            errors.Add("An observation reports unauthorized disclosure.");
        }

        string? source = StringValue(observation, "source");
        if (source is not null && !string.Equals(source, "production-raw-response-header-adapter", StringComparison.Ordinal))
        {
            errors.Add("Only production raw-response-header adapter observations are release eligible.");
        }

        string? candidateCommit = StringValue(observation, "candidateCommit");
        string? envelopeCommit = StringValue(envelope["candidate"] as JsonObject, "commit");
        if (candidateCommit is not null && !string.Equals(candidateCommit, envelopeCommit, StringComparison.Ordinal))
        {
            errors.Add("Stream observation candidate does not match the envelope candidate.");
        }

        if (observation.ContainsKey("observationSignature") && StringValue(observation, "observationSignature") is null)
        {
            errors.Add("Stream observation provenance must be signed.");
        }

        string? processId = StringValue(observation, "processObservationId");
        string? envelopeProcessId = StringValue(envelope["environment"]?["streamObservationAuthority"] as JsonObject, "processObservationId");
        if (processId is not null && !string.Equals(processId, envelopeProcessId, StringComparison.Ordinal))
        {
            errors.Add("Stream observation process does not match the same-process preflight.");
        }
    }

    private static void ValidateCaseAttempts(JsonObject item, List<string> errors)
    {
        if (!string.Equals(StringValue(item, "providerDisposition"), "invoked", StringComparison.Ordinal))
        {
            return;
        }

        if (item["streamAttempts"] is not JsonArray attempts || attempts.Count is < 1 or > 2)
        {
            errors.Add("An invoked case requires one primary attempt and at most one repair attempt.");
            return;
        }

        string? first = StringValue(attempts[0] as JsonObject, "attemptKind");
        string? second = attempts.Count == 2 ? StringValue(attempts[1] as JsonObject, "attemptKind") : null;
        if (first != "primary" || (attempts.Count == 2 && second != "syntax-repair"))
        {
            errors.Add("Stream attempts must be ordered primary then optional syntax-repair, without duplicates.");
        }
    }

    private static void ValidateStreamAttempt(JsonObject item, List<string> errors)
    {
        string? transport = StringValue(item, "transportOutcome");
        string? rejection = StringValue(item, "rejectionCategory");
        string? validation = StringValue(item, "validationOutcome");

        if (transport is not null && !_transportOutcomes.Contains(transport))
        {
            errors.Add($"Unknown transportOutcome {transport}.");
        }
        if (rejection is not null && (rejection == "completed" || !_transportOutcomes.Contains(rejection)))
        {
            errors.Add($"Unknown rejectionCategory {rejection}.");
        }
        if (validation is not null && !_validationOutcomes.Contains(validation))
        {
            errors.Add($"Unknown validationOutcome {validation}.");
        }

        if (transport == "completed" &&
            (IntValue(item, "terminalEventCount") != 1 || rejection is not null))
        {
            errors.Add("A completed stream requires exactly one terminal and no rejection category.");
        }
        if (StringValue(item, "attemptKind") == "syntax-repair" && validation == "accepted" && transport != "completed")
        {
            errors.Add("An accepted syntax-repair attempt requires completed transport.");
        }
        if (IntValue(item, "overallDeadlineMilliseconds") is int deadline && deadline != 29000)
        {
            errors.Add("All attempts share the coordinator-created 29,000 millisecond deadline.");
        }
        if (IntValue(item, "inactivityLimitMilliseconds") is int inactivity && inactivity != 10000)
        {
            errors.Add("Stream inactivity limit must be exactly 10,000 milliseconds.");
        }
    }

    private static void ValidateEnum(
        JsonElement enumElement,
        IReadOnlySet<string> expected,
        string name,
        List<string> errors)
    {
        HashSet<string> actual = enumElement.EnumerateArray()
            .Select(value => value.GetString())
            .Where(value => value is not null)
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            errors.Add($"Schema {name} vocabulary differs from the accepted closed vocabulary.");
        }
    }

    private static string? StringValue(JsonObject? value, string propertyName) =>
        value?[propertyName] is JsonValue property && property.TryGetValue(out string? result)
            ? result
            : null;

    private static int? IntValue(JsonObject? value, string propertyName) =>
        value?[propertyName] is JsonValue property && property.TryGetValue(out int result)
            ? result
            : null;

    private static double? DoubleValue(JsonObject? value, string propertyName) =>
        value?[propertyName] is JsonValue property && property.TryGetValue(out double result)
            ? result
            : null;

    [GeneratedRegex("^vulgata_story125_[a-z0-9_]{8,48}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnedDatabaseNameRegex();

    [GeneratedRegex("^story125-([0-9]{8}t[0-9]{6}z)-([a-z0-9]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex RunIdRegex();
}
