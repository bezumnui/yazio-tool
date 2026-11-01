# yazio CLI reference

A self-contained single-file binary for Linux (x64 / arm64). No installed .NET runtime is needed.
It works with the YAZIO food diary through the unofficial API (v19).

## Installation

```bash
xz -d yazio-linux-x64.xz && chmod +x yazio-linux-x64   # ARM: yazio-linux-arm64.xz
```

Requirements: glibc, OpenSSL and ca-certificates, network access to `yzapi.yazio.com`.

## Environment variables

| Variable | Purpose |
|---|---|
| `YAZIO_USERNAME` | YAZIO e-mail. Needed only to log in when there is no valid token |
| `YAZIO_PASSWORD` | password, in the same cases |
| `YAZIO_TOKEN_CACHE` | token cache path, default `~/.local/share/yazio-tool/token.json` (mode 600) |
| `TZ` | time zone for "today" and "now", e.g. `Asia/Dubai`. **Always set it in a sandbox**, otherwise UTC is used |
| `Yazio__BaseUrl` | overrides the API base URL, default `https://yzapi.yazio.com/v19/` |

Token handling: after logging in, the token is saved to the file and refreshed automatically with the refresh token.
The CLI logs in again with the password only if refreshing fails.

## General contract

- Success: one JSON document on **stdout**, exit code `0`.
- Failure: `{"error": "message"}` on **stderr**.
  - `2`: invalid input, or username/password missing.
  - `3`: YAZIO or network error. The message includes the HTTP status.
- Argument syntax errors (missing required option, etc.): an error message and help text from System.CommandLine, exit code `1`.
- Meal (`meal`): `breakfast | lunch | dinner | snack`. Russian `завтрак | обед | ужин | перекус` are accepted too, case-insensitive.
- Date format `YYYY-MM-DD`, time format `HH:mm` (`H:mm` and `HH:mm:ss` also work).
- Units: energy in **kcal**, protein, fat and carbs in **grams**.

## `yazio --help`

```
Usage:
  yazio [command] [options]

Options:
  -?, -h, --help  Show help and usage information
  --version       Show version information

Commands:
  diary         Show the diary for a day: entries by meal with ids, totals, goals, remaining.
  add           Add a quick entry (name + kcal and macros, no product from the database).
  remove <ids>  Remove diary entries by id.
```

## `yazio diary`: the diary for a day

```
Usage:
  yazio diary [options]

Options:
  -d, --date <date>  Day in YYYY-MM-DD. Defaults to today.
  -?, -h, --help     Show help and usage information
```

Examples: `yazio diary`, `yazio diary -d 2026-10-31`.

Output (field names in camelCase):

```json
{
  "date": "2026-11-02",
  "total":     { "kcal": 1000, "protein": 50,    "fat": 40,   "carbs": 110 },
  "goal":      { "kcal": 2150, "protein": 167.8, "fat": 71.7, "carbs": 194 },
  "remaining": { "kcal": 1150, "protein": 117.8, "fat": 31.7, "carbs": 84 },
  "meals": [
    {
      "meal": "lunch",
      "consumed": { "kcal": 1000, "protein": 50, "fat": 40, "carbs": 110 },
      "kcalGoal": 860,
      "entries": [
        {
          "id": "22e3353e-5645-4ba8-81d5-afff323aae0e",
          "time": "00:09",
          "kind": "QuickEntry",
          "name": "Test meal",
          "amount": null,
          "nutrition": { "kcal": 1000, "protein": 50, "fat": 40, "carbs": 110 }
        }
      ]
    }
  ]
}
```

- `meals` always contains all 4 meals, in the order breakfast, lunch, dinner, snack. An empty meal has `entries: []`.
- `kind` takes three values:
  - `QuickEntry`: a quick entry;
  - `Product`: a product from the database, with `amount` like `"150 g"` or `"2 × piece (120 g)"`;
  - `Recipe`: a recipe portion, with `nutrition: null` and a name like `"Recipe <id>"`.
- `goal` and `remaining` can be `null` if YAZIO returns no goals.
- `total` comes from the YAZIO totals, so it includes recipes too.

## `yazio add`: quick entry (name + kcal and macros)

```
Usage:
  yazio add [options]

Options:
  -n, --name <name> (REQUIRED)        What was eaten.
  --kcal <kcal> (REQUIRED)            Energy of the whole portion, kcal.
  -p, --protein <protein> (REQUIRED)  Protein, g.
  -f, --fat <fat> (REQUIRED)          Fat, g.
  -c, --carbs <carbs> (REQUIRED)      Carbohydrates, g.
  -m, --meal <meal> (REQUIRED)        breakfast | lunch | dinner | snack.
  -d, --date <date>                   Day in YYYY-MM-DD. Defaults to today.
  -t, --time <time>                   HH:mm. Defaults to now for today, otherwise a typical time for the meal.
  -?, -h, --help                      Show help and usage information
```

- Values are for **the whole portion eaten**, not per 100 g. Decimals use a dot: `12.5`.
- Negative values and an empty name are rejected with exit code 2.
- Default time: the current time for today; for other days `08:00`, `13:00`, `19:00` or `16:00` depending on the meal.
- `--kcal` has no short form.

Examples:

```bash
yazio add -n "Plov, 350 g" --kcal 650 -p 25 -f 28 -c 70 -m lunch
yazio add -n "Coffee with milk" --kcal 60 -p 3 -f 3 -c 5 -m breakfast -d 2026-10-31 -t 08:30
```

Output:

```json
{
  "id": "22e3353e-5645-4ba8-81d5-afff323aae0e",
  "name": "Plov, 350 g",
  "meal": "lunch",
  "consumedAt": "2026-11-02 13:30",
  "nutrition": { "kcal": 650, "protein": 25, "fat": 28, "carbs": 70 }
}
```

## `yazio remove`: remove entries

```
Usage:
  yazio remove <ids>... [options]

Arguments:
  <ids>  Ids of diary entries (from `diary`).

Options:
  -?, -h, --help  Show help and usage information
```

- Takes one or more ids from `diary`. Duplicates are ignored. An invalid GUID gives exit code 2.
- Example: `yazio remove 22e3353e-5645-4ba8-81d5-afff323aae0e`.
- Output: `{"removed": ["22e3353e-..."]}`.
- ⚠️ Removal has not been verified against the live API yet.

## Limitations

- No product search in the YAZIO database. Only quick entries can be added.
- Entries cannot be edited, only removed and added again.
- The API is unofficial: v20 and later are already blocked, and v19 may be closed too. If that happens, `Yazio__BaseUrl` helps.
