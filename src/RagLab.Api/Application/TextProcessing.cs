using System.Security.Cryptography;
using System.Text;

namespace RagLab.Application;

public static class ContentHash
{
    public static string Compute(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

public sealed class TextChunker(RagOptions options)
{
    public IReadOnlyList<TextChunk> Split(string text)
    {
        options.Validate();
        var chunks = new List<TextChunk>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        for (var start = 0; start < text.Length; start += options.ChunkSize - options.ChunkOverlap)
        {
            var length = Math.Min(options.ChunkSize, text.Length - start);
            var value = text.Substring(start, length);
            if (!string.IsNullOrWhiteSpace(value))
            {
                chunks.Add(new(chunks.Count, start, value));
            }

            if (start + length == text.Length)
            {
                break;
            }
        }

        return chunks;
    }
}

public static class EmbeddingValidation
{
    public static void Validate(float[] vector, int dimensions)
    {
        if (vector.Length != dimensions || vector.Any(x => !float.IsFinite(x)) || vector.All(x => x == 0))
        {
            throw new InvalidOperationException($"Embedding must contain {dimensions} finite values and have nonzero magnitude.");
        }
    }
}
