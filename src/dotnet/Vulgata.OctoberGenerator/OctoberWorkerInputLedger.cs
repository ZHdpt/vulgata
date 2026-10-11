using System.Collections.Immutable;
using Vulgata.Agents.WorkerExecution;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Core.DomainServices;
using System.Text;
using System.Text.Json;

namespace Vulgata.OctoberGenerator;

public sealed record OctoberWorkerInputAdmission(DisclosureStatus Status, object? Result = null);

/// <summary>Host-created immutable input set; only its issuing ledger can authenticate completeness.</summary>
public sealed class OctoberWorkerInputSnapshot
{
    internal OctoberWorkerInputSnapshot(ImmutableArray<Contributor> contributors,
        ImmutableArray<VerifiedKnowledgeSource> verifiedSources)
    {
        Contributors = contributors;
        VerifiedSources = verifiedSources;
    }

    public ImmutableArray<Contributor> Contributors { get; }
    public ImmutableArray<VerifiedKnowledgeSource> VerifiedSources { get; }
}

/// <summary>One attempt's host-observed inputs, including uncited support and metadata.</summary>
public sealed class OctoberWorkerInputLedger : IOctoberVerifiedFileFrame, IOctoberKnowledgeSourceVerifier
{
    private readonly IOctoberVerifiedFileFrame _primary;
    private readonly OctoberFileWorkerRequest _request;
    private readonly PreparedOrigin _origin;
    private readonly string _text;
    private readonly string _identity;
    private readonly long _byteLength;
    private readonly int _lineCount;
    private readonly Func<string, CancellationToken, Task<IOctoberVerifiedFileFrame?>> _acquire;
    private readonly Func<ImmutableArray<Contributor>, CancellationToken, Task<DisclosureStatus>> _authorize;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, SourceEntry> _sources = new(StringComparer.Ordinal);
    private readonly List<Contributor> _contributors = [];
    private readonly List<MetadataEntry> _metadata = [];
    private OctoberWorkerInputSnapshot? _snapshot;
    private bool _faulted;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public OctoberWorkerInputLedger(OctoberFileWorkerRequest originalRequest,
        Func<string, CancellationToken, Task<IOctoberVerifiedFileFrame?>> acquireSameRootFile,
        Func<ImmutableArray<Contributor>, CancellationToken, Task<DisclosureStatus>> checkLiveInputs)
    {
        ArgumentNullException.ThrowIfNull(originalRequest);
        ArgumentNullException.ThrowIfNull(acquireSameRootFile);
        ArgumentNullException.ThrowIfNull(checkLiveInputs);
        _primary = originalRequest.Source ?? throw new ArgumentException("A trusted primary frame is required.");
        if (_primary is OctoberWorkerInputLedger) throw new ArgumentException("The original primary frame is required.");
        _request = originalRequest;
        _origin = _primary.Origin;
        _text = _primary.Text;
        _identity = _primary.FileIdentity;
        _byteLength = _primary.ByteLength;
        _lineCount = _primary.LineCount;
        _acquire = acquireSameRootFile;
        _authorize = checkLiveInputs;
        _sources.Add(_origin.RelativePath, new(_primary, _origin, _text, _byteLength, _lineCount, _identity));
        if (originalRequest.Contributors.IsDefaultOrEmpty || originalRequest.Advisory.IsDefault)
            _faulted = true;
        else
        {
            foreach (Contributor contributor in originalRequest.Contributors)
            {
                if (contributor.Kind == ContributorKind.Source)
                {
                    if (!OctoberKnowledgeInputPolicy.IsValidSourceContributor(contributor) ||
                        contributor.RepositoryId != _origin.RepositoryId || contributor.RootBindingIdentity != _origin.RootBindingIdentity ||
                        contributor.ContentDigest != _origin.SourceDigest || contributor.SourceOrigin is { } explicitOrigin && explicitOrigin != _origin)
                        _faulted = true;
                    if (!_contributors.Any(item => item.Kind == ContributorKind.Source))
                        _contributors.Add(contributor with { SourceOrigin = null });
                }
                else if (contributor.Kind is ContributorKind.GlobalAdvisory or ContributorKind.SystemAdvisory or ContributorKind.RepositoryAdvisory)
                {
                    if (contributor.SourceOrigin is not null || contributor.MetadataOrigin is not null ||
                        !originalRequest.Advisory.Any(item => item.Contributor == contributor &&
                            OctoberKnowledgePolicy.Hash(item.Text) == contributor.ContentDigest)) _faulted = true;
                    _contributors.Add(contributor);
                }
                else _faulted = true;
            }
            if (!_contributors.Any(item => item.Kind == ContributorKind.Source) ||
                originalRequest.Advisory.Any(item => !_contributors.Contains(item.Contributor))) _faulted = true;
        }
    }

    public PreparedOrigin Origin => _origin;
    public string Text => _text;
    public long ByteLength => _byteLength;
    public int LineCount => _lineCount;
    public string FileIdentity => _identity;

    public async Task<bool> VerifyCurrentAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await VerifyCoreAsync(_contributors.ToImmutableArray(), _sources.Values, cancellationToken) == DisclosureStatus.Allowed; }
        catch (Exception) { _faulted = true; return false; }
        finally { _gate.Release(); }
    }

    public async Task<OctoberWorkerInputAdmission> AdmitToolResultAsync(SourceMetadataOperation operation,
        string normalizedScopePath, string safeRequestJson, object typedResult,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot is not null || _faulted) return new(DisclosureStatus.Denied);
            DisclosureStatus current = await VerifyCoreAsync(_contributors.ToImmutableArray(), _sources.Values, cancellationToken);
            if (current != DisclosureStatus.Allowed) return new(current);
            if (!Enum.IsDefined(operation) || safeRequestJson is null ||
                Utf8.GetByteCount(safeRequestJson) > OctoberKnowledgeInputLimits.MaximumSafeRequestUtf8Bytes)
                return Denied();
            using JsonDocument request = JsonDocument.Parse(safeRequestJson, new JsonDocumentOptions { MaxDepth = 16 });
            var staged = new Dictionary<string, SourceEntry>(_sources, StringComparer.Ordinal);
            var contributors = new List<Contributor>(_contributors);
            object projection;
            object admitted;
            if (operation is not (SourceMetadataOperation.List or SourceMetadataOperation.ReadTextRange))
                (admitted, projection) = await PrepareOtherAsync(operation, normalizedScopePath, request.RootElement,
                    typedResult, staged, contributors, cancellationToken);
            else
            {
            if (typedResult is not PreparedSourceResult original || original.RepositoryId != _origin.RepositoryId ||
                original.Operation != (operation == SourceMetadataOperation.List ? PreparedSourceOperation.List : PreparedSourceOperation.ReadTextRange) ||
                !Enum.IsDefined(original.Outcome) ||
                original.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput or PreparedSourceOutcome.Cancelled or PreparedSourceOutcome.DeadlineExceeded)
                return Denied();
            PreparedSourceResult result = original with
            {
                Entries = original.Entries.ToArray(), Matches = original.Matches.ToArray(), Limitations = original.Limitations.ToArray()
            };
            if (operation == SourceMetadataOperation.List)
            {
                RequestFields(request.RootElement, ["relativeDirectory", "pageSize"], ["continuation"]);
                string directory = request.RootElement.GetProperty("relativeDirectory").GetString()!;
                if (!SafePath(directory, true) || normalizedScopePath != (directory.Length == 0 ? "." : directory) ||
                    request.RootElement.GetProperty("pageSize").GetInt32() is < 1 or > PreparedSourceCallLimits.MaximumListEntries ||
                    request.RootElement.TryGetProperty("continuation", out JsonElement continuation) && continuation.ValueKind != JsonValueKind.Null ||
                    result.Text is not null || result.ReadLocation is not null || result.Matches.Count != 0)
                    return Denied();
                foreach (PreparedSourceEntry entry in result.Entries)
                    if (!LocationValid(entry.Location, directory) || !DirectChild(directory, entry.Location.RelativePath) ||
                        !Enum.IsDefined(entry.Kind) || entry.ObservedByteLength is < 0) return Denied();
                if (!LimitationsValid(result.Limitations, directory)) return Denied();
                projection = result;
            }
            else
            {
                RequestFields(request.RootElement, ["relativePath", "byteOffset", "byteCount"], []);
                string path = request.RootElement.GetProperty("relativePath").GetString()!;
                long offset = request.RootElement.GetProperty("byteOffset").GetInt64();
                int count = request.RootElement.GetProperty("byteCount").GetInt32();
                if (!SafePath(path, false) || normalizedScopePath != path || offset < 0 || count is < 1 or > PreparedSourceCallLimits.MaximumReadBytes ||
                    result.Entries.Count != 0 || result.Matches.Count != 0 || !LimitationsValid(result.Limitations, path)) return Denied();
                if (result.Text is not null || result.ReadLocation is not null)
                {
                    if (result.Text is null || result.ReadLocation is not { } location || location.RepositoryId != _origin.RepositoryId ||
                        location.RelativePath != path || location.ByteStart != offset || location.ByteEndExclusive < offset ||
                        location.ByteEndExclusive - offset > count || Utf8.GetByteCount(result.Text) > count) return Denied();
                    SourceEntry source = await AcquireAsync(path, staged, contributors, cancellationToken);
                    byte[] full = Utf8.GetBytes(source.Text);
                    byte[] returned = Utf8.GetBytes(result.Text);
                    if (result.ObservedFileByteLength != full.LongLength || location.ByteEndExclusive > full.LongLength ||
                        location.ByteEndExclusive != offset + returned.LongLength ||
                        !full.AsSpan(checked((int)offset), returned.Length).SequenceEqual(returned)) return Denied();
                }
                else if (result.Outcome is PreparedSourceOutcome.Complete or PreparedSourceOutcome.CompleteEmpty)
                    return Denied();
                projection = result with { Text = null };
            }
            admitted = result;
            }
            string requestJson = OctoberKnowledgePolicy.CanonicalizeJson(safeRequestJson);
            string projectionJson = OctoberKnowledgePolicy.SerializeCanonical(projection);
            var origin = new SourceMetadataOrigin(_origin.RepositoryId, _origin.RootBindingIdentity, _origin.PreparedVersion,
                operation, normalizedScopePath, string.Empty);
            string digest = OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(origin, requestJson, projectionJson);
            origin = origin with { ResultDigest = digest };
            if (_metadata.Count >= OctoberKnowledgeInputLimits.MaximumMetadataObservations) return Denied();
            var metadataContributor = new Contributor(ContributorKind.SourceMetadata, _origin.RepositoryId, null,
                _origin.RootBindingIdentity, null, digest) { MetadataOrigin = origin };
            if (!contributors.Contains(metadataContributor)) contributors.Add(metadataContributor);
            current = await VerifyCoreAsync(contributors.ToImmutableArray(), staged.Values, cancellationToken);
            if (current != DisclosureStatus.Allowed) return new(current);
            _sources.Clear();
            foreach (var pair in staged) _sources.Add(pair.Key, pair.Value);
            _contributors.Clear();
            _contributors.AddRange(contributors);
            _metadata.Add(new(origin, requestJson, projectionJson));
            return new(DisclosureStatus.Allowed, admitted);
        }
        catch (OperationCanceledException) { _faulted = true; return new(DisclosureStatus.Denied); }
        catch (Exception) { _faulted = true; return new(DisclosureStatus.Denied); }
        finally { _gate.Release(); }
    }

    public async Task<OctoberWorkerInputSnapshot?> FreezeForCompletionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (await VerifyCoreAsync(_contributors.ToImmutableArray(), _sources.Values, cancellationToken) != DisclosureStatus.Allowed) return null;
            return _snapshot ??= new(_contributors.ToImmutableArray(), _sources.Values.Select(source =>
                new VerifiedKnowledgeSource(source.Origin, source.LineCount)).ToImmutableArray());
        }
        catch (Exception) { _faulted = true; return null; }
        finally { _gate.Release(); }
    }

    public async Task<bool> VerifySnapshotAsync(OctoberWorkerInputSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return snapshot is not null && ReferenceEquals(snapshot, _snapshot) &&
                await VerifyCoreAsync(snapshot.Contributors, _sources.Values, cancellationToken) == DisclosureStatus.Allowed; }
        catch (Exception) { _faulted = true; return false; }
        finally { _gate.Release(); }
    }

    public async Task<VerifiedKnowledgeSource?> VerifyAsync(KnowledgeBinding binding, PreparedOrigin origin,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (binding != _request.Binding || origin is null || !_sources.TryGetValue(origin.RelativePath, out SourceEntry? source) ||
                source.Origin != origin || await VerifyCoreAsync(_contributors.ToImmutableArray(), _sources.Values, cancellationToken) != DisclosureStatus.Allowed)
                return null;
            return new(source.Origin, source.LineCount);
        }
        catch (Exception) { _faulted = true; return null; }
        finally { _gate.Release(); }
    }

    private async Task<SourceEntry> AcquireAsync(string path, Dictionary<string, SourceEntry> staged,
        List<Contributor> contributors, CancellationToken token)
    {
        if (staged.TryGetValue(path, out SourceEntry? existing)) return existing;
        if (staged.Count >= OctoberKnowledgeInputLimits.MaximumContentOriginsIncludingPrimary)
            throw new InvalidDataException("The source input bound was exhausted.");
        IOctoberVerifiedFileFrame frame = await _acquire(path, token) ?? throw new InvalidDataException("Contained supporting input unavailable.");
        PreparedOrigin origin = frame.Origin;
        if (origin.RepositoryId != _origin.RepositoryId || origin.RootBindingIdentity != _origin.RootBindingIdentity ||
            origin.PreparedVersion != _origin.PreparedVersion || origin.RelativePath != path || !SafePath(path, false))
            throw new InvalidDataException("Supporting input binding mismatch.");
        var entry = new SourceEntry(frame, origin, frame.Text, frame.ByteLength, frame.LineCount, frame.FileIdentity);
        if (!await VerifySourceAsync(entry, token)) throw new InvalidDataException("Supporting input is not current.");
        staged.Add(path, entry);
        contributors.Add(new Contributor(ContributorKind.Source, origin.RepositoryId, null, origin.RootBindingIdentity, null, origin.SourceDigest)
        { SourceOrigin = origin });
        return entry;
    }

    private async Task<DisclosureStatus> VerifyCoreAsync(ImmutableArray<Contributor> contributors,
        IEnumerable<SourceEntry> sources, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_faulted) return DisclosureStatus.Denied;
        foreach (SourceEntry source in sources)
            if (!await VerifySourceAsync(source, token)) { _faulted = true; return DisclosureStatus.Denied; }
        foreach (MetadataEntry metadata in _metadata)
            if (OctoberKnowledgeInputPolicy.ComputeMetadataResultDigest(metadata.Origin, metadata.RequestJson, metadata.ProjectionJson) != metadata.Origin.ResultDigest)
            { _faulted = true; return DisclosureStatus.Denied; }
        DisclosureStatus allowed = await _authorize(contributors, token);
        token.ThrowIfCancellationRequested();
        if (allowed != DisclosureStatus.Allowed) _faulted = true;
        else
            foreach (SourceEntry source in sources)
                if (!await VerifySourceAsync(source, token)) { _faulted = true; return DisclosureStatus.Denied; }
        return allowed;
    }

    private async Task<(object Admitted, object Projection)> PrepareOtherAsync(SourceMetadataOperation operation,
        string scope, JsonElement request, object result, Dictionary<string, SourceEntry> staged,
        List<Contributor> contributors, CancellationToken token)
    {
        if (operation == SourceMetadataOperation.SearchText)
        {
            RequestFields(request, ["relativeDirectory", "literal", "pageSize"], ["continuation"]);
            string directory = request.GetProperty("relativeDirectory").GetString()!;
            string literal = request.GetProperty("literal").GetString()!;
            if (!SafePath(directory, true) || scope != (directory.Length == 0 ? "." : directory) ||
                string.IsNullOrEmpty(literal) || literal.Contains('\r') || literal.Contains('\n') ||
                Utf8.GetByteCount(literal) > PreparedSourceCallLimits.MaximumSearchLiteralUtf8Bytes ||
                request.GetProperty("pageSize").GetInt32() is < 1 or > PreparedSourceCallLimits.MaximumSearchMatches ||
                request.TryGetProperty("continuation", out JsonElement cursor) && cursor.ValueKind != JsonValueKind.Null ||
                result is not PreparedSourceResult search || search.RepositoryId != _origin.RepositoryId ||
                search.Operation != PreparedSourceOperation.SearchText || !Enum.IsDefined(search.Outcome) ||
                search.Outcome is PreparedSourceOutcome.Denied or PreparedSourceOutcome.AuthorityUnavailable or PreparedSourceOutcome.ChangedInput or PreparedSourceOutcome.Cancelled or PreparedSourceOutcome.DeadlineExceeded ||
                search.Text is not null || search.ReadLocation is not null || search.Entries.Count != 0 ||
                !LimitationsValid(search.Limitations, directory)) throw InvalidInput();
            search = search with { Matches = search.Matches.ToArray(), Limitations = search.Limitations.ToArray() };
            foreach (PreparedSourceMatch match in search.Matches)
            {
                var location = match.Location;
                if (!LocationValid(new(location.RepositoryId, location.RelativePath), directory) ||
                    location.ByteStart < 0 || location.ByteLength != Utf8.GetByteCount(literal) || location.Line < 1 ||
                    location.UnicodeScalarColumn < 0) throw InvalidInput();
                if (match.Excerpt is null) continue;
                SourceEntry source = await AcquireAsync(location.RelativePath, staged, contributors, token);
                byte[] bytes = Utf8.GetBytes(source.Text);
                byte[] query = Utf8.GetBytes(literal);
                if (location.ByteStart > bytes.LongLength - query.Length ||
                    !bytes.AsSpan((int)location.ByteStart, query.Length).SequenceEqual(query) ||
                    location.Line > source.LineCount || !source.Text.Contains(match.Excerpt, StringComparison.Ordinal)) throw InvalidInput();
            }
            return (search, search with { Matches = search.Matches.Select(match => match with { Excerpt = null }).ToArray() });
        }
        if (operation is SourceMetadataOperation.JavaDefinition or SourceMetadataOperation.JavaReferences)
        {
            RequestFields(request, ["relativePath", "line", "character", "maximumLocations"], []);
            string path = request.GetProperty("relativePath").GetString()!;
            int maximum = request.GetProperty("maximumLocations").GetInt32();
            if (!SafePath(path, false) || scope != path || request.GetProperty("line").GetInt32() < 1 ||
                request.GetProperty("character").GetInt32() < 1 || maximum is < 1 or > 100 ||
                result is not PreparedJavaNavigationResult java || java.RepositoryId != _origin.RepositoryId ||
                java.Operation != (operation == SourceMetadataOperation.JavaDefinition ? PreparedJavaNavigationOperation.Definition : PreparedJavaNavigationOperation.References) ||
                !Enum.IsDefined(java.Outcome) || java.Outcome is PreparedJavaNavigationOutcome.Denied or PreparedJavaNavigationOutcome.AuthorityUnavailable or
                    PreparedJavaNavigationOutcome.ChangedInput or PreparedJavaNavigationOutcome.Cancelled or PreparedJavaNavigationOutcome.DeadlineExceeded ||
                java.Locations.Count > maximum) throw InvalidInput();
            java = java with { Locations = java.Locations.ToArray(), Limitations = java.Limitations.ToArray() };
            foreach (PreparedJavaLocation location in java.Locations)
                if (!LocationValid(location.Source, "") || location.StartLine < 1 || location.EndLine < location.StartLine ||
                    location.StartCharacter < 0 || location.EndCharacter < 0 ||
                    location.StartLine == location.EndLine && location.EndCharacter < location.StartCharacter) throw InvalidInput();
            foreach (PreparedJavaLimitation limitation in java.Limitations)
                if (string.IsNullOrWhiteSpace(limitation.Code) || limitation.Source is { } location && !LocationValid(location, "")) throw InvalidInput();
            return (java, java);
        }
        if (operation == SourceMetadataOperation.CodeGraphExplore)
        {
            RequestFields(request, ["query"], ["maxFiles"]);
            string query = request.GetProperty("query").GetString()!;
            int maximum = request.TryGetProperty("maxFiles", out JsonElement supplied) ? supplied.GetInt32() : 4;
            if (scope != "." || string.IsNullOrWhiteSpace(query) || Utf8.GetByteCount(query) > 512 || maximum is < 1 or > 12 ||
                result is not CodeGraphToolInvocationResult graph || graph.RepositoryId != _origin.RepositoryId ||
                !Enum.IsDefined(graph.Status) || graph.Status is CodeGraphToolInvocationStatus.Denied or CodeGraphToolInvocationStatus.AuthorityUnavailable or
                    CodeGraphToolInvocationStatus.Cancelled or CodeGraphToolInvocationStatus.Expired ||
                !Enum.IsDefined(graph.IssueCode) || graph.Limitations.Any(code => !Enum.IsDefined(code))) throw InvalidInput();
            // No trusted producer currently associates free-form structural Text with
            // exact source. Withhold it honestly; never infer provenance from that text.
            graph = graph with
            {
                Text = null, Locations = graph.Locations.ToArray(), Excerpts = graph.Excerpts.ToArray(),
                Limitations = graph.Text is null ? graph.Limitations.ToArray() : graph.Limitations.Append(CodeGraphToolIssueCode.UncontainedSource).Distinct().ToArray()
            };
            if (graph.Locations.Concat(graph.Excerpts.Select(excerpt => excerpt.Location)).Distinct().Count() > maximum)
                throw InvalidInput();
            foreach (PreparedSourceLocation location in graph.Locations.Concat(graph.Excerpts.Select(excerpt => excerpt.Location)))
                if (!LocationValid(location, "")) throw InvalidInput();
            foreach (CodeGraphSourceExcerpt excerpt in graph.Excerpts)
            {
                if (string.IsNullOrEmpty(excerpt.Text)) throw InvalidInput();
                SourceEntry source = await AcquireAsync(excerpt.Location.RelativePath, staged, contributors, token);
                if (!source.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Contains(
                    excerpt.Text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal) ||
                    excerpt.LineStart is { } start && (start < 1 || start > source.LineCount) ||
                    excerpt.LineEnd is { } end && (end < 1 || end > source.LineCount) ||
                    excerpt.LineStart is { } first && excerpt.LineEnd is { } last && last < first) throw InvalidInput();
            }
            return (graph, graph with { Excerpts = graph.Excerpts.Select(excerpt => excerpt with { Text = string.Empty }).ToArray() });
        }
        throw InvalidInput();
    }

    private static InvalidDataException InvalidInput() => new("The typed tool result or request is not admitted.");

    private static async Task<bool> VerifySourceAsync(SourceEntry entry, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Pinned() || string.IsNullOrWhiteSpace(entry.FileIdentity) || entry.LineCount < 0 ||
            Utf8.GetByteCount(entry.Text) != entry.ByteLength || OctoberKnowledgePolicy.Hash(entry.Text) != entry.Origin.SourceDigest)
            return false;
        bool current = await entry.Frame.VerifyCurrentAsync(token);
        token.ThrowIfCancellationRequested();
        return current && Pinned();

        bool Pinned() => entry.Frame.Origin == entry.Origin && entry.Frame.Text == entry.Text &&
            entry.Frame.ByteLength == entry.ByteLength && entry.Frame.LineCount == entry.LineCount &&
            entry.Frame.FileIdentity == entry.FileIdentity;
    }

    private OctoberWorkerInputAdmission Denied() { _faulted = true; return new(DisclosureStatus.Denied); }

    private bool LocationValid(PreparedSourceLocation location, string scope)
        => location.RepositoryId == _origin.RepositoryId && SafePath(location.RelativePath, false) &&
            (scope.Length == 0 || location.RelativePath == scope || location.RelativePath.StartsWith(scope + "/", StringComparison.Ordinal));

    private bool LimitationsValid(IReadOnlyList<PreparedSourceLimitation> limitations, string scope)
        => limitations.All(item => item is not null && Enum.IsDefined(item.Reason) && LocationValid(item.Location, scope));

    private static bool DirectChild(string directory, string path)
    {
        int slash = path.LastIndexOf('/');
        return (slash < 0 ? string.Empty : path[..slash]) == directory;
    }

    private static bool SafePath(string? value, bool root)
        => value is not null && value.Length <= 2048 && !value.Contains('\\') && !value.Contains(':') &&
            !value.Any(char.IsControl) && (value.Length == 0 ? root : value.Split('/').All(part => part is not ("" or "." or "..") && part.Trim() == part));

    private static void RequestFields(JsonElement value, string[] required, string[] optional)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a safe request object.");
        foreach (JsonProperty property in value.EnumerateObject())
            if ((!required.Contains(property.Name) && !optional.Contains(property.Name)) || !seen.Add(property.Name))
                throw new InvalidDataException("Invalid safe request field.");
        if (required.Any(name => !seen.Contains(name))) throw new InvalidDataException("Missing safe request field.");
    }

    private sealed record SourceEntry(IOctoberVerifiedFileFrame Frame, PreparedOrigin Origin, string Text,
        long ByteLength, int LineCount, string FileIdentity);
    private sealed record MetadataEntry(SourceMetadataOrigin Origin, string RequestJson, string ProjectionJson);
}
