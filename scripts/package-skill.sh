#!/usr/bin/env bash
# Builds the CLI and packages the skill into ./artifacts/skill/yazio.skill
# with the binary bundled and an empty credentials.json.
#
# Skill uploads reject nested archives, so the binary is bundled as a plain file.
# It is built without single-file compression: the skill zip compresses it far better.
set -euo pipefail

cd "$(dirname "$0")/.."

DOTNET=dotnet
command -v dotnet >/dev/null || { echo "dotnet is required" >&2; exit 1; }
$DOTNET publish src/YazioTool.Cli/YazioTool.Cli.csproj -c Release -r linux-x64 \
  -p:DebugType=none -p:EnableCompressionInSingleFile=false -o artifacts/skill-cli

python3 - <<'PY'
import zipfile
from pathlib import Path

import shutil

skill = Path("skill/yazio")
bundled = skill / "bin" / "yazio-linux-x64"
bundled.parent.mkdir(exist_ok=True)
for stale in bundled.parent.iterdir():
    stale.unlink()
shutil.copy2("artifacts/skill-cli/yazio", bundled)

out = Path("artifacts/skill/yazio.skill")
out.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(skill.rglob("*")):
        rel = path.relative_to(skill)
        if path.is_dir() or rel.parts[0] == "evals" or "__pycache__" in rel.parts:
            continue
        zf.write(path, Path("yazio") / rel, compress_type=zipfile.ZIP_DEFLATED, compresslevel=9)
print(f"{out} ({out.stat().st_size / 1024 / 1024:.1f} MB)")
PY
