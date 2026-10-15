# MothaSync

.NET 10 solution (`MothaSync.slnx`), SDK pinned in `global.json`.

## Layout

- `src/MothaSync.Shared` — class library with contracts/models shared by client and server.
- `src/MothaSync.Client` — Avalonia desktop app (MVVM template).
- `src/MothaSync.Server` — ASP.NET Core Web API (controllers).

`Client` and `Server` both reference `Shared`; they must not reference each other.

## Commands

- Build: `dotnet build`
- Run server: `dotnet run --project src/MothaSync.Server`
- Run client: `dotnet run --project src/MothaSync.Client`

## Conventions

- Put anything used by both client and server in `Shared`.
- Keep Avalonia views thin; logic belongs in view models.
