using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RagLab.Application;
using RagLab.Infrastructure;
using Xunit;

namespace RagLab.IntegrationTests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAGLAB_TEST_POSTGRES")))
        {
            Skip = "Set RAGLAB_TEST_POSTGRES to a real pgvector-enabled PostgreSQL database.";
        }
    }
}

public sealed class EndToEndFactAttribute : FactAttribute
{
    public EndToEndFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RAGLAB_TEST_POSTGRES")) || Environment.GetEnvironmentVariable("RAGLAB_TEST_OLLAMA") != "1")
        {
            Skip = "Requires RAGLAB_TEST_POSTGRES and RAGLAB_TEST_OLLAMA=1, plus both Ollama models.";
        }
    }
}

public sealed class PostgresTests
{
    // Each test uses a unique schema. Never drop a user's database or shared tables.
    private static async Task<(NpgsqlDataSource Db, string Schema)> OpenAsync()
    {
        var connection = Environment.GetEnvironmentVariable("RAGLAB_TEST_POSTGRES")!;
        var schema = "ragtest_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        // Keep the extension outside disposable test schemas.
        await admin.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector WITH SCHEMA public");
        await admin.ExecuteAsync($"CREATE SCHEMA {schema}");
        var builder = new NpgsqlConnectionStringBuilder(connection)
        {
            SearchPath = schema + ",public"
        };
        return (NpgsqlDataSource.Create(builder.ConnectionString), schema);
    }

    private static async Task CloseAsync(NpgsqlDataSource db, string schema)
    {
        await using (var connection = await db.OpenConnectionAsync())
        {
            await connection.ExecuteAsync($"DROP SCHEMA {schema} CASCADE");
        }

        await db.DisposeAsync();
    }

    [DatabaseFact]
    public async Task RetrievalReplacementFailureAndDuplicateBehavior()
    {
        var (db, schema) = await OpenAsync();
        try
        {
            var options = new RagOptions
            {
                Dimensions = 3,
                AllowedRoot = "integration-fixture"
            };
            var store = new PostgresDocumentStore(db, options);
            await store.InitializeAsync(default);
            var profile = options.IndexProfile;
            var id = await store.ReplaceAsync("a.md", "a.md", "v1", profile, null, [new(new(0, 0, "old"), [1, 0, 0])], default);
            await store.ReplaceAsync("b.md", "b.md", "v1", profile, null, [new(new(0, 0, "orthogonal"), [0, 1, 0])], default);
            var result = await store.SearchAsync([1, 0, 0], 5, .5, default);
            Assert.Single(result);
            Assert.Equal(id, result[0].DocumentId);
            Assert.Equal(1, result[0].Score, 6);
            Assert.Empty(await store.SearchAsync([-1, 0, 0], 5, .5, default));
            var duplicate = await store.ReplaceAsync("a.md", "a.md", "v1", profile, "v1", [new(new(0, 0, "old"), [1, 0, 0])], default);
            Assert.Equal(id, duplicate);
            Assert.Equal(1, (await store.ListAsync(default)).Single(d => d.Id == id).ChunkCount);
            // Fail inside the transaction after deleting old chunks: duplicate ordinal violates PK.
            await Assert.ThrowsAsync<PostgresException>(() => store.ReplaceAsync(
                "a.md",
                "a.md",
                "v2",
                profile,
                "v1",
                [new(new(0, 0, "new"), [1, 0, 0]), new(new(0, 1, "duplicate"), [1, 0, 0])],
                default));
            Assert.Equal("v1", (await store.FindAsync("a.md", default))!.ContentHash);
            Assert.Equal("old", (await store.SearchAsync([1, 0, 0], 5, .5, default)).Single().Text);
            await store.RecordFailureAsync("a.md", "a.md", default);
            Assert.Equal("failed", (await store.ListAsync(default)).Single(d => d.Id == id).Status);
            Assert.Single(await store.SearchAsync([1, 0, 0], 5, .5, default));
            await store.ReplaceAsync(
                "a.md",
                "a.md",
                "v2",
                profile,
                "v1",
                [new(new(0, 0, "replacement"), [1, 0, 0])],
                default);
            Assert.Equal("replacement", (await store.SearchAsync([1, 0, 0], 5, .5, default)).Single().Text);
            Assert.Equal(id, (await store.FindAsync("a.md", default))!.Id);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReplaceAsync("a.md", "a.md", "v3", profile, "v1", [], default));
            await store.ReplaceAsync("a.md", "a.md", "empty", profile, "v2", [], default);
            Assert.Empty(await store.SearchAsync([1, 0, 0], 5, .5, default));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SearchAsync([1, 0, 0], 5, .5, canceled.Token));
            var otherModel = new RagOptions
            {
                Dimensions = 3,
                EmbeddingModel = "different",
                AllowedRoot = options.AllowedRoot
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PostgresDocumentStore(db, otherModel).InitializeAsync(default));
        }
        finally
        {
            await CloseAsync(db, schema);
        }
    }

    [EndToEndFact]
    public async Task RealOllamaIngestRetrieveAndAnswer()
    {
        var (db, schema) = await OpenAsync();
        var root = Path.Combine(Path.GetTempPath(), "raglab-e2e-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "manual.md"),
                "# Project Atlas\nProject Atlas retains database backups for exactly 37 days. The backup owner is the Platform team.");
            var options = new RagOptions
            {
                AllowedRoot = root,
                MinimumScore = .3
            };
            var store = new PostgresDocumentStore(db, options);
            await store.InitializeAsync(default);
            using var http = new HttpClient
            {
                BaseAddress = new(options.OllamaUrl),
                Timeout = TimeSpan.FromMinutes(5)
            };
            var model = new OllamaProvider(http, options);
            var rag = new RagService(store, model, model, new(options), options, NullLogger<RagService>.Instance);
            var index = await rag.IndexAsync("manual.md", default);
            Assert.Equal("indexed", index.Status);
            Assert.Equal("unchanged", (await rag.IndexAsync("manual.md", default)).Status);
            var answer = await rag.AskAsync("How many days does Project Atlas retain database backups?", default);
            Assert.False(answer.InsufficientContext);
            Assert.Contains("37", answer.Answer);
            Assert.Contains(answer.Citations, c => c.SourceLocation == "manual.md");
            var unknown = await rag.AskAsync("What is Project Atlas's annual revenue?", default);
            Assert.True(unknown.InsufficientContext);
        }
        finally
        {
            Directory.Delete(root, true);
            await CloseAsync(db, schema);
        }
    }
}
