# RAG Lab — HTML, CSS, and Browser JavaScript Standards

Applies to the plain browser interface, including embedded CSS and JavaScript in `src/RagLab.Api/wwwroot/index.html`. Also follow the [shared engineering standards](README.md). React-specific conventions remain in [TypeScript/React](typescript.md).

## Design priorities

- Make asking questions, reading answers, and checking sources the primary workflow.
- Keep document indexing available without letting setup controls dominate the answer area.
- Use a clean, restrained appearance: readable typography, generous spacing, subtle borders, and one primary accent color.
- Prefer native browser controls and a small amount of CSS over introducing a UI framework for Stage 1.
- Show useful content and state clearly; avoid decorative animations, unnecessary navigation, and implementation details that do not help the user.

## Document structure and formatting

- Use two-space indentation for HTML, CSS, and browser JavaScript; no tabs or minified source.
- Include explicit `<!doctype html>`, `<html lang="en">`, `<head>`, and `<body>` elements, with charset, viewport, and a descriptive title.
- Put structural elements on separate lines. Break long attributes and nested markup into readable blocks; aim for roughly 120 characters per line.
- Use semantic elements such as `<header>`, `<main>`, `<section>`, and `<form>` where they describe the content. Give sections descriptive headings and keep heading levels consistent.
- Use classes for styling and descriptive IDs for labels, accessibility relationships, and JavaScript hooks.
- Use CSS for spacing rather than `<br>` elements or repeated empty markup.
- Keep UTF-8, consistent line endings, no trailing whitespace, and a final newline.
- Embedded `<style>` and `<script>` blocks are acceptable for the small MVP. Move them into separate static files when their size or reuse makes the document difficult to navigate.

## Visual consistency and responsive layout

- Define recurring colors, spacing, border radii, and content widths as CSS custom properties. Reuse them rather than adding slightly different values for each component.
- Use a system font stack and comfortable body line height. Keep text legible at browser zoom and avoid unnecessarily small source metadata.
- Use a centered, bounded reading area on wide screens and fluid widths with modest padding on narrow screens.
- Allow long filenames, paths, source passages, and error messages to wrap. Avoid page-wide horizontal scrolling.
- Keep a clear hierarchy: page title, section heading, label, content, then secondary metadata.
- Give primary actions a consistent appearance. Distinguish loading, disabled, hover, and keyboard-focus states without relying only on color or opacity.
- Respect reduced-motion preferences if motion is introduced. The MVP needs no animation.

## Accessibility and forms

- Associate every input with a visible `<label>`. Placeholder text supplements a label; it does not replace one.
- Associate help text and field errors with inputs using `aria-describedby`; mark invalid inputs with `aria-invalid` when appropriate.
- Use native buttons and form submission so keyboard interaction works naturally. Specify button types explicitly.
- Preserve visible focus indicators and a logical tab order. Do not move focus unexpectedly when an asynchronous response arrives.
- Announce concise asynchronous status with `role="status"` or a polite live region. Avoid having multiple live regions announce the same answer or large passage collections.
- Provide accessible names and adequate contrast for controls and text. Never communicate an error or result state through color alone.
- Use native `<details>`/`<summary>` for optional diagnostics. Keep essential answer and source information visible without opening diagnostics.

## Answer, citation, and document presentation

- Visually separate the answer, supporting citations, and retrieval diagnostics.
- Preserve answer and passage line breaks. Display model and document content as untrusted text.
- Make citation markers match their source entries. Show a readable source location and the supporting passage so users can inspect evidence.
- Label retrieval scores as similarity scores; do not present them as confidence percentages or guarantees of correctness.
- Keep chunk ordinals, scores, and other technical details secondary or inside diagnostics.
- Show an explicit insufficient-information state when the API cannot support an answer. Do not style it as an application failure.
- Make document lists readable as separate rows or items, rather than depending on newline characters inside a normal `<div>`.
- Distinguish an indexed document from an unchanged document and an indexing failure. If the previous version remains searchable after a failure, explain that state when the API exposes it.

## Browser JavaScript and request states

- Use descriptive names such as `loadDocuments`, `renderCitations`, and `handleQuestionSubmit`; avoid opaque shorthand in application logic.
- Use standard JavaScript braces, explicit control-statement blocks, and readable multiline functions, object literals, and calls.
- Keep request handling, DOM rendering, and form event handlers distinguishable. Extract helpers when they reduce duplication; avoid speculative component abstractions.
- Handle idle, loading, empty, success, insufficient-information, and failure states explicitly.
- Disable the relevant submit button while its request is in flight, give a short progress message, and restore controls in `finally`.
- Prevent stale responses from replacing newer results if concurrent requests are introduced; support cancellation when it meaningfully helps the user.
- Handle non-success HTTP responses and malformed or unexpected response bodies with an actionable message. Do not assume every response contains valid JSON.
- Use `textContent` and DOM element creation for document/model output. Do not insert that output with `innerHTML`, inline event attributes, or executable code.
- Keep credentials and API keys out of HTML and JavaScript, and avoid logging sensitive question/document text.

## Verification and tooling

- Let `.editorconfig` define indentation and file hygiene. If Prettier is adopted, commit its configuration and use its output consistently rather than hand-formatting against it.
- Check HTML structure and browser JavaScript syntax after changes. Verify behavior in the browser for meaningful interface changes.
- Exercise keyboard navigation, narrow-screen layout, long source paths, loading states, empty results, insufficient information, and request failures as appropriate to the change.
- Check that displayed citation markers agree with returned sources and that retrieved text is rendered safely.
- Do not add a frontend build pipeline or React solely to format the Stage 1 page. Those remain separate implementation decisions.

## Current interface alignment

The Stage 1 page was reshaped on 2026-10-10 with readable source, explicit document landmarks and section headings, shared CSS tokens, focus styles, document-list rows, and robust request-error parsing. Questions and answers appear first; document browsing and indexing are secondary disclosures. Model and source content continue to render as plain text.

Browser checks covered a real cited answer, loading feedback, unchanged-document indexing, a missing-file error, an insufficient-information answer, keyboard navigation from the question to its submit button, and a 390-pixel viewport with no horizontal overflow. This is targeted interface verification, not a comprehensive accessibility audit. Empty-corpus and malformed-response states are implemented but were not separately exercised against a modified server.
