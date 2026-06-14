# Localizzazione UI — RadioE45

## Approccio scelto: `.resx` + `x:Static`

La soluzione standard .NET per le app MAUI multi-lingua è basata sui file `.resx`.
Il framework legge automaticamente `CultureInfo.CurrentUICulture` (impostata da MAUI in
base alla lingua del dispositivo) e seleziona il file `.resx` giusto. Se la lingua non è
disponibile, usa il file di default (inglese).

Non serve alcun codice per il rilevamento della lingua: il meccanismo è automatico.

**Perché `x:Static` e non il pattern `LocalizationResourceManager`?**  
Il pattern con `INotifyPropertyChanged` serve solo se vuoi che l'app cambi lingua *senza
riavvio* mentre è in esecuzione. Qui la lingua segue l'OS: nessun cambio a runtime, quindi
`x:Static` è sufficiente e molto più semplice.

---

## 1. Struttura dei file

```
RadioE45/
└── Resources/
    └── Strings/
        ├── AppResources.resx          ← inglese (default / fallback)
        ├── AppResources.it.resx       ← italiano
        └── AppResources.fr.resx       ← (esempio per aggiungere francese)
```

---

## 2. Modifiche al `.csproj`

```xml
<PropertyGroup>
  <!-- Obbligatorio: definisce la lingua di fallback -->
  <NeutralLanguage>en-US</NeutralLanguage>
</PropertyGroup>

<!-- Facoltativo: aiuta VS Code a rigenerare il .Designer.cs dopo le modifiche ai .resx -->
<PropertyGroup>
  <CoreCompileDependsOn>PrepareResources;$(CoreCompileDependsOn)</CoreCompileDependsOn>
</PropertyGroup>
```

> **Nota:** Senza `NeutralLanguage`, `ResourceManager` restituisce `null` per la cultura
> di default e tutte le stringhe risultano vuote.

---

## 3. File `.resx`

### Come creare il file

In Rider o Visual Studio: tasto destro su `Resources/Strings` → *Add → New Item →
Resources File*. Rinominare in `AppResources.resx`.

Il build system genera automaticamente `AppResources.Designer.cs` con la classe `AppResources`
e una proprietà `static string` per ogni chiave.

Assicurarsi che nelle proprietà del file sia impostato:
- **Access Modifier:** `public`
- **Custom Tool:** `ResXFileCodeGenerator` (VS) oppure lasciare che il `.csproj` lo gestisca

### `AppResources.resx` (inglese — default)

| Key | Value |
|-----|-------|
| `Tab_OnAir` | `On Air` |
| `Tab_RadioList` | `Radio Channels` |
| `Tab_Schedule` | `Schedule` |
| `Tab_Settings` | `Settings` |
| `Badge_Live` | `• LIVE` |
| `Badge_Now` | `● NOW` |
| `Badge_Offline` | `● Off Line` |
| `Label_Next` | `Next` |
| `EmptyState_Schedule` | `No schedule available` |
| `EmptyState_RadioList` | `No stations available` |
| `Section_Audio` | `AUDIO` |
| `Section_Playback` | `PLAYBACK` |
| `Section_Theme` | `THEME` |
| `Section_Display` | `DISPLAY` |
| `Section_Diagnostics` | `DIAGNOSTICS` |
| `Section_Info` | `INFO` |
| `Label_Volume` | `Volume` |
| `Setting_StartWithFavorite` | `Start with favourite station` |
| `Setting_StartWithFavorite_Hint` | `If no favourite is set, the first station in the list is loaded` |
| `Setting_ThemePreference` | `Theme preference` |
| `Setting_WindowOrientation` | `Window orientation` |
| `Setting_WindowOrientation_Hint` | `Change the application layout orientation.` |
| `Button_SaveSettings` | `Save settings` |
| `Setting_ResetDB_Title` | `Restore default data` |
| `Setting_ResetDB_Hint` | `Clears all stations and seed data, then reinserts them with the values defined in code.` |
| `Button_ResetDB` | `Reset database` |
| `Setting_CrashReporting` | `Send crashes to developer` |
| `Setting_CrashReporting_Hint` | `When enabled, crashes are automatically sent to help diagnosis. The change requires an app restart.` |
| `Button_SendTestCrash` | `Send test crash report` |
| `Label_AppVersion` | `App version` |
| `Label_DBVersion` | `DB version` |
| `Label_UpdateRequired` | `Update required` |
| `Label_Yes` | `Yes` |
| `Label_No` | `No` |
| `Link_VisitSite` | `Visit website` |
| `Label_Copyright` | `© RadioE45 — All rights reserved` |

### `AppResources.it.resx` (italiano)

Stessa struttura del file inglese, con i valori in italiano.

| Key | Value |
|-----|-------|
| `Tab_OnAir` | `In Onda` |
| `Tab_RadioList` | `Canali Radio` |
| `Tab_Schedule` | `Palinsesto` |
| `Tab_Settings` | `Impostazioni` |
| `Badge_Live` | `• LIVE` |
| `Badge_Now` | `● ORA` |
| `Badge_Offline` | `● Off Line` |
| `Label_Next` | `Prossimo` |
| `EmptyState_Schedule` | `Nessun palinsesto disponibile` |
| `EmptyState_RadioList` | `Nessuna stazione disponibile` |
| `Section_Audio` | `AUDIO` |
| `Section_Playback` | `RIPRODUZIONE` |
| `Section_Theme` | `TEMA` |
| `Section_Display` | `VISUALIZZAZIONE` |
| `Section_Diagnostics` | `DIAGNOSTICA` |
| `Section_Info` | `INFO` |
| `Label_Volume` | `Volume` |
| `Setting_StartWithFavorite` | `Avvia con la stazione preferita` |
| `Setting_StartWithFavorite_Hint` | `Se non c'è una stazione preferita viene caricata la prima della lista` |
| `Setting_ThemePreference` | `Preferenza tema` |
| `Setting_WindowOrientation` | `Orientamento finestra` |
| `Setting_WindowOrientation_Hint` | `Modifica orientamento dell'applicazione.` |
| `Button_SaveSettings` | `Salva impostazioni` |
| `Setting_ResetDB_Title` | `Ripristina dati di default` |
| `Setting_ResetDB_Hint` | `Cancella tutte le stazioni e i dati di seed, poi li reinserisce con i valori definiti nel codice.` |
| `Button_ResetDB` | `Reset database` |
| `Setting_CrashReporting` | `Invia i crash allo sviluppatore` |
| `Setting_CrashReporting_Hint` | `Se attivo, i crash dell'app vengono inviati automaticamente per aiutare la diagnosi. La modifica richiede il riavvio dell'app.` |
| `Button_SendTestCrash` | `Invia crash report di test` |
| `Label_AppVersion` | `Versione app` |
| `Label_DBVersion` | `Versione DB` |
| `Label_UpdateRequired` | `Aggiornamento richiesto` |
| `Label_Yes` | `Sì` |
| `Label_No` | `No` |
| `Link_VisitSite` | `Visita il sito` |
| `Label_Copyright` | `© RadioE45 — Tutti i diritti riservati` |

---

## 4. Uso in XAML con `x:Static`

Aggiungere il namespace XML in ogni pagina:

```xml
xmlns:resx="clr-namespace:RadioE45.Resources.Strings"
```

Poi sostituire le stringhe hardcoded:

```xml
<!-- PRIMA -->
<Label Text="Prossimo" />

<!-- DOPO -->
<Label Text="{x:Static resx:AppResources.Label_Next}" />
```

Esempi concreti per ogni file:

### `AppShell.xaml`
```xml
<ShellContent Title="{x:Static resx:AppResources.Tab_OnAir}" ... />
<ShellContent Title="{x:Static resx:AppResources.Tab_RadioList}" ... />
<ShellContent Title="{x:Static resx:AppResources.Tab_Schedule}" ... />
<ShellContent Title="{x:Static resx:AppResources.Tab_Settings}" ... />
```

### `OnAirPage.xaml`
```xml
<!-- Title della pagina -->
Title="{x:Static resx:AppResources.Tab_OnAir}"

<!-- Badge LIVE -->
<Label Text="{x:Static resx:AppResources.Badge_Live}" />

<!-- Label "Prossimo" -->
<Label Text="{x:Static resx:AppResources.Label_Next}" />
```

### `SchedulePage.xaml`
```xml
Title="{x:Static resx:AppResources.Tab_Schedule}"

<!-- Badge ORA -->
<Label Text="{x:Static resx:AppResources.Badge_Now}" />

<!-- Empty state -->
<Label Text="{x:Static resx:AppResources.EmptyState_Schedule}" />
```

### `RadioListPage.xaml`
```xml
Title="{x:Static resx:AppResources.Tab_RadioList}"

<!-- Off Line -->
<Label Text="{x:Static resx:AppResources.Badge_Offline}" />

<!-- Empty state -->
<Label Text="{x:Static resx:AppResources.EmptyState_RadioList}" />
```

### `SettingsPage.xaml`
Tutti i `Text=` con valore letterale diventano `x:Static`. Esempio:
```xml
<Label Text="{x:Static resx:AppResources.Section_Audio}" />
<Label Text="{x:Static resx:AppResources.Label_Volume}" />
<Label Text="{x:Static resx:AppResources.Setting_StartWithFavorite}" />
<Button Text="{x:Static resx:AppResources.Button_SaveSettings}" />
```

---

## 5. Caso speciale: `BoolToObjectConverter` con stringhe localizzate

L'attuale `BoolToObjectConverter` con `TrueObject='Sì'` / `FalseObject='No'` non può
accedere direttamente a `x:Static`. La soluzione più pulita è esporre la stringa dal
ViewModel o usare risorse di pagina.

**Opzione A — proprietà stringa nel ViewModel** (consigliata):

```csharp
// SettingsViewModel.cs
public string MustUpdateText =>
    MustUpdate ? AppResources.Label_Yes : AppResources.Label_No;
```

```xml
<!-- SettingsPage.xaml -->
<Label Text="{Binding MustUpdateText}" />
```

**Opzione B — risorse di pagina** (meno codice):

```xml
<ContentPage.Resources>
    <x:String x:Key="Yes">{x:Static resx:AppResources.Label_Yes}</x:String>
    <x:String x:Key="No">{x:Static resx:AppResources.Label_No}</x:String>
</ContentPage.Resources>

<Label Text="{Binding MustUpdate,
    Converter={toolkit:BoolToObjectConverter
        TrueObject={StaticResource Yes},
        FalseObject={StaticResource No}}}" />
```

---

## 6. Configurazione piattaforme

### iOS e Mac Catalyst

Aggiungere `CFBundleLocalizations` in **entrambi** i file `Info.plist`:
- `Platforms/iOS/Info.plist`
- `Platforms/MacCatalyst/Info.plist`

```xml
<key>CFBundleLocalizations</key>
<array>
    <string>en</string>
    <string>it</string>
</array>
```

Senza questa chiave, iOS ignora la lingua selezionata nelle impostazioni di sistema.

### Windows

In `Platforms/Windows/Package.appxmanifest`, aggiornare la sezione `<Resources>`:

```xml
<Resources>
    <Resource Language="en-US" />
    <Resource Language="it-IT" />
</Resources>
```

### Android

Nessuna modifica necessaria: Android rileva automaticamente la cultura dai file `.resx`.

---

## 7. Aggiungere una nuova lingua

1. Creare `AppResources.{codice-lingua}.resx` (es. `AppResources.fr.resx` per il francese)
2. Copiare tutte le chiavi da `AppResources.resx` e tradurre i valori
3. Aggiungere `<string>fr</string>` in `Info.plist` (iOS/Mac)
4. Aggiungere `<Resource Language="fr-FR" />` in `Package.appxmanifest` (Windows)
5. Eseguire `dotnet build` per rigenerare i file di designer

---

## 8. Uso in codice C#

Le stringhe `.resx` sono accessibili anche nel codice senza alcuna configurazione aggiuntiva:

```csharp
using RadioE45.Resources.Strings;

// Legge automaticamente la lingua corrente del dispositivo
string msg = AppResources.EmptyState_Schedule;

// Sovrascrivere la cultura manualmente (raramente necessario)
AppResources.Culture = new CultureInfo("it");
```

---

## 9. Checklist implementazione

- [ ] Aggiungere `<NeutralLanguage>en-US</NeutralLanguage>` in `RadioE45.csproj`
- [ ] Creare `Resources/Strings/AppResources.resx` con tutte le chiavi in inglese
- [ ] Creare `Resources/Strings/AppResources.it.resx` con le traduzioni italiane
- [ ] Eseguire `dotnet build` e verificare che `AppResources.Designer.cs` sia generato
- [ ] Aggiungere `xmlns:resx="clr-namespace:RadioE45.Resources.Strings"` in ogni XAML
- [ ] Sostituire ogni stringa hardcoded nei file XAML con `{x:Static resx:AppResources.KeyName}`
- [ ] Gestire `BoolToObjectConverter` con `Sì/No` (ViewModel o resource di pagina)
- [ ] Aggiornare `Platforms/iOS/Info.plist` con `CFBundleLocalizations`
- [ ] Aggiornare `Platforms/MacCatalyst/Info.plist` con `CFBundleLocalizations`
- [ ] Aggiornare `Platforms/Windows/Package.appxmanifest` con `<Resource Language="..." />`
- [ ] Testare con lingua del dispositivo impostata su inglese e su italiano
