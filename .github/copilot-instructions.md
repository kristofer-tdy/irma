# Copilot Instructions

These directions apply to GitHub Copilot and any AI agent working in this repository.

## Always
- Read the user prompt carefully, ask when requirements are unclear, and confirm assumptions.
- Keep solutions simple; follow existing patterns and only add complexity when justified.
- Never implement custom solutions when an official Microsoft-supported package covers the requirement. Prefer that package, or ask for confirmation before deviating.
- Prefer built-in tools and CLIs to modify project assets (e.g., use `git` for repository changes, `dotnet` for project files) instead of manual edits when those tools exist.
- For .NET and C#, do not directly edit `.csproj` or `.sln` files without explicit user approval. Use the `dotnet` CLI to make changes (e.g., `dotnet add package`, `dotnet remove package`, `dotnet add reference`, `dotnet sln add/remove`, `dotnet new`). Using `dotnet` for these edits is allowed without asking; manual file edits require asking the user first.
- Work in small, reviewable steps and highlight open risks or follow-ups.
- Run `dotnet build` and `dotnet test` before declaring work complete, unless explicitly told otherwise; never leave unverified builds or tests for the user.

## Code
- Primary stack: .NET Framework with C# . Match the current formatting, naming, and idioms.
- Write clear code; add short comments only when intent is not obvious from the code itself.
- Use `/docs` for project context. Create or update `/docs/adr` entries when decisions affect architecture.
- If Remote MCP for Microsoft Docs is available, use it to find relevant information about Microsoft products and services to inform your code and documentation. Can also be used to check for best practices and patterns. Change your code accordingly.
- Make sure to don't fall into the trap of taking first documentation or sample as a true representation of best practices. Always cross-check with multiple sources and official guidelines. Check for the latest updates in case of rapidly evolving technologies.

## Documentation
- Prefer self-explanatory code over generated prose. Only add concise documentation that helps future readers.

## Testing
- Default to TDD: Red → Green → Refactor unless instructed otherwise.
- Cover new logic with unit tests that include positive, negative, and edge scenarios; name tests descriptively.
- Use mocks to isolate external dependencies. Make tests fail first before writing the implementation and show that progression to the user when possible.
