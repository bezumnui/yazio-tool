# yazio-tool

Инструменты для дневника питания YAZIO на C# / .NET 10:

| Проект | Что это |
|---|---|
| `src/YazioTool.Core` | Библиотека («бэк»): домен, клиент **неофициального** YAZIO API, авторизация, DI |
| `src/YazioTool.Mcp` | MCP-сервер (Streamable HTTP) поверх библиотеки |
| `src/YazioTool.Cli` | CLI `yazio` поверх библиотеки: self-contained single-file бинарник, JSON-вывод (для скиллов/песочниц) |
| `tests/YazioTool.Core.Tests` | xUnit v3 тесты библиотеки |

> ⚠️ У YAZIO нет публичного API. Используется приватный API приложения (`yzapi.yazio.com/v20`)
> с публично известными client credentials. Может сломаться в любой момент; использование нарушает ToS.

## Возможности

- `get_diary` / `yazio diary` — дневник за день: записи по приёмам пищи (с id), итог КБЖУ, цели, остаток.
- `add_quick_entry` / `yazio add` — быстрое добавление: название + ккал/Б/Ж/У (без продукта из базы).
- `remove_entries` / `yazio remove` — удаление записей по id.

Приём пищи: `breakfast | lunch | dinner | snack` (понимает и `завтрак | обед | ужин | перекус`).
Время по умолчанию: «сейчас» для сегодняшнего дня, иначе типичное время приёма пищи (08:00 / 13:00 / 19:00 / 16:00).

## Архитектура (SOLID)

```
Front ends:   YazioTool.Mcp (DiaryTools)      YazioTool.Cli (DiaryCommands)
                       \                         /
Core/Application:     DiaryInput (парсинг ввода), DiaryView (представление)
                                   |
Core/Abstractions:      IDiaryReader            IDiaryWriter         ← зависят только от этого (DIP)
                                   |
Core/Yazio:   YazioDiaryReader / YazioDiaryWriter
                 ├─ IYazioApiClient  → YazioApiClient (HttpClient "yazio-api" + BearerTokenHandler)
                 ├─ IProductCatalog  → CachingProductCatalog ⟶ YazioProductCatalog   (декоратор, OCP)
                 └─ IAccessTokenProvider → YazioAccessTokenProvider
                        ├─ IYazioTokenClient → YazioTokenClient (oauth/token)
                        └─ ITokenStore → NullTokenStore (MCP) | FileTokenStore (CLI)
```

- **SRP** — авторизация, HTTP, маппинг, кэш продуктов, парсинг ввода, представление разнесены по классам.
- **OCP** — хранилище токенов и кэширование подключаются без изменения существующего кода.
- **LSP** — `CachingProductCatalog` взаимозаменяем с `YazioProductCatalog`.
- **ISP** — чтение и запись дневника разделены (`IDiaryReader` / `IDiaryWriter`).
- **DIP** — front-end'ы зависят от абстракций; вся сборка в `AddYazio()`.

Токен: кэшируется, за минуту до истечения обновляется по refresh token, при неудаче — повторный вход по паролю; при 401 инвалидируется.

## Конфигурация

| Ключ (`appsettings` / env) | Описание |
|---|---|
| `Yazio:Username` / `Yazio__Username` | e-mail YAZIO (обязательно) |
| `Yazio:Password` / `Yazio__Password` | пароль (обязательно) |
| `Yazio:BaseUrl` | по умолчанию `https://yzapi.yazio.com/v20/` |
| `Yazio:ClientId`, `Yazio:ClientSecret` | по умолчанию — credentials приложения YAZIO |
| `Mcp:ApiKey` / `Mcp__ApiKey` | ключ MCP-эндпоинта (только MCP) |
| `TZ` | часовой пояс для «сегодня/сейчас» |

CLI дополнительно понимает `YAZIO_USERNAME`, `YAZIO_PASSWORD`, `YAZIO_TOKEN_CACHE`
(путь к кэшу токена; по умолчанию `~/.local/share/yazio-tool/token.json`, права `600`).

Локально удобно через user-secrets (общие для MCP и CLI):

```bash
dotnet user-secrets --project src/YazioTool.Mcp set Yazio:Username you@example.com
dotnet user-secrets --project src/YazioTool.Mcp set Yazio:Password '...'
```

## MCP-сервер

```bash
cp .env.example .env   # заполнить
docker compose up -d --build
# или локально:
dotnet run --project src/YazioTool.Mcp   # http://localhost:5080/mcp
```

Эндпоинт: `POST /mcp` (stateless Streamable HTTP), healthcheck: `GET /health`.
Если задан `Mcp:ApiKey`, нужен `Authorization: Bearer <key>` или `?key=<key>`
(второй вариант — для клиентов, которые не умеют задавать заголовки, напр. коннекторы claude.ai).

Claude Code:

```bash
claude mcp add --transport http yazio http://localhost:8080/mcp --header "Authorization: Bearer <key>"
```

## CLI

```bash
./scripts/publish-cli.sh                      # → artifacts/cli/linux-x64/yazio (≈40 МБ, .NET не нужен)
./scripts/publish-cli.sh linux-x64 linux-arm64 osx-arm64
```

Скрипт использует локальный `dotnet`, а если его нет — Docker-образ SDK.

```bash
export YAZIO_USERNAME=you@example.com YAZIO_PASSWORD='...' TZ=Asia/Dubai
yazio diary                       # сегодня
yazio diary -d 2026-10-31
yazio add -n "Плов, 350 г" --kcal 650 -p 25 -f 28 -c 70 -m lunch
yazio add -n "Кофе с молоком" --kcal 60 -p 3 -f 3 -c 5 -m breakfast -d 2026-10-31 -t 08:30
yazio remove 3f2c...e1 9a1b...44
```

Контракт для скриптов/скиллов:
- успех → JSON в **stdout**, код `0`;
- ошибка → `{"error": "..."}` в **stderr**; код `2` — неверный ввод/конфигурация, `3` — ошибка YAZIO/сети.

## Разработка

```bash
dotnet build
dotnet test --solution YazioTool.slnx
```

## Не проверено на реальном API

Формат `simple_products` при **добавлении** выведен из ответа `GET /user/consumed-items`
(в открытых обёртках добавления быстрых записей нет). Если YAZIO вернёт 4xx — снять реальный
запрос приложения (Reqable) и поправить `NewSimpleProductDto`.
