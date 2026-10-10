# Stage 1 verification record

Date: 2026-10-10. **Stage 1 end-to-end acceptance verified for the supplied sample corpus.**

## Repository and environment

The existing repository is `C:\Repos\GitHub\mthouve\rag-lab`, originally at commit `1f8a653`. It initially contained a README, Visual Studio `.gitignore`, `.gitattributes` and MIT LICENSE. There was no prototype to reuse. Existing Git history/configuration and license were preserved; no new repository was created.

Prerequisites installed with user authorization: .NET SDK 10.0.401 alongside .NET 9; Ollama 0.40.2; Docker Desktop 4.94.0 per user. Docker CLI 29.8.2 and Compose 5.5.1 are available. The user installed WSL with `--no-distribution`; WSL 3.0.1.0 was verified and `docker-desktop` runs under WSL 2. Ubuntu is intentionally absent.

Official Docker and Ollama installer signatures were verified. Docker subsequently failed to locate its launcher registry key. The official installer would not repair the current version. Restoring the missing HKCU `Software\Docker Inc.\Docker Desktop` key with `InstallLocation` pointing to the existing verified binaries resolved the launcher error, and the Docker engine responded successfully. No Docker data was deleted. See [Windows setup](windows-setup.md) for the repair record.

Local PostgreSQL credentials were randomly generated in the ignored `.env` file and stored in .NET user secrets for the API. Credentials were not committed. The Docker credential-helper PATH issue in the pre-installation terminal was resolved by adding Docker's bin directory to that process's PATH.

## Executed checks and results

| Check | Result |
| --- | --- |
| NuGet restore with pinned package versions | Passed |
| .NET 10 build | Passed: zero warnings/errors |
| Unit tests | 28 passed, zero failed/skipped |
| Real PostgreSQL integration test | Passed |
| Real Ollama embedding/chat integration test | Passed |
| Real PostgreSQL + Ollama ingestion/retrieval/answer integration test | Passed |
| Docker Compose configuration and startup | Passed; PostgreSQL healthy |
| Default `dotnet run --project src/RagLab.Api` launch profile | Passed; Development user secrets loaded |
| `scripts/Verify-Stage1.ps1` through real HTTP endpoints | Passed |
| Browser JavaScript syntax and PowerShell script syntax | Passed |
| `git diff --check` | Passed |

The final full test suite used the permanently installed SDK, with `RAGLAB_TEST_POSTGRES` and `RAGLAB_TEST_OLLAMA=1`. **31 tests passed, none failed or skipped.**

Unit coverage includes chunk overlap/reconstruction, empty input, invalid settings, known SHA-256 digest, allowlist rejection, unchanged detection, chunk-profile changes, embedding failures, empty replacement, file-size bounds, vector validation, cancellation, empty retrieval, insufficient evidence, citation validation/marker normalization, provider request shapes, HTTP errors and malformed model JSON.

The database test executes real cosine retrieval, score filtering, duplicate processing, transaction rollback after a failed insert, retention of the previous searchable version, replacement without stale chunks, stable IDs, rejection of stale writes, empty replacement, cancellation and incompatible-model startup rejection. Each database integration test creates and drops its own schema, leaving the application's index intact.

The real-model tests verify 768-dimensional embeddings, the supported 37-day backup-retention answer, source references, and refusal of an unsupported revenue question. An initial end-to-end run exposed the model omitting inline markers despite valid citation IDs. The API now adds missing markers from validated IDs while rejecting unknown/inconsistent IDs; a regression test and the real end-to-end test pass. A development launch profile also ensures the documented run command loads local user secrets.

## HTTP acceptance demonstration

The smoke script indexed `rag-overview.md` and `postgresql-notes.txt`, then verified unchanged-document detection. The active index contains two documents and three chunks.

Question: "How does RAG Lab preserve the previous index when an update fails?"

Actual answer: "The last successful version of a document remains searchable in case of an update failure. New and stale chunks are never mixed for a document. [1]"

Citation `[1]` identified `rag-overview.md`, chunk 0, with cosine similarity approximately 0.700. Retrieval diagnostics also included the PostgreSQL notes and the second overview chunk. The unsupported Project Atlas revenue question returned insufficient information. This establishes a real HTTP pipeline through Ollama embeddings, PostgreSQL pgvector retrieval and chat inference, rather than compilation alone.

Both models ran 100% on the RTX 4070 Laptop GPU with the chat context set to 8192. NVIDIA reported roughly 4141 MiB total GPU memory use during the earlier model check, within the available 8188 MiB.

## Limits of this verification

This is a small sample-corpus check, not a general retrieval/answer accuracy benchmark. Browser JavaScript syntax and the HTTP workflow were verified; interactive browser clicks and visual layout were not separately automated. Multi-process contention under load, adversarial prompt-injection robustness, model weight changes under a tag and production migration/security operations remain unverified. They are not claimed as complete.

Stage 2 has not been started. The next recommended task is to select a small allowlisted set of real technical notes and evaluate retrieved passages and citation support.
