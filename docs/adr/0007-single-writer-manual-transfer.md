# ADR 0007 — Single-writer manual transfer

- **Status:** Proposed
- **Date:** TBD

## Context

Multi-host use invites conflicts if two devices edit at once.

## Decision

v1 assumes **one primary writing host** at a time:

1. Edit on one host.
2. Export the durable replica.
3. Move the file by hand.
4. Import on the other host before editing there.

No background sync.
Concurrent writers are not supported.

## Consequences

Conflicts show up as two different replica files from two hosts.
The UI asks the user to choose one full vault.
UX stays understandable for a learning project.

## Alternatives considered

- Automatic multi-writer sync — delayed for a long time / maybe never.
