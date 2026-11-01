---
name: yazio
description: Log food into the user's YAZIO food diary and read it back (daily calories, protein/fat/carbs, goals, what is left for today) through a bundled CLI. Use this skill whenever the user wants to add, log, record or track something they ate or drank, asks how many calories or macros they have left, wants to see or review today's or another day's food diary, their YAZIO goals or meals, or wants to fix or delete a diary entry — even if YAZIO is not named explicitly (e.g. "запиши в дневник обед", "добавь в yazio кофе", "сколько калорий осталось", "что я сегодня ел", "I just had a burger, log it"). Also use it when the user sends a photo of food or a nutrition label and wants it counted.
---

# YAZIO food diary

This skill talks to the user's YAZIO account through a bundled command-line tool. It can:

- add **quick entries** (a name plus kcal / protein / fat / carbs) to a meal on any day,
- show the diary for a day: entries by meal, totals, daily goals and what is remaining,
- remove entries (editing = remove and add again).

It cannot search the YAZIO product database, and it has no access to weight, water or activity data.
YAZIO has no public API: the tool uses the private API of the YAZIO app ([source code](https://github.com/bezumnui/yazio-tool)).

## Running the tool

Always go through the wrapper, never run the binary directly. The wrapper reads `credentials.json`, unpacks the bundled binary into a temp folder and passes everything through:

```bash
python3 <skill-dir>/scripts/yazio.py diary
python3 <skill-dir>/scripts/yazio.py diary -d 2026-10-31
python3 <skill-dir>/scripts/yazio.py add -n "Plov, 350 g" --kcal 650 -p 25 -f 28 -c 70 -m lunch
python3 <skill-dir>/scripts/yazio.py add -n "Coffee with milk" --kcal 60 -p 3 -f 3 -c 5 -m breakfast -d 2026-10-31 -t 08:30
python3 <skill-dir>/scripts/yazio.py remove <id> [<id> ...]
```

`<skill-dir>` is the directory this SKILL.md is in. Output is JSON on stdout. Errors are JSON on stderr with a non-zero exit code.
The full option list, output formats and exit codes are in `references/cli.md`. Read it if you need anything beyond the examples above.

Time zone matters, because "today" and the default entry time depend on it. If `TZ` is not set in the environment, the wrapper uses `timezone` from `credentials.json`. If that is empty too, the sandbox time zone is used, which is usually UTC. When you know the user's time zone and the file has none, prefix calls with `TZ=<IANA zone>`, for example `TZ=Europe/Berlin python3 ...`.

## Setup problems (exit code 4)

### `credentials_missing`

`email` / `password` in `credentials.json` are empty. Nothing can work until the user fills them in, so stop and explain the steps in the user's language:

1. Take the `yazio.skill` file you installed. It is an ordinary ZIP archive (rename it to `yazio.zip` if your archiver insists).
2. Unpack it. You get a `yazio/` folder.
3. Open `yazio/credentials.json` and fill in `email` and `password` of the YAZIO account. Optionally set `timezone`, e.g. `"Europe/Moscow"`.
4. Pack the `yazio/` folder back into a ZIP (the archive must contain the `yazio/` folder itself, not just its contents) and rename it to `yazio.skill`.
5. Upload it again in the skill settings, replacing the old version.

On Linux/macOS: `unzip yazio.skill && nano yazio/credentials.json && rm yazio.skill && zip -r yazio.skill yazio`.

Mention that the password is stored in plain text inside this skill file, so they should not share the file with anyone.
Do not ask the user to paste the password into the chat: chats are stored and may be shared, while the skill file stays with the user.
If they paste it anyway, you can pass it for this session as `YAZIO_USERNAME=... YAZIO_PASSWORD=... python3 ...`. Never repeat it back, and still recommend the file.

### `unsupported_platform` / `binary_missing`

The bundled binary needs Linux x86_64. Tell the user this environment cannot run it.

Never print the content of `credentials.json`, the password or the token cache. Treat them as secrets.

## Logging food

1. **Split the meal into items.** Use one entry per dish or product ("borscht", "bread", "tea with sugar"), so a single item can be fixed or removed later.
2. **Get the numbers.**
   - If the user gave kcal / macros, or a label photo, use those numbers, scaled to the amount actually eaten.
   - Otherwise estimate from typical recipes and portion sizes. YAZIO quick entries store only the totals of the portion eaten, not per-100 g values.
   - If the portion is unknown, assume a normal single portion and say what you assumed. Ask only when the uncertainty is huge, like "pizza" (a slice or the whole thing?).
   - Self-check: kcal ≈ 4·protein + 4·carbs + 9·fat, within about 15% (alcohol adds about 7 kcal/g on top). If your estimate is off, fix it before adding.
3. **Name** the entry in the user's language and include the amount, e.g. `"Борщ, 300 г"`.
4. **Meal and date.**
   - Take them from the user's words ("на завтрак", "вчера вечером").
   - If no meal is given, pick one by the user's local time: before 11:00 breakfast, 11–16 lunch, 16–18 snack, 18–23 dinner, otherwise snack.
   - Resolve relative dates ("вчера", "в пятницу") to `YYYY-MM-DD` using the user's time zone.
   - Pass `-t HH:mm` only if the user mentioned a time.
5. **Add without asking for confirmation.** The user already asked for it, and entries are easy to remove. Run one `add` per item.
6. **Confirm briefly.** After adding, run `diary` for that day. Reply with:
   - what was added (name, kcal / P / F / C, meal),
   - which numbers were estimates,
   - what is left for the day (`remaining`).

   Keep the entry ids to yourself unless the user asks for them. You can use them for fixes.

## Viewing the diary

- Run `diary` (today) or `diary -d YYYY-MM-DD`.
- Lead with what the user cares about: eaten vs goal and remaining kcal / macros. Then list meals with their entries.
- A short table or list works well. Skip empty meals.
- The YAZIO "profile" data available here is the daily goals: kcal and macros per day, and the kcal goal of each meal. If the user asks for weight, water or steps, say the tool cannot access them.
- For a period ("за неделю"), call `diary` once per day and summarize: per-day totals, averages, days over the goal.

## Fixing and removing

There is no edit command. Find the entry id with `diary`, run `remove <id>`, then `add` the corrected version.
If it is unclear which entry the user means (e.g. two coffees), ask before removing.

## Errors

| Exit | Meaning | What to do |
|---|---|---|
| 1 | Bad command syntax | Fix the call (see `references/cli.md`) and retry |
| 2 | Invalid input (date, meal, negative values, bad id) | Fix the arguments and retry |
| 3 | YAZIO or network error | See below |
| 4 | Skill setup | See "Setup problems" |

For exit code 3:

- **Network errors** (DNS failure, connection refused, proxy 403 / "host not allowed"): the sandbox cannot reach `yzapi.yazio.com`. Ask the user to allow that domain in the code-execution network settings of their app. Some sandboxes have no internet at all, and then the skill cannot work there.
- **`403 version_blocked` or `410`:** YAZIO changed its private API, and the tool needs an update from https://github.com/bezumnui/yazio-tool.
- **`401` / `invalid_grant` on login:** the email or password in `credentials.json` is wrong. Walk the user through the credentials steps again.
