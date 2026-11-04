# ADR 0003 — Manual transfer; one durable replica; no cloud

- **Status:** Accepted
- **Date:** 2026-11-04

## Context

Users may need the vault on more than one host.
Cloud sync adds keys, network trust, and conflict code.

## Decision

- Move an encrypted SQLite replica **by hand** only.
- Store **one durable replica** next to the app.
- On export: **temp → check → replace** (no second long-lived local replica).
- No Cloudflare R2 or other cloud sync in version 1.
- Keep an **independent off-app copy** for important data.

## Why

Smaller security surface and clearer UX.
Temp+replace keeps export safe without keeping old ciphertext beside the app.

## Not chosen

Latest+backup pair in the app folder; R2/S3 snapshots; Git/OneDrive as the sync engine.
