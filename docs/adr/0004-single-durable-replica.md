# ADR 0004 — One durable replica next to the app

- **Status:** Accepted
- **Date:** 2026-11-04
- **Milestone:** docs update for version 1

## Context

Each hosted app needs a safe export for hand transfer and recovery.
Keeping two durable files (`latest` and `backup`) adds retention risk: more ciphertext on disk and older deleted data in the second file.
Export still must not destroy the only good copy if it fails halfway.

## Decision

Store **one durable encrypted SQLite replica** next to the application (beside the working vault).

During export only:

1. Write a **temporary** replica file.
2. Open and check it.
3. **Replace** the durable replica with that temp file in one safe step (atomic move when the OS allows).
4. Delete leftover temp files.

Do not keep a second long-lived replica file in the app folder.

True disaster recovery uses an **independent copy outside the app folder** (for example an encrypted USB drive).

## Consequences

**Good**

- Less retention of old ciphertext next to the app.
- Simpler mental model: working DB + one replica file.
- Export stays safe if temp + replace is done correctly.

**Limits we accept**

- If the durable replica is lost and the working DB is also lost, recovery needs the independent off-app copy.
- After a password change, any old copies the user kept elsewhere may still use the old wrapping.

## Alternatives considered

- Latest + backup pair in the app folder — better local undo, more retention and confusion.
- Many versioned snapshot files — more storage and UI complexity than v1 needs.
