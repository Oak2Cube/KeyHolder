# ADR 0005 — Whole-vault replacement (no field merge)

- **Status:** Proposed
- **Date:** TBD

## Context

Importing a replica could merge fields or replace the local vault.

## Decision

Imports and restores **replace the whole local vault** after authentication and staged checks.
No field-level merging between two vaults.

## Consequences

Conflict model stays simple and matches one-writer intent.
Editing on two hosts at the same time is out of scope for v1.
The user chooses one full vault or cancels.

## Alternatives considered

- CRDT / field merge — much more complexity for a personal vault.
