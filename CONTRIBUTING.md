# Contributing to Glyph

Thanks for your interest in contributing to Glyph! We welcome code contributions, bug reports, documentation improvements, and patches.

## How to contribute

- Open an issue to discuss larger changes before implementing.
- Fork the repository and create a feature branch from `master`.
- Keep changes small and focused; one pull request per logical change.
- Add tests where applicable and ensure `dotnet build` succeeds.

## Code style

- Follow existing C# conventions used in the repo.
- Run `dotnet format` where applicable and keep diffs minimal.

## Pull request checklist

- The branch builds cleanly: `dotnet build`.
- Relevant tests added and passing (if present).
- Clear description of the change and rationale in the PR body.
- Update `docs/` or README if behavior or usage changed.

## Using Agents for Large Tasks

If you're working on a large refactor, cross-codebase analysis, or complex research task, see [docs/agent-patterns.md](docs/agent-patterns.md) for guidance on using agents effectively to explore, analyze, and plan changes. Agents can accelerate:

- Finding all usages of a type or pattern across the codebase
- Identifying architectural boundaries and dependency chains
- Auditing performance or correctness properties
- Proposing refactoring strategies with evidence

## Communications

- Be respectful and constructive in comments and PR descriptions.

Thanks — we appreciate your help!
