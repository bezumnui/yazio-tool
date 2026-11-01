#!/usr/bin/env python3
"""Runs the bundled yazio CLI with the credentials from the skill's credentials.json.

Usage: python3 scripts/yazio.py <diary|add|remove> [options]

Exit codes: those of the CLI (0 ok, 1 syntax, 2 input, 3 YAZIO/network),
plus 4 for skill setup problems (empty credentials, unsupported platform).
"""

import json
import os
import platform
import stat
import sys
import shutil
import tempfile
from pathlib import Path

SKILL_DIR = Path(__file__).resolve().parent.parent
CREDENTIALS_FILE = SKILL_DIR / "credentials.json"
BINARIES = {"x86_64": "yazio-linux-x64"}
CACHE_DIR = Path(os.environ.get("YAZIO_SKILL_CACHE") or Path(tempfile.gettempdir()) / "yazio-skill")
EXIT_SETUP = 4


def fail(error: str, message: str, **details) -> None:
    payload = {"error": error, "message": message, **details}
    print(json.dumps(payload, ensure_ascii=False, indent=2), file=sys.stderr)
    sys.exit(EXIT_SETUP)


def load_credentials() -> dict:
    try:
        data = json.loads(CREDENTIALS_FILE.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        data = {}
        broken = str(exc)
    else:
        broken = None

    creds = {key: str(data.get(key) or "").strip() for key in ("email", "password", "timezone")}
    # Environment variables are a fallback for local use (e.g. Claude Code).
    creds["email"] = creds["email"] or os.environ.get("YAZIO_USERNAME", "")
    creds["password"] = creds["password"] or os.environ.get("YAZIO_PASSWORD", "")

    if not creds["email"] or not creds["password"]:
        fail(
            "credentials_missing",
            "YAZIO email/password are not set. The user has to put them into credentials.json "
            "inside the skill archive and re-install the skill.",
            credentials_file=str(CREDENTIALS_FILE),
            **({"parse_error": broken} if broken else {}),
        )
    return creds


def binary_path() -> Path:
    machine = platform.machine().lower()
    machine = {"amd64": "x86_64", "x64": "x86_64"}.get(machine, machine)
    binary_name = BINARIES.get(machine)
    if platform.system() != "Linux" or binary_name is None:
        fail("unsupported_platform", f"The bundled CLI supports Linux x86_64 only, this is {platform.system()} {machine}.")

    bundled = SKILL_DIR / "bin" / binary_name
    if not bundled.exists():
        fail("binary_missing", f"{bundled} is missing from the skill package.")

    # Installed skills may be read-only and lose the executable bit,
    # so run an executable copy from the cache (one per binary version).
    target = CACHE_DIR / f"yazio-{bundled.stat().st_size}"
    if not target.exists():
        CACHE_DIR.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=CACHE_DIR, delete=False) as tmp:
            with bundled.open("rb") as src:
                shutil.copyfileobj(src, tmp)
        os.chmod(tmp.name, stat.S_IRWXU | stat.S_IRGRP | stat.S_IXGRP | stat.S_IROTH | stat.S_IXOTH)
        os.replace(tmp.name, target)
    return target


def main() -> None:
    args = sys.argv[1:]
    wants_help = not args or any(a in ("-h", "--help", "-?", "--version") for a in args)
    creds = None if wants_help else load_credentials()
    binary = binary_path()

    env = os.environ.copy()
    if creds:
        env["YAZIO_USERNAME"] = creds["email"]
        env["YAZIO_PASSWORD"] = creds["password"]
        if creds["timezone"] and not env.get("TZ"):
            env["TZ"] = creds["timezone"]
    env.setdefault("YAZIO_TOKEN_CACHE", str(CACHE_DIR / "token.json"))
    env.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")

    os.execve(binary, [str(binary), *args], env)


if __name__ == "__main__":
    main()
