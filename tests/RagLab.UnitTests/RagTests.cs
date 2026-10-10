using Microsoft.Extensions.Logging.Abstractions;
using RagLab.Application;
using Xunit;

namespace RagLab.UnitTests;

public sealed class RagTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "raglab-tests-" + Guid.NewGuid());

    public RagTests() => Directory.CreateDirectory(root);

    public void Dispose() => Directory.Delete(root, true);

    private RagOptions Options() => new()
    {
        AllowedRoot = root,
        Dimensions = 3,
        ChunkSize = 100,
        ChunkOverlap = 20
    };

    private RagService Service(FakeStore store, FakeModels models, RagOptions? options = null)
    {
        var config = options ?? Options();
        return new(store, models, models, new(config), config, NullLogger<RagService>.Instance);
    }

    [Fact]
    public void ChunkingPreservesTextAndExactOverlap()
    {
        var text = string.Concat(Enumerable.Range(0, 243).Select(i => (char)('a' + i % 26)));
        var chunks = new TextChunker(Options()).Split(text);
        Assert.Equal(new[] { 0, 80, 160 }, chunks.Select(c => c.Start));
        Assert.Equal(new[] { 100, 100, 83 }, chunks.Select(c => c.Text.Length));
        Assert.Equal(chunks[0].Text[^20..], chunks[1].Text[..20]);
        Assert.Equal(text, chunks[0].Text + string.Concat(chunks.Skip(1).Select(c => c.Text[20..])));
    }

    [Fact]
    public void BlankTextProducesNoChunks() => Assert.Empty(new TextChunker(Options()).Split("   \r\n"));

    [Fact]
    public void InvalidOverlapIsRejected()
    {
        var o = Options();
        o.ChunkOverlap = o.ChunkSize;
        Assert.Throws<ArgumentException>(() => new TextChunker(o).Split("hello"));
    }

    [Fact]
    public void HashIsStableAndSensitiveToChanges()
    {
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            ContentHash.Compute("abc"));
        Assert.NotEqual(ContentHash.Compute("abc"), ContentHash.Compute("abcd"));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("bad.pdf")]
    [InlineData("")]
    public void RejectsFilesOutsideAllowlist(string path) => Assert.Throws<ArgumentException>(() => Service(new(), new()).ResolveAllowedFile(path));

    [Fact]
    public async Task UnchangedDocumentSkipsEmbeddingAndReplacement()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), "unchanged");
        var o = Options();
        var store = new FakeStore
        {
            State = new(Guid.NewGuid(), ContentHash.Compute("unchanged"), o.IndexProfile)
        };
        var models = new FakeModels();
        var result = await Service(store, models, o).IndexAsync("note.md", default);
        Assert.Equal("unchanged", result.Status);
        Assert.Equal(0, models.EmbeddingCalls);
        Assert.Equal(0, store.Replacements);
    }

    [Fact]
    public async Task ChangedChunkingProfileReindexesUnchangedContent()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), "unchanged");
        var store = new FakeStore
        {
            State = new(Guid.NewGuid(), ContentHash.Compute("unchanged"), "old-profile")
        };
        var models = new FakeModels();
        await Service(store, models).IndexAsync("note.md", default);
        Assert.Equal(1, store.Replacements);
        Assert.Equal(1, models.EmbeddingCalls);
    }

    [Fact]
    public async Task EmbeddingFailureDoesNotReplaceExistingIndex()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), new string('x', 200));
        var store = new FakeStore
        {
            State = new(Guid.NewGuid(), "old", "old")
        };
        var models = new FakeModels
        {
            FailOnCall = 2
        };
        await Assert.ThrowsAsync<HttpRequestException>(() => Service(store, models).IndexAsync("note.md", default));
        Assert.Equal(0, store.Replacements);
        Assert.Equal(1, store.Failures);
        Assert.Equal("old", store.State.ContentHash);
    }

    [Fact]
    public async Task EmptyDocumentAtomicallyReplacesWithNoChunks()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), "  ");
        var store = new FakeStore();
        var models = new FakeModels();
        await Service(store, models).IndexAsync("note.md", default);
        Assert.Empty(store.Chunks);
        Assert.Equal(1, store.Replacements);
        Assert.Equal(0, models.EmbeddingCalls);
    }

    [Fact]
    public async Task EmptyRetrievalDoesNotCallChat()
    {
        var models = new FakeModels();
        var result = await Service(new(), models).AskAsync("unknown", default);
        Assert.True(result.InsufficientContext);
        Assert.Empty(result.Citations);
        Assert.Equal(0, models.ChatCalls);
    }

    [Fact]
    public async Task ValidAnswerReturnsOnlyCitedPassagesAndDiagnostics()
    {
        var store = WithPassage();
        var result = await Service(store, new()).AskAsync("known", default);
        Assert.False(result.InsufficientContext);
        Assert.Single(result.Citations);
        Assert.Single(result.Retrieved);
        Assert.Equal(store.Passages[0].DocumentId, result.Citations[0].DocumentId);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 99)]
    [InlineData(false, 0)]
    public async Task UnsupportedOrInvalidCitationsReturnInsufficientContext(bool insufficient, int id)
    {
        var models = new FakeModels
        {
            Answer = new("unsupported", insufficient, id == 0 ? [] : [id])
        };
        var result = await Service(WithPassage(), models).AskAsync("question", default);
        Assert.True(result.InsufficientContext);
        Assert.Empty(result.Citations);
        Assert.Single(result.Retrieved);
    }

    [Fact]
    public async Task ValidCitationIdsReceiveMissingInlineMarkers()
    {
        var result = await Service(WithPassage(), new()
        {
            Answer = new("Supported answer", false, [1])
        }).AskAsync("question", default);
        Assert.False(result.InsufficientContext);
        Assert.Equal("Supported answer [1]", result.Answer);
        Assert.Single(result.Citations);
    }

    [Fact]
    public async Task InlineCitationMismatchIsRejected()
    {
        var result = await Service(WithPassage(), new()
        {
            Answer = new("Invented citation [99]", false, [1])
        }).AskAsync("question", default);
        Assert.True(result.InsufficientContext);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutRecordingFailure()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), "hello");
        var store = new FakeStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(store, new()).IndexAsync("note.md", cts.Token));
        Assert.Equal(0, store.Replacements);
        Assert.Equal(0, store.Failures);
    }

    [Fact]
    public async Task OversizeFileIsRejectedBeforeEmbedding()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "note.md"), "too large");
        var o = Options();
        o.MaxFileBytes = 2;
        var models = new FakeModels();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(new(), models, o).IndexAsync("note.md", default));
        Assert.Equal(0, models.EmbeddingCalls);
    }

    [Fact]
    public void InvalidVectorsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => EmbeddingValidation.Validate([1, 2], 3));
        Assert.Throws<InvalidOperationException>(() => EmbeddingValidation.Validate([0, 0, 0], 3));
        Assert.Throws<InvalidOperationException>(() => EmbeddingValidation.Validate([1, float.NaN, 0], 3));
    }

    private static FakeStore WithPassage() => new()
    {
        Passages = [new(Guid.NewGuid(), "note.md", "note.md", 0, 0, "evidence", .9)]
    };

    private sealed class FakeModels : IEmbeddingProvider, IChatProvider
    {
        public int EmbeddingCalls;
        public int ChatCalls;
        public int FailOnCall;
        public ModelAnswer Answer = new("Supported answer [1]", false, [1]);

        public Task<float[]> EmbedAsync(string text, bool query, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (++EmbeddingCalls == FailOnCall)
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult<float[]>([1, 0, 0]);
        }

        public Task<ModelAnswer> AnswerAsync(string question, IReadOnlyList<Citation> context, CancellationToken ct)
        {
            ChatCalls++;
            return Task.FromResult(Answer);
        }
    }

    private sealed class FakeStore : IDocumentStore
    {
        public DocumentState? State;
        public int Replacements;
        public int Failures;
        public IReadOnlyList<IndexedChunk> Chunks = [];
        public IReadOnlyList<Passage> Passages = [];

        public Task<DocumentState?> FindAsync(string location, CancellationToken ct) => Task.FromResult(State);

        public Task<Guid> ReplaceAsync(
            string location,
            string filename,
            string hash,
            string profile,
            string? expectedHash,
            IReadOnlyList<IndexedChunk> chunks,
            CancellationToken ct)
        {
            Replacements++;
            Chunks = chunks;
            State = new(State?.Id ?? Guid.NewGuid(), hash, profile);
            return Task.FromResult(State.Id);
        }

        public Task MarkUnchangedAsync(string location, string hash, string profile, CancellationToken ct) => Task.CompletedTask;

        public Task RecordFailureAsync(string location, string filename, CancellationToken ct)
        {
            Failures++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Passage>> SearchAsync(float[] vector, int count, double minimumScore, CancellationToken ct) => Task.FromResult(Passages);

        public Task<IReadOnlyList<DocumentInfo>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DocumentInfo>>([]);
    }
}
