# README.md

## Project overview

A portfolio management app: a .NET 10 Clean Architecture API (`src/`) backed by PostgreSQL, with a separate
Next.js frontend (`frontend/`). 

## Conventions (from `.cursorrules`)

- Target C# 12 / .NET 10 (despite `Directory.Build.props` defaulting to net10.0.
- Primary constructors for DI; prefer `record` for immutable data.
- Favor explicit typing — only use `var` when the type is evident from the right-hand side.
- Types are `internal sealed` by default unless there's a reason otherwise.
- Prefer `Guid` for identifiers unless otherwise specified.
- Use `is null` / `is not null` instead of `== null` / `!= null`.
- Prefer endpoint classes (`IEndpoint`) over controllers for new endpoints;


