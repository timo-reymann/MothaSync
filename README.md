<p align="center">
  <img src="assets/logo.png" alt="MothaSync logo: a mother holding a drive in each hand" width="160">
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

`assets/logo-original.jpg` is the source artwork.
`assets/logo.png` is the square crop used in this README,
and a 256px copy ships inside the client under `src/MothaSync.Client/Assets/`.

## Fonts

The client bundles Open Sans and JetBrains Mono, both under the SIL Open Font License.
The license texts sit next to the font files in `src/MothaSync.Client/Assets/Fonts/`.
