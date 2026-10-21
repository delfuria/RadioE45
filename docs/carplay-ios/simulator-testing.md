# CarPlay sul simulatore iOS — procedura di test

> Verificata il 21 ottobre 2026 con Xcode 26.3, simulatore iPhone 16e (iOS 26.2), .NET iOS SDK 26.2.10233.
> Ogni passo ha una misura che dice se è riuscito. Se una misura fallisce, fermarsi lì: i passi dopo non sono affidabili.

## 1. Build

```bash
dotnet build RadioE45 -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64 --no-incremental
```

`--no-incremental` serve **ogni volta che cambia un file di entitlement** (vedi §5.1). Per le altre modifiche basta la build normale.

**Misure (tutte obbligatorie):**

```bash
APP=RadioE45/bin/Debug/net10.0-ios/iossimulator-arm64/RadioE45.app
# a) entitlement nell'eseguibile (sezione __TEXT,__entitlements): deve stampare la chiave
strings -a $APP/RadioE45 | grep "com.apple.developer.carplay-audio"
otool -l $APP/RadioE45 | grep -A3 "sectname __entitlements"
# b) scena CarPlay nel manifest: deve stampare CarPlaySceneDelegate
/usr/libexec/PlistBuddy -c "Print :UIApplicationSceneManifest:UISceneConfigurations" $APP/Info.plist | grep CarPlaySceneDelegate
```

Nota: `codesign -d --entitlements -` sul bundle simulatore mostra un dizionario **vuoto** ed è normale. Per il simulatore il SDK mette gli entitlement nell'eseguibile, non nella firma. Rifirmare a mano non serve.

## 2. Installazione e collegamento CarPlay

```bash
D=<udid del simulatore>
xcrun simctl install $D $APP
xcrun simctl launch $D com.radioe45.app        # facoltativo: CarPlay può anche avviare l'app a freddo
```

Collegare il display: menu del Simulator **I/O → External Displays → CarPlay**. Il simulatore deve essere in primo piano e avviato del tutto, altrimenti la voce è disabilitata.

**Misura:** nella home CarPlay compare l'icona RadioE45. Se manca, l'entitlement non è nell'eseguibile: tornare al §1, misura a).

## 3. Interazione con la finestra CarPlay

La finestra CarPlay **non** è raggiungibile con Quern o `idb`, che lavorano solo sul display del telefono.

- Usare **click veri** del mouse (eventi `CGEvent`) sulle coordinate visive della finestra.
- **Non** usare `System Events … click at {x, y}`. È un comando di accessibilità (AXPress) sull'intero riquadro CarPlay e attiva l'elemento che ha il focus, non quello sotto il punto. Il 21 ottobre ha aperto Messaggi invece di RadioE45.

Posizione della finestra (ID, origine, dimensione in punti):

```swift
// wins.swift — swift wins.swift
import CoreGraphics
let l = CGWindowListCopyWindowInfo([.optionOnScreenOnly], kCGNullWindowID) as! [[String: Any]]
for w in l where (w[kCGWindowOwnerName as String] as? String) == "Simulator" {
    let b = w[kCGWindowBounds as String] as? [String: Any]
    print(w[kCGWindowNumber as String] ?? "", "|", b?["X"] ?? "", b?["Y"] ?? "", "|", b?["Width"] ?? "", "x", b?["Height"] ?? "")
}
```

Click (coordinate schermo in punti):

```swift
// click.swift — swift click.swift <x> <y>
import CoreGraphics
import Foundation
let p = CGPoint(x: Double(CommandLine.arguments[1])!, y: Double(CommandLine.arguments[2])!)
for t in [CGEventType.mouseMoved, .leftMouseDown, .leftMouseUp] {
    CGEvent(mouseEventSource: nil, mouseType: t, mouseCursorPosition: p, mouseButton: .left)?.post(tap: .cghidEventTap)
    usleep(80_000)
}
```

Conversione: `screencapture -l <ID>` produce un'immagine a 2x che **include la barra del titolo**. Un punto (px, py) dell'immagine corrisponde allo schermo (X + px/2, Y + py/2).

Screenshot solo della finestra (regola del progetto: mai lo schermo intero):

```bash
screencapture -x -o -l <ID> cattura.png
```

## 4. Log da raccogliere

Segnare l'ora prima di ogni azione e leggere i log da quel momento. I file `--stdout/--stderr` di `simctl launch` non vengono creati nella cartella temporanea della sessione: usare il log di sistema.

```bash
T=$(date "+%Y-%m-%d %H:%M:%S")
# ... azione ...
xcrun simctl spawn $D log show --start "$T" --style compact --predicate 'process == "RadioE45"'
```

| Cosa | Riga attesa |
|---|---|
| Entitlement letto da CarPlay | `[com.apple.carplayframework:General] Application declares audio entitlement.` |
| Scena CarPlay collegata | `Connecting to listener endpoint for scene identifier: Car[...]:com.radioe45.app` |
| Lista impostata | `Setting root template <CPListTemplate ...>` |
| Catalogo caricato | `Station loaded: <nome> online=True` |
| Riproduzione | `StateChanged: ... → Playing` |
| Now Playing | `Creating template: <CPNowPlayingTemplate ...>` |
| Scollegamento | `disconnecting Car[...]`, poi `Connection invalidated` |
| Avvio senza finestra principale | nessuna riga con `UIWindowSceneSessionRoleApplication` |

Stato del Now Playing visto dal sistema (processo `mediaremoted`):

```bash
xcrun simctl spawn $D log show --start "$T" --style compact --predicate 'process == "mediaremoted"' | grep -E "PlaybackState changed|playbackRate changed" | grep radioe45
```

## 5. Errori già incontrati e cause misurate

1. **Icona RadioE45 assente nella home CarPlay.** L'eseguibile nativo era fermo al link delle 13:50 e non conteneva `__entitlements`. La cache del link (`_LinkNativeExecutableCache` in `Xamarin.Shared.Sdk.targets`) include il **percorso** del file entitlement, non il contenuto, quindi cambiare entitlement non rifà il link. Soluzione: `--no-incremental`.
2. **Click sulla finestra CarPlay che apre l'app sbagliata.** `System Events click at` non è un click vero (§3).
3. **Voce di menu CarPlay disabilitata.** Simulatore appena riavviato o non in primo piano. Non dipende dall'app avviata: misurato abilitato anche ad app chiusa.
4. **"MAUI rimuove il manifest delle scene"** (note di giugno). Falso: era `obj/.../AppManifest.plist` non rigenerato da una build incrementale. Con il manifest nel `Info.plist` sorgente, il bundle lo contiene.
