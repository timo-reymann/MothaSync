<p align="center">
  <img src="assets/logo.png" alt="MothaSync logo: a pink mother figure holding a drive in each hand" width="160">
</p>

<h1 align="center">MothaSync</h1>

<p align="center">A system tray tool that syncs a folder from disk A to disk B.</p>

## Projects

| Project | Description |
| --- | --- |
| `src/MothaSync.Shared` | Contracts and models shared by client and server. |
| `src/MothaSync.Client` | Avalonia desktop app. |
| `src/MothaSync.Server` | ASP.NET Core Web API. |

## Getting started

Requires the .NET SDK pinned in `global.json`.

```bash
dotnet build
dotnet run --project src/MothaSync.Client
dotnet run --project src/MothaSync.Server
```

## Bundled rclone

`scripts/download-rclone.sh` downloads a pinned rclone release for every supported
platform into `src/MothaSync.Client/Assets/rclone/` (gitignored), verifying each zip
against the upstream `SHA256SUMS`. The client bundles the binary for the build's
runtime identifier and on startup copies it to the AppData `MothaSync/bin` folder
whenever the bundled checksum differs from the installed copy. Renovate bumps the
version pin in the script through the regex custom manager in `renovate.json`.

## Autostart

Release builds register MothaSync to start at user login on the current platform:

- Windows: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- macOS: `~/Library/LaunchAgents/de.timo_reymann.mothasync.plist`
- Linux: `~/.config/autostart/mothasync.desktop` (or `$XDG_CONFIG_HOME/autostart`)

Debug builds do not modify the operating system autostart configuration. The
registration launches the same executable with `--mothasync-autostart`.

The client starts the bundled rclone binary locally and uses its loopback REST API
(`sync/sync`, `job/status`, and `core/stats`) for real sync runs. For an end-to-end
local test, generate ignored fixtures and point Drive A and Drive B at the two
created folders:

```bash
scripts/generate-test-data.sh
# Configure the app with e2e-data/source and e2e-data/destination
```

The generator creates 10 random files between 5 MiB and 20 MiB by default. Pass a
file count as the second argument to change it; it rejects requests whose minimum
size would exceed the 1 GiB total cap. The generated `e2e-data/` directory is
gitignored.

On macOS, install `terminal-notifier` if you want native error notifications to
use the MothaSync logo: `brew install terminal-notifier`. Without it, notifications
fall back to the built-in macOS notification script.

## Release builds

`scripts/build.sh` builds self-contained zips for every platform into `dist/`:

| Platform | Archive |
| --- | --- |
| Windows x64 | `mothasync-1.0.0-win-x64.zip` |
| Linux x64 | `mothasync-1.0.0-linux-x64.zip` |
| Linux arm64 | `mothasync-1.0.0-linux-arm64.zip` |
| macOS Apple Silicon | `mothasync-1.0.0-osx-arm64.zip` |
| macOS Intel | `mothasync-1.0.0-osx-x64.zip` |

The Windows executable embeds `src/MothaSync.Client/MothaSync.ico`.
The Linux archives include `mothasync.desktop` and `mothasync.png` from `packaging/linux/` for desktop integration.
The build needs network access once to download the runtime packs.

## macOS app bundle

The Dock and Finder icon comes from the `.app` bundle, so `dotnet run` always shows the generic icon.
Build the bundle with:

```bash
dotnet restore src/MothaSync.Client -r osx-arm64
dotnet msbuild src/MothaSync.Client -t:BundleApp -p:RuntimeIdentifier=osx-arm64 -p:Configuration=Release -p:SelfContained=true
codesign --force --deep -s - src/MothaSync.Client/bin/Release/net10.0/osx-arm64/publish/MothaSync.app
```

The bundle identifier is `de.timo_reymann.mothasync`.
The icon is `src/MothaSync.Client/MothaSync.icns`, generated from the logo.

## Design

Screens, colors, typography and components are documented in [DESIGN.md](DESIGN.md).

## Logo

`assets/logo-original.jpg` is the source sheet.
`assets/logo.png` (512px, transparent) is the logo used in this README,
`assets/drive.png` is the hard drive artwork,
and `assets/trays/` holds the tray icon states (`idle`, `waiting`, `syncing`, `paused`, `ok`, `error`).
A 256px logo and the tray icons ship inside the client under `src/MothaSync.Client/Assets/`.
`MothaSync.ico`, `MothaSync.icns` and `packaging/linux/mothasync.png` are generated from `assets/logo.png`.

## Fonts

The client bundles Open Sans and JetBrains Mono, both under the SIL Open Font License.
The license texts sit next to the font files in `src/MothaSync.Client/Assets/Fonts/`.
