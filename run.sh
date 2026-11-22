#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_BIN="$SCRIPT_DIR/EscapeToTheSkies/bin/Release/net8.0/linux-x64/publish/EscapeToTheSkies"
if [[ -x "$PUBLISH_BIN" ]]; then
    exec "$SCRIPT_DIR/launch.sh" "$@"
else
    dotnet run --project "$SCRIPT_DIR/EscapeToTheSkies/EscapeToTheSkies.csproj" -c Release -- "$@"
fi
