# ADR 0003 — Manual encrypted SQLite replicas

- **Status:** Accepted
- **Date:** 2026-11-04
- **Milestone:** docs update for version 1

## Context

Users may need the same vault on more than one Windows host.
A cloud sync service adds credentials, network trust, and conflict rules.
Version 1 should stay simple and offline-first.

## Decision

Move an **encrypted SQLite replica** between hosts **only by hand**.
There is no Cloudflare R2 (or other cloud object sync) in version 1.

## Consequences

**Good**

- No cloud API keys or remote object protocol.
- User sees every transfer.
- Works fully offline.

**Limits we accept**

- The user must copy files carefully.
- Two hosts that both edit without exporting/importing can diverge; version 1 does not merge them.

## Alternatives considered

- Cloudflare R2 / S3 snapshots — deferred; too much protocol and conflict work for v1.
- Git or folder sync (OneDrive/Dropbox) as the engine — unclear “latest” state and merge risk.
