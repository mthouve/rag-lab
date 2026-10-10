using RagLab.Application;
using RagLab.Infrastructure;
using Xunit;

namespace RagLab.IntegrationTests;

public sealed class OllamaFactAttribute : FactAttribute
{
    public OllamaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RAGLAB_TEST_OLLAMA") != "1")
        {
            Skip = "Requires RAGLAB_TEST_OLLAMA=1 and real Ollama models.";
        }
    }
}

public sealed class OllamaLiveTests
{
    [OllamaFact]
    public async Task RealModelsEmbedAndGenerateGroundedJson()
    {
        var options = new RagOptions();
        using var http = new HttpClient
        {
            BaseAddress = new(options.OllamaUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        var provider = new OllamaProvider(http, options);
        var document = await provider.EmbedAsync("Project Atlas retains backups for exactly 37 days.", false, default);
        var query = await provider.EmbedAsync("How long are Project Atlas backups retained?", true, default);
        EmbeddingValidation.Validate(document, options.Dimensions);
        EmbeddingValidation.Validate(query, options.Dimensions);
        var passage = new Citation(
            1,
            Guid.NewGuid(),
            "manual.md",
            "manual.md",
            0,
            "Project Atlas retains backups for exactly 37 days. The backup owner is the Platform team.",
            .9);
        var answer = await provider.AnswerAsync("How many days are Project Atlas backups retained?", [passage], default);
        Assert.False(answer.InsufficientContext);
        Assert.Contains("37", answer.Answer);
        Assert.Contains(1, answer.CitationIds);
        var unknown = await provider.AnswerAsync("What is Project Atlas's annual revenue?", [passage], default);
        Assert.True(unknown.InsufficientContext);
    }
}
