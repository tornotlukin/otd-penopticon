# Penopticon

An [OpenTabletDriver](https://opentabletdriver.net/) plugin that makes your pen act as a **plain mouse while chosen apps have focus**, and restores your normal pen setup (Windows Ink, pressure and all) everywhere else.

Built and tested against OpenTabletDriver 0.6.7 on Windows 11.

## Why this exists

`Windows Ink Absolute Mode` (from the `Windows Ink` plugin) is what gives Photoshop and other painting apps pressure and tilt. But Windows Ink is system-wide: every app that reads Windows pen input receives pen events, not just the ones you want.

Some apps behave badly when they get pen input. In Adobe Illustrator 2027, holding Ctrl / Alt / Shift while using the pen tools freezes the canvas until a physical mouse moves, at which point every queued input lands at once. InDesign has similar trouble. Both work perfectly with plain mouse input.

OpenTabletDriver has no per-app profiles, so before Penopticon the choice was all-or-nothing: Windows Ink everywhere (pressure in Photoshop, broken Illustrator) or mouse everywhere (working Illustrator, no pressure). Penopticon watches which window is in front and switches for you.

## How it works

1. Penopticon checks the foreground window every 50 ms and looks up the process that owns it.
2. When that process is on your **Mouse Apps** list, it builds a mouse-mode copy of your current settings and applies it:
   - `Windows Ink Absolute Mode` → `Absolute Mode` (and `Windows Ink Relative Mode` → `Relative Mode`)
   - Windows Ink tip / eraser bindings → left click
   - Windows Ink pen-button and `Adaptive Binding` buttons → mouse buttons (`Button 1` → Left, `Button 2` → Right, `Button 3` → Middle)
   - [Buttonalicious](https://github.com/tornotlukin/otd-buttonalicious) `Pen Tap` bindings → `Left`, keeping their timing
   - Everything else (tablet and display areas, filters, keyboard bindings, other tools) is left exactly as it was.
3. When focus moves to any other app, your original settings are put back.

The mouse-mode copy only ever exists in memory: it's never written to `settings.json`, and you never maintain a second profile. If you change your settings in the OTD window, the next switch picks up the change automatically.

Switches wait until no mouse button is held, so a stroke is never cut in half, and they're debounced so alt-tabbing past a listed app doesn't flicker.

## Install

**Option A — through OpenTabletDriver's Plugin Manager (recommended):**

1. Plugins menu → **Open Plugin Manager** → **Use alternate source...**
2. Owner `tornotlukin`, Name `otd-plugins`, Ref empty → confirm.
3. Select **Penopticon** → **Install**.

See [otd-plugins](https://github.com/tornotlukin/otd-plugins) for details. The alternate source replaces the official list until you reopen the Plugin Manager.

**Option B — manual install:**

1. Download the latest release ZIP from the [Releases](../../releases) page.
2. Plugin Manager → **Install plugin...** → pick the ZIP.

Or extract the ZIP yourself into `%LOCALAPPDATA%\OpenTabletDriver\Plugins\Penopticon\` (portable installs: `<OTD folder>\userdata\Plugins\Penopticon\`) and restart OpenTabletDriver.

## Setup

1. Open OpenTabletDriver → **Tools** tab → enable **Penopticon**.
2. Fill in **Mouse Apps** (defaults to `Illustrator, InDesign`).
3. Click **Apply**, then **Save**.

Your Output Mode should stay on `Windows Ink Absolute Mode`. Penopticon only does anything when your normal settings use a Windows Ink output mode; if they're already in a mouse mode, there's nothing to switch.

### The Mouse Apps list

Process names separated by commas, for example:

```
Illustrator, InDesign, Acrobat
```

- Use the program's **process name**: open the program, then find it in Task Manager → **Details** → Name column.
- `.exe` is optional and case doesn't matter: `Illustrator`, `illustrator.exe` and `ILLUSTRATOR` are the same entry.
- Semicolons and new lines also work as separators.

Matching is by process name, not install path, so app updates that install to a new folder (Illustrator 2026 → 2027) keep working without edits. Any program with the same file name matches too.

Apply and Save after editing. Saving from the OTD window always stores your normal settings, never the mouse-mode copy.

## Checking that it works

OpenTabletDriver's log (Console tab, or `OpenTabletDriver.Console log`) shows a line per switch:

```
[Penopticon:Info]  Mouse mode for: Illustrator, InDesign
[Penopticon:Info]  Illustrator focused: mouse mode
[Penopticon:Info]  explorer focused: normal mode
```

## Notes and limitations

- **Windows only.**
- **Floating panels hosted by another process.** Some apps show dialogs or panels from a helper process with a different name. If the pen switches back to Windows Ink inside such a panel, find the helper's name in Task Manager and add it to the list.
- **Applying settings restarts tools.** OpenTabletDriver re-creates every tool plugin whenever settings are applied, so each switch also restarts other enabled tools.
- **Settings changes made elsewhere win.** If settings are applied from somewhere other than Penopticon (the OTD window, a Preset Binding, the console) while a listed app has focus, Penopticon adopts them as your new normal settings and rebuilds mouse mode from them.
- **Tied to the OpenTabletDriver version.** Penopticon applies settings through OpenTabletDriver's daemon interface, which is not part of the stable plugin API. Each release targets one driver version (see `SupportedDriverVersion` in `metadata.json`) and may need a rebuild for newer drivers.

## Building from source

Requires the .NET 8 SDK and Git.

```powershell
pwsh tools/build-otd-reference.ps1 -Tag v0.6.7
dotnet build -c Release
```

`build-otd-reference.ps1` builds OpenTabletDriver's own `OpenTabletDriver.Desktop` and `OpenTabletDriver` assemblies from the matching source tag into `lib/`. They're compile-time references only: the official release ships them inside single-file executables, and at runtime the plugin binds to the copies already loaded by the daemon. Only `Penopticon.dll` ships.

## License

GPL-3.0-only. See [LICENSE](LICENSE).
