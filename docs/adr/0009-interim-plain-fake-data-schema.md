# ADR 0009 — Interim plain-column schema for fake data (Milestone 1)

- **Status:** Accepted (Milestone 1 only; superseded by the envelope schema in Milestone 2)
- **Date:** 2026-11-08

## Context

Milestone 1 builds the local vault, search, filter, sort, and locking before any cryptography exists.
The storage still has to survive restart, run in transactions, and not decide the later crypto format.

## Decision

Store entries in SQLite with **plain columns in two groups**:

- metadata group: `title`, `username`, `email`, `url`, `d_tags`, `s_tags`, `importance`, `metadata_revision`
- secret group: `password`, `notes`, `secret_revision`

Tags are JSON text arrays.
Schema versions use `PRAGMA user_version` with transactional, ordered migrations.
The UI only sees `IEntryRepository`, and list reads return summaries without the secret group.
`GetSecretAsync` reads the secret group of exactly one entry.
Every successful change sets `replica_state.local_changed_since_replica` in the same transaction.

The database file is `%LOCALAPPDATA%\KeyHolder\working.sqlite`.
It holds **fake demo data only**, and the app shows a permanent warning banner.

## Consequences

- Milestone 2 replaces the two column groups with `metadata_envelope` and `secret_envelope` in a new migration.
  Repository interfaces, view models, search, filter, and sort do not change.
- Unlock has no master password in this milestone.
  Idle and Windows-session locking still clear all in-memory vault state.
- Real credentials must not be entered until Milestone 2 is implemented and reviewed.

## Alternatives considered

- One flat table without a metadata/secret split — would force a second redesign in Milestone 2.
- In-memory only — would not prove restart persistence or the migration system.
