# Architecture decisions

## 1. Single API assembly with application/infrastructure folders

Chosen to deliver the smallest working Stage 1 pipeline. Interfaces exist only at the database and inference boundaries. Separate class libraries would enforce compile-time layering but add project overhead before there is a second host. Keep infrastructure dependencies out of application classes; extract libraries when a worker actually needs reuse. This is a modular monolith, not a distributed deployment.

## 2. Npgsql + Dapper and explicit SQL

Chosen to make transactions, PostgreSQL types and pgvector search visible and understandable. EF Core is a valid alternative when broader entity lifecycle/migrations justify it. Text vector parameters are explicitly cast in SQL, avoiding an extra mapping package while keeping values parameterized. Stage 1's advisory-locked initializer is suitable for a fresh local schema; production needs versioned migrations and separate schema/application permissions.

## 3. Exact cosine vector search

Chosen for full recall and a small corpus. HNSW/IVFFlat are alternatives for larger workloads, trading recall and operational complexity for speed. Exact search scans candidate vectors, so query cost grows with corpus size. Measure a representative corpus before adding an approximate index. A cosine cutoff must be evaluated per model/data set and is not an answer-confidence probability.

## 4. Character windows with overlap

Chosen for deterministic behavior and no tokenizer dependency. Overlap preserves some context across boundaries; offsets are retained for diagnostics. Token-based, paragraph-aware and source-aware chunkers are alternatives. Character counts do not bound tokens; embedding calls use `truncate=false` and fail on overlong inputs. Chunk profile changes require reindexing. Tune with actual documents before professional use.

## 5. Generate first, publish with one transaction

Chosen so an embedding failure never removes the last searchable version. A failed replacement transaction rolls back metadata and chunks together. This holds generated vectors in memory and makes indexing synchronous; a bounded file size and one in-process ingestion gate constrain local resource use. Transaction advisory locks and expected-hash checks prevent stale cross-process publication. Durable snapshots, queues and retries are later stages, not part of this local design.

## 6. Explicit source selection and loopback-only access

Chosen to prevent accidental disk crawling and keep an unauthenticated MVP local. Relative paths, supported extensions, containment checks, size limits and symlink rejection protect the file boundary. The browser renders model/document content as text. CORS is disabled; browser cross-origin POSTs are rejected. This is not cloud authorization: Entra ID, per-user data access and hardening are required before external exposure. Avoid letting untrusted users mutate the allowlisted folder during ingestion; path validation cannot fully eliminate filesystem race conditions.

## 7. Model-independent interfaces and index compatibility guard

Chosen so later Foundry adapters can replace Ollama without changing the RAG workflow. Database metadata records model name/dimensions/root; startup fails on incompatibility rather than searching mixed vector spaces. Same-dimensional models are not interchangeable. Ollama tag names do not pin weight revisions; changing weights under a tag still requires a deliberate full reindex. A migration must handle vector column dimensions and any future vector indexes.

## 8. Structured answer citations with conservative fallback

Chosen to distinguish cited evidence from retrieval diagnostics and reject nonexistent source IDs. Empty retrieval, explicit insufficiency, malformed JSON and unknown or inconsistent citation markers fall back to insufficient information. Prompting alone or free-form citation parsing are weaker alternatives. These checks validate structure, not factual support; evaluate real model answers and adversarial documents before relying on this assistant for consequential decisions.
