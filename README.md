# MothaSync

[![License](https://img.shields.io/github/license/timo-reymann/MothaSync)](https://github.com/timo-reymann/MothaSync/blob/main/LICENSE)
[![CircleCI](https://circleci.com/gh/timo-reymann/MothaSync.svg?style=shield)](https://app.circleci.com/pipelines/github/timo-reymann/MothaSync)
[![GitHub Release](https://img.shields.io/github/v/tag/timo-reymann/MothaSync?label=version)](https://github.com/timo-reymann/MothaSync/releases)
[![Renovate](https://img.shields.io/badge/renovate-enabled-green?logo=renovate)](https://renovatebot.com)

<p align="center">
  <img width="300" src="./assets/logo.png" alt="MothaSync logo">
  <br>
  A system tray tool that syncs a folder from disk A to disk B.
</p>

## Features

- Sync folders through a desktop system tray application.
- Run the same self-contained client on Windows, Linux, and macOS.
- Use the bundled rclone binary for local sync operations.
- Start automatically when on user login and snychronizes when all drives are connected

## Requirements

- Windows 10 or later, macOS 12 or later, or a recent 64-bit Linux distribution.
- Two accessible local folders or drives to synchronize.
- `terminal-notifier` on macOS is optional; without it, MothaSync uses the built-in notification fallback.

## Installation

Download the archive for your platform from the [GitHub Releases](https://github.com/timo-reymann/MothaSync/releases) page, extract it, and start MothaSync.

Release archives are available for Windows x64, Linux x64, Linux arm64, macOS Apple Silicon, and macOS Intel.

## Usage

Configure Drive A and Drive B in the client, then start a sync from the tray application.

For an end-to-end local test, generate fixtures and point Drive A and Drive B at the two created folders:

```bash
scripts/generate-test-data.sh
# Configure the app with e2e-data/source and e2e-data/destination
```

## Motivation

MothaSync provides a focused desktop interface for keeping a folder mirrored between two local drives without requiring users to operate rclone directly.

## Documentation

- [Design documentation](DESIGN.md)
- [Release build instructions](#build)
- [GitHub Releases](https://github.com/timo-reymann/MothaSync/releases)

## Contributing

I welcome contributions, including bug reports, documentation improvements, fixes, and feature proposals.

Please read the [Contribution Guidelines](CONTRIBUTING.md) before opening an issue or pull request.

## Development

### Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://docs.docker.com/get-docker/) when running ORT locally

### Test

```bash
dotnet test
```

### Build

Build the solution with:

```bash
dotnet build
```

Release archives for all supported platforms are created with:

```bash
scripts/build.sh
```

The build output is written to `dist/`. The script downloads the pinned rclone release into the client assets and verifies it against the upstream SHA256 checksum.

### Credits

- [rclone](https://rclone.org/) provides the sync engine.
- [Open Sans](https://github.com/googlefonts/opensans) and [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) are bundled under the [SIL Open Font License 1.1](https://openfontlicense.org).

### Alternatives

- [rclone](https://rclone.org/) can be used directly from the command line.

## License

MothaSync source code and original project assets are released under the [MIT License](LICENSE).

The bundled Open Sans and JetBrains Mono font files are separate works and remain licensed under [OFL-1.1](https://spdx.org/licenses/OFL-1.1.html). Their license texts are stored next to the font files in `src/MothaSync.Client/Assets/Fonts/` and must be preserved when redistributing them.
