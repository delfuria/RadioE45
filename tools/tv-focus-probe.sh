#!/usr/bin/env bash
# Mappa del focus D-pad su Android TV (docs/ANDROID-TV-ROADMAP.md §2.3).
# Manda tasti del telecomando all'emulatore o al TV e legge con uiautomator quale elemento ha il focus.
#
#   tools/tv-focus-probe.sh now                       focus attuale
#   tools/tv-focus-probe.sh keys DOWN RIGHT CENTER    manda i tasti e stampa il focus dopo ciascuno
#   tools/tv-focus-probe.sh tour [--expect "A,B,C"]   esplora tutti i tasti del D-pad da ogni elemento raggiunto; con
#                                                      --expect esce con 1 se qualche etichetta non è raggiunta (TRACE=1 mostra gli archi)
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

    # Esplora il grafo del focus: da ogni elemento raggiunto prova SU/GIÙ/SINISTRA/DESTRA, ricorda dove
    # porta ogni tasto e, finita la strada davanti, torna (seguendo gli archi noti) a un elemento con
    # direzioni ancora da provare. Un percorso "per righe" non basta: in un layout con una fila in alto
    # e una lista sotto, SINISTRA dalla lista salta alla fila (cerca il vicino più prossimo nella direzione).
    ADB="$ADB" SERIAL="$SERIAL" SETTLE="$SETTLE" MAX_PRESSES="${MAX_PRESSES:-80}" TRACE="${TRACE:-}" EXPECT="$expect" \
        python3 -I - <<'PY'
import os, subprocess, sys, tempfile, time
import xml.etree.ElementTree as ET
from collections import deque

adb, serial = os.environ["ADB"], os.environ["SERIAL"]
settle, max_presses = float(os.environ["SETTLE"]), int(os.environ["MAX_PRESSES"])
trace, expect = bool(os.environ.get("TRACE")), os.environ.get("EXPECT", "")
keys = {"UP": "KEYCODE_DPAD_UP", "DOWN": "KEYCODE_DPAD_DOWN", "LEFT": "KEYCODE_DPAD_LEFT", "RIGHT": "KEYCODE_DPAD_RIGHT"}
dirs = ["DOWN", "RIGHT", "UP", "LEFT"]
tmp = tempfile.mkdtemp()

def run(*a):
    return subprocess.run([adb, "-s", serial, *a], capture_output=True, text=True)

def focus():
    run("shell", "uiautomator", "dump", "/sdcard/tv-focus.xml")
    path = os.path.join(tmp, "ui.xml")
    run("pull", "/sdcard/tv-focus.xml", path)
    try:
        nodes = [n for n in ET.parse(path).getroot().iter("node") if n.get("focused") == "true"]
    except Exception:
        return ("none", "", "")
    if not nodes:
        return ("none", "", "")
    n = nodes[-1]
    return (n.get("text") or n.get("content-desc") or "", n.get("class") or "", n.get("bounds") or "")

def label(state):
    text = state[0]
    if not text:
        return "(senza etichetta)"
    if not text.isprintable():
        return "(icona U+%04X)" % ord(text[0])
    return text

presses = 0
def press(d):
    global presses
    run("shell", "input", "keyevent", keys[d])
    presses += 1
    time.sleep(settle)

edges, seen = {}, []
def see(state):
    if state not in seen:
        seen.append(state)

cur = focus()
see(cur)

def untried(state):
    return [d for d in dirs if (state, d) not in edges]

def route_to_untried(start):
    queue, visited = deque([(start, [])]), {start}
    while queue:
        state, path = queue.popleft()
        if state != start and untried(state):
            return path
        for d in dirs:
            nxt = edges.get((state, d))
            if nxt is not None and nxt not in visited:
                visited.add(nxt)
                queue.append((nxt, path + [d]))
    return None

while presses < max_presses:
    todo = untried(cur)
    if todo:
        d = todo[0]
        press(d)
        nxt = focus()
        edges[(cur, d)] = nxt
        if trace:
            print(f"  {label(cur)} --{d}--> {label(nxt)}", file=sys.stderr)
        see(nxt)
        cur = nxt
        continue
    route = route_to_untried(cur)
    if not route:
        break
    for d in route:
        press(d)
    cur = focus()
    see(cur)

print("Elementi raggiunti dal D-pad:")
for s in seen:
    print(f"  - {label(s)}  [{s[1]} {s[2]}]")
if presses >= max_presses:
    print(f"(fermato dopo {max_presses} tasti: alzare MAX_PRESSES se la pagina è grande)")

if expect:
    labels = [s[0] for s in seen]
    missing = [w.strip() for w in expect.split(",") if w.strip() and not any(w.strip() in l for l in labels)]
    for w in missing:
        print(f"NON RAGGIUNTO: {w}")
    if missing:
        sys.exit(1)
    print("Tutte le etichette attese sono raggiungibili.")
PY
    exit $?
    ;;

*)
    sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
