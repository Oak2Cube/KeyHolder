# ADR 0010 — Three-panel vault layout and adaptive window

- **Status:** Accepted
- **Date:** 2026-11-24

## Context

The unlocked view had two panels: tools plus entry list on the left, details or editor on the right.
The tools (Search, Sort, Filter) and the list competed for the same height.
The window had a fixed size, so content was either cut off or the window had to be made tall by hand.

## Decision

### Three panels

The unlocked view has three panels side by side:

1. **Tools panel (narrow):** the "+ New entry" button and the Search, Sort, and Filter expanders.
   It has its own vertical scrollbar. It never scrolls horizontally; long labels are trimmed.
2. **Entry list panel (narrow):** only the entry list, with its own scrollbar.
   The "N of M entries" text is not shown here; the status bar keeps it.
3. **Details panel (wide):** takes the remaining width.
   It has its own vertical and horizontal scrollbars, shown only when needed.

While an entry is being edited, the editor replaces all three panels; see [ADR 0011](0011-full-area-two-column-editor.md).

### Adaptive window (grow only)

- The window **grows** (width and height) when the tools panel or the details/editor panel needs more room
  to show its content without scrollbars.
- Growth stops at the **work area of the monitor the window is on** (taskbar excluded).
  A grown window that would cross a screen edge is moved back inside.
- Only when the screen does not allow more room do scrollbars appear, in the panel that overflows.
- The window **never shrinks by itself**. The user can still resize it by hand.
  Growth is skipped while the window is maximized or minimized.
- Manual resizing always wins. The user can drag the window down to its minimum size (900 x 600) even when the
  content is larger; the panels then show scrollbars instead of clipping.
  Growth reacts only to a change of the **content** size (for example opening an expander or the editor),
  never to a change of the window size, and it is suspended while the user drags a window edge.
  After a manual resize, the next content change grows the window again, horizontally and vertically.
- Only the tools and details/editor panels drive growth. The entry list can hold hundreds of entries,
  so it always scrolls on its own and never makes the window grow.
- The rule is implemented as a small pure function (`AutoGrowWindow.Compute`) that is unit-tested,
  and a hook that reads the scroll viewers' extent and viewport sizes (`VaultView`).

### Themed scrollbars

All scrollbars use one app-wide implicit style in `App.xaml`: thin, no arrow buttons,
a rounded thumb in the card border color that turns lighter on hover and uses the accent color while dragging.
The scroll viewer corner uses the card color, so the default light square never appears.

## Consequences

- No view model changes; the layout is view-only.
- Scrollbar and window-growth behavior is visual and is checked with the manual smoke checklist in the README.
- If the user shrinks the window below the content size, scrollbars appear at once; the window grows again
  only when the content changes.

## Alternatives considered

- `SizeToContent="WidthAndHeight"`: WPF turns it off as soon as the user resizes the window, and it also
  shrinks the window, which was not wanted.
- Letting the entry list drive growth: a long list would always make the window as large as the screen.
