# ADR 0004 — Whole-vault replace; one writer

- **Status:** Proposed
- **Date:** TBD

## Context

Two hosts may both change data.
Merge logic is complex and easy to get wrong.

## Decision

- Import **replaces the whole vault** after checks (no field merge).
- **One writing host** at a time: edit → export → hand copy → import → then edit elsewhere.

## Why

Simple conflicts: keep both files and let the user pick one full vault, or cancel.
Timestamps do not pick a winner.

## Not chosen

CRDT / field merge; automatic multi-writer sync.
