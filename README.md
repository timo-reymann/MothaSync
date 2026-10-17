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
- Start automatically on user login and synchronize when all drives are connected.
- Report sync runs to an optional server after device enrollment with a shared token.
- Serve Prometheus metrics that show failed runs and syncs that never happened.

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

MothaSync gives a focused desktop interface for keeping a folder mirrored between two local drives. No user must operate rclone by hand. This section explains the problem MothaSync solves and where the optional server fits in.

### The problem

One person carries an external drive with important data every day. If that drive fails, the data is gone. One drive is not a backup, so the data needs copies in two more places.

### The setup

The person owns three places for the data:

- Drive A is the working copy. It travels with the person every day.
- Drive B is a hot copy. It stays home and holds a second full copy.
- A NAS takes incremental backups of Drive B to the cloud, for example with Duplicati.

The daily routine is short. The person comes home, plugs both drives into the PC, and lets the client do its work. MothaSync starts the sync on its own once both drives are present. The delta is small, so the sync takes seconds or minutes. The person then unplugs both drives. No technical step and no manual action is needed.

The protection comes from layers. If the travel drive fails, the hot copy on Drive B still holds all data. If Drive B fails too, the NAS and the cloud still hold older generations. Data loss then needs three failures at once.

### The optional server

MothaSync runs in the background by itself. But "it just works" gives no proof. Two faults are possible, and both show in the same place:

- A sync fails for a technical reason, for example a broken drive.
- A person forgets to plug the drives in.

The optional server receives a report on every sync run and serves Prometheus metrics. A failed run appears in the metrics. A missed sync shows up because the last-run timestamp grows old. The metrics go into an existing Prometheus stack, and alerts tell the admin about both faults.

In short: MothaSync is a simple wrapper around rclone. It gives one person a backup flow that works by itself, and the optional server adds the proof that it worked.

## Server (optional)

The server stores sync run reports and serves Prometheus metrics. Clients enroll once with a shared enrollment token, receive their own device token, and send one report per sync run with it.

The image is published on Docker Hub as [`timoreymann/mothasync-server`](https://hub.docker.com/r/timoreymann/mothasync-server) for linux/amd64 and linux/arm64.

### Quick start

The root `docker-compose.yml` is a sample setup. It starts the server and a Prometheus instance that scrapes the metrics port.

```bash
cp .env.example .env
# Set MOTHASYNC_ENROLLMENT_TOKEN in .env to the token you enter in the client
docker compose up -d
```

After the start:

- Server API: `http://localhost:8080`
- Metrics: `http://localhost:9100/metrics`
- Prometheus UI: `http://localhost:9090`

To enroll a client, enter the server URL and the enrollment token in the client settings, press test, and save. The client registers itself and reports each sync run to the server from then on.

### Configuration

The server reads its settings from nested `appsettings.json` keys or environment variables. Environment variables replace each `:` with `__`:

| Setting | Environment variable | Default | Purpose |
|---|---|---|---|
| `Server:EnrollmentToken` | `Server__EnrollmentToken` | *(empty)* | Shared secret for enrollment. Clients get `401` when it is empty. |
| `Server:DatabasePath` | `Server__DatabasePath` | `mothasync-server.db` | SQLite database file. Docker default: `/data/mothasync-server.db`. |
| `Metrics:Enabled` | `Metrics__Enabled` | `true` | Starts the metrics listener. |
| `Metrics:Port` | `Metrics__Port` | `9100` | Port that serves `/metrics`. |

The metrics listener serves only `/metrics`. The API endpoints are not reachable through the metrics port, and `/metrics` is not reachable through the API port.

### Metrics

The server serves the default .NET runtime, process and HTTP request metrics, plus these metrics for sync runs:

| Metric | Type | Labels | Purpose |
|---|---|---|---|
| `mothasync_sync_runs_total` | Counter | `device_id`, `status` | Sync runs reported by clients. `status` is one of `running`, `completed`, `failed`, `cancelled`. |
| `mothasync_sync_files_changed_total` | Counter | `device_id` | Files added, updated or deleted. |
| `mothasync_sync_bytes_transferred_total` | Counter | `device_id` | Bytes transferred. |
| `mothasync_sync_run_duration_seconds` | Histogram | `device_id` | Wall-clock time of sync runs. |
| `mothasync_sync_last_run_timestamp_seconds` | Gauge | `device_id` | Unix timestamp of the last reported run. |
| `mothasync_sync_last_run_result` | Gauge | `device_id` | `1` when the last run completed, `0` otherwise. |
| `mothasync_devices_registered_total` | Counter | *(none)* | Devices registered with the server. |

The most useful check is the sync that never happens. The expression below matches each device with no report for two days:

```promql
time() - max by (device_id) (mothasync_sync_last_run_timestamp_seconds) > 86400 * 2
```

A failed run shows up in the run counter:

```promql
increase(mothasync_sync_runs_total{status="failed"}[7d]) > 0
```

## Documentation

- [Design documentation](DESIGN.md)
- [Server quick start and metrics](#server-optional)
- [Container image build](#makefile)
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

### Makefile

A `Makefile` wraps the common tasks. `make help` lists every target. The most used ones:

```bash
make build         # Build the solution
make test          # Run all tests
make build-release # Create the client release archives in dist/
make build-image   # Build and push the multi-arch server container image
make compose-up    # Start the server sample with Prometheus
```

CircleCI builds and pushes the server image for linux/amd64 and linux/arm64 on `main` and on `v*` tags. The job needs a CircleCI context `docker-hub` with the variables `DOCKERHUB_USERNAME` and `DOCKERHUB_ACCESS_TOKEN`.

### Container image

The server image uses the chiseled .NET runtime image, so it runs as a non-root user with no shell. It builds with:

```bash
docker buildx build -t mothasync-server:local -f docker/server.Dockerfile .
```

### Credits

- [rclone](https://rclone.org/) provides the sync engine.
- [Open Sans](https://github.com/googlefonts/opensans) and [JetBrains Mono](https://github.com/JetBrains/JetBrainsMono) are bundled under the [SIL Open Font License 1.1](https://openfontlicense.org).

### Alternatives

- [rclone](https://rclone.org/) can be used directly from the command line.

## License

MothaSync source code and original project assets are released under the [MIT License](LICENSE).

The bundled Open Sans and JetBrains Mono font files are separate works and remain licensed under [OFL-1.1](https://spdx.org/licenses/OFL-1.1.html). Their license texts are stored next to the font files in `src/MothaSync.Client/Assets/Fonts/` and must be preserved when redistributing them.
