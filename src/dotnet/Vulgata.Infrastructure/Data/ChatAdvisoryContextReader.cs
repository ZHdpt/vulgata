using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;

namespace Vulgata.Infrastructure.Data;

public sealed class ChatAdvisoryContextReader(VulgataDbContext context) :
    IChatAdvisoryContextReader
{
    private const int _maximumEncodedBytes = 8_192;

    private static readonly HashSet<string> _ignoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "如何", "哪些", "什么", "主要", "完整", "流程", "系统", "客户", "用户", "进行", "可以", "需要", "之后",
    };

    public async Task<ChatAdvisoryContextReadResult> ReadAsync(
        AuthorizedChatScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        Guid[] requestedRepositoryIds = scope.RepositoryIds?.ToArray() ?? [];
        if (scope.SystemId == Guid.Empty ||
            requestedRepositoryIds.Length != 2 ||
            requestedRepositoryIds.Any(id => id == Guid.Empty) ||
            requestedRepositoryIds.Distinct().Count() != 2 ||
            scope.AccessScope is null ||
            scope.AccessScope.AuthorizedRepositoryIds.Count != 2 ||
            !requestedRepositoryIds.All(scope.AccessScope.Contains) ||
            !await scope.AccessScope.HasCurrentAccessAsync(
                Array.AsReadOnly(requestedRepositoryIds),
                cancellationToken))
        {
            return ChatAdvisoryContextReadResult.NotFoundOrUnauthorized();
        }

        SystemContextRow? system = await context.Systems
            .AsNoTracking()
            .Where(value => value.Id == scope.SystemId)
            .Select(value => new SystemContextRow(value.Id, value.Context, value.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (system is null)
        {
            return ChatAdvisoryContextReadResult.NotFoundOrUnauthorized();
        }

        List<RepositoryContextRow> repositories = await context.Repositories
            .AsNoTracking()
            .Where(value => requestedRepositoryIds.Contains(value.Id))
            .Select(value => new RepositoryContextRow(
                value.Id,
                value.SystemId,
                value.Context,
                value.UpdatedAt))
            .ToListAsync(cancellationToken);

        if (repositories.Count != 2 || repositories.Any(value => value.SystemId != scope.SystemId))
        {
            return ChatAdvisoryContextReadResult.NotFoundOrUnauthorized();
        }

        List<GlobalContext> globalContexts = await context.GlobalContexts
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (globalContexts.Count > 1)
        {
            return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
        }

        var units = new List<ChatAdvisoryContextSourceUnit>(4);
        if (globalContexts.SingleOrDefault() is { } global &&
            !TryAddUnit(
                units,
                ChatAdvisoryContextSourceKind.Global,
                global.Id,
                global.UpdatedAt,
                global.Context))
        {
            return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
        }

        if (!TryAddUnit(
                units,
                ChatAdvisoryContextSourceKind.System,
                system.Id,
                system.UpdatedAt,
                system.Context))
        {
            return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
        }

        repositories.Sort(static (left, right) => CompareRfc4122(left.Id, right.Id));
        foreach (RepositoryContextRow repository in repositories)
        {
            if (!TryAddUnit(
                    units,
                    ChatAdvisoryContextSourceKind.Repository,
                    repository.Id,
                    repository.UpdatedAt,
                    repository.Context))
            {
                return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
            }
        }

        string composedText = string.Join('\n', units.Select(unit => unit.Text));
        int encodedBytes = Encoding.UTF8.GetByteCount(composedText);
        if (encodedBytes > _maximumEncodedBytes)
        {
            return ChatAdvisoryContextReadResult.InconsistentCurrentEvidence();
        }

        var snapshot = new ChatAdvisoryContextSnapshot(
            units.AsReadOnly(),
            composedText,
            ExtractTerms(composedText),
            encodedBytes,
            ComputeAuthorityFingerprint(units));
        return ChatAdvisoryContextReadResult.Succeeded(snapshot);
    }

    private static bool TryAddUnit(
        ICollection<ChatAdvisoryContextSourceUnit> units,
        ChatAdvisoryContextSourceKind kind,
        Guid sourceId,
        DateTimeOffset updatedAt,
        string? value)
    {
        string normalized = Normalize(value);
        if (ContainsDisallowedControl(normalized))
        {
            return false;
        }

        if (normalized.Length > 0)
        {
            units.Add(new ChatAdvisoryContextSourceUnit(
                kind,
                sourceId,
                updatedAt,
                normalized));
        }

        return true;
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Normalize(NormalizationForm.FormC)
            .Trim();

    private static bool ContainsDisallowedControl(string value) =>
        value.Any(character => char.IsControl(character) && character != '\n');

    private static IReadOnlyList<string> ExtractTerms(string value)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        var ascii = new StringBuilder();
        var cjk = new StringBuilder();

        void FlushAscii()
        {
            if (ascii.Length >= 2)
            {
                _ = terms.Add(ascii.ToString());
            }

            _ = ascii.Clear();
        }

        void FlushCjk()
        {
            for (int index = 0; index < cjk.Length - 1; index++)
            {
                string term = cjk.ToString(index, 2);
                if (!_ignoredTerms.Contains(term))
                {
                    _ = terms.Add(term);
                }
            }

            _ = cjk.Clear();
        }

        foreach (char character in value)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                FlushCjk();
                _ = ascii.Append(char.ToLowerInvariant(character));
            }
            else if (IsCjk(character))
            {
                FlushAscii();
                _ = cjk.Append(character);
            }
            else
            {
                FlushAscii();
                FlushCjk();
            }
        }

        FlushAscii();
        FlushCjk();
        return terms.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsCjk(char value) =>
        value is >= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff';

    private static int CompareRfc4122(Guid left, Guid right)
    {
        Span<byte> leftBytes = stackalloc byte[16];
        Span<byte> rightBytes = stackalloc byte[16];
        _ = left.TryWriteBytes(leftBytes, bigEndian: true, out _);
        _ = right.TryWriteBytes(rightBytes, bigEndian: true, out _);
        return leftBytes.SequenceCompareTo(rightBytes);
    }

    private static string ComputeAuthorityFingerprint(
        IReadOnlyCollection<ChatAdvisoryContextSourceUnit> units)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "chat-advisory-context/1");
        Append(hash, units.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (ChatAdvisoryContextSourceUnit unit in units)
        {
            Append(hash, unit.Kind.ToString());
            Append(hash, unit.SourceId.ToString("D"));
            Append(hash, unit.UpdatedAt.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            Append(hash, unit.Text);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed record SystemContextRow(Guid Id, string? Context, DateTimeOffset UpdatedAt);

    private sealed record RepositoryContextRow(
        Guid Id,
        Guid? SystemId,
        string? Context,
        DateTimeOffset UpdatedAt);
}
