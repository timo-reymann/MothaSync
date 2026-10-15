# SyncMotha design

SyncMotha is a compact desktop utility.
It copies a source folder (Drive A) to a destination drive (Drive B) with rclone and stays out of the way.
The look is dark, calm and precise.
It should feel trustworthy, with no playful decoration.

## Window

- 640 × 600 px, fixed size, not resizable.
- Headerless: no OS title bar. A custom header sits at the top of every screen.
- Settings, History and run detail open as modal dialogs of the same size, centered over the main window.
- Dialogs close with Escape, with their own Cancel, Save or back controls.
- Corner radius 12–16 px on cards and panels, generous padding (24 px body padding).

## Header

Left to right: logo tile (32 px), app name "SyncMotha" in semibold, a status dot, then a spacer
and 30 px icon buttons: history, settings, a thin divider, then minimize and close.
Minimize only appears on the main window.
Close ends the app on the main window and closes the dialog elsewhere. It turns red on hover.
The button for the screen you are on is highlighted with the orange soft fill.
A 1 px border separates the header from the body.

### Status dot

| State | Color | Meaning |
| --- | --- | --- |
| Idle | slate `#94A3B8` | nothing running, not configured or waiting |
| Running | yellow `#FACC15`, pulsing | sync in progress |
| Done | green `#10B981` | last sync succeeded |
| Error | red `#EF4444` | last sync failed |

Amber `#F59E0B` is reserved for paused and for warning icons. Keep it clearly different from the running yellow.

## Tray icon

The logo also serves as the system tray icon on Windows, macOS and Linux.
Clicking it, or choosing "Open SyncMotha" from its menu, shows and focuses the main window.
"Exit" in the tray menu quits the app.
On Linux the tray needs a StatusNotifier host, which GNOME only provides with the AppIndicator extension.

## Logo

The logo is a friendly mother holding one drive in each hand (`assets/logo.png`,
source in `assets/logo-original.jpg`).
It uses a blue, cream and warm yellow palette on an off-white ground.
The header shows it as a rounded square tile.
The UI accent stays orange, so the logo is the only blue element on screen.

## Color

Dark mode only.

| Token | Value | Use |
| --- | --- | --- |
| `AppBg` | `#282727` | window background |
| `AppCard` | `#343232` | cards, pills, drive cards |
| `AppLine` | `#494646` | borders and dividers |
| `AppText` | `#F1F5F9` | primary text |
| `AppMuted` | `#94A3B8` | secondary text |
| `AppAccent` | `#F97316` | progress bars, arrows, selection highlights, icons |
| `AppAccentHover` | `#FB8A3C` | primary button hover |
| `AppOk` | `#10B981` | success |
| `AppErr` | `#EF4444` | error |
| `AppWarn` | `#F59E0B` | paused, warning icons |
| `AppRun` | `#FACC15` | running status dot |
| `AppLogBg` | `#1F1E1E` | log panel background |

The single orange accent is the only decorative color. Status colors carry meaning and are never used for decoration.

## Typography

- Open Sans for all interface text: regular for body, semibold for titles, labels and primary buttons.
- JetBrains Mono for file paths and log text.
- Sizes: 22 px headings, 16 px dialog titles, 14 px body, 12–13 px secondary text, 11 px log lines,
  10 px uppercase captions with letter spacing.

## Components

- **Drive card**: card surface, orange drive icon, title, path in monospace,
  and a status dot with label on the waiting screen.
  Two sit side by side with the sync arrow between them.
- **Sync arrow**: large orange right arrow with the caption "SYNC DIRECTION".
  It always points from Drive A to Drive B.
- **Selectable drive card** (settings): a clickable card with the role ("Drive A · Source"), icon, path
  and the hint "click to change folder".
  The selected card gets an orange border and a soft orange fill.
- **Progress bar**: 6 px, orange. Red and frozen on failure.
- **Log panel**: bordered panel titled "rclone log" with an "auto-scroll" indicator, monospace 11 px lines,
  timestamps dimmed, ERROR lines red and NOTICE lines amber.
- **Pill**: rounded chip for counts and totals, for example "1284 files". The error variant is tinted red.
- **Buttons**: primary (orange fill, dark text), outline (default), error outline (retry), small and large sizes,
  icon buttons for the header.

## Screens

### Main · not configured

First run, or after the config is cleared.
Centered orange drive icon in a soft circle, the heading "Set up your drives", one line of explanation,
and a large orange **Configure** button that opens Settings.
No other actions are shown.

### Main · waiting

Intro line:
"Waiting for both drives.
Sync starts automatically 3 seconds after both are connected
and writable." Two drive cards with the sync arrow between them.
Drive A shows a green dot and "connected".
Drive B shows a gray dot and "waiting for drive".
There is no manual sync button: the sync always starts by itself once both drives are ready.

### Main · syncing

Status row with a pulsing yellow dot, the label "Syncing…", the current file name truncated from the left,
and a **Pause** button.
Below it a slim orange progress bar with the percentage on the left
and "1284 files · 3.2 GB · ETA 0m 41s" on the right, then the rclone log with auto-scroll on.

### Main · failed

Red banner with a warning icon:
"Sync failed — 1 file could not be copied.
Nothing was deleted from the destination." with a **Retry sync** button.
The progress bar is red and frozen, and the log shows red ERROR lines.

### Main · done

Large green circle with a check, the heading "Sync complete", three pills (files, size, duration),
and the buttons **View history** (orange) and **Back to overview** (outline).

### Settings

Two large clickable drive cards, "Drive A · Source" and "Drive B · Destination",
with the big orange sync arrow between them.
Clicking a card opens a folder picker.
A muted note says files only flow from A to B and that changes on Drive B are overwritten at the next sync.
Below the note is a **Server URL** text field, in monospace with an orange focus border.
It sets where sync metrics and logs are sent, and an empty value keeps them on the device.
**Cancel** and **Save** sit bottom right.

### History

A list of the last 10 sync runs in one bordered group.
Each row has a status dot, date, file count, size, duration and a trailing check or warning icon.
Rows are clickable and open the run detail.

### History · run detail

Back arrow, the run date and its status.
Summary pills for files, size and duration, plus a red "1 error" pill on failed runs.
Below, the full rclone log for that run.

## Behavior notes

- The current build is a UI mock: all data is hardcoded in XAML
  and the only live behavior is opening dialogs (Settings, History, run detail and the folder picker).
- The window shows one main state at a time. The state is chosen in `MainWindow.axaml`.
- Fonts and the logo are bundled as Avalonia resources, so the look is identical on Linux and macOS.
