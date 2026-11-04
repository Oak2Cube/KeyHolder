# ADR 0001 — WPF instead of WinUI 3

- **Status:** Accepted
- **Date:** 2026-10-28

## Context

Need a Windows desktop UI with MVVM, DI, and a simple learning path on .NET 10.

## Decision

Use **WPF** (`net10.0-windows`).

## Why

Mature tools and docs.
Enough for a personal utility.
WinUI 3 adds Windows App SDK complexity.
Cross-platform UI stacks are not needed for a Windows-only app.
