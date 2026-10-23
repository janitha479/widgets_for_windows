# WidgetTools

A single, lightweight Windows tray app that hosts several always-on-top desktop
widgets. Turn each one on or off from the tray menu — only what you enable runs,
and your choice is remembered between launches.

Each widget is a frameless, transparent overlay that **collapses to a small
badge** and **expands to a detailed card on hover**, so your desktop stays clean
until you glance at it.

## Widgets

| Widget | Collapsed | On hover |
|--------|-----------|----------|
| **Music** | A spinning album disk with an audio-reactive ring | A "now playing" card — art, title/artist, prev · play/pause · next, seekable progress bar |
| **System monitor** | A CPU ring badge | CPU and RAM as filled rings with the live percentage in the center |
| **Battery** | A charge ring with the percentage | Charge ring + state plus voltage, power draw, current, and time to full/empty |

- **Music** drives the Windows System Media Transport Controls (SMTC) — the same
  session your keyboard's media keys control — so it works with Spotify, browser
  playback, and more, with no login or API keys.
- **System monitor** reads CPU load via `GetSystemTimes` and memory via
  `GlobalMemoryStatusEx` (plain Win32 — no perf-counter setup).
- **Battery** reads `root\wmi` (`BatteryStatus` + `BatteryFullChargedCapacity`)
  for telemetry Windows doesn't show anywhere else.

Every overlay is draggable, each ring/bar shifts **green → amber → red** as it
fills, and each card has its own ✕ to hide that widget.

## Requirements

- Windows 10 (build 19041+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download) with the **.NET Desktop**
  workload (to build). A published self-contained build needs no .NET installed
  to *run*.

## Build & run

```bash
dotnet run
```

The app has no main window — it lives in the system tray. **Right-click the tray
icon** to toggle widgets on/off or to exit.

## Ship it as a single .exe

Self-contained (runs on any Windows PC, no .NET needed — larger file):

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Framework-dependent (small file; needs the .NET 8 runtime installed):

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The `.exe` lands in `bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`.
It's a GUI app, so double-clicking it runs with **no console window**.

### Start automatically at login

Press `Win+R`, type `shell:startup`, and drop a shortcut to `WidgetTools.exe`
into that folder.

## Settings

Enabled widgets are stored at:

```
%AppData%\DesktopWidgets\config.json
```

Delete this file to reset to defaults (music on, the rest off).

## Architecture

The app is a small shell that loads independent widget modules. Adding a widget
is one line — everything else is wiring the shell already provides.

| File | Role |
|------|------|
| `Widgets/IWidget.cs` | The contract each widget implements: `Id`, `Name`, `Show()`, `Hide()`, `Closed`. |
| `Widgets/WidgetHost.cs` | The tray icon + checkbox menu; shows/hides widgets and persists the choice. **Register new widgets in `BuildWidgets()`.** |
| `Widgets/WidgetConfig.cs` | Loads/saves which widgets are enabled. |
| `Widgets/Ui.cs` | Shared helpers: progress-ring arc geometry and the expand/collapse animation. |
| `Widgets/MusicWidget.cs` · `MainWindow.xaml(.cs)` | Music overlay + its host wrapper. |
| `Widgets/SystemMonitorWidget.cs` · `SystemMonitorWindow.xaml(.cs)` | CPU/RAM overlay. |
| `Widgets/BatteryWidget.cs` · `BatteryWindow.xaml(.cs)` · `BatteryReader.cs` | Battery overlay + WMI reader. |
| `MediaController.cs` | SMTC: play/pause/next/prev, metadata, timeline, seek. |
| `AudioReactor.cs` | WASAPI loopback capture + FFT → ring band levels. |

### Add your own widget

1. Create a `Window` for the overlay (copy `SystemMonitorWindow` for the
   collapse/expand pattern).
2. Create a `YourWidget : IWidget` wrapper (copy `SystemMonitorWidget`).
3. Add `new YourWidget(),` to `BuildWidgets()` in `WidgetHost.cs`.

That's it — it shows up in the tray menu with its own on/off toggle.

## Tech

C# · WPF (per-widget transparent overlays) · WinForms `NotifyIcon` (tray) ·
[NAudio](https://github.com/naudio/NAudio) (audio capture) · `System.Management`
(battery WMI).
