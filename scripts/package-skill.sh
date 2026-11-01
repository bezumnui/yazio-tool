#!/usr/bin/env bash
# Builds the CLI and packages the skill into ./artifacts/skill/yazio.skill
# with the binary bundled and an empty credentials.json.
set -euo pipefail

cd "$(dirname "$0")/.."
./scripts/publish-cli.sh linux-x64

python3 - <<'PY'
import zipfile
from pathlib import Path

skill = Path("skill/yazio")
binary = Path("artifacts/cli/linux-x64/yazio")
archive = skill / "bin" / "yazio-linux-x64.zip"
archive.parent.mkdir(exist_ok=True)
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
    zf.write(binary, "yazio")

out = Path("artifacts/skill/yazio.skill")
out.parent.mkdir(parents=True, exist_ok=True)
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(skill.rglob("*")):
        rel = path.relative_to(skill)
        if path.is_dir() or rel.parts[0] == "evals" or "__pycache__" in rel.parts:
            continue
        # The bundled binary archive is already compressed.
        compress = zipfile.ZIP_STORED if path.suffix == ".zip" else zipfile.ZIP_DEFLATED
        zf.write(path, Path("yazio") / rel, compress_type=compress)
print(f"{out} ({out.stat().st_size / 1024 / 1024:.1f} MB)")
PY
