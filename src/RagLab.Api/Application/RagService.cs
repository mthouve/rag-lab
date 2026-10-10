using System.Text;
using System.Text.RegularExpressions;

namespace RagLab.Application;

public sealed class RagService(
    IDocumentStore store,
    IEmbeddingProvider embeddings,
    IChatProvider chat,
    TextChunker chunker,
    RagOptions options,
    ILogger<RagService> logger)
{
    private readonly SemaphoreSlim ingestionGate = new(1, 1);
    public const string InsufficientAnswer = "There is insufficient information in the indexed documents to answer this question.";

    public async Task<IndexResult> IndexAsync(string relativePath, CancellationToken ct)
    {
        await ingestionGate.WaitAsync(ct);
        try
        {
            return await IndexCoreAsync(relativePath, ct);
        }
        finally
        {
            ingestionGate.Release();
        }
    }

    private async Task<IndexResult> IndexCoreAsync(string relativePath, CancellationToken ct)
    {
        var path = ResolveAllowedFile(relativePath);
        var filename = Path.GetFileName(path);
        var location = Path.GetRelativePath(options.AllowedRoot, path).Replace('\\', '/');
        if (OperatingSystem.IsWindows())
        {
            location = location.ToLowerInvariant();
        }

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new FileNotFoundException("Allowed document was not found.");
            }

            if (info.Length > options.MaxFileBytes)
            {
                throw new ArgumentException("Document exceeds MaxFileBytes.");
            }

            // Bound the read even if a file grows between the length check and reading.
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            var bytes = new byte[checked((int)options.MaxFileBytes + 1)];
            var read = 0;
            while (read < bytes.Length)
            {
                var count = await stream.ReadAsync(bytes.AsMemory(read), ct);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            if (read > options.MaxFileBytes)
            {
                throw new ArgumentException("Document exceeds MaxFileBytes.");
            }

            var text = new UTF8Encoding(false, true).GetString(bytes, 0, read).TrimStart('\uFEFF');
            var hash = ContentHash.Compute(text);
            var existing = await store.FindAsync(location, ct);
            if (existing?.ContentHash == hash && existing.IndexProfile == options.IndexProfile)
            {
                await store.MarkUnchangedAsync(location, hash, options.IndexProfile, ct);
                return new(existing.Id, filename, "unchanged", 0);
            }

            var chunks = chunker.Split(text);
            var indexed = new List<IndexedChunk>();
            foreach (var chunk in chunks)
            {
                var vector = await embeddings.EmbedAsync(chunk.Text, false, ct);
                EmbeddingValidation.Validate(vector, options.Dimensions);
                indexed.Add(new(chunk, vector));
            }

            var id = await store.ReplaceAsync(location, filename, hash, options.IndexProfile, existing?.ContentHash, indexed, ct);
            logger.LogInformation(
                "Indexed {SourceLocation} as {DocumentId}: {ChunkCount} chunks",
                location,
                id,
                chunks.Count);
            return new(id, filename, "indexed", chunks.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Indexing failed for {SourceLocation}; previous chunks retained", location);
            try
            {
                await store.RecordFailureAsync(location, filename, ct);
            }
            catch (Exception failure)
            {
                logger.LogWarning(failure, "Could not persist indexing failure");
            }

            throw;
        }
    }

    public string ResolveAllowedFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("Supply a relative .md or .txt filename under AllowedRoot.");
        }

        var root = Path.GetFullPath(options.AllowedRoot);
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison) || Path.GetExtension(path).ToLowerInvariant() is not (".md" or ".txt"))
        {
            throw new ArgumentException("Only .md and .txt files within AllowedRoot can be indexed.");
        }

        // Reject symlinks/junctions anywhere along the selected path, including the root's ancestors.
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Symbolic links and junctions are not allowed in document paths.");
            }
        }

        return path;
    }

    public async Task<AnswerResult> AskAsync(string question, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question) || question.Length > 2000)
        {
            throw new ArgumentException("Question must contain 1â€“2000 characters.");
        }

        var vector = await embeddings.EmbedAsync(question, true, ct);
        EmbeddingValidation.Validate(vector, options.Dimensions);
        var passages = await store.SearchAsync(vector, options.TopK, options.MinimumScore, ct);
        var retrieved = passages.Select((p, i) => new Citation(i + 1, p.DocumentId, p.Filename, p.SourceLocation, p.Ordinal, p.Text, p.Score)).ToArray();
        if (retrieved.Length == 0)
        {
            return new(InsufficientAnswer, true, [], retrieved);
        }

        var answer = await chat.AnswerAsync(question, retrieved, ct);
        var ids = answer.CitationIds ?? [];
        var markers = Regex.Matches(answer.Answer ?? "", @"\[(\d+)\]").Select(m => int.TryParse(m.Groups[1].Value, out var id) ? id : -1).Distinct().ToArray();
        if (answer.InsufficientContext || string.IsNullOrWhiteSpace(answer.Answer) || ids.Length == 0 || ids.Any(id => !retrieved.Any(c => c.Id == id)) || markers.Any(id => !ids.Contains(id)))
        {
            return new(InsufficientAnswer, true, [], retrieved);
        }

        var citations = retrieved.Where(c => ids.Contains(c.Id)).ToArray();
        logger.LogInformation(
            "Answered question using {RetrievedCount} passages, {CitationCount} citations",
            retrieved.Length,
            citations.Length);
        var missingMarkers = ids.Distinct().Where(id => !markers.Contains(id)).Select(id => $"[{id}]");
        var formattedAnswer = answer.Answer + (missingMarkers.Any() ? " " + string.Join(" ", missingMarkers) : "");
        return new(formattedAnswer, false, citations, retrieved);
    }
}
