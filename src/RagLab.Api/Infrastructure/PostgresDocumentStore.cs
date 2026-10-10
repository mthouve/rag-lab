using System.Globalization;
using Dapper;
using Npgsql;
using RagLab.Application;

namespace RagLab.Infrastructure;

public sealed class PostgresDocumentStore(NpgsqlDataSource dataSource, RagOptions options) : IDocumentStore
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        options.Validate();
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_xact_lock(76498321)", transaction: tx, cancellationToken: ct));
        await db.ExecuteAsync(new CommandDefinition(
            """
            CREATE EXTENSION IF NOT EXISTS vector;
            CREATE TABLE IF NOT EXISTS rag_settings (
                singleton boolean PRIMARY KEY DEFAULT true CHECK (singleton),
                dimensions integer NOT NULL, embedding_model text NOT NULL, source_root text NOT NULL);
            """,
            transaction: tx,
            cancellationToken: ct));
        var settings = await db.QuerySingleOrDefaultAsync<Settings>(new CommandDefinition(
            "SELECT dimensions AS \"Dimensions\", embedding_model AS \"EmbeddingModel\", source_root AS \"SourceRoot\" FROM rag_settings",
            transaction: tx,
            cancellationToken: ct));
        if (settings is not null && (settings.Dimensions != options.Dimensions || settings.EmbeddingModel != options.EmbeddingModel || settings.SourceRoot != options.AllowedRoot))
        {
            throw new InvalidOperationException("Index configuration differs from the database. Use a new database or explicitly migrate and reindex; never mix embedding models or source roots.");
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO rag_settings(singleton, dimensions, embedding_model, source_root)
            VALUES(true, @Dimensions, @EmbeddingModel, @AllowedRoot) ON CONFLICT DO NOTHING;
            CREATE TABLE IF NOT EXISTS documents (
                id uuid PRIMARY KEY, source_type text NOT NULL DEFAULT 'local', external_source_id text,
                filename text NOT NULL, source_location text NOT NULL UNIQUE, content_hash text NOT NULL,
                source_revision text, index_profile text NOT NULL, last_indexed_at timestamptz,
                status text NOT NULL CHECK(status IN ('indexed','failed','deleted')), last_error text);
            """,
            options,
            tx,
            cancellationToken: ct));
        // Dimensions are validated integers, never request input. Vector values below remain SQL parameters.
        await db.ExecuteAsync(new CommandDefinition(
            $"""
            CREATE TABLE IF NOT EXISTS chunks (
                document_id uuid NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
                ordinal integer NOT NULL, start_offset integer NOT NULL, content text NOT NULL,
                embedding vector({options.Dimensions}) NOT NULL,
                PRIMARY KEY(document_id, ordinal));
            """,
            transaction: tx,
            cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<DocumentState?> FindAsync(string location, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        return await db.QuerySingleOrDefaultAsync<DocumentState>(new CommandDefinition(
            """
            SELECT id AS "Id", content_hash AS "ContentHash", index_profile AS "IndexProfile"
            FROM documents WHERE source_location = @location AND last_indexed_at IS NOT NULL AND status <> 'deleted'
            """,
            new
            {
                location
            },
            cancellationToken: ct));
    }

    public async Task<Guid> ReplaceAsync(
        string location,
        string filename,
        string hash,
        string profile,
        string? expectedHash,
        IReadOnlyList<IndexedChunk> chunks,
        CancellationToken ct)
    {
        foreach (var chunk in chunks)
        {
            EmbeddingValidation.Validate(chunk.Embedding, options.Dimensions);
        }

        await using var db = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        await LockAsync(db, tx, location, ct);
        var current = await db.QuerySingleOrDefaultAsync<DocumentState>(new CommandDefinition(
            """
            SELECT id AS "Id", content_hash AS "ContentHash", index_profile AS "IndexProfile"
            FROM documents WHERE source_location = @location FOR UPDATE
            """,
            new
            {
                location
            },
            tx,
            cancellationToken: ct));
        if (current?.ContentHash == hash && current.IndexProfile == profile)
        {
            await db.ExecuteAsync(new CommandDefinition(
                "UPDATE documents SET status='indexed', last_error=NULL WHERE id=@id",
                new
                {
                    id = current.Id
                },
                tx,
                cancellationToken: ct));
            await tx.CommitAsync(ct);
            return current.Id;
        }

        var previousHash = current?.ContentHash;
        if (previousHash == "")
        {
            previousHash = null; // A failed first attempt has no active version.
        }

        if (previousHash != expectedHash)
        {
            throw new InvalidOperationException("Another process updated this document. Retry indexing the current file.");
        }

        var id = current?.Id ?? Guid.NewGuid();
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO documents(id, filename, source_location, content_hash, index_profile, last_indexed_at, status)
            VALUES(@id, @filename, @location, @hash, @profile, now(), 'indexed')
            ON CONFLICT(source_location) DO UPDATE SET filename=EXCLUDED.filename,
                content_hash=EXCLUDED.content_hash, index_profile=EXCLUDED.index_profile,
                last_indexed_at=EXCLUDED.last_indexed_at, status='indexed', last_error=NULL;
            DELETE FROM chunks WHERE document_id=@id;
            """,
            new
            {
                id,
                filename,
                location,
                hash,
                profile
            },
            tx,
            cancellationToken: ct));
        foreach (var chunk in chunks)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO chunks(document_id, ordinal, start_offset, content, embedding)
                VALUES(@id, @ordinal, @start, @text, CAST(@vector AS vector))
                """,
                new
                {
                    id,
                    ordinal = chunk.Chunk.Ordinal,
                    start = chunk.Chunk.Start,
                    text = chunk.Chunk.Text,
                    vector = VectorLiteral(chunk.Embedding)
                },
                tx,
                cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
        return id;
    }

    public async Task MarkUnchangedAsync(string location, string hash, string profile, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE documents SET status='indexed', last_error=NULL
            WHERE source_location=@location AND content_hash=@hash AND index_profile=@profile AND status='failed'
            """,
            new
            {
                location,
                hash,
                profile
            },
            cancellationToken: ct));
    }

    public async Task RecordFailureAsync(string location, string filename, CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO documents(id, filename, source_location, content_hash, index_profile, status, last_error)
            VALUES(@id, @filename, @location, '', '', 'failed', 'Indexing failed; see server logs.')
            ON CONFLICT(source_location) DO UPDATE SET status='failed', last_error=EXCLUDED.last_error
            """,
            new
            {
                id = Guid.NewGuid(),
                filename,
                location
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Passage>> SearchAsync(float[] vector, int count, double minimumScore, CancellationToken ct)
    {
        EmbeddingValidation.Validate(vector, options.Dimensions);
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<Passage>(new CommandDefinition(
            """
            SELECT d.id AS "DocumentId", d.filename AS "Filename", d.source_location AS "SourceLocation",
                c.ordinal AS "Ordinal", c.start_offset AS "Start", c.content AS "Text",
                1 - (c.embedding <=> CAST(@vector AS vector)) AS "Score"
            FROM chunks c JOIN documents d ON d.id=c.document_id
            WHERE d.status <> 'deleted' AND d.last_indexed_at IS NOT NULL
              AND 1 - (c.embedding <=> CAST(@vector AS vector)) >= @minimumScore
            ORDER BY c.embedding <=> CAST(@vector AS vector), d.id, c.ordinal LIMIT @count
            """,
            new
            {
                vector = VectorLiteral(vector),
                count,
                minimumScore
            },
            cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<IReadOnlyList<DocumentInfo>> ListAsync(CancellationToken ct)
    {
        await using var db = await dataSource.OpenConnectionAsync(ct);
        var rows = await db.QueryAsync<DocumentInfo>(new CommandDefinition(
            """
            SELECT d.id AS "Id", d.filename AS "Filename", d.source_location AS "SourceLocation",
                d.content_hash AS "ContentHash", d.last_indexed_at AS "LastIndexedAt", d.status AS "Status",
                d.last_error AS "LastError", count(c.ordinal)::integer AS "ChunkCount"
            FROM documents d LEFT JOIN chunks c ON c.document_id=d.id GROUP BY d.id ORDER BY d.source_location
            """,
            cancellationToken: ct));
        return rows.ToArray();
    }

    private static Task LockAsync(NpgsqlConnection db, NpgsqlTransaction tx, string location, CancellationToken ct) => db.ExecuteAsync(new CommandDefinition(
        "SELECT pg_advisory_xact_lock(hashtextextended(@location, 0))",
        new
        {
            location
        },
        tx,
        cancellationToken: ct));

    private static string VectorLiteral(float[] vector) => "[" + string.Join(",", vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";

    private sealed record Settings(int Dimensions, string EmbeddingModel, string SourceRoot);
}
