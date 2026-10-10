# Project and build XML conventions

Apply these rules to `.csproj`, `.props`, `.targets`, and NuGet configuration files.

- Use two spaces per indentation level, with each element on its own line.
- Keep short attributes on the opening element; use a space before `/>` on empty elements.
- Keep related properties together in a `PropertyGroup` and related references together in an `ItemGroup`.
- Separate project references and package references into different groups when it improves readability.
- Keep shared build settings in `Directory.Build.props`; keep project-specific settings in the project file.
- Preserve package versions, reference paths, conditions, property values, and element order during formatting. MSBuild evaluation can depend on order.
- Add comments only to explain a non-obvious build constraint or decision.
- Avoid introducing build targets, dependencies, or abstractions solely for formatting.

For formatting-only changes, verify that parsed XML content is unchanged. Build the solution when changing build behavior.
