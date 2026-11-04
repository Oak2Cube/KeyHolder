# ADR 0008 — Installer and update mechanism

- **Status:** Proposed (Milestone 6 hardening)
- **Date:** TBD

## Context

Distribution must stay simple while crypto and replica paths are still growing.

## Decision

Until operational hardening: ship via **CI-published framework-dependent build** (and optional self-build).
Delay signed installer / auto-update until after security review.

## Consequences

Users need the .NET 10 Desktop Runtime.
Early milestones have no silent update channel.

## Alternatives considered

- MSIX / signed installer from day one — too early before threat model and release controls exist.
