# ADR 0002 — Per-entry encryption envelopes

- **Status:** Proposed (Milestone 2)
- **Date:** TBD

## Context

Data must stay secret in the working DB and in the durable replica.

## Decision

Encrypt **per entry** with `metadata_envelope` and `secret_envelope` (AES-256-GCM), not only full-file encryption.

## Why

After unlock, search/filter/sort can use metadata without unlocking passwords/notes.
Reveal/Copy touch only the selected secret envelope.

## Not chosen

Full-file encryption only — simpler, but all-or-nothing unlock.
