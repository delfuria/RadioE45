#!/bin/bash
# Install the MAUI Android app on the connected phone and start the Android Auto Desktop Head Unit (DHU).
#
#   tools/dhu-test.sh              build + install + forward port + start DHU (+ logcat in logs/dhu-logcat.txt)
#   tools/dhu-test.sh --no-build   skip the build/install (app already on the phone)
#   tools/dhu-test.sh --serial X   pick a device when several are connected
#
# Before the DHU can connect, on the phone: Android Auto -> Developer settings -> "Start head unit server"
# (open Android Auto settings, tap Version ten times once to enable developer mode).
set -euo pipefail

SDK="${ANDROID_SDK_ROOT:-$HOME/Library/Android/sdk}"
ADB="$SDK/platform-tools/adb"
DHU="$SDK/extras/google/auto/desktop-head-unit"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD=1
SERIAL=""

while [ $# -gt 0 ]; do
  case "$1" in
    --no-build) BUILD=0 ;;
    --serial) SERIAL="$2"; shift ;;
    *) echo "unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done

if [ -z "$SERIAL" ]; then
  SERIAL="$("$ADB" devices | awk 'NR>1 && $2=="device" && $1 !~ /^emulator-/ {print $1; exit}')"
  [ -n "$SERIAL" ] || SERIAL="$("$ADB" devices | awk 'NR>1 && $2=="device" {print $1; exit}')"
fi
[ -n "$SERIAL" ] || { echo "no Android device connected" >&2; exit 1; }
echo "device: $SERIAL"
ADB_D=("$ADB" -s "$SERIAL")

if [ "$BUILD" = 1 ]; then
  echo "== build and install"
  dotnet build "$ROOT/RadioE45" -f net10.0-android -t:Install -p:AdbTarget="-s $SERIAL"
fi

echo "== forward port 5277"
"${ADB_D[@]}" forward tcp:5277 tcp:5277

LOG="${TMPDIR:-/tmp}/radioe45-dhu-logcat.txt"
"${ADB_D[@]}" logcat -c
"${ADB_D[@]}" logcat -v time > "$LOG" 2>&1 &
LOGCAT_PID=$!
trap 'kill $LOGCAT_PID 2>/dev/null || true' EXIT
echo "logcat: $LOG (filter: grep -E 'RadioPlaybackService|RadioE45' $LOG)"

echo "== start DHU (close its window to stop)"
echo "If it says it cannot connect: on the phone start the head unit server (see the header of this script)."
"$DHU"
