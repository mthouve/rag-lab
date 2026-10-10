# RAG Lab — C# Coding Standards

Applies to C# and ASP.NET Core code. Also follow [shared engineering standards](README.md).

## Layout and formatting

- Use four spaces for indentation; no tabs.
- Use Allman braces: opening braces on a separate line.
- Use braces for `if`, `else`, loops, and other control statements, even with one statement.
- Leave one blank line between method declarations and between other members when it improves visual separation.
- Leave blank lines between top-level type declarations; prefer one primary public type per file.
- Put every attribute on its own line immediately above its target declaration, including controller, method, property, and test attributes. Never combine multiple attributes inside the same pair of brackets.
- Use file-scoped namespaces and organize `using` directives consistently.
- Prefer readable multiline argument lists, initializers, and fluent calls when they become hard to scan.
- Aim for approximately 120 characters per line without imposing awkward wrapping.
- Use UTF-8, consistent line endings, no trailing whitespace, and a final newline.

```csharp
[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DocumentResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _documentService.GetAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        return Ok(document);
    }
}
```

## Names and types

- Use `PascalCase` for types, methods, properties, constants, and public members.
- Use `camelCase` for parameters and local variables, `_camelCase` for private instance fields, and an `I` prefix for interfaces.
- Suffix asynchronous methods with `Async` when appropriate.
- Prefer clear names such as `documentRepository` over opaque abbreviations.
- Enable nullable reference types; model absence explicitly rather than routinely using `!`.
- Use `var` when the type is apparent from the right-hand side; specify a type when it improves comprehension.

## Classes, methods, and dependencies

- Prefer ordinary constructors for services with several dependencies or initialization rules. Primary constructors are acceptable for small, clear types; don't force either style everywhere.
- Prefer constructor injection over service location.
- Keep orchestration at the application layer; controllers should not call PostgreSQL or the embedding provider directly.
- Use expression-bodied members for simple properties or operations, not multi-step business logic.
- Use early returns to reduce nesting, but don't fragment straightforward logic into unnecessary helpers.
- Choose records for genuine data-shaped models; do not use them indiscriminately.

## Async, persistence, and failures

- Use async APIs for I/O and pass `CancellationToken` through relevant calls.
- Avoid `.Result`, `.Wait()`, and `async void` (except event handlers).
- Parameterize SQL and format longer statements vertically. Use transactions for atomic updates such as replacing a document's chunks.
- Catch exceptions when recovery, translation, or additional context is useful. Never catch-and-ignore.
- Use structured logging, such as `logger.LogInformation("Indexed {DocumentId}", documentId)`, without exposing sensitive text or credentials.

## Tests and documentation

- Prefer descriptive test names and Arrange / Act / Assert structure.
- Use one separate attribute per line, including `[Fact]`, `[Theory]`, and `[InlineData(...)]`.
- Test behavior and important failure cases; use real PostgreSQL in integration tests for vector-search behavior.
- Write comments about constraints and intent, not obvious statements. Use XML docs when a public API's semantics need explanation.
