#!/usr/bin/env bash
# Verifies the now_playing.elapsed fix against the running buildDelfo stack.
#
# Checks:
#   1. REST: polls /api/nowplaying/{station} repeatedly and prints the
#      Cache-Control/X-Accel-Expires headers (should be absent/no-cache now,
#      previously "public, max-age=15") and the elapsed value (should tick up
#      every request instead of freezing in 15s steps).
#   2. Tells you how to watch the SSE (Centrifugo) channel for the same station.
#
# Usage: ./test-fix.sh [station_shortcode] [base_url]
# If station_shortcode is omitted, it's auto-detected from /api/nowplaying.
# base_url defaults to the real RadioE45 AzuraCast instance; override with
# AZURACAST_BASE_URL or the second positional arg to point at Muse
# (https://hear.moe) or any other instance.

set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

BASE_URL="${2:-${AZURACAST_BASE_URL:-https://radioe45.ddns.net}}"
STATION="${1:-}"

if [[ -z "$STATION" ]]; then
  echo "==> No station given, auto-detecting from ${BASE_URL}/api/nowplaying ..."
  STATION=$(curl -fsS "${BASE_URL}/api/nowplaying" | jq -r '.[0].station.shortcode // empty')
  if [[ -z "$STATION" ]]; then
    echo "ERROR: could not auto-detect a station. Pass one explicitly: ./test-fix.sh <shortcode>" >&2
    echo "List stations with: curl -s ${BASE_URL}/api/nowplaying | jq '.[].station.shortcode'" >&2
    exit 1
  fi
  echo "==> Using station: $STATION"
fi

URL="${BASE_URL}/api/nowplaying/${STATION}"

echo "==> Polling ${URL} every 2s, 10 times."
echo "==> Watch the 'elapsed' column: it should increase on every single request."
echo "==> Watch 'cache-control': it should be absent/no-cache (was 'public, max-age=15' before the fix)."
echo

for i in $(seq 1 10); do
  RESPONSE_HEADERS=$(curl -fsS -D - -o /tmp/np_body.json "$URL")
  ELAPSED=$(jq -r '.now_playing.elapsed // "n/a"' /tmp/np_body.json)
  REMAINING=$(jq -r '.now_playing.remaining // "n/a"' /tmp/np_body.json)
  DURATION=$(jq -r '.now_playing.duration // "n/a"' /tmp/np_body.json)
  SONG=$(jq -r '.now_playing.song.text // "n/a"' /tmp/np_body.json)
  PLAYLIST=$(jq -r '.now_playing.playlist // "n/a"' /tmp/np_body.json)
  IS_LIVE=$(jq -r '.live.is_live // "n/a"' /tmp/np_body.json)
  STREAMER=$(jq -r '.live.streamer_name // "n/a"' /tmp/np_body.json)
  LISTENERS=$(jq -r '.listeners.current // "n/a"' /tmp/np_body.json)
  CACHE_CONTROL=$(echo "$RESPONSE_HEADERS" | grep -i '^cache-control:' || echo "cache-control: (none)")
  printf '[%2d] elapsed=%-6s remaining=%-6s duration=%-6s listeners=%-4s live=%-5s streamer=%-12s playlist=%-15s song=%s\n' \
    "$i" "$ELAPSED" "$REMAINING" "$DURATION" "$LISTENERS" "$IS_LIVE" "$STREAMER" "$PLAYLIST" "$SONG"
  echo "     $CACHE_CONTROL"
  sleep 2
done

rm -f /tmp/np_body.json

echo
echo "For the SSE side, subscribe to the station's Centrifugo channel per:"
echo "  https://www.azuracast.com/docs/developers/now-playing-data/"
echo "and confirm 'elapsed' in each push matches wall-clock time since the track started,"
echo "not a value frozen at the last sync-cycle/webhook-dispatch instant."
