# ADR 0011 — Full-area two-column entry editor

- **Status:** Accepted
- **Date:** 2026-11-24

## Context

The editor opened in the right panel while the tools and list panels stayed visible but disabled.
That looked awkward and left the editor a narrow single column.

## Decision

- While an entry is created or edited, the editor **covers the whole vault area** and replaces the three panels. The title bar, the fake-data banner with Lock, and the status bar stay visible.
- The three panels are hidden (collapsed), not disabled. They return after **Save** or **Cancel**; the selection is kept.
- The form has **two columns**. Left: title, username, email, URL, importance, notes. Right: password with generator, record type tags, security category tags. Validation messages, **Save**, and **Cancel** sit below both columns.
- The editor has its own scrollbars and takes part in the adaptive window growth of [ADR 0010](0010-three-panel-layout-and-adaptive-window.md).
- The Title box gets the focus when the editor opens.
- The view-model is unchanged: the editor is shown when `IsEditing` is true.

## Consequences

- The editor uses the full window width, so the window rarely needs to grow.
- The list and tools are not visible while editing. This is intended; Cancel brings them back.
