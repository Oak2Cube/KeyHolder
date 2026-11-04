# ADR 0002 — Per-entry encryption envelopes

- **Status:** Proposed (accept in Milestone 2)
- **Date:** TBD

## Context

Vault data must stay secret at rest in the working SQLite database and in replica files.
Options: encrypt the whole DB/file only, or encrypt each entry (or each envelope) separately.

## Decision

Encrypt **per entry** with separate `metadata_envelope` and `secret_envelope` (AES-256-GCM).
Do not rely only on full-file encryption.

Metadata includes title, username, email, URL, `d-tags`, `s-tags`, and optional `importance`.
Secrets include password and notes.

## Consequences

After unlock, search, filter, and sort can use unlocked metadata without unlocking passwords and notes.
Reveal/Copy touch only the selected entry’s secret envelope.
Build and test cost is higher than one big encrypted blob.

## Alternatives considered

- Full-file/DB encryption only — simpler, but forces all-or-nothing unlock and weaker least privilege for secrets at rest.
