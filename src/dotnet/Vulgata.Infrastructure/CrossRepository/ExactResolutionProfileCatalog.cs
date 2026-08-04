using System.Security.Cryptography;
using System.Text.Json;
using Vulgata.Core.DomainServices;

namespace Vulgata.Infrastructure.CrossRepository;

public sealed class ExactResolutionProfileCatalog
{
    private const string PreEvidenceRefusalMessage = "精确跨仓库解析请求被拒绝。";
    private const string ResourceName =
        "Vulgata.Infrastructure.CrossRepository.Profiles.demo-resolution-profile.v1.json";

    public ExactResolutionProfile Load()
    {
        using Stream resource = typeof(ExactResolutionProfileCatalog).Assembly
            .GetManifestResourceStream(ResourceName) ??
            throw new InvalidDataException("The reviewed exact-resolution profile is unavailable.");
        if (resource.CanSeek && resource.Length > ExactResolutionLimits.MaxProfileBytes)
        {
            throw new InvalidDataException("The reviewed exact-resolution profile exceeds its bound.");
        }

        using var buffer = new MemoryStream(capacity: 4 * 1024);
        resource.CopyTo(buffer);
        if (buffer.Length > ExactResolutionLimits.MaxProfileBytes)
        {
            throw new InvalidDataException("The reviewed exact-resolution profile exceeds its bound.");
        }

        return ParseForValidation(buffer.ToArray());
    }

    internal static ExactResolutionProfile ParseForValidation(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.Length is 0 or > ExactResolutionLimits.MaxProfileBytes)
        {
            throw new InvalidDataException("The exact-resolution profile has an invalid size.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = ExactResolutionLimits.MaxJsonDepth,
                });
            JsonElement root = RequireObject(
                document.RootElement,
                "schemaVersion",
                "profileId",
                "profileVersion",
                "repositories",
                "occurrences",
                "unsupportedObservations");

            ProfileRepository[] repositories = ReadArray(
                root.GetProperty("repositories"),
                ReadRepository);
            ResolutionOccurrence[] occurrences = ReadArray(
                root.GetProperty("occurrences"),
                ReadOccurrence);
            UnsupportedObservation[] unsupported = ReadArray(
                root.GetProperty("unsupportedObservations"),
                ReadUnsupportedObservation);
            string fingerprint = Convert.ToHexString(SHA256.HashData(utf8Json.Span))
                .ToLowerInvariant();
            return new ExactResolutionProfile(
                RequireString(root, "schemaVersion"),
                RequireString(root, "profileId"),
                RequireString(root, "profileVersion"),
                fingerprint,
                repositories,
                occurrences,
                unsupported,
                utf8Json);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("The exact-resolution profile is invalid.", exception);
        }
    }

    public static ExactResolutionRuntimeRequest BindForValidation(
        Guid systemId,
        ExactResolutionProfile profile,
        string profileId,
        string profileVersion,
        string profileFingerprint,
        IReadOnlyCollection<ExactResolutionRuntimeBinding> bindings,
        IReadOnlyCollection<ExactResolutionRuntimeBindingEvidence> evidence)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(bindings);
            ArgumentNullException.ThrowIfNull(evidence);
            if (!StringComparer.Ordinal.Equals(profile.ProfileId, profileId) ||
                !StringComparer.Ordinal.Equals(profile.ProfileVersion, profileVersion) ||
                !StringComparer.Ordinal.Equals(profile.ProfileFingerprint, profileFingerprint))
            {
                throw new InvalidOperationException();
            }

            ExactResolutionRuntimeRequest request = ExactResolutionRuntimeRequest.Create(
                systemId,
                profile,
                bindings);
            if (evidence.Count != request.Bindings.Count ||
                evidence.Select(value => value.Alias).Distinct(StringComparer.Ordinal).Count() != evidence.Count ||
                evidence.Select(value => value.RepositoryId).Distinct().Count() != evidence.Count)
            {
                throw new InvalidOperationException();
            }

            Dictionary<string, ExactResolutionRuntimeBindingEvidence> byAlias =
                evidence.ToDictionary(value => value.Alias, StringComparer.Ordinal);
            foreach (ExactResolutionRuntimeBinding binding in request.Bindings)
            {
                ProfileRepository repository = profile.Repositories.Single(
                    value => StringComparer.Ordinal.Equals(value.Alias, binding.Alias));
                if (!byAlias.TryGetValue(binding.Alias, out ExactResolutionRuntimeBindingEvidence? fact) ||
                    !StringComparer.Ordinal.Equals(binding.StableGitIdentity, fact.StableGitIdentity) ||
                    binding.RepositoryId != fact.RepositoryId ||
                    binding.ScanRunId != fact.ScanRunId ||
                    binding.ScanExecutionId != fact.ScanExecutionId ||
                    fact.SystemId != systemId ||
                    !AllRevisionsEqual(repository.ExpectedCommit, fact))
                {
                    throw new InvalidOperationException();
                }
            }

            return request;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException(PreEvidenceRefusalMessage);
        }
    }

    private static bool AllRevisionsEqual(
        string expectedCommit,
        ExactResolutionRuntimeBindingEvidence evidence) =>
        StringComparer.Ordinal.Equals(expectedCommit, evidence.SnapshotProfiledRevision) &&
        StringComparer.Ordinal.Equals(expectedCommit, evidence.ScanRunProfiledRevision) &&
        StringComparer.Ordinal.Equals(expectedCommit, evidence.ScanExecutionProfiledRevision) &&
        StringComparer.Ordinal.Equals(expectedCommit, evidence.DocumentVersionProfiledRevision) &&
        StringComparer.Ordinal.Equals(expectedCommit, evidence.SourceAnchorProfiledRevision);

    private static ProfileRepository ReadRepository(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "alias",
            "stableGitIdentity",
            "expectedRef",
            "expectedCommit");
        return new(
            RequireString(item, "alias"),
            RequireString(item, "stableGitIdentity"),
            RequireSafeFullTagRef(RequireString(item, "expectedRef")),
            RequireString(item, "expectedCommit"));
    }

    private static string RequireSafeFullTagRef(string value)
    {
        if (value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The profile contains an unsafe Git ref.");
        }

        return value;
    }

    private static ResolutionOccurrence ReadOccurrence(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "occurrenceId",
            "protocol",
            "consumerAlias",
            "providerAlias",
            "consumer",
            "provider",
            "normalization",
            "comparison");
        return new(
            RequireString(item, "occurrenceId"),
            ReadProtocol(RequireString(item, "protocol")),
            RequireString(item, "consumerAlias"),
            RequireString(item, "providerAlias"),
            ReadConsumer(item.GetProperty("consumer")),
            ReadProvider(item.GetProperty("provider")),
            ReadNormalization(item.GetProperty("normalization")),
            ReadComparison(RequireString(item, "comparison")));
    }

    private static ExactConsumerSelector ReadConsumer(JsonElement value)
    {
        JsonElement item = RequireObject(value, "files", "invocation");
        return new(
            ReadArray(item.GetProperty("files"), static path => RequireStringValue(path)),
            ReadConsumerInvocation(item.GetProperty("invocation")));
    }

    private static ExactConsumerInvocationSelector ReadConsumerInvocation(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "form",
            "receiverSymbol",
            "memberName",
            "endpointArgumentIndex",
            "line",
            "occurrenceOrdinalOnLine");
        return new(
            ReadInvocationForm(RequireString(item, "form")),
            RequireString(item, "receiverSymbol"),
            RequireString(item, "memberName"),
            RequireInt32(item, "endpointArgumentIndex"),
            RequireInt32(item, "line"),
            RequireInt32(item, "occurrenceOrdinalOnLine"));
    }

    private static ExactProviderSelector ReadProvider(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "root",
            "files",
            "declaration",
            "implementationDocument");
        return new(
            RequireString(item, "root"),
            ReadArray(item.GetProperty("files"), static path => RequireStringValue(path)),
            ReadProviderDeclaration(item.GetProperty("declaration")),
            ReadDocumentSelector(item.GetProperty("implementationDocument")));
    }

    private static ExactProviderDeclarationSelector ReadProviderDeclaration(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "elementLocalName",
            "endpointAttributeLocalName",
            "line",
            "occurrenceOrdinalOnLine");
        return new(
            RequireString(item, "elementLocalName"),
            RequireString(item, "endpointAttributeLocalName"),
            RequireInt32(item, "line"),
            RequireInt32(item, "occurrenceOrdinalOnLine"));
    }

    private static ExactDocumentSelector ReadDocumentSelector(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "repositoryRelativePath",
            "symbolKind",
            "symbol");
        return new(
            RequireString(item, "repositoryRelativePath"),
            ReadSymbolKind(RequireString(item, "symbolKind")),
            RequireString(item, "symbol"));
    }

    private static ExactEndpointNormalization ReadNormalization(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "id",
            "consumerRequiredPrefix",
            "providerRequiredPrefix");
        string id = RequireString(item, "id");
        string consumerPrefix = RequireString(item, "consumerRequiredPrefix", allowEmpty: true);
        string providerPrefix = RequireString(item, "providerRequiredPrefix");
        if (!StringComparer.Ordinal.Equals(id, "custom-rpc-in-prefix-v1") ||
            consumerPrefix.Length != 0 ||
            !StringComparer.Ordinal.Equals(providerPrefix, "in/"))
        {
            throw new InvalidDataException("The profile selects an unsupported normalizer.");
        }

        return new(id, consumerPrefix, providerPrefix);
    }

    private static UnsupportedObservation ReadUnsupportedObservation(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "observationId",
            "kind",
            "ownerAlias",
            "ownerDocument",
            "selector",
            "evidenceLocator");
        return new(
            RequireString(item, "observationId"),
            ReadUnsupportedKind(RequireString(item, "kind")),
            RequireString(item, "ownerAlias"),
            ReadDocumentSelector(item.GetProperty("ownerDocument")),
            ReadMemberInvocation(item.GetProperty("selector")),
            RequireString(item, "evidenceLocator"));
    }

    private static ExactMemberInvocationSelector ReadMemberInvocation(JsonElement value)
    {
        JsonElement item = RequireObject(
            value,
            "form",
            "receiverSymbol",
            "memberName",
            "line",
            "occurrenceOrdinalOnLine");
        return new(
            ReadInvocationForm(RequireString(item, "form")),
            RequireString(item, "receiverSymbol"),
            RequireString(item, "memberName"),
            RequireInt32(item, "line"),
            RequireInt32(item, "occurrenceOrdinalOnLine"));
    }

    private static JsonElement RequireObject(JsonElement value, params string[] expectedProperties)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("A JSON object was required.");
        }

        string[] actual = [.. value.EnumerateObject().Select(property => property.Name)];
        if (!actual.SequenceEqual(expectedProperties, StringComparer.Ordinal))
        {
            throw new InvalidDataException("JSON properties are unknown, duplicated, or noncanonical.");
        }

        return value;
    }

    private static T[] ReadArray<T>(JsonElement value, Func<JsonElement, T> read)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("A JSON array was required.");
        }

        return [.. value.EnumerateArray().Select(read)];
    }

    private static string RequireString(
        JsonElement parent,
        string propertyName,
        bool allowEmpty = false) =>
        RequireStringValue(parent.GetProperty(propertyName), allowEmpty);

    private static string RequireStringValue(JsonElement value, bool allowEmpty = false)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("A JSON string was required.");
        }

        string result = value.GetString() ?? throw new InvalidDataException("A JSON string was required.");
        if (!allowEmpty && result.Length == 0)
        {
            throw new InvalidDataException("An empty JSON string is not allowed.");
        }

        return result;
    }

    private static int RequireInt32(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
        {
            throw new InvalidDataException("A JSON integer was required.");
        }

        return result;
    }

    private static ExactResolutionProtocol ReadProtocol(string value) => value switch
    {
        "custom-rpc" => ExactResolutionProtocol.CustomRpc,
        _ => throw new InvalidDataException("The protocol is outside the closed contract."),
    };

    private static ExactResolutionComparison ReadComparison(string value) => value switch
    {
        "ordinal" => ExactResolutionComparison.Ordinal,
        _ => throw new InvalidDataException("The comparison is outside the closed contract."),
    };

    private static ExactInvocationForm ReadInvocationForm(string value) => value switch
    {
        "simple-member-invocation" => ExactInvocationForm.SimpleMemberInvocation,
        _ => throw new InvalidDataException("The invocation form is outside the closed contract."),
    };

    private static ExactSymbolKind ReadSymbolKind(string value) => value switch
    {
        "type" => ExactSymbolKind.Type,
        _ => throw new InvalidDataException("The symbol kind is outside the closed contract."),
    };

    private static ExactUnsupportedObservationKind ReadUnsupportedKind(string value) => value switch
    {
        "file-storage" => ExactUnsupportedObservationKind.FileStorage,
        _ => throw new InvalidDataException("The observation kind is outside the closed contract."),
    };
}

public sealed record ExactResolutionRuntimeBindingEvidence(
    string Alias,
    string StableGitIdentity,
    Guid RepositoryId,
    Guid SystemId,
    Guid ScanRunId,
    Guid ScanExecutionId,
    string SnapshotProfiledRevision,
    string ScanRunProfiledRevision,
    string ScanExecutionProfiledRevision,
    string DocumentVersionProfiledRevision,
    string SourceAnchorProfiledRevision);
