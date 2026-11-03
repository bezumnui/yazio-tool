# yazio-tool

Tools for the YAZIO food diary in C# / .NET 10:

| Project | What it is |
|---|---|
| `src/YazioTool.Core` | Library ("backend"): domain, client for the **unofficial** YAZIO API, authentication, DI |
| `src/YazioTool.Mcp` | MCP server (Streamable HTTP) on top of the library |
| `src/YazioTool.Cli` | `yazio` CLI on top of the library: self-contained single-file binary with JSON output (for skills/sandboxes) |
| `skill/yazio` | Agent skill (Claude / ChatGPT) that drives the CLI from a sandbox |
| `tests/YazioTool.Core.Tests` | xUnit v3 tests for the library |

> ⚠️ YAZIO has no public API. This project uses the private API of the YAZIO app (`yzapi.yazio.com/v22`)
> with publicly known client credentials. It may break at any time, and using it violates the YAZIO ToS.

## Features

- `get_diary` / `yazio diary`: the diary for a day, with entries by meal (including ids), nutrition totals, goals and what is remaining.
- `add_quick_entry` / `yazio add`: quick entry, a name plus kcal/protein/fat/carbs (no product from the database).
- `remove_entries` / `yazio remove`: remove entries by id.
- `get_streak` / `yazio streak`: the streak (the flame): current count, freezes, recent tracked days.
- `set_streak` / `yazio streak set`: overwrite the streak count and/or freezes (0–2) of a day.
- Adding food with the streak: `add_quick_entry` uploads the day to the streak by default (`updateStreak: false` to skip);
  the CLI does it with `yazio add --streak`. See [docs/CLI.md](docs/CLI.md#how-the-streak-works) for how the streak works.

Meal: `breakfast | lunch | dinner | snack` (Russian `завтрак | обед | ужин | перекус` are accepted too).
Default time: "now" for today, otherwise a typical time for the meal (08:00 / 13:00 / 19:00 / 16:00).

## Architecture (SOLID)

```
Front ends:   YazioTool.Mcp (DiaryTools)      YazioTool.Cli (DiaryCommands)
                       \                         /
Core/Application:     DiaryInput (input parsing), DiaryView (presentation)
                                   |
Core/Application:     StreakRules (streak day derivation), StreakView
Core/Abstractions:      IDiaryReader       IDiaryWriter       IStreakService   ← front ends depend only on these (DIP)
                                   |
Core/Yazio:   YazioDiaryReader / YazioDiaryWriter / YazioStreakService
                 ├─ IYazioApiClient  → YazioApiClient (HttpClient "yazio-api" + BearerTokenHandler)
                 ├─ IProductCatalog  → CachingProductCatalog ⟶ YazioProductCatalog   (decorator, OCP)
                 └─ IAccessTokenProvider → YazioAccessTokenProvider
                        ├─ IYazioTokenClient → YazioTokenClient (oauth/token)
                        └─ ITokenStore → NullTokenStore (MCP) | FileTokenStore (CLI)
```

- **SRP**: authentication, HTTP, mapping, product cache, input parsing and presentation each live in their own classes.
- **OCP**: token storage and caching plug in without changing existing code.
- **LSP**: `CachingProductCatalog` is interchangeable with `YazioProductCatalog`.
- **ISP**: diary reading and writing are separate (`IDiaryReader` / `IDiaryWriter`).
- **DIP**: front ends depend on abstractions, and everything is wired up in `AddYazio()`.

Token handling: the token is cached and refreshed with the refresh token one minute before it expires. If refreshing fails, the tool logs in again with the password. A 401 response invalidates the token.

## Configuration

| Key (`appsettings` / env) | Description |
|---|---|
| `Yazio:Username` / `Yazio__Username` | YAZIO e-mail (needed only to log in when there is no valid or refreshable token) |
| `Yazio:Password` / `Yazio__Password` | password (same) |
| `Yazio:BaseUrl` | defaults to `https://yzapi.yazio.com/v22/` |
| `Yazio:AppVersion` | sent as `X-Yazio-App-Version`, defaults to `26.43.1` |
| `Yazio:BuildNumber` | sent as `X-Yazio-Build-Number`, defaults to `2643100` |
| `Yazio:Platform` | sent as `X-Yazio-Platform`, defaults to `android` |
| `Yazio:ClientId`, `Yazio:ClientSecret` | default to the YAZIO app credentials |
| `Mcp:ApiKey` / `Mcp__ApiKey` | key for the MCP endpoint (MCP only) |
| `TZ` | time zone for "today"/"now" |

The CLI also reads `YAZIO_USERNAME`, `YAZIO_PASSWORD` and `YAZIO_TOKEN_CACHE`
(token cache path, default `~/.local/share/yazio-tool/token.json`, mode `600`).

For local development, user-secrets are convenient (shared by MCP and CLI):

```bash
dotnet user-secrets --project src/YazioTool.Mcp set Yazio:Username you@example.com
dotnet user-secrets --project src/YazioTool.Mcp set Yazio:Password '...'
```

## MCP server

```bash
cp .env.example .env   # fill it in
docker compose up -d --build
# or locally:
dotnet run --project src/YazioTool.Mcp   # http://localhost:5080/mcp
```

Endpoint: `POST /mcp` (stateless Streamable HTTP). Health check: `GET /health`.
If `Mcp:ApiKey` is set, requests need `Authorization: Bearer <key>` or `?key=<key>`.
The query parameter is for clients that cannot set headers, e.g. claude.ai connectors.

Claude Code:

```bash
claude mcp add --transport http yazio http://localhost:8080/mcp --header "Authorization: Bearer <key>"
```

## CLI

Prebuilt binaries are attached to [GitHub releases](https://github.com/bezumnui/yazio-tool/releases).
To build them yourself:

```bash
./scripts/publish-cli.sh                      # → artifacts/cli/linux-x64/yazio (~40 MB, no .NET needed)
./scripts/publish-cli.sh linux-x64 linux-arm64 osx-arm64
```

The script uses the local `dotnet` if present, otherwise the .NET SDK Docker image.

```bash
export YAZIO_USERNAME=you@example.com YAZIO_PASSWORD='...' TZ=Asia/Dubai
yazio diary
yazio add -n "Plov, 350 g" --kcal 650 -p 25 -f 28 -c 70 -m lunch
yazio add -n "Candy" --kcal 52 -p 0.5 -f 2.3 -c 7.2 -m snack --streak
yazio remove <id>
yazio streak
yazio streak set --count 100 --freezes 2
```

Full command reference, output formats and exit codes: [docs/CLI.md](docs/CLI.md).

## Skill

`skill/yazio` is an agent skill that logs food and reads the diary through the bundled CLI.
Build the installable package (the binary is bundled, `credentials.json` stays empty):

```bash
./scripts/package-skill.sh   # → artifacts/skill/yazio.skill (~7 MB)
```

The user puts their YAZIO e-mail and password (and optionally a time zone) into `yazio/credentials.json`
inside the archive before installing it. With empty credentials, the skill explains how to do that.

## Development

```bash
dotnet build
dotnet test --solution YazioTool.slnx
```

## API version

The current app (26.43.1) uses `v22`, which is the default. YAZIO blocks requests by client version, not by URL:
without the headers below every data endpoint returns `403 {"error":"version_blocked"}` (`oauth/token` still works).

| Header | Checked |
|---|---|
| `X-Yazio-Platform: android` | must be present |
| `X-Yazio-App-Version: 26.43.1` | value is checked (e.g. `1.0.0` is blocked) |
| `X-Yazio-Build-Number: 2643100` | must be present, value is not checked |

All three are sent with every request and configurable (`Yazio:Platform`, `Yazio:AppVersion`, `Yazio:BuildNumber`).
When YAZIO blocks the app version, raising `Yazio:AppVersion` to the current release should help.
Older notes (2026-11, without these headers): `v15`–`v19` worked, `v20`–`v22` returned `403 version_blocked`, `v23+` returned `410`.
