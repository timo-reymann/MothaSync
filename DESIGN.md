# MothaSync design

MothaSync is a compact desktop utility.
It copies a source folder (Drive A) to a destination drive (Drive B) with rclone and stays out of the way.
The look is dark, calm and precise.
It should feel trustworthy, with no playful decoration.

## Window

- 700 × 700 px, fixed size, not resizable.
- Headerless: no OS title bar. A custom header sits at the top of every screen.
- Settings, History and run detail update the view within the main window rather than opening separate popup windows
  over it.
- Views navigate back with Escape, or with their own Cancel, Save or back controls.
- Corner radius 12–16 px on cards and panels, generous padding (24 px body padding).

## Header

Left to right: logo (48 px), app name "MothaSync" in semibold, a status dot, then a spacer
and 30 px icon buttons: history, settings, a thin divider, then minimize and close.
The status dot shows the app's current status on every screen, including Settings and History.
Minimize minimizes the main window.
Close ends the app. It turns red on hover.
The button for the screen you are on is highlighted with the orange soft fill.
A 1 px border separates the header from the body.

### Status dot

| State   | Color                     | Meaning                                    |
|---------|---------------------------|--------------------------------------------|
| Idle    | slate `#94A3B8`           | nothing running, not configured or in settings |
| Waiting | yellow `#FACC15`          | waiting for drives, pre-sync in progress   |
| Running | blue `#3B82F6`, pulsing   | sync in progress                           |
| Done    | green `#10B981`           | ready (both drives connected), last sync succeeded |
| Error   | red `#EF4444`             | last sync failed                           |

Amber `#F59E0B` is reserved for paused and for warning icons. Keep it clearly different from the waiting yellow.

## Tray icon

The tray icon is the logo's face with a status overlay, on Windows, macOS and Linux:

| Status  | Icon      | Overlay                 |
|---------|-----------|-------------------------|
| Idle (not configured) | `idle.png`    | gear                    |
| Waiting for drives    | `waiting.png` | dashed grey arrows      |
| Syncing               | `syncing.png` | green sync arrows       |
| Paused                | `paused.png`  | pause bars and check    |
| Ok                    | `ok.png`      | green check             |
| Error                 | `error.png`   | red cross               |

Clicking it, or choosing "Open MothaSync" from its menu, shows and focuses the main window.
"Exit" in the tray menu quits the app.
On Linux the tray needs a StatusNotifier host, which GNOME only provides with the AppIndicator extension.

## Logo

The logo is a friendly pink mother figure holding one drive in each hand (`assets/logo.png`,
source sheet in `assets/logo-original.jpg`), with a transparent background.
It uses a pink, magenta and cream palette.
The header shows it directly on the dark surface, without a tile.

## Color

Dark mode only.

| Token            | Value     | Use                                                |
|------------------|-----------|----------------------------------------------------|
| `AppBg`          | `#282727` | window background                                  |
| `AppCard`        | `#343232` | cards, pills, drive cards                          |
| `AppLine`        | `#494646` | borders and dividers                               |
| `AppText`        | `#F1F5F9` | primary text                                       |
| `AppMuted`       | `#94A3B8` | secondary text                                     |
| `AppAccent`      | `#F97316` | progress bars, arrows, selection highlights, icons |
| `AppAccentHover` | `#FB8A3C` | primary button hover                               |
| `AppOk`          | `#10B981` | success                                            |
| `AppErr`         | `#EF4444` | error                                              |
| `AppWarn`        | `#F59E0B` | paused, warning icons                              |
| `AppWaiting`     | `#FACC15` | waiting status dot                                 |
| `AppSync`        | `#3B82F6` | running status dot                                 |
| `AppIdle`        | `#94A3B8` | idle status dot                                    |
| `AppLogBg`       | `#1F1E1E` | log panel background                               |

The single orange accent is the only decorative color. Status colors carry meaning and are never used for decoration.

## Typography

- Open Sans for all interface text: regular for body, semibold for titles, labels and primary buttons.
- JetBrains Mono for file paths and log text.
- Sizes: 22 px headings, 16 px dialog titles, 14 px body, 12–13 px secondary text, 11 px log lines,
  10 px uppercase captions with letter spacing.

## Components

- **Drive card** (waiting screen): centered column on the card surface with the title ("Drive A · <label>" using the
  configured label), the drive artwork (`Assets/drive.png`, 96 px high), the path in monospace, and a status dot with
  label ("connected" / "waiting for drive") driven by whether the configured path exists.
  Two sit side by side with the sync arrow between them on the waiting screen; Settings shows one per
  configured drive in its own card box, checking the currently entered path rather than the saved one.
- **Sync arrow**: long orange right arrow, 180 px wide, with the caption "SYNC DIRECTION" below it on
  a single line. The drive cards on the waiting screen shrink to fit it between them.
  It always points from Drive A to Drive B.
- **Selectable drive card** (settings): a clickable card with the role ("Drive A · Source"), icon, path
  and editable volume label. Labels default to the selected volume's label when available.
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
The cards show the configured drive labels and paths from Settings.
Drive A shows a green dot and "connected" while its path exists, otherwise a gray dot
and "waiting for drive"; Drive B behaves the same way.
The header status dot is yellow while any drive is missing and turns green once both
drives are connected and the app is ready to sync.
There is no manual sync button: the sync always starts by itself once both drives are ready.

### Main · syncing

Status row with a pulsing blue dot, the label "Syncing…", the current file name truncated from the left,
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

Linear layout with distinct card boxes for each setting:

- **Drive A · Source**: card box with drive icon, label, text box for the path, and a "Browse" button opening a folder
  picker.
- **Drive B · Destination**: card box with drive icon, label, text box for the path, and a "Browse" button opening a
  folder picker.
- Drive labels default to the selected volume label when available and can be customized independently.
- A muted note explaining that files flow only from A to B and that changes on Drive B are overwritten at the next sync.
- **Server URL**: card box with label, text box for the URL with validation (must be empty or start with http:// or
  https://), validation warning message when invalid, and description note. Invalid URLs highlight in red and disable
  the Save button.
- **Cancel** and **Save** sit bottom right.

### History

A list of the last 10 sync runs in one bordered group.
Each row has a status dot, date, file count, size, duration and a trailing check or warning icon.
Rows are clickable and open the run detail.

### History · run detail

Back arrow, the run date and its status.
Summary pills for files, size and duration, plus a red "1 error" pill on failed runs.
Below, the full rclone log for that run.

## Behavior notes

- Settings are loaded from and saved to the user's application data directory as JSON.
  Sync run history is stored in a local SQLite database in the same directory, retaining the latest 100 runs.
  A dummy sync engine stands in for the real one: once both drives are connected it runs for 10 seconds,
  emitting one log line every 500 ms into the syncing screen, then completes and shows the done screen.
  History details remain UI mock views until the real sync engine is implemented.
- The header status dot is bound to a shared status store; each screen publishes its status when shown.
  The waiting screen publishes yellow while a drive is missing and green once both are connected.
- Drive presence is polled every 2 seconds from the saved paths; the waiting screen shows the live
  connected state and refreshes when settings change. The Settings drive cards instead check the path
  currently entered in their text boxes.
- The default screen is set in `MainWindow.axaml.cs` (defaults to `WaitingView`).
- Fonts and the logo are bundled as Avalonia resources, so the look is identical on Linux and macOS.
