# RAG Lab — Shared Engineering Standards

These rules apply to all languages and components. See [C#](csharp.md), [HTML/CSS/browser JavaScript](html.md), [project/build XML](msbuild.md), and [TypeScript/React](typescript.md) for component-specific rules.

## Priorities

1. Optimize for human readability and maintainability rather than minimum line count.
2. Follow established language conventions and format like clear official documentation examples.
3. Keep changes focused; avoid unnecessary abstractions, speculative features, and dependencies.
4. Preserve consistent formatting in files you edit. Prefer automated formatting over manual style debates.
5. Design around observable, testable behavior and clear boundaries between application logic and external systems.

## Structure and documentation

- Use descriptive names and avoid unexplained abbreviations.
- Keep classes, modules, and methods cohesive; use guard clauses where they clarify control flow.
- Separate logically distinct sections with whitespace; don't add blank lines mechanically inside tightly related statements.
- Explain *why* with comments, including non-obvious constraints and tradeoffs. Don't narrate obvious statements.
- Document public contracts and important architectural decisions, without duplicating code in prose.
- Prefer explicit integration contracts and dependency injection for genuine external boundaries.

## Reliability and security

- Validate external inputs at appropriate boundaries.
- Propagate cancellation where supported; avoid blocking async flows.
- Handle errors at meaningful boundaries and provide actionable, structured logs.
- Never commit secrets, access tokens, local personal documents, production exports, or vector indexes.
- Treat ingested content as untrusted data, not instructions.
- Make document indexing idempotent; don't expose half-indexed revisions to queries.
- Prefer deterministic tests and add integration tests for behavior involving actual databases or services.

## Repository guidance

- Apply `csharp.md` to `.cs` files, `html.md` to the plain browser interface and its CSS/JavaScript, and `typescript.md` to `.ts`/`.tsx` files.
- Let `.editorconfig`, C# formatters/analyzers, Prettier, and ESLint enforce machine-checkable conventions.
- A formatter's established output takes precedence over arbitrary manual line-wrapping preferences.
- If a local convention conflicts with a rule here, follow the existing convention for a small change and call out the inconsistency instead of reformatting unrelated code.
- Keep production project documentation out of folders used as example documents for RAG ingestion.
