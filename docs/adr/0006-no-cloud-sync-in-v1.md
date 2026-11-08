# ADR 0006 — No cloud sync in version 1

- **Status:** Accepted
- **Date:** 2026-11-04
- **Milestone:** docs update for version 1

## Context

Earlier designs considered private object storage for remote snapshots.
That path needs credentials, conditional updates, and conflict handling.

## Decision

Version 1 has **no cloud sync**.
No Cloudflare R2, no Worker/D1, and no automatic remote upload/download.
Replicas move only as local encrypted SQLite files.

## Consequences

Smaller security and operations surface for the first usable product.
Remote freshness is not automatic; the user moves files when needed.
A later milestone may revisit cloud storage only after local export/import is solid.

## Alternatives considered

- Direct bucket credentials in Windows Credential Manager — not used in v1.
- Public or shared sync folders as the source of truth — rejected for unclear conflict rules.
