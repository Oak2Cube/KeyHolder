# ADR 0005 — Installer and updates

- **Status:** Proposed (Milestone 6)
- **Date:** TBD

## Context

Distribution should stay simple until crypto and replica flows are solid.

## Decision

Ship a CI **framework-dependent** build first.
Delay signed installer / auto-update until after security review.

## Why

Users need the .NET 10 Desktop Runtime.
Early milestones do not need a silent update channel.

## Not chosen

MSIX / signed installer from day one.
