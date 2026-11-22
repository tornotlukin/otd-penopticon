# Changelog

## 0.1.0 — 2026-11-22

Initial release.

- `Penopticon` tool (`ITool`): watches the foreground window and, while a listed app has focus, swaps the driver to a mouse-mode copy of the current settings; restores the original settings when focus moves elsewhere.
- `Mouse Apps` property: comma-, semicolon- or newline-separated process names, case-insensitive, `.exe` optional. Defaults to `Illustrator, InDesign`.
- Mouse-mode conversion: Windows Ink Absolute / Relative output modes → built-in Absolute / Relative modes; Windows Ink and Adaptive bindings → mouse-button bindings; Buttonalicious `Pen Tap` → `Left`. Areas, filters, tools and other bindings are kept.
- Mouse-mode settings are built in memory from the live settings on every switch and never saved to `settings.json`.
- Switches are debounced (150 ms) and deferred while a mouse button is held.
- Settings applied from outside Penopticon are adopted as the new normal settings; re-applies of the mouse-mode settings (e.g. on tablet reconnect) are recognized and left alone.
- If an OpenTabletDriver window was opened during mouse mode, it is resynchronized when normal mode is restored so a later Save cannot persist the mouse-mode copy.
- Targets OpenTabletDriver 0.6.7 / .NET 8, Windows only.
