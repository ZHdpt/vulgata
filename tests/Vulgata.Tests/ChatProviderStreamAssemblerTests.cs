using System.Runtime.CompilerServices;
using System.Text;
using Vulgata.Agents.ChatExecution;
using Vulgata.Core.DomainServices;

namespace Vulgata.Tests;

/// <summary>
/// T030 RED contract for the server-only provider byte-to-answer boundary.
///
/// These tests deliberately call the pure assembler seam, not the provider SDK
/// or an aggregate fake.  The input is an arbitrary-read sequence of response
/// body octets and the output is a content-dark assembly result; no provider
/// fragment is suitable for client rendering.
/// </summary>
public sealed class ChatProviderStreamAssemblerTests
{
    [Fact]
    public async Task AssembleAsync_SplitsUtf8CrLfAndSseJsonAcrossEveryRead_ProducesOneAnswerAsync()
    {
        byte[] body = Wire(
            "data: {\"choices\":[{\"delta\":{\"content\":\"中\"}}]}\r\n\r\n" +
            ": keepalive\n\n" +
            "event: message\r\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"文\"}}]}\n\n" +
            "data: [DONE]\r\n\r\n");

        var result =
            await AssembleAsync(SplitAtEveryByte(body));

        Assert.True(result.Accepted, result.RejectionCategory);
        Assert.Equal("中文", result.Answer);
        Assert.Equal(2, result.AnswerDeltaCount);
        Assert.Equal(6, result.AssembledAnswerByteCount);
        Assert.Equal(1, result.TerminalCount);
    }

    [Fact]
    public async Task AssembleAsync_SplitJsonEscapesAndSurrogatePair_UsesDecodedDeltaBytesAsync()
    {
        byte[] body = Wire(
            "data: {\"choices\":[{\"delta\":{\"content\":\"\\uD83D\\uDE00\\u4e2d\"}}]}\n\n" +
            "data: [DONE]\n\n");

        var result =
            await AssembleAsync(SplitAtEveryByte(body));

        Assert.True(result.Accepted, result.RejectionCategory);
        Assert.Equal("😀中", result.Answer);
        Assert.Equal(7, result.AnswerDeltaByteCount);
        Assert.Equal(7, result.AssembledAnswerByteCount);
    }

    [Fact]
    public async Task AssembleAsync_MalformedWireUtf8_RejectsWithoutReplacementAsync()
    {
        byte[] body = Wire("data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}]}\n\n")
            .Concat(new byte[] { 0xC3, 0x28 })
            .Concat(Wire("data: [DONE]\n\n"))
            .ToArray();

        var result =
            await AssembleAsync(SplitAtEveryByte(body));

        AssertRejected(result, "invalid-wire-utf8");
        Assert.Null(result.Answer);
        Assert.DoesNotContain('\uFFFD', result.PartialAnswer ?? string.Empty);
    }

    [Theory]
    [InlineData("data: {not-json}\n\n", "invalid-json-event")]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"\\q\"}}]}\n\n", "invalid-json-escape")]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"\\uD800\"}}]}\n\n", "invalid-json-escape")]
    public async Task AssembleAsync_InvalidJsonOrEscapes_RejectWithClosedCategoryAsync(
        string eventText,
        string category)
    {
        var result =
            await AssembleAsync(SplitAtEveryByte(Wire(eventText + "data: [DONE]\n\n")));

        AssertRejected(result, category);
    }

    [Fact]
    public async Task AssembleAsync_MissingTerminalAtEof_RejectsAndDiscardsAssemblyAsync()
    {
        var result = await AssembleAsync(SplitAtEveryByte(
            Wire("data: {\"choices\":[{\"delta\":{\"content\":\"answer\"}}]}\n\n")));

        AssertRejected(result, "missing-terminal");
        Assert.Null(result.Answer);
    }

    [Fact]
    public async Task AssembleAsync_DuplicateTerminal_RejectsAfterReadingToPhysicalEofAsync()
    {
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            "data: {\"choices\":[{\"delta\":{\"content\":\"answer\"}}]}\n\n" +
            "data: [DONE]\n\n" +
            ": inert after terminal\n\n" +
            "data: [DONE]\n\n")));

        AssertRejected(result, "duplicate-terminal");
    }

    [Fact]
    public async Task AssembleAsync_NonEmptyDataAfterTerminal_RejectsPostTerminalDataAsync()
    {
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            "data: [DONE]\n\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"late\"}}]}\n\n")));

        AssertRejected(result, "post-terminal-data");
    }

    [Theory]
    [InlineData("data: [DONE]\n", "missing-terminal")]
    [InlineData("wat value\n\n", "invalid-sse")]
    [InlineData("data {\"choices\":[]}\n\n", "invalid-sse")]
    public async Task AssembleAsync_InvalidTerminalOrFieldGrammar_RejectsClosedCategoryAsync(
        string body,
        string category)
    {
        var result = await AssembleAsync(
            SplitAtEveryByte(Wire(body)));

        AssertRejected(result, category);
    }

    [Fact]
    public async Task AssembleAsync_DecodedDeltaAtExactLimit_IsAcceptedAsync()
    {
        string content = new('a', 16_384);
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}}}}]}}\n\n" +
            "data: [DONE]\n\n")));

        Assert.True(result.Accepted, result.RejectionCategory);
        Assert.Equal(16_384, result.AnswerDeltaByteCount);
    }

    [Fact]
    public async Task AssembleAsync_DecodedDeltaOneByteOverLimit_RejectsChunkOverflowAsync()
    {
        string content = new('a', 16_385);
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}}}}]}}\n\n" +
            "data: [DONE]\n\n")));

        AssertRejected(result, "chunk-overflow");
    }

    [Fact]
    public async Task AssembleAsync_EscapedWireSixBytesButDecodedDeltaThreeBytes_UsesDecodedBudgetAsync()
    {
        string escaped = string.Concat(Enumerable.Repeat("\\u4e2d", 16_384 / 3));
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{escaped}\"}}}}]}}\n\n" +
            "data: [DONE]\n\n")));

        Assert.True(result.Accepted, result.RejectionCategory);
        Assert.Equal(16_383, result.AnswerDeltaByteCount);
    }

    [Fact]
    public async Task AssembleAsync_ConcatenatedDeltasAtExactFinalLimit_IsAcceptedAsync()
    {
        string first = new('a', 16_384);
        string second = new('b', 16_384);
        string third = new('c', 16_384);
        string fourth = new('d', 16_384);
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            Delta(first) + Delta(second) + Delta(third) + Delta(fourth) + "data: [DONE]\n\n")));

        Assert.True(result.Accepted, result.RejectionCategory);
        Assert.Equal(65_536, result.AssembledAnswerByteCount);
    }

    [Fact]
    public async Task AssembleAsync_ConcatenatedDeltasOneByteOverFinalLimit_RejectsTotalOverflowAsync()
    {
        string first = new('a', 16_384);
        string second = new('b', 16_384);
        string third = new('c', 16_384);
        string fourth = new('d', 16_384);
        string fifth = "e";
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            Delta(first) + Delta(second) + Delta(third) + Delta(fourth) + Delta(fifth) +
            "data: [DONE]\n\n")));

        AssertRejected(result, "total-overflow");
    }

    [Fact]
    public async Task AssembleAsync_CommentPaddingAtRawBodySafetyLimit_RejectsEventOverflowAsync()
    {
        byte[] valid = Wire("data: [DONE]\n\n");
        byte[] padding = CommentPadding(1_048_576 - valid.Length);
        var result = await AssembleAsync(
            SplitAtEveryByte(padding.Concat(valid).ToArray()));

        AssertRejected(result, "sse-event-overflow");
    }

    [Fact]
    public async Task AssembleAsync_RawBodySafetyLimitPlusOne_RejectsRawBodyOverflowAsync()
    {
        byte[] valid = Wire("data: [DONE]\n\n");
        byte[] padding = CommentPadding(1_048_577 - valid.Length);
        var result = await AssembleAsync(
            SplitAtEveryByte(padding.Concat(valid).ToArray()));

        AssertRejected(result, "raw-body-overflow");
    }

    [Fact]
    public async Task AssembleAsync_SseLineAndEventCapsAreIndependentFromAnswerCapsAsync()
    {
        string longComment = new('x', 131_070);
        var exact = await AssembleAsync(SplitAtEveryByte(Wire(
            $": {longComment}\n\ndata: [DONE]\n\n")));
        AssertRejected(exact, "sse-event-overflow");

        string overlongComment = new('x', 131_071);
        var overflow = await AssembleAsync(SplitAtEveryByte(Wire(
            $": {overlongComment}\n\ndata: [DONE]\n\n")));
        AssertRejected(overflow, "sse-line-overflow");
    }

    [Fact]
    public async Task AssembleAsync_CommentFields_CountTowardEventSafetyCapAsync()
    {
        string overhead = string.Concat(Enumerable.Repeat(": keepalive\n", 50_000));
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            overhead + "data: [DONE]\n\n")));

        AssertRejected(result, "sse-event-overflow");
    }

    [Fact]
    public async Task AssembleAsync_UnknownSseFields_CountTowardEventSafetyCapAsync()
    {
        string overhead = string.Concat(Enumerable.Repeat("retry: 1000\n", 50_000));
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            overhead + "data: [DONE]\n\n")));

        // Characterization: ordinary SSE fields already participate in the
        // event-byte counter; this should remain GREEN while comment handling
        // is corrected.
        AssertRejected(result, "sse-event-overflow");
    }

    [Fact]
    public async Task AssembleAsync_ProviderErrorAfterTerminal_PreservesPostTerminalClassificationAsync()
    {
        var result = await AssembleAsync(SplitAtEveryByte(Wire(
            "data: [DONE]\n\n" +
            "data: {\"error\":{\"message\":\"provider stopped\"}}\n\n")));

        // Characterization: this accepted boundary already distinguishes a
        // provider error after [DONE] from a provider error before terminal.
        AssertRejected(result, "post-terminal-data");
    }

    private static async Task<dynamic> AssembleAsync(
        IReadOnlyList<ReadOnlyMemory<byte>> chunks) =>
        await ChatProviderStreamAssembler.AssembleAsync(
            ToAsync(chunks),
            CancellationToken.None);

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ToAsync(
        IReadOnlyList<ReadOnlyMemory<byte>> chunks,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (ReadOnlyMemory<byte> chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>> SplitAtEveryByte(byte[] body) =>
        body.Select(value => new ReadOnlyMemory<byte>([value])).ToArray();

    private static byte[] Wire(string value) => Encoding.UTF8.GetBytes(value);

    private static string Delta(string content) =>
        $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}}}}]}}\n\n";

    private static byte[] CommentPadding(int length)
    {
        const string comment = ":x\n";
        int fullComments = length / comment.Length;
        int remainder = length % comment.Length;
        return Encoding.UTF8.GetBytes(string.Concat(
            string.Concat(Enumerable.Repeat(comment, fullComments)),
            new string('\n', remainder)));
    }

    private static void AssertRejected(
        dynamic result,
        string category)
    {
        Assert.False(result.Accepted);
        Assert.Equal(category, result.RejectionCategory);
        Assert.Null(result.Answer);
    }
}
