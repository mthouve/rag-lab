# Learning notes

## Embeddings and vector spaces

An embedding model maps text to a numeric vector whose geometry captures learned similarity. It does not store a factual answer or guarantee relevant retrieval. Use the same model for both passages and questions, with the task prefixes expected by that model. `nomic-embed-text` normally returns 768 dimensions. The API validates actual responses rather than trusting configuration. Equal vector lengths do not imply compatible geometry.

## PostgreSQL + pgvector

pgvector is a PostgreSQL extension, not a separate vector database. Chunks and metadata participate in normal database transactions. `<=>` computes cosine distance; `1 - distance` is cosine similarity, theoretically between -1 and 1. A score of 0.8 does not mean 80% confidence. Exact retrieval provides full recall for the chosen metric; approximate indexes require separate recall/performance decisions. See the [pgvector documentation](https://github.com/pgvector/pgvector).

Npgsql is the ADO.NET provider; its data source pools connections. Dapper maps parameterized SQL results to records. PostgreSQL `ON CONFLICT` supports atomic upserts. Advisory transaction locks serialize cooperating writers per source key and are automatically released on commit/rollback. The application uses MVCC transaction visibility so readers do not see a document half replaced.

## Retrieval versus generation

Retrieval chooses passages. Generation synthesizes an answer from those passages. A capable chat model cannot recover evidence absent from retrieval. Inspect `retrieved` to diagnose the selected passages and scores separately from the answer's `citations`. Increase/decrease a threshold using labeled questions rather than treating the threshold as universal.

Ollama's [embedding API](https://docs.ollama.com/api/embed) accepts text and returns vectors; disabling truncation makes oversized embedding inputs fail visibly. Its [chat API](https://docs.ollama.com/api/chat) supports non-streaming JSON output. JSON formatting is a structural aid, not proof that an answer is correct. Citation IDs must refer to supplied passages. The API adds missing inline markers from validated IDs and rejects unknown or inconsistent markers.

## Idempotency and failure safety

Idempotency means retrying the same source version does not create duplicate chunks or repeatedly generate embeddings. A hash plus indexing profile detects this version. An atomic replacement means all chunks of a new document version become active together. These are different properties: hash checks alone do not make a multi-step replacement safe. Keep the old committed version available while producing a new version.

## Local versus professional operation

A local schema owner can install extensions and create tables. In a hosted environment, separate privileged migrations from normal data access. Local loopback binding is a development boundary, not identity-based authorization. Production also needs durable job state, provider/model revision management, retrieval evaluation, backups and monitoring; those remain later stages.
