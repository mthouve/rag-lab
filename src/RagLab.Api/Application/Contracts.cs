namespace RagLab.Application;

public sealed class RagOptions
{
    public string AllowedRoot { get; set; } = "../..";
    public int ChunkSize { get; set; } = 1400;
    public int ChunkOverlap { get; set; } = 200;
    public int Dimensions { get; set; } = 768;
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.2:3b";
    public string OllamaUrl { get; set; } = "http://localhost:11434/";
    public int TopK { get; set; } = 5;
    public double MinimumScore { get; set; } = 0.45;
    public long MaxFileBytes { get; set; } = 1048576;
    public string IndexProfile => $"{EmbeddingModel}:{Dimensions}:chars-v1:{ChunkSize}:{ChunkOverlap}";

    public void Validate()
    {
        if (ChunkSize < 100 || ChunkSize > 8000 || ChunkOverlap < 0 || ChunkOverlap >= ChunkSize || Dimensions < 1 || Dimensions > 2000 || TopK < 1 || TopK > 10 || !double.IsFinite(MinimumScore) || MinimumScore < -1 || MinimumScore > 1 || (MaxFileBytes < 1 || MaxFileBytes > 16777216) || string.IsNullOrWhiteSpace(EmbeddingModel) || string.IsNullOrWhiteSpace(ChatModel) || !Uri.TryCreate(OllamaUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Invalid RAG configuration; check chunking, models, dimensions and retrieval limits.");
        }
    }
}

public record TextChunk(int Ordinal, int Start, string Text);
public record IndexedChunk(TextChunk Chunk, float[] Embedding);
public record DocumentState(Guid Id, string ContentHash, string IndexProfile);
public record DocumentInfo(
    Guid Id,
    string Filename,
    string SourceLocation,
    string ContentHash,
    DateTime? LastIndexedAt,
    string Status,
    string? LastError,
    int ChunkCount);
public record Passage(
    Guid DocumentId,
    string Filename,
    string SourceLocation,
    int Ordinal,
    int Start,
    string Text,
    double Score);
public record Citation(
    int Id,
    Guid DocumentId,
    string Filename,
    string SourceLocation,
    int ChunkOrdinal,
    string Text,
    double Score);
public record AnswerResult(
    string Answer,
    bool InsufficientContext,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<Citation> Retrieved);
public record ModelAnswer(string Answer, bool InsufficientContext, int[] CitationIds);
public record IndexResult(Guid DocumentId, string Filename, string Status, int ChunkCount);
public interface IEmbeddingProvider
{
    Task<float[]> EmbedAsync(string text, bool query, CancellationToken ct);
}

public interface IChatProvider
{
    Task<ModelAnswer> AnswerAsync(string question, IReadOnlyList<Citation> context, CancellationToken ct);
}

public interface IDocumentStore
{
    Task<DocumentState?> FindAsync(string location, CancellationToken ct);
    Task<Guid> ReplaceAsync(
        string location,
        string filename,
        string hash,
        string profile,
        string? expectedHash,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken ct);
    Task MarkUnchangedAsync(string location, string hash, string profile, CancellationToken ct);
    Task RecordFailureAsync(string location, string filename, CancellationToken ct);
    Task<IReadOnlyList<Passage>> SearchAsync(float[] vector, int count, double minimumScore, CancellationToken ct);
    Task<IReadOnlyList<DocumentInfo>> ListAsync(CancellationToken ct);
}
