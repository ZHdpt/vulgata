using System.Runtime.CompilerServices;
using System.Text;
using Vulgata.Agents.ChatExecution.October;
using Vulgata.Core.DomainServices.OctoberKnowledge;
using Vulgata.Tests.TestInfrastructure;

namespace Vulgata.Tests.October.Chat;

/// <summary>Prompt loading/wiring checks, not model-output faithfulness or production resource registration proof.</summary>
public sealed class OctoberChatQualificationTests
{
    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData(KnowledgeMode.Business)]
    [InlineData(KnowledgeMode.IT)]
    public async Task LoadAsync_ExplicitOwnedPromptFile_PreservesExactSelectedTextAsync(KnowledgeMode mode)
    {
        string business = PromptPath("OctoberChatBusiness.txt");
        string it = PromptPath("OctoberChatIT.txt");
        int businessOpens = 0;
        int itOpens = 0;
        var catalog = new OctoberChatPromptCatalog([
            new(OctoberChatPromptCatalog.BusinessResourceName, () => { businessOpens++; return File.OpenRead(business); }),
            new(OctoberChatPromptCatalog.ItResourceName, () => { itOpens++; return File.OpenRead(it); }),
        ]);

        string? text = await catalog.LoadAsync(mode);

        Assert.Equal(await File.ReadAllTextAsync(mode == KnowledgeMode.Business ? business : it, new UTF8Encoding(false, true)), text);
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Equal(mode == KnowledgeMode.Business ? 1 : 0, businessOpens);
        Assert.Equal(mode == KnowledgeMode.IT ? 1 : 0, itOpens);
        // Exact byte-derived text equality includes qualification/continuation instructions.
        // It does not assert that a model will obey them or that source meaning is proven.
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_MissingOrDuplicateFixedName_FailsClosedBeforeOpeningAnyCandidateAsync()
    {
        Assert.Null(await new OctoberChatPromptCatalog([]).LoadAsync(KnowledgeMode.Business));
        int opens = 0;
        Stream Open()
        {
            opens++;
            return new MemoryStream(Encoding.UTF8.GetBytes("not selected"));
        }
        var duplicate = new OctoberChatPromptCatalog([
            new(OctoberChatPromptCatalog.BusinessResourceName, Open),
            new(OctoberChatPromptCatalog.BusinessResourceName, Open),
        ]);
        var wrongMode = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.ItResourceName, Open)]);

        Assert.Null(await duplicate.LoadAsync(KnowledgeMode.Business));
        Assert.Null(await wrongMode.LoadAsync(KnowledgeMode.Business));
        Assert.Equal(0, opens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_UnknownMode_DoesNotOpenAnotherModeOrLegacyPromptAsync()
    {
        int opens = 0;
        var catalog = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.BusinessResourceName, () =>
        {
            opens++;
            return new MemoryStream(Encoding.UTF8.GetBytes("controlled"));
        })]);

        Assert.Null(await catalog.LoadAsync((KnowledgeMode)99));
        Assert.Equal(0, opens);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteTheory("SelfContained")]
    [InlineData("invalid-utf8")]
    [InlineData("utf16-bom")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("oversize-ascii")]
    [InlineData("oversize-unicode")]
    public async Task LoadAsync_InvalidOrOversizedStream_ReturnsNoPromptAndDisposesStreamAsync(string invalid)
    {
        byte[] bytes = invalid switch
        {
            "invalid-utf8" => [0xc3, 0x28],
            "utf16-bom" => [0xff, 0xfe, 0x41, 0x00],
            "empty" => [],
            "whitespace" => Encoding.UTF8.GetBytes(" \r\n\t"),
            "oversize-ascii" => Encoding.UTF8.GetBytes(new string('a', 32769)),
            _ => Encoding.UTF8.GetBytes(new string('中', 11000)),
        };
        var stream = new TrackingStream(bytes);
        var catalog = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.BusinessResourceName, () => stream)]);

        Assert.Null(await catalog.LoadAsync(KnowledgeMode.Business));
        Assert.True(stream.WasDisposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_ExactByteLimit_IsAcceptedWithoutTruncationAsync()
    {
        string expected = new('a', 32768);
        var stream = new TrackingStream(Encoding.UTF8.GetBytes(expected));
        var catalog = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.ItResourceName, () => stream)]);

        Assert.Equal(expected, await catalog.LoadAsync(KnowledgeMode.IT));
        Assert.True(stream.WasDisposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_NonSeekableSplitUtf8Stream_DecodesCompleteCharactersWithoutLengthAccessAsync()
    {
        const string expected = "受控说明😀与资格限制";
        var stream = new TrackingStream(Encoding.UTF8.GetBytes(expected), nonSeekable: true, chunkSize: 1);
        var catalog = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.BusinessResourceName, () => stream)]);

        Assert.Equal(expected, await catalog.LoadAsync(KnowledgeMode.Business));
        Assert.True(stream.WasDisposed);
    }

    [Trait("Suite", "SelfContained")]
    [SuiteFact("SelfContained")]
    public async Task LoadAsync_CancelledBeforeRead_DoesNotOpenResourceOrSubstitutePromptAsync()
    {
        int opens = 0;
        var catalog = new OctoberChatPromptCatalog([new(OctoberChatPromptCatalog.BusinessResourceName, () =>
        {
            opens++;
            return new MemoryStream(Encoding.UTF8.GetBytes("controlled"));
        })]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.LoadAsync(KnowledgeMode.Business, cancellation.Token));
        Assert.Equal(0, opens);
    }

    private static string PromptPath(string fileName, [CallerFilePath] string testFile = "")
    {
        DirectoryInfo directory = new(Path.GetDirectoryName(testFile)!);
        string repository = directory.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(repository, "src", "dotnet", "Vulgata.Agents", "Prompts", fileName);
    }

    private sealed class TrackingStream(byte[] bytes, bool nonSeekable = false, int chunkSize = int.MaxValue) : MemoryStream(bytes)
    {
        public bool WasDisposed { get; private set; }
        public override bool CanSeek => !nonSeekable;
        public override long Length => nonSeekable ? throw new NotSupportedException("Non-seekable controlled stream") : base.Length;
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, chunkSize));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, chunkSize)]);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
