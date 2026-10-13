using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Vulgata.Core.DomainServices.OctoberKnowledge;

namespace Vulgata.Agents.OctoberBusiness;

public enum BusinessSourceStatus { Complete, Limited, Unavailable, Denied, Changed, BudgetExceeded }
/// <param name="ExaminedBytes">Conservative charged byte upper bound, including verification passes and failed reads; not measured I/O.</param>
public sealed record BusinessSourcePage([property: JsonConverter(typeof(JsonStringEnumConverter<BusinessSourceStatus>))] BusinessSourceStatus Status, SourceAnchor Source, int Offset,
    int? NextOffset, string? Text, long ExaminedBytes);

/// <summary>Trusted host reader; the agent never receives a filesystem capability.</summary>
public interface IOctoberBusinessSourceReader
{
    Task<BusinessSourcePage> ReadAsync(SourceAnchor source, int byteOffset, int maximumBytes,
        long maximumExaminedBytes, CancellationToken cancellationToken);
}

public sealed record BusinessSourceDiagnostic(string SourceReference, BusinessSourcePage Page);

internal sealed class OctoberBusinessSourceRetrieval
{
    internal const int MaximumCalls = 12, MaximumRounds = 6, MaximumRequests = 16;
    internal const int PageBytes = 32 * 1024, ReturnedBytes = 192 * 1024;
    internal const long ExaminedBytes = 16 * 1024 * 1024;
    private readonly IOctoberBusinessSourceReader _reader;
    private readonly Dictionary<string, SourceAnchor> _anchors = new(StringComparer.Ordinal);
    private readonly List<BusinessSourceDiagnostic> _pages = [];
    private int _calls, _returned;
    private long _examined;
    internal int Rounds { get; set; }
    internal int Requests { get; set; }
    internal ImmutableArray<BusinessSourceDiagnostic> Pages => _pages.ToImmutableArray();
    internal AIFunction Tool { get; } = AIFunctionFactory.Create(
        (Func<string, int, string>)((sourceReference, byteOffset) => throw new InvalidOperationException("Host dispatch required.")),
        "read_referenced_source", "Read a bounded page of an exact source span referenced by this flow's CL. Use only a supplied sourceReference handle; byteOffset starts at zero.");

    internal OctoberBusinessSourceRetrieval(IOctoberBusinessSourceReader reader, FlowPlanningInput input,
        Guid groupId, FlowPlanningLimits? limits)
    {
        _reader = reader;
        var group = OctoberFlowPlanner.Plan(input, limits).Groups.Single(g => g.GroupVersionId == groupId);
        var anchors = input.Corpus.Documents.Where(d => group.Members.Contains(d.Version))
            .SelectMany(d => d.Sections.SelectMany(s => s.Claims).SelectMany(c => c.Evidence)
                .Where(e => e.Source is not null).Select(e => (Document: d.Version, Anchor: e.Source!))).Distinct().ToArray();
        foreach (var entry in anchors)
        {
            var anchor = entry.Anchor;
            bool primary = input.Units.Any(u => u.Document == entry.Document && u.PrimaryOrigin == anchor.Origin);
            if (anchor.StartLine < 1 || anchor.EndLine < anchor.StartLine ||
                !group.Dependencies.Contributors.Any(c => c.Kind == ContributorKind.Source &&
                    c.RepositoryId == anchor.Origin.RepositoryId && c.RootBindingIdentity == anchor.Origin.RootBindingIdentity &&
                    c.ContentDigest == anchor.Origin.SourceDigest && (c.SourceOrigin == anchor.Origin || c.SourceOrigin is null && primary)))
                throw new ArgumentException("Source anchor is outside the complete contributor ledger.");
            if (!_anchors.ContainsValue(anchor)) _anchors.Add(Guid.NewGuid().ToString("N"), anchor);
        }
    }

    internal string AddEvidence(string packet)
    {
        var data = JsonNode.Parse(packet)!.AsObject();
        data["SourceReferences"] = JsonSerializer.SerializeToNode(_anchors.Select(x => new { SourceReference = x.Key, Source = x.Value }));
        data["RetrievedSource"] = JsonSerializer.SerializeToNode(_pages);
        return data.ToJsonString();
    }

    internal async Task<BusinessSourceDiagnostic> ReadAsync(FunctionCallContent call, CancellationToken token)
    {
        if (++_calls > MaximumCalls || call.Name != "read_referenced_source" || call.Arguments is null ||
            call.Arguments.Count != 2 || !call.Arguments.TryGetValue("sourceReference", out var handleValue) ||
            !call.Arguments.TryGetValue("byteOffset", out var offsetValue))
            throw new InvalidOperationException("Invalid or exhausted source tool call.");
        var handle = JsonSerializer.SerializeToElement(handleValue).GetString();
        int offset = JsonSerializer.SerializeToElement(offsetValue).GetInt32();
        if (handle is null || !_anchors.TryGetValue(handle, out var anchor) || offset < 0)
            throw new InvalidOperationException("Source handle is not allowed.");
        int remaining = Math.Min(PageBytes, ReturnedBytes - _returned);
        if (remaining <= 0 || _examined >= ExaminedBytes) throw new InvalidOperationException("Source budget exhausted.");
        var page = await _reader.ReadAsync(anchor, offset, remaining, ExaminedBytes - _examined, token);
        token.ThrowIfCancellationRequested();
        int returned = Encoding.UTF8.GetByteCount(page.Text ?? "");
        if (page.Source != anchor || page.Offset != offset || !Enum.IsDefined(page.Status) ||
            page.ExaminedBytes < 0 || page.ExaminedBytes > ExaminedBytes - _examined || returned > remaining ||
            page.Status is not (BusinessSourceStatus.Complete or BusinessSourceStatus.Limited) && page.Text is not null ||
            page.Status is BusinessSourceStatus.Complete or BusinessSourceStatus.Limited && page.Text is null ||
            page.Status == BusinessSourceStatus.Complete && page.NextOffset is not null ||
            page.Status == BusinessSourceStatus.Limited && page.NextOffset is null ||
            page.NextOffset is { } next && (next <= offset || next != offset + returned))
            throw new InvalidOperationException("Invalid source page binding.");
        _examined += page.ExaminedBytes; _returned += returned;
        var result = new BusinessSourceDiagnostic(handle, page);
        _pages.Add(result);
        return result;
    }
}
