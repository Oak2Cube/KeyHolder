# ADR 0012 — Built-in entry icons

- **Status:** Accepted
- **Date:** 2026-11-27

## Context

List and detail rows were text-only. Users need a quick visual cue for an entry without uploading custom images.

## Decision

- Each entry has one built-in **icon** and one **icon color**, both metadata fields (Milestone 2: inside `metadata_envelope`).
- Icons are vector `Geometry` paths shipped with the app (`IconCatalog`). Users cannot upload their own icons.
- Icon color reuses the existing tag palette (Default + 9 colors = 10).
- `EntryIcon.Default` is the key icon and is not stored (NULL in SQLite). Unknown stored names fall back to Default.
- UI: 40×40 icons before the three-line list text and before the details title; the create/edit form has an icon picker and color dropdown after the tag editors.
- Schema migration 3 adds nullable `icon` and `icon_color` columns on `entry`.

## Consequences

- Existing vaults upgrade cleanly and show the default key icon.
- A small fixed set of icons must be maintained in code; new icons are a code change, not a user upload.
