#!/usr/bin/env bash
# Android Auto Desktop Head Unit (DHU) sul Mac, collegato al telefono via USB.
#
#   tools/dhu.sh start            inoltra la porta 5277 e apre il DHU (il server unità principale
#                                 deve essere già avviato sul telefono)
#   tools/dhu.sh next|prev|play|pause|playpause
#                                 manda il tasto media al telefono (come i tasti del volante Bluetooth)
#   tools/dhu.sh restart          chiude l'app (force-stop, anche il servizio) e la riapre: avvio da zero
#   tools/dhu.sh log              logcat filtrato sull'app (Ctrl+C per uscire)
#   tools/dhu.sh status           dispositivi adb e sessioni media dell'app
#
# Ambiente: ANDROID_SERIAL (default: il primo dispositivo fisico, escluso l'emulatore),
#           ADB, DHU (percorsi del SDK), PKG (default: ricavato dal telefono).

set -uo pipefail

SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
ADB="${ADB:-$SDK/platform-tools/adb}"
DHU="${DHU:-$SDK/extras/google/auto/desktop-head-unit}"

if [[ ! -x "$ADB" ]]; then echo "adb non trovato: $ADB" >&2; exit 1; fi

# Primo dispositivo in stato "device" che non sia un emulatore.
SERIAL="${ANDROID_SERIAL:-$("$ADB" devices | awk '$2=="device" && $1 !~ /^emulator-/ {print $1; exit}')}"
if [[ -z "$SERIAL" ]]; then
  echo "Nessun telefono collegato (adb devices). Controlla cavo e debug USB." >&2
  exit 1
fi

adb() { "$ADB" -s "$SERIAL" "$@"; }

pkg() {
  if [[ -n "${PKG:-}" ]]; then echo "$PKG"; return; fi
  adb shell pm list packages 2>/dev/null | tr -d '\r' | sed 's/^package://' | grep -i 'radioe45' | head -1
}

key() { adb shell input keyevent "KEYCODE_$1"; echo "inviato KEYCODE_$1 a $SERIAL"; }

case "${1:-}" in
  start)
    if [[ ! -x "$DHU" ]]; then
      echo "DHU non trovato: $DHU" >&2
      echo "Installalo da SDK Manager: Android Auto Desktop Head Unit Emulator." >&2
      exit 1
    fi
    adb forward tcp:5277 tcp:5277 || exit 1
    echo "Telefono: $SERIAL. Porta 5277 inoltrata. Apro il DHU (Ctrl+C o 'quit' per uscire)."
    echo "Se non si connette: sul telefono Android Auto -> menu -> 'Avvia server unità principale'."
    cd "$(dirname "$DHU")" && exec "$DHU"
    ;;
  next)      key MEDIA_NEXT ;;
  prev)      key MEDIA_PREVIOUS ;;
  play)      key MEDIA_PLAY ;;
  pause)     key MEDIA_PAUSE ;;
  playpause) key MEDIA_PLAY_PAUSE ;;
  restart)
    P="$(pkg)"
    if [[ -z "$P" ]]; then echo "App RadioE45 non installata sul telefono." >&2; exit 1; fi
    adb shell am force-stop "$P"
    sleep 1
    adb shell monkey -p "$P" -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
    echo "App $P riavviata da zero su $SERIAL."
    ;;
  log)
    P="$(pkg)"
    if [[ -z "$P" ]]; then echo "App RadioE45 non installata sul telefono." >&2; exit 1; fi
    adb logcat -c
    # Filtra sul processo dell'app; se il processo non c'è ancora aspetta che parta.
    echo "Log di $P su $SERIAL (Ctrl+C per uscire)"
    while true; do
      PID="$(adb shell pidof "$P" 2>/dev/null | tr -d '\r')"
      if [[ -n "$PID" ]]; then adb logcat --pid="$PID" -v time; fi
      sleep 1
    done
    ;;
  status)
    "$ADB" devices -l
    P="$(pkg)"
    echo "Pacchetto: ${P:-non installato}"
    adb shell dumpsys media_session 2>/dev/null | grep -A12 -i "${P:-radioe45}" | head -40
    ;;
  *)
    sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'
    exit 1
    ;;
esac
