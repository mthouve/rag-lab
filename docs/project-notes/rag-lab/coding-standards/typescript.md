# RAG Lab — TypeScript and React Coding Standards

Applies to `.ts` and `.tsx` files. Also follow [shared engineering standards](README.md).

## Formatting

- Use two-space indentation, no tabs.
- Use standard JavaScript/TypeScript braces with opening braces on the declaration line.
- Leave one blank line between function/component declarations and distinct logical sections.
- Favor readable, documentation-style examples over compact one-liners.
- Break long object literals, calls, and JSX props across lines when readability improves.
- Use Prettier as the formatting authority; choose and document its settings once rather than hand-formatting against it.
- Use UTF-8, a final newline, and no trailing whitespace.

```tsx
interface SearchFormProps {
  onSearch: (question: string) => Promise<void>;
}

export function SearchForm({ onSearch }: SearchFormProps) {
  const [question, setQuestion] = useState("");

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!question.trim()) {
      return;
    }

    await onSearch(question.trim());
  }

  return (
    <form onSubmit={handleSubmit}>
      <label htmlFor="question">Question</label>
      <input
        id="question"
        value={question}
        onChange={(event) => setQuestion(event.target.value)}
      />
      <button type="submit">Search</button>
    </form>
  );
}
```

## Naming and types

- Use `PascalCase` for React components, classes, types, and interfaces; `camelCase` for functions, variables, and fields.
- Use descriptive event handler names such as `handleSubmit` and callback prop names such as `onSearch`.
- Enable TypeScript `strict` mode and avoid `any`. Use `unknown` for untrusted values and narrow deliberately.
- Define types for API request/response contracts and component props. Prefer discriminated unions for states with mutually exclusive cases.
- Prefer named exports for reusable modules; follow established conventions when modifying existing files.

## React component practices

- Use function components and hooks appropriately.
- Keep rendering concerns separate from API request implementation where that improves testability.
- Do not add state for values that can be derived from existing props/state.
- Use effects only for external synchronization; avoid using effects to compute derived values.
- Handle loading, empty, success, and error states explicitly.
- Ensure forms are labeled, keyboard accessible, and provide feedback for errors.
- Avoid gratuitous memoization and broad component abstractions before there is a real need.

## Networking and security

- Centralize API calls and handle HTTP failures, cancellation, and response validation where appropriate.
- Never place API keys or server secrets in frontend code or `VITE_` environment variables.
- Treat model-generated content and retrieved documents as untrusted text; do not render them as unsanitized HTML.
- Avoid logging sensitive documents or tokens to the browser console.

## Testing and tooling

- Use ESLint to catch correctness and React hook problems; Prettier for formatting.
- Write meaningful tests for user behavior, not component implementation details.
- Mock network boundaries in UI tests and retain integration tests for actual backend behavior.
- Keep comments focused on intent and non-obvious constraints rather than narrating JSX.
