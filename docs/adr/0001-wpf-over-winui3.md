# ADR 0001 — WPF instead of WinUI 3

- **Status:** Accepted
- **Date:** 2026-10-28
- **Milestone:** 0

## Context

KeyHolder is a personal Windows desktop vault.
The UI must support a locked shell, MVVM, dependency injection, later idle/session locking, and a simple learning path on .NET 10.
The two main UI choices are WPF and WinUI 3.

## Decision

Use **WPF** (`net10.0-windows`, `UseWPF`) for the Desktop project.

## Consequences

**Good**

- Mature tools, docs, and samples for MVVM and Windows desktop apps.
- Works well with Visual Studio 2026 and the `dotnet` CLI used in this repo.
- Enough for a single-user utility.
  Version 1 does not need Store packaging or WinUI islands.

**Limits we accept**

- Default look is older; a modern look needs custom styling.
- Windows only (already a project rule).
- Moving to WinUI 3 / Windows App SDK later would need a migration if Store packaging or WinUI controls become required.

## Alternatives considered

- **WinUI 3:** newer UI stack; more moving parts (Windows App SDK); less learning material for this project’s goals.
- **Avalonia / MAUI:** cross-platform cost is not worth it for a Windows-only WPF design.
