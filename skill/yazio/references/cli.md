# yazio CLI reference

Run every command through the skill wrapper, which supplies the credentials:
`python3 <skill-dir>/scripts/yazio.py <command> [options]`. Below, `yazio` stands for that.

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
  streak        Show the YAZIO streak: current count, freezes, recent tracked days.
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
  -s, --streak                        Also mark the day in the YAZIO streak (the flame), like the app does.
  -?, -h, --help                      Show help and usage information
```

- Values are for **the whole portion eaten**, not per 100 g. Decimals use a dot: `12.5`.
- Negative values and an empty name are rejected with exit code 2.
- Default time: the current time for today; for other days `08:00`, `13:00`, `19:00` or `16:00` depending on the meal.
- `--kcal` has no short form.
- `--streak` also uploads the day to the YAZIO streak (see `yazio streak`). Without it the entry is added but the flame does not light up.

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
  "nutrition": { "kcal": 650, "protein": 25, "fat": 28, "carbs": 70 },
  "streak": null
}
```

With `--streak`, `streak` holds the updated day, e.g. `{"date": "2026-11-02", "meals": ["lunch"], "streakCount": 76, "freezeCount": 1}`.

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

## `yazio streak`: the streak (the flame)

```
Usage:
  yazio streak [command] [options]

Options:
  --days <days>   How many recent tracked days to show. [default: 14]
  -?, -h, --help  Show help and usage information

Commands:
  set  Overwrite the streak count and/or freeze count of a day. The app shows it after a re-login.
```

Output:

```json
{
  "currentStreak": 76,
  "freezes": 1,
  "lastTrackedDay": "2026-11-03",
  "trackedToday": true,
  "days": [
    { "date": "2026-11-02", "meals": ["breakfast"], "streakCount": 75, "freezeCount": 1 },
    { "date": "2026-11-03", "meals": ["breakfast", "snack"], "streakCount": 76, "freezeCount": 1 }
  ]
}
```

- `currentStreak` is the count of the last tracked day if it is today or yesterday, otherwise `0`.
- `days` holds only tracked days, oldest first; a missing date is a day without tracking.

## `yazio streak set`: change the streak count / freezes

```
Usage:
  yazio streak set [options]

Options:
  --count <count>      Streak length including the day.
  --freezes <freezes>  Streak freezes in stock, 0-2.
  -d, --date <date>    Day in YYYY-MM-DD. Defaults to today.
  -?, -h, --help       Show help and usage information
```

- At least one of `--count` / `--freezes` is required. An omitted value is kept; for an untracked day it is computed from the previous day.
- Freezes are limited to 0–2 (two slots in the app; YAZIO fails with HTTP 500 on larger values).
- Example: `yazio streak set --count 100 --freezes 2`. Output: one day in the format of `days` above.

### How the streak works

YAZIO does not compute the streak on the server. The app derives each day on the phone
(`daytimes` = meals tracked that day, `streak_count` = previous day + 1, `freeze_count` = freezes in stock)
and uploads it with `POST user/streak/{date}`; the server stores the values as they are.
Food added through the API therefore lights the flame only if the day is uploaded too (`add --streak`).
The app caches the streak and shows server-side changes only **after logging out and in again**.

## Limitations

- No product search in the YAZIO database. Only quick entries can be added.
- Entries cannot be edited, only removed and added again.
- The API is unofficial. v22 works only with the `X-Yazio-Platform`, `X-Yazio-App-Version` and `X-Yazio-Build-Number` headers (sent automatically); when YAZIO blocks the app version, raise `Yazio__AppVersion`.
