# RAG Lab architecture

RAG Lab is a personal knowledge assistant. It uses a modular ASP.NET Core application, PostgreSQL with pgvector, and Ollama for local inference.

## Ingestion and updates

Only explicitly selected Markdown and UTF-8 text files inside the configured AllowedRoot directory are indexed. Each document has a stable internal UUID and a source location relative to that folder. SHA-256 detects unchanged document contents. The indexing profile also captures the embedding model, dimensions, chunk size, and overlap.

Text is split into overlapping character windows. The default chunk size is 1400 characters with 200 characters of overlap. Ollama's nomic-embed-text model generates 768-dimensional embeddings. The document and all its chunks are replaced in one PostgreSQL transaction after every embedding has been generated. If an update fails, the last successful version remains searchable. New and stale chunks are never mixed for a document.

## Question answering

The question is embedded with the same model as the documents. PostgreSQL computes cosine distance using pgvector. The API retrieves up to five passages that meet the minimum similarity score, then sends them to llama3.2:3b through Ollama. Answers include citations pointing to retrieved document passages. Empty retrieval and invalid model citation IDs result in an insufficient-information response. Retrieved passages are untrusted data, not instructions.

## Local development

Docker Compose starts PostgreSQL on loopback port 5432. Ollama runs on port 11434. The API and minimal browser interface run on http://127.0.0.1:5080. Credentials come from environment variables or .NET user secrets. Stage 1 has no cloud deployment or public authentication.
