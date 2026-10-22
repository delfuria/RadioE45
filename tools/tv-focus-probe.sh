#!/usr/bin/env bash
# Mappa del focus D-pad su Android TV (docs/ANDROID-TV-ROADMAP.md §2.3).
# Manda tasti del telecomando all'emulatore o al TV e legge con uiautomator quale elemento ha il focus.
#
#   tools/tv-focus-probe.sh now                       focus attuale
#   tools/tv-focus-probe.sh keys DOWN RIGHT CENTER    manda i tasti e stampa il focus dopo ciascuno
#   tools/tv-focus-probe.sh tour [--expect "A,B,C"]   percorso a serpentina con il D-pad; con --expect
#                                                      esce con 1 se qualche etichetta non è raggiunta
#
# Tasti: UP DOWN LEFT RIGHT CENTER (D-pad); ogni altro nome diventa KEYCODE_<nome> (BACK, HOME, MEDIA_PLAY_PAUSE).
# Ambiente: ANDROID_SERIAL (default emulator-5554), ADB (default ~/Library/Android/sdk/platform-tools/adb).
# Richiede python3 e uno schermo dell'app già avviato (non preme CENTER da solo nel tour).

set -uo pipefail

ADB="${ADB:-$HOME/Library/Android/sdk/platform-tools/adb}"
SERIAL="${ANDROID_SERIAL:-emulator-5554}"
SETTLE="${SETTLE:-0.7}"
MAX_STEPS="${MAX_STEPS:-14}"

adb() { "$ADB" -s "$SERIAL" "$@"; }

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Stampa "etichetta|classe|bounds" dell'elemento col focus più profondo, oppure "none||".
focus() {
    adb shell uiautomator dump /sdcard/tv-focus.xml >/dev/null 2>&1
    adb pull /sdcard/tv-focus.xml "$tmp/ui.xml" >/dev/null 2>&1
    python3 -I - "$tmp/ui.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
try:
    root = ET.parse(sys.argv[1]).getroot()
except Exception:
    print("none||")
    raise SystemExit
focused = [n for n in root.iter("node") if n.get("focused") == "true"]
if not focused:
    print("none||")
else:
    n = focused[-1]
    label = n.get("text") or n.get("content-desc") or ""
    print(f'{label}|{n.get("class")}|{n.get("bounds")}')
PY
}

press() {
    local key="$1"
    case "$key" in
    UP | DOWN | LEFT | RIGHT | CENTER) key="DPAD_$key" ;;
    esac
    adb shell input keyevent "KEYCODE_$key"
    sleep "$SETTLE"
}

show() { # etichetta leggibile da "label|class|bounds"
    local label="${1%%|*}" rest="${1#*|}"
    printf '%s  [%s]\n' "${label:-(senza etichetta)}" "${rest//|/ }"
}

mode="${1:-}"
case "$mode" in
now)
    show "$(focus)"
    ;;

keys)
    shift
    echo "start -> $(show "$(focus)")"
    for k in "$@"; do
        press "$k"
        echo "$k -> $(show "$(focus)")"
    done
    ;;

tour)
    shift
    expect=""
    if [[ "${1:-}" == "--expect" ]]; then expect="${2:-}"; fi

    seen_file="$tmp/seen"
    : >"$seen_file"
    record() { grep -qxF -- "$1" "$seen_file" || printf '%s\n' "$1" >>"$seen_file"; }

    # Percorre una riga in una direzione finché il focus non cambia più.
    walk() {
        local dir="$1" cur next steps=0
        cur="$(focus)"
        record "$cur"
        while ((steps < MAX_STEPS)); do
            press "$dir"
            next="$(focus)"
            [[ "$next" == "$cur" ]] && break
            record "$next"
            cur="$next"
            ((steps++))
        done
    }

    row=0
    while ((row < 8)); do
        walk LEFT
        walk RIGHT
        before="$(focus)"
        press DOWN
        after="$(focus)"
        [[ "$after" == "$before" ]] && break
        record "$after"
        ((row++))
    done

    echo "Elementi raggiunti dal D-pad:"
    while IFS= read -r line; do echo "  - $(show "$line")"; done <"$seen_file"

    if [[ -n "$expect" ]]; then
        missing=0
        IFS=',' read -ra wanted <<<"$expect"
        for w in "${wanted[@]}"; do
            w="${w#"${w%%[![:space:]]*}"}"
            w="${w%"${w##*[![:space:]]}"}"
            if ! grep -qF -- "$w" "$seen_file"; then
                echo "NON RAGGIUNTO: $w"
                missing=1
            fi
        done
        if ((missing)); then exit 1; fi
        echo "Tutte le etichette attese sono raggiungibili."
    fi
    ;;

*)
    sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
