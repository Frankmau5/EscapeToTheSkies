#!/usr/bin/env bash
set -euo pipefail

# Builds a single self-extracting Steam Deck / Linux installer for itch.io.
# Output: ./EscapeToTheSkies-SteamDeck-Linux-Installer.sh

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_FILE="$ROOT_DIR/EscapeToTheSkies-SteamDeck-Linux-Installer.sh"
STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/ets-release-stage.XXXXXX")"
trap 'rm -rf "$STAGE_DIR"' EXIT

APP_STAGE="$STAGE_DIR/app"
mkdir -p "$APP_STAGE"

echo "Publishing single-file self-contained linux-x64 build..."
PUBLISH_ARGS=(
  "$ROOT_DIR/EscapeToTheSkies/EscapeToTheSkies.csproj"
  -c Release
  -r linux-x64
  --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=true
  -p:DebugType=none
  -p:DebugSymbols=false
  -o "$APP_STAGE"
)

if ! dotnet publish "${PUBLISH_ARGS[@]}" --no-restore; then
  dotnet publish "${PUBLISH_ARGS[@]}"
fi

cp "$ROOT_DIR/assets/icon.png" "$APP_STAGE/escape-to-the-skies.png"

cat >"$APP_STAGE/launch.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/EscapeToTheSkies"
mkdir -p "$LOG_DIR" 2>/dev/null || LOG_DIR="/tmp/EscapeToTheSkies"
mkdir -p "$LOG_DIR" 2>/dev/null || true

cd "$SCRIPT_DIR"
export LD_LIBRARY_PATH="$SCRIPT_DIR${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
exec "$SCRIPT_DIR/EscapeToTheSkies" "$@" 2>"$LOG_DIR/launch.log"
EOF
chmod +x "$APP_STAGE/launch.sh" "$APP_STAGE/EscapeToTheSkies"

cat >"$APP_STAGE/uninstall.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
DESKTOP_DIR=""
if [[ -n "${XDG_DESKTOP_DIR:-}" ]]; then
  DESKTOP_DIR="$XDG_DESKTOP_DIR"
elif command -v xdg-user-dir >/dev/null 2>&1; then
  DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
fi
DESKTOP_DIR="${DESKTOP_DIR:-$HOME/Desktop}"

rm -f "$DATA_HOME/applications/EscapeToTheSkies.desktop"
rm -f "$DESKTOP_DIR/EscapeToTheSkies.desktop"
rm -f "$DATA_HOME/icons/hicolor/256x256/apps/escape-to-the-skies.png"
rm -f "$DATA_HOME/pixmaps/escape-to-the-skies.png"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$DATA_HOME/applications" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f -t "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
fi

rm -rf "$SCRIPT_DIR"
echo "Uninstalled Escape to the Skies (save data in ~/.config/EscapeToTheSkies preserved)."
EOF
chmod +x "$APP_STAGE/uninstall.sh"

PAYLOAD_TAR="$STAGE_DIR/payload.tar.gz"
tar -C "$APP_STAGE" -czf "$PAYLOAD_TAR" .

cat >"$OUT_FILE" <<'INSTALLER_HEADER'
#!/usr/bin/env bash
set -euo pipefail

APP_NAME="Escape to the Skies"
DESKTOP_ID="EscapeToTheSkies.desktop"
ICON_NAME="escape-to-the-skies"

DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
INSTALL_DIR="$DATA_HOME/EscapeToTheSkies"
APPS_DIR="$DATA_HOME/applications"
ICON_HICOLOR_DIR="$DATA_HOME/icons/hicolor/256x256/apps"
ICON_PIXMAPS_DIR="$DATA_HOME/pixmaps"

DESKTOP_DIR=""
if [[ -n "${XDG_DESKTOP_DIR:-}" ]]; then
  DESKTOP_DIR="$XDG_DESKTOP_DIR"
elif command -v xdg-user-dir >/dev/null 2>&1; then
  DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
fi
DESKTOP_DIR="${DESKTOP_DIR:-$HOME/Desktop}"

YES_MODE=0
NO_LAUNCH=0
UNINSTALL_MODE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -y|--yes|--silent)
      YES_MODE=1
      NO_LAUNCH=1
      ;;
    --no-launch)
      NO_LAUNCH=1
      ;;
    --uninstall)
      UNINSTALL_MODE=1
      ;;
    --dir)
      if [[ $# -lt 2 ]]; then
        echo "Missing argument for --dir" >&2
        exit 1
      fi
      INSTALL_DIR="$2"
      shift
      ;;
    -h|--help)
      cat <<HELP
Escape to the Skies - Steam Deck & Linux Installer
Usage: $0 [options]
  --yes, --silent   Install without interactive prompts
  --no-launch       Do not prompt to launch after installation
  --dir <path>      Custom installation directory (default: $INSTALL_DIR)
  --uninstall       Remove installed files, Desktop shortcut, and Menu entry
HELP
      exit 0
      ;;
  esac
  shift
done

use_gui() {
  [[ $YES_MODE -eq 0 && ! -t 0 && ( -n "${DISPLAY:-}" || -n "${WAYLAND_DISPLAY:-}" ) ]]
}

notify_msg() {
  local title="$1"
  local msg="$2"
  if use_gui && command -v kdialog >/dev/null 2>&1; then
    kdialog --title "$title" --msgbox "$msg" >/dev/null 2>&1 || true
  elif use_gui && command -v zenity >/dev/null 2>&1; then
    zenity --info --title="$title" --text="$msg" >/dev/null 2>&1 || true
  else
    printf '%s\n' "$msg"
  fi
}

ask_yes_no() {
  local title="$1"
  local msg="$2"
  if [[ $YES_MODE -eq 1 ]]; then
    return 0
  fi
  if use_gui && command -v kdialog >/dev/null 2>&1; then
    kdialog --title "$title" --yesno "$msg" >/dev/null 2>&1
    return $?
  elif use_gui && command -v zenity >/dev/null 2>&1; then
    zenity --question --title="$title" --text="$msg" >/dev/null 2>&1
    return $?
  fi
  return 0
}

do_uninstall() {
  rm -f "$APPS_DIR/$DESKTOP_ID"
  rm -f "$DESKTOP_DIR/$DESKTOP_ID"
  rm -f "$ICON_HICOLOR_DIR/${ICON_NAME}.png"
  rm -f "$ICON_PIXMAPS_DIR/${ICON_NAME}.png"
  rm -rf "$INSTALL_DIR"
  command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS_DIR" >/dev/null 2>&1 || true
  command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -f -t "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
  notify_msg "$APP_NAME" "$APP_NAME has been uninstalled from your system."
  exit 0
}

if [[ $UNINSTALL_MODE -eq 1 ]]; then
  do_uninstall
fi

if ! ask_yes_no "$APP_NAME Installer" "Install $APP_NAME for Steam Deck / Linux?\n\nThis will install the game into:\n$INSTALL_DIR\nand add shortcuts to your Desktop and Application Menu."; then
  exit 0
fi

ARCHIVE_LINE="$(awk '/^__ARCHIVE_BELOW__$/ {print NR + 1; exit 0;}' "$0")"
if [[ -z "$ARCHIVE_LINE" ]]; then
  echo "Error: Embedded payload marker not found." >&2
  exit 1
fi

mkdir -p "$INSTALL_DIR" "$APPS_DIR" "$ICON_HICOLOR_DIR" "$ICON_PIXMAPS_DIR" "$DESKTOP_DIR"

echo "Extracting $APP_NAME to $INSTALL_DIR..."
tail -n +"$ARCHIVE_LINE" "$0" | tar -xz -C "$INSTALL_DIR"
chmod +x "$INSTALL_DIR/EscapeToTheSkies" "$INSTALL_DIR/launch.sh" "$INSTALL_DIR/uninstall.sh"

# Install 256x256 icon into standard XDG icon directories
cp -f "$INSTALL_DIR/${ICON_NAME}.png" "$ICON_HICOLOR_DIR/${ICON_NAME}.png"
cp -f "$INSTALL_DIR/${ICON_NAME}.png" "$ICON_PIXMAPS_DIR/${ICON_NAME}.png"

DESKTOP_FILE="$APPS_DIR/$DESKTOP_ID"
cat >"$DESKTOP_FILE" <<DESKTOP_EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=Escape to the Skies
GenericName=Vertical Speedrun Platformer
Comment=Climb from the Deep Chasm ruins to the sunlit skies and beat your best time!
Exec="$INSTALL_DIR/launch.sh"
Path=$INSTALL_DIR
Icon=$INSTALL_DIR/${ICON_NAME}.png
Terminal=false
Categories=Game;ActionGame;
Keywords=platformer;speedrun;steamdeck;indie;
StartupNotify=true
StartupWMClass=EscapeToTheSkies
DESKTOP_EOF
chmod 0755 "$DESKTOP_FILE"

if command -v desktop-file-validate >/dev/null 2>&1; then
  desktop-file-validate "$DESKTOP_FILE" || true
fi

# Install Desktop icon shortcut
DESKTOP_SHORTCUT="$DESKTOP_DIR/$DESKTOP_ID"
cp -f "$DESKTOP_FILE" "$DESKTOP_SHORTCUT"
chmod 0755 "$DESKTOP_SHORTCUT"

if command -v gio >/dev/null 2>&1; then
  gio set "$DESKTOP_SHORTCUT" metadata::trusted true >/dev/null 2>&1 || true
fi
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$APPS_DIR" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f -t "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installed successfully:"
echo "  App:     $INSTALL_DIR/launch.sh"
echo "  Menu:    $DESKTOP_FILE"
echo "  Desktop: $DESKTOP_SHORTCUT"
echo "  Icon:    $ICON_HICOLOR_DIR/${ICON_NAME}.png"

if [[ $NO_LAUNCH -eq 0 ]] && use_gui; then
  if ask_yes_no "$APP_NAME Installed!" "$APP_NAME has been installed with Desktop and Application Menu shortcuts.\n\nLaunch $APP_NAME now?"; then
    nohup "$INSTALL_DIR/launch.sh" >/dev/null 2>&1 &
  fi
fi

exit 0
__ARCHIVE_BELOW__
INSTALLER_HEADER

cat "$PAYLOAD_TAR" >>"$OUT_FILE"
chmod 0755 "$OUT_FILE"

ZIP_FILE="$ROOT_DIR/EscapeToTheSkies-SteamDeck-Linux.zip"
rm -f "$ZIP_FILE"
(cd "$ROOT_DIR" && zip -q "$ZIP_FILE" "$(basename "$OUT_FILE")")

ls -lh "$OUT_FILE" "$ZIP_FILE"
echo "Built single-file installer: $OUT_FILE"
echo "Built itch.io zip archive:   $ZIP_FILE"
