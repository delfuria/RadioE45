#!/usr/bin/env bash
# Claude Code hooks for the MAUI ↔ Uno ↔ TV parity system (docs/LINUX-UNO-ROADMAP.md §6.4, §7.3;
# docs/ANDROID-TV-ROADMAP.md §5).
# Registered in .claude/settings.json; reads the hook input JSON on stdin.
#
#   tools/parity-hook.sh post-edit   PostToolUse (Edit|Write): reminder after a MAUI UI change
#   tools/parity-hook.sh stop        Stop: runs ParityCheck, blocks the stop only on exit 2
#
# Requires python3 (JSON handling) and the .NET SDK.

set -uo pipefail

mode="${1:-}"
root="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/.." && pwd)}"
input="$(cat)"

json_field() { # json_field <python expression on d>
    printf '%s' "$input" | python3 -c "import json,sys
try: d=json.load(sys.stdin)
except Exception: d={}
v=$1
print('' if v is None else v)"
}

case "$mode" in
post-edit)
    file="$(json_field "d.get('tool_input',{}).get('file_path')")"
    file="${file//\\//}"
    case "$file" in
    */RadioE45/Views/Tv/*)
        python3 -c 'import json,sys; print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": sys.argv[1]}}))' \
            "Modifica vista TV ($(basename "$file")): aggiornare tv (e tvNotes) in parity/features.yaml e il campo tv in parity/pages.yaml, con la skill tv-parity-port. Verificare la mappa del focus sull'emulatore (docs/ANDROID-TV-ROADMAP.md §2.3) e lanciare dotnet run --project tools/ParityCheck -- --include-tv."
        ;;
    */RadioE45/Views/* | */RadioE45/AppShell.xaml* | */RadioE45.Core/ViewModels/* | */RadioE45/Resources/Styles/*)
        python3 -c 'import json,sys; print(json.dumps({"hookSpecificOutput": {"hookEventName": "PostToolUse", "additionalContext": sys.argv[1]}}))' \
            "Modifica UI MAUI ($(basename "$file")): aggiornare parity/features.yaml (feature nuova o modificata, desktop/web/tv = todo se non portata; tv = n/a con tvNotes se non ha senso su TV) e portarla su RadioE45.Uno con la skill ui-parity-port, oppure dichiarare il debito. Se è in ambito Android TV, usare anche la skill tv-parity-port. Se cambia l'API pubblica di un ViewModel: dotnet run --project tools/ParityCheck -- --update-surface."
        ;;
    esac
    exit 0
    ;;

stop)
    stop_active="$(json_field "d.get('stop_hook_active')")"
    dll="$root/tools/ParityCheck/bin/Debug/net10.0/ParityCheck.dll"
    stamp="$root/tools/ParityCheck/obj/parity-hook.stamp"

    # Rebuild only when Core or the tool changed since the last hook build (keeps the hook < 5 s).
    if [[ ! -f "$dll" || ! -f "$stamp" ]] ||
        [[ -n "$(find "$root/RadioE45.Core" "$root/tools/ParityCheck" \
            \( -name bin -o -name obj \) -prune -o \( -name '*.cs' -o -name '*.csproj' -o -name '*.resx' \) -newer "$stamp" -print -quit)" ]]; then
        if ! build_log="$(dotnet build "$root/tools/ParityCheck/ParityCheck.csproj" -c Debug -v q -nologo 2>&1)"; then
            python3 -c 'import json; print(json.dumps({"systemMessage": "ParityCheck non eseguito: la build di tools/ParityCheck (o di RadioE45.Core) non riesce."}))'
            exit 0
        fi
        touch "$stamp"
    fi

    report="$(cd "$root" && dotnet "$dll" --json 2>/dev/null)"
    code=$?

    if [[ $code -eq 2 ]]; then
        if [[ "$stop_active" == "True" ]]; then
            # Already continuing because of this hook: warn the user instead of looping.
            printf '%s' "$report" | python3 -c 'import json,sys
r=json.load(sys.stdin)
print(json.dumps({"systemMessage": "ParityCheck: disallineamento non dichiarato ancora presente (%d errori). Eseguire: dotnet run --project tools/ParityCheck" % len(r["errors"])}))'
            exit 0
        fi
        printf '%s' "$report" | python3 -c 'import json,sys
r=json.load(sys.stdin)
lines="\n".join("- [%s] %s" % (e["check"], e["message"]) for e in r["errors"])
print(json.dumps({"decision": "block", "reason":
    "ParityCheck ha trovato un disallineamento MAUI/Uno non dichiarato (exit 2). "
    "Correggere oppure dichiarare il debito in parity/features.yaml (stato todo) / pages.yaml (ignore con motivo), "
    "poi rilanciare dotnet run --project tools/ParityCheck. Errori:\n" + lines}))'
    fi
    if [[ $code -ne 0 && $code -ne 1 && $code -ne 2 ]]; then
        python3 -c 'import json,sys; print(json.dumps({"systemMessage": "ParityCheck terminato in modo anomalo (exit %s)." % sys.argv[1]}))' "$code"
        exit 0
    fi
    # Exit 0 (aligned) and 1 (declared debt only) do not interrupt: the debt list is long and
    # would be repeated at every stop. Read it with: dotnet run --project tools/ParityCheck
    exit 0
    ;;

*)
    echo "usage: $0 post-edit|stop" >&2
    exit 1
    ;;
esac
