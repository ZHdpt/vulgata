using System.Text;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using Vulgata.Core.DomainServices;
using Vulgata.Core.Entities;
using Vulgata.Infrastructure.Data;

namespace Vulgata.Infrastructure.CrossRepository;

public sealed class ExactCandidateExtractors(VulgataDbContext context)
{
    internal const int MaximumFileBytes = 1_048_576;
    internal const int MaximumAggregateBytes = 4_194_304;
    internal const int MaximumCandidates = 2_048;

    public async Task<IReadOnlyList<ConsumerDispatchCandidate>> ExtractConsumersAsync(
        ICommittedRepositorySnapshotLease snapshot,
        ResolutionOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(occurrence);
        await snapshot.VerifyIdentityAsync(cancellationToken);

        var candidates = new List<ConsumerDispatchCandidate>();
        long aggregateBytes = 0;
        foreach (string repositoryRelativePath in occurrence.Consumer.Files)
        {
            string fullPath = ResolveListedFile(snapshot.RootPath, repositoryRelativePath);
            byte[] bytes = await ReadBoundedAsync(fullPath, cancellationToken);
            aggregateBytes = checked(aggregateBytes + bytes.Length);
            if (aggregateBytes > MaximumAggregateBytes)
            {
                throw new InvalidDataException("The configured source input exceeds its aggregate bound.");
            }

            string source = DecodeUtf8(bytes);
            string? rawIdentifier = FindConfiguredLiteral(
                source,
                occurrence.Consumer.Invocation);
            if (rawIdentifier is null)
            {
                continue;
            }

            ExactResolutionOwner? owner = await MapCurrentOwnerAsync(
                snapshot,
                repositoryRelativePath,
                occurrence.Consumer.Invocation.Line,
                symbol: null,
                cancellationToken);
            if (owner is null)
            {
                continue;
            }

            string locator =
                $"{repositoryRelativePath}#line={occurrence.Consumer.Invocation.Line};" +
                $"ordinal={occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine};" +
                "rule=exact-java-direct-literal-v1";
            candidates.Add(new ConsumerDispatchCandidate(
                occurrence.OccurrenceId,
                owner,
                occurrence.Consumer.Invocation.Form,
                occurrence.Consumer.Invocation.ReceiverSymbol,
                occurrence.Consumer.Invocation.MemberName,
                occurrence.Consumer.Invocation.EndpointArgumentIndex,
                occurrence.Consumer.Invocation.Line,
                occurrence.Consumer.Invocation.OccurrenceOrdinalOnLine,
                rawIdentifier,
                rawIdentifier,
                locator));
            if (candidates.Count > MaximumCandidates)
            {
                throw new InvalidDataException("The configured source input exceeds its candidate bound.");
            }
        }

        return candidates.AsReadOnly();
    }

    public async Task<IReadOnlyList<ProviderEndpointCandidate>> ExtractProvidersAsync(
        ICommittedRepositorySnapshotLease snapshot,
        ResolutionOccurrence occurrence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(occurrence);
        await snapshot.VerifyIdentityAsync(cancellationToken);

        var candidates = new List<ProviderEndpointCandidate>();
        long aggregateBytes = 0;
        foreach (string repositoryRelativePath in occurrence.Provider.Files)
        {
            if (!IsBeneathRoot(repositoryRelativePath, occurrence.Provider.Root))
            {
                throw new InvalidDataException("A configured provider file is outside its closed root.");
            }

            string fullPath = ResolveListedFile(snapshot.RootPath, repositoryRelativePath);
            byte[] bytes = await ReadBoundedAsync(fullPath, cancellationToken);
            aggregateBytes = checked(aggregateBytes + bytes.Length);
            if (aggregateBytes > MaximumAggregateBytes)
            {
                throw new InvalidDataException("The configured XML input exceeds its aggregate bound.");
            }

            string? rawIdentifier = await ReadProviderIdentifierAsync(
                bytes,
                occurrence.Provider.Declaration,
                cancellationToken);
            if (rawIdentifier is null)
            {
                continue;
            }

            ExactResolutionOwner? owner = await MapCurrentOwnerAsync(
                snapshot,
                occurrence.Provider.ImplementationDocument.RepositoryRelativePath,
                line: null,
                occurrence.Provider.ImplementationDocument.Symbol,
                cancellationToken);
            if (owner is null)
            {
                continue;
            }

            string locator =
                $"{repositoryRelativePath}#line={occurrence.Provider.Declaration.Line};" +
                $"ordinal={occurrence.Provider.Declaration.OccurrenceOrdinalOnLine};" +
                "rule=exact-xml-root-attribute-v1";
            candidates.Add(new ProviderEndpointCandidate(
                occurrence.OccurrenceId,
                owner,
                rawIdentifier,
                rawIdentifier,
                locator));
            if (candidates.Count > MaximumCandidates)
            {
                throw new InvalidDataException("The configured XML input exceeds its candidate bound.");
            }
        }

        return candidates.AsReadOnly();
    }

    private async Task<ExactResolutionOwner?> MapCurrentOwnerAsync(
        ICommittedRepositorySnapshotLease snapshot,
        string repositoryRelativePath,
        int? line,
        string? symbol,
        CancellationToken cancellationToken)
    {
        ExactResolutionOwner[] owners = await (
            from logicalDocument in context.LogicalDocuments.AsNoTracking()
            join documentVersion in context.DocumentVersions.AsNoTracking()
                on logicalDocument.CurrentVersionId equals documentVersion.Id
            join sourceAnchor in context.SourceAnchors.AsNoTracking()
                on documentVersion.Id equals sourceAnchor.DocumentVersionId
            join codeUnit in context.CodeUnits.AsNoTracking()
                on sourceAnchor.CodeUnitId equals codeUnit.Id
            where logicalDocument.DocumentType == DocumentType.CodeLogic &&
                documentVersion.RepositoryId == snapshot.RepositoryId &&
                documentVersion.ScanRunId == snapshot.ScanRunId &&
                documentVersion.ProfiledRevision == snapshot.ProfiledRevision &&
                sourceAnchor.RepositoryId == snapshot.RepositoryId &&
                sourceAnchor.ScanRunId == snapshot.ScanRunId &&
                sourceAnchor.ProfiledRevision == snapshot.ProfiledRevision &&
                sourceAnchor.RepositoryRelativePath == repositoryRelativePath &&
                (line == null ||
                    sourceAnchor.LineStart <= line && sourceAnchor.LineEnd >= line) &&
                codeUnit.RepositoryId == snapshot.RepositoryId &&
                codeUnit.ScanRunId == snapshot.ScanRunId &&
                codeUnit.FilePath == repositoryRelativePath &&
                (symbol == null || codeUnit.SymbolName == symbol)
            select new ExactResolutionOwner(
                documentVersion.RepositoryId,
                documentVersion.ScanRunId,
                documentVersion.Id,
                sourceAnchor.Id,
                documentVersion.ProfiledRevision))
            .Take(2)
            .ToArrayAsync(cancellationToken);
        return owners.Length == 1 ? owners[0] : null;
    }

    private static async Task<string?> ReadProviderIdentifierAsync(
        byte[] bytes,
        ExactProviderDeclarationSelector selector,
        CancellationToken cancellationToken)
    {
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumFileBytes,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using XmlReader reader = XmlReader.Create(stream, settings);
            int ordinalOnLine = 0;
            string? selected = null;
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.Depth != 0)
                {
                    continue;
                }

                if (!StringComparer.Ordinal.Equals(reader.LocalName, selector.ElementLocalName) ||
                    reader.NamespaceURI.Length != 0 ||
                    reader.AttributeCount != 1 ||
                    !reader.MoveToFirstAttribute() ||
                    !StringComparer.Ordinal.Equals(
                        reader.LocalName,
                        selector.EndpointAttributeLocalName) ||
                    reader.NamespaceURI.Length != 0 ||
                    string.IsNullOrWhiteSpace(reader.Value))
                {
                    return null;
                }

                string value = reader.Value.Normalize(NormalizationForm.FormC);
                reader.MoveToElement();
                int line = reader is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                    ? lineInfo.LineNumber
                    : 0;
                if (line == selector.Line)
                {
                    if (ordinalOnLine == selector.OccurrenceOrdinalOnLine)
                    {
                        selected = value;
                    }

                    ordinalOnLine++;
                }
            }

            return selected;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException("A configured provider XML file is malformed.", exception);
        }
    }

    private static bool IsBeneathRoot(string path, string root) =>
        path.StartsWith(root + '/', StringComparison.Ordinal);

    private static string? FindConfiguredLiteral(
        string source,
        ExactConsumerInvocationSelector selector)
    {
        IReadOnlyList<JavaToken> tokens = Tokenize(source);
        int ordinalOnLine = 0;
        for (int index = 0; index < tokens.Count; index++)
        {
            JavaToken start = tokens[index];
            if (start.Line != selector.Line ||
                start.Kind != JavaTokenKind.Identifier ||
                !StringComparer.Ordinal.Equals(start.Value, selector.ReceiverSymbol) ||
                index > 0 && tokens[index - 1] is
                    { Kind: JavaTokenKind.Punctuation, Value: "." } ||
                index + 3 >= tokens.Count ||
                tokens[index + 1] is not { Kind: JavaTokenKind.Punctuation, Value: "." } ||
                tokens[index + 2] is not { Kind: JavaTokenKind.Identifier } member ||
                !StringComparer.Ordinal.Equals(member.Value, selector.MemberName) ||
                tokens[index + 3] is not { Kind: JavaTokenKind.Punctuation, Value: "(" })
            {
                continue;
            }

            IReadOnlyList<IReadOnlyList<JavaToken>>? arguments = ReadArguments(tokens, index + 3);
            if (arguments is null || selector.EndpointArgumentIndex >= arguments.Count)
            {
                continue;
            }

            IReadOnlyList<JavaToken> argument = arguments[selector.EndpointArgumentIndex];
            if (ordinalOnLine++ != selector.OccurrenceOrdinalOnLine)
            {
                continue;
            }

            return argument.Count == 1 && argument[0].Kind == JavaTokenKind.StringLiteral
                ? argument[0].Value
                : null;
        }

        return null;
    }

    private static IReadOnlyList<IReadOnlyList<JavaToken>>? ReadArguments(
        IReadOnlyList<JavaToken> tokens,
        int openingParenthesis)
    {
        var arguments = new List<IReadOnlyList<JavaToken>>();
        var current = new List<JavaToken>();
        var nesting = new Stack<string>();
        nesting.Push(")");
        for (int index = openingParenthesis + 1; index < tokens.Count; index++)
        {
            JavaToken token = tokens[index];
            if (token.Kind == JavaTokenKind.Punctuation)
            {
                if (token.Value is "(" or "[" or "{")
                {
                    nesting.Push(token.Value switch
                    {
                        "(" => ")",
                        "[" => "]",
                        _ => "}",
                    });
                }
                else if (token.Value is ")" or "]" or "}")
                {
                    if (nesting.Count == 0 || !StringComparer.Ordinal.Equals(nesting.Pop(), token.Value))
                    {
                        return null;
                    }

                    if (nesting.Count == 0)
                    {
                        arguments.Add(current.AsReadOnly());
                        return arguments.AsReadOnly();
                    }
                }
                else if (token.Value == "," && nesting.Count == 1)
                {
                    arguments.Add(current.AsReadOnly());
                    current = [];
                    continue;
                }
            }

            current.Add(token);
        }

        return null;
    }

    private static IReadOnlyList<JavaToken> Tokenize(string source)
    {
        var tokens = new List<JavaToken>();
        int line = 1;
        for (int index = 0; index < source.Length;)
        {
            char current = source[index];
            if (current == '\r' || current == '\n' || char.IsWhiteSpace(current))
            {
                if (current == '\n' || current == '\r' &&
                    (index + 1 >= source.Length || source[index + 1] != '\n'))
                {
                    line++;
                }

                index++;
                continue;
            }

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '/')
            {
                index += 2;
                while (index < source.Length && source[index] is not ('\r' or '\n'))
                {
                    index++;
                }

                continue;
            }

            if (current == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                index += 2;
                bool closed = false;
                while (index < source.Length)
                {
                    if (source[index] == '\n')
                    {
                        line++;
                    }

                    if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        index += 2;
                        closed = true;
                        break;
                    }

                    index++;
                }

                if (!closed)
                {
                    return [];
                }

                continue;
            }

            int tokenLine = line;
            if (char.IsAsciiLetter(current) || current is '_' or '$')
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '_' or '$'))
                {
                    index++;
                }

                tokens.Add(new(JavaTokenKind.Identifier, source[start..index], tokenLine));
                continue;
            }

            if (current == '"')
            {
                string? decoded = ReadOrdinaryString(source, ref index, ref line);
                if (decoded is null)
                {
                    return [];
                }

                tokens.Add(new(JavaTokenKind.StringLiteral, decoded, tokenLine));
                continue;
            }

            tokens.Add(new(JavaTokenKind.Punctuation, current.ToString(), tokenLine));
            index++;
        }

        return tokens.AsReadOnly();
    }

    private static string? ReadOrdinaryString(string source, ref int index, ref int line)
    {
        if (index + 2 < source.Length && source[index..(index + 3)] == "\"\"\"")
        {
            return null;
        }

        var value = new StringBuilder();
        index++;
        while (index < source.Length)
        {
            char current = source[index++];
            if (current == '"')
            {
                return value.ToString().Normalize(NormalizationForm.FormC);
            }

            if (current is '\r' or '\n')
            {
                line++;
                return null;
            }

            if (current != '\\')
            {
                value.Append(current);
                continue;
            }

            if (index >= source.Length)
            {
                return null;
            }

            char escaped = source[index++];
            value.Append(escaped switch
            {
                'b' => '\b',
                't' => '\t',
                'n' => '\n',
                'f' => '\f',
                'r' => '\r',
                '"' => '"',
                '\'' => '\'',
                '\\' => '\\',
                _ => '\0',
            });
            if (value[^1] == '\0')
            {
                return null;
            }
        }

        return null;
    }

    private static string ResolveListedFile(string rootPath, string repositoryRelativePath)
    {
        string root = Path.GetFullPath(rootPath);
        string fullPath = Path.GetFullPath(Path.Combine(
            root,
            repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(prefix, pathComparison))
        {
            throw new InvalidDataException("A configured file escapes the committed snapshot.");
        }

        string current = root;
        foreach (string segment in repositoryRelativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("A configured file traverses a reparse point.");
                }
            }
        }

        return fullPath;
    }

    private static async Task<byte[]> ReadBoundedAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 16 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumFileBytes)
        {
            throw new InvalidDataException("A configured file exceeds its bound.");
        }

        using var buffer = new MemoryStream((int)stream.Length);
        byte[] chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumFileBytes)
            {
                throw new InvalidDataException("A configured file exceeds its bound.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("A configured file is not valid UTF-8.", exception);
        }
    }

    private enum JavaTokenKind
    {
        Identifier,
        StringLiteral,
        Punctuation,
    }

    private sealed record JavaToken(JavaTokenKind Kind, string Value, int Line);
}
