#!/usr/bin/env bash
set -Eeuo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WEB_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$WEB_DIR"
RUN_ID="$(date -u +%Y%m%dT%H%M%SZ)"
OUT_DIR="$WEB_DIR/test-results/unit-$RUN_ID"
mkdir -p "$OUT_DIR"
export SORA_IT_TRX_DIR="$OUT_DIR"

if command -v dotnet >/dev/null 2>&1; then
  MSBUILDDISABLENODEREUSE=1 dotnet test tests/Sora.UnitTests/Sora.UnitTests.csproj -m:1 \
    --results-directory "$OUT_DIR" --logger trx --collect:"XPlat Code Coverage"
else
  command -v podman >/dev/null 2>&1 || { echo "Instala .NET SDK 10 o Podman." >&2; exit 2; }
  podman run --rm --network host -v "$WEB_DIR:/src:Z" -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
    dotnet test tests/Sora.UnitTests/Sora.UnitTests.csproj -m:1 \
      --results-directory "/src/test-results/unit-$RUN_ID" --logger trx --collect:"XPlat Code Coverage"
fi
python3 scripts/publish-test-report.py
