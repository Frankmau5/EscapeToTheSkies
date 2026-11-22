#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/EscapeToTheSkies/bin/Release/net8.0/linux-x64/publish"
GAME_BIN="$PUBLISH_DIR/EscapeToTheSkies"

LOG_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/EscapeToTheSkies"
if ! mkdir -p "$LOG_DIR" 2>/dev/null; then
    LOG_DIR="/tmp/EscapeToTheSkies"
    mkdir -p "$LOG_DIR"
fi

if [[ ! -x "$GAME_BIN" ]]; then
    echo "Standalone build missing at $GAME_BIN" >"$LOG_DIR/launch.log"
    exit 1
fi

cd "$PUBLISH_DIR"
export LD_LIBRARY_PATH="$PUBLISH_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$GAME_BIN" "$@" 2>"$LOG_DIR/launch.log"
