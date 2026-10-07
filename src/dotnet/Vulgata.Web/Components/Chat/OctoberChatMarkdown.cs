using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Vulgata.Web.Components.Chat;

/// <summary>
/// Encoded-text rendering for a conservative Markdown subset, not full CommonMark.
/// Every current answer prefix remains visible, including incomplete syntax.
/// Model HTML, URLs and language labels never become executable markup or attributes.
/// </summary>
public sealed class OctoberChatMarkdown : ComponentBase
{
    private const int MaximumParsedCharacters = 65_536;
    private const int MaximumNodes = 4_096;
    private const int MaximumScanWork = 8 * MaximumParsedCharacters;
    [Parameter] public string Text { get; set; } = string.Empty;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        string text = Text ?? string.Empty;
        if (text.Length > MaximumParsedCharacters) { builder.AddContent(0, text); return; }
        List<Node> nodes;
        try { nodes = new Parser(text).Parse(); }
        catch (ParseLimitException) { builder.AddContent(0, text); return; }
        Render(builder, nodes);
    }

    // Only these application constants become element names. There are no model
    // attributes, raw HTML operations, navigation elements or image elements.
    private enum Kind { Text, Paragraph, H1, H2, H3, H4, H5, H6, Unordered, Ordered, Item, Quote, Pre, Code, Strong, Emphasis }
    private sealed record Node(Kind Kind, string? Text, List<Node>? Children);
    private readonly record struct Line(int Start, int ContentEnd, int End);
    private sealed class ParseLimitException : Exception { }

    private static void Render(RenderTreeBuilder builder, List<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            builder.OpenRegion(0);
            if (node.Kind == Kind.Text) builder.AddContent(1, node.Text);
            else
            {
                builder.OpenElement(2, node.Kind switch
                {
                    Kind.Paragraph => "p", Kind.H1 => "h1", Kind.H2 => "h2", Kind.H3 => "h3",
                    Kind.H4 => "h4", Kind.H5 => "h5", Kind.H6 => "h6", Kind.Unordered => "ul",
                    Kind.Ordered => "ol", Kind.Item => "li", Kind.Quote => "blockquote", Kind.Pre => "pre",
                    Kind.Code => "code", Kind.Strong => "strong", Kind.Emphasis => "em",
                    _ => throw new InvalidOperationException("Unsupported presentation element.")
                });
                // Structural depth is fixed: at most list/item/inline/text. The
                // parser never recursively interprets inline or nested block input.
                if (node.Children is not null) Render(builder, node.Children);
                builder.CloseElement();
            }
            builder.CloseRegion();
        }
    }

    private sealed class Parser(string source)
    {
        private readonly List<Line> _lines = [];
        private int _nodes;
        private int _work;

        public List<Node> Parse()
        {
            int start = 0;
            while (start < source.Length)
            {
                int newline = source.IndexOf('\n', start);
                int end = newline < 0 ? source.Length : newline + 1;
                Spend(end - start);
                if (_lines.Count == MaximumNodes) throw new ParseLimitException();
                int contentEnd = newline < 0 ? end : newline;
                if (contentEnd > start && source[contentEnd - 1] == '\r') contentEnd--;
                _lines.Add(new(start, contentEnd, end));
                start = end;
            }
            List<Node> result = [];
            for (int index = 0; index < _lines.Count;)
            {
                Line line = _lines[index];
                ReadOnlySpan<char> content = Content(line);
                Spend(content.Length);
                if (content.IsWhiteSpace()) { result.Add(Text(source[line.Start..line.End])); index++; continue; }
                if (Fence(content))
                {
                    int close = index + 1;
                    for (; close < _lines.Count; close++)
                    {
                        ReadOnlySpan<char> possible = Content(_lines[close]);
                        Spend(possible.Length);
                        if (possible.TrimEnd().SequenceEqual("```")) break;
                    }
                    if (close == _lines.Count)
                    {
                        // An unfinished fence stays fully visible now, without
                        // interpreting any of its provisional code as Markdown.
                        result.Add(Text(source[line.Start..]));
                        break;
                    }
                    result.Add(Element(Kind.Pre, [Element(Kind.Code, [Text(source[line.End.._lines[close].Start])])]));
                    AddEnding(result, _lines[close]);
                    index = close + 1;
                    continue;
                }
                int heading = Heading(content);
                if (heading > 0)
                {
                    result.Add(Element((Kind)((int)Kind.H1 + heading - 1), Inline(content[(heading + 1)..].ToString())));
                    AddEnding(result, line);
                    index++;
                    continue;
                }
                var (listKind, _, number) = ListMarker(content);
                if (listKind is not null && (listKind == Kind.Unordered || number == 1))
                {
                    List<Node> items = [];
                    int expected = 1;
                    while (index < _lines.Count)
                    {
                        line = _lines[index];
                        var marker = ListMarker(Content(line));
                        if (marker.Kind != listKind || listKind == Kind.Ordered && marker.Number != expected) break;
                        Spend(line.End - line.Start);
                        items.Add(Element(Kind.Item, Inline(source[(line.Start + marker.Offset)..line.ContentEnd])));
                        AddEnding(items, line);
                        expected++;
                        index++;
                    }
                    result.Add(Element(listKind.Value, items));
                    continue;
                }
                if (Quote(content))
                {
                    List<Node> quoted = [];
                    while (index < _lines.Count && Quote(Content(_lines[index])))
                    {
                        line = _lines[index++];
                        Spend(line.End - line.Start);
                        quoted.AddRange(Inline(source[Math.Min(line.Start + 2, line.ContentEnd)..line.ContentEnd]));
                        AddEnding(quoted, line);
                    }
                    result.Add(Element(Kind.Quote, quoted));
                    continue;
                }
                List<Node> paragraph = [];
                do
                {
                    line = _lines[index++];
                    paragraph.AddRange(Inline(source[line.Start..line.ContentEnd]));
                    AddEnding(paragraph, line);
                } while (index < _lines.Count && !StartsBlock(Content(_lines[index])));
                result.Add(Element(Kind.Paragraph, paragraph));
            }
            return result;
        }

        private List<Node> Inline(string text)
        {
            Spend(text.Length);
            // Unsupported links and HTML-heavy lines are readable literal input.
            // Backtick spans still allow ordinary comparisons such as `a < b`.
            if (text.Contains("](", StringComparison.Ordinal) || text.Contains('<') && !text.Contains('`')) return [Text(text)];
            List<Node> result = [];
            var exhausted = new HashSet<string>(StringComparer.Ordinal);
            int position = 0;
            int literal = 0;
            while (position < text.Length)
            {
                char value = text[position];
                if (value is not ('`' or '*' or '_')) { position++; continue; }
                int width = value != '`' && position + 1 < text.Length && text[position + 1] == value ? 2 : 1;
                string delimiter = new(value, width);
                if (exhausted.Contains(delimiter)) { position += width; continue; }
                int end = text.IndexOf(delimiter, position + width, StringComparison.Ordinal);
                Spend(end < 0 ? text.Length - position : end - position);
                if (end <= position + width)
                {
                    if (end < 0) exhausted.Add(delimiter);
                    position += width;
                    continue;
                }
                if (position > literal) result.Add(Text(text[literal..position]));
                Kind kind = value == '`' ? Kind.Code : width == 2 ? Kind.Strong : Kind.Emphasis;
                result.Add(Element(kind, [Text(text[(position + width)..end])]));
                position = end + width;
                literal = position;
            }
            if (literal < text.Length) result.Add(Text(text[literal..]));
            return result;
        }

        private ReadOnlySpan<char> Content(Line line) => source.AsSpan(line.Start, line.ContentEnd - line.Start);
        private static bool Fence(ReadOnlySpan<char> line) => line.StartsWith("```") && (line.Length == 3 || line[3] != '`');
        private static bool Quote(ReadOnlySpan<char> line) => line.SequenceEqual(">") || line.StartsWith("> ");
        private static int Heading(ReadOnlySpan<char> line)
        {
            int count = 0;
            while (count < line.Length && count < 7 && line[count] == '#') count++;
            return count is > 0 and <= 6 && count < line.Length && line[count] == ' ' ? count : 0;
        }

        private static (Kind? Kind, int Offset, int Number) ListMarker(ReadOnlySpan<char> line)
        {
            if (line.Length >= 2 && line[0] is '-' or '*' or '+' && line[1] == ' ') return (Kind.Unordered, 2, 0);
            int digits = 0;
            while (digits < line.Length && digits < 9 && char.IsAsciiDigit(line[digits])) digits++;
            if (digits > 0 && digits + 1 < line.Length && line[digits] == '.' && line[digits + 1] == ' ' &&
                int.TryParse(line[..digits], out int number)) return (Kind.Ordered, digits + 2, number);
            return (null, 0, 0);
        }

        private static bool StartsBlock(ReadOnlySpan<char> line) => line.IsWhiteSpace() || Fence(line) || Heading(line) > 0 || Quote(line) ||
            ListMarker(line) is { Kind: Kind.Unordered } or { Kind: Kind.Ordered, Number: 1 };

        private void AddEnding(List<Node> nodes, Line line)
        {
            if (line.ContentEnd < line.End) nodes.Add(Text(source[line.ContentEnd..line.End]));
        }
        private Node Text(string value) { CountNode(); return new(Kind.Text, value, null); }
        private Node Element(Kind kind, List<Node> children) { CountNode(); return new(kind, null, children); }
        private void CountNode() { if (++_nodes > MaximumNodes) throw new ParseLimitException(); }
        private void Spend(int characters) { _work += characters; if (_work > MaximumScanWork) throw new ParseLimitException(); }
    }
}
