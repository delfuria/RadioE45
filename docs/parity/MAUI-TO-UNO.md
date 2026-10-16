# Conversione UI MAUI → Uno/WinUI

Tabella di riferimento per portare le pagine di `RadioE45/` su `RadioE45.Uno/` (skill `ui-parity-port`, roadmap §8).
Le pagine Uno valgono sia per desktop sia per web: le differenze stanno nei servizi, non nel XAML.

## Convenzioni obbligatorie (servono a `tools/ParityCheck`)

- Il code-behind della pagina Uno espone il ViewModel come proprietà **`ViewModel`** (risolta dalla DI) e i binding usano **`{x:Bind ViewModel.X, Mode=OneWay}`**. ParityCheck toglie il prefisso `ViewModel.` e confronta il percorso con quello di `{Binding X}` in MAUI.
- Una pagina che usa un secondo ViewModel lo espone con il suo nome di tipo (`ShellPage.OnAirViewModel`) e lega `{x:Bind OnAirViewModel.X}`: ParityCheck toglie anche il prefisso `XxxViewModel.`.
- Nei template degli elementi (`x:DataType="models:AzuraStation"`) usare `{x:Bind Name}` con lo stesso percorso usato in MAUI.
- Chiamate al ViewModel dal code-behind: `ViewModel.X` in Uno, `_viewModel.X` in MAUI; anche queste vengono confrontate.
- Stringhe: `{loc:Translate Key=Tab_OnAir}` (`xmlns:loc="using:RadioE45.Services.Localization"`). Le markup extension WinUI non accettano argomenti posizionali, quindi `Key=` è obbligatorio; ParityCheck riconosce entrambe le forme. Il valore viene letto una volta al caricamento della pagina (l'app non cambia lingua a runtime).
- Un membro usato solo in MAUI per una ragione di piattaforma va in `ignore` in `parity/pages.yaml`, con `reason`.
- Conversioni di valore: funzioni statiche di `RadioE45.Uno/Converters/Fn.cs` dentro `x:Bind` (`{x:Bind conv:Fn.IsNotNull(ViewModel.X), Mode=OneWay}`, `xmlns:conv="using:RadioE45.Converters"`). ParityCheck legge anche i percorsi passati come argomenti. Nelle stringhe letterali di `x:Bind` le entità XML (`'&#xf04b;'`) **non** vengono decodificate: i glifi vanno restituiti da una funzione (`Fn.PlayPauseGlyph`).
- Emoji e simboli Unicode delle pagine MAUI (▶ ⏸ ⏮ ⏭ 🔊 🔇 👥 ▲ ▼): sul web il renderer Skia non ha un font per mostrarli (quadratini). In Uno si usano i glifi Font Awesome (`FaSolid`).
- Immagini da URL: segnaposto **sempre sotto** l'immagine (non con `IsNull`), così resta visibile anche quando il caricamento fallisce (sul web, immagini senza CORS, es. `/static/img/generic_song.jpg` di AzuraCast).
- Immagini locali (`Resources/Images/*.svg` MAUI): copiare l'SVG in `RadioE45.Uno/Assets/Images/` (Resizetizer genera i PNG) e usare `ms-appx:///Assets/Images/<nome>.png`.
- Stili con nome: stesse chiavi di MAUI in `RadioE45.Uno/Styles/Styles.xaml` (`HeadlineLabel`, `SubtitleLabel`, `CaptionLabel`, `PlayButton`, `CardBorder`…).
- Layout orizzontale (pagine che in MAUI cambiano layout con `OnSizeAllocated`): sul web non va mai attivato, perché l'app è una colonna da 500 centrata (`UnoLayout.IsSingleColumn`, impostata in `ShellPage`).
- Pagine impilate (route Shell MAUI aperte sopra una tab): `ShellPage.Push(typeof(Pagina), parametro)` dal servizio di navigazione; il titolo della barra MAUI diventa un `TextBlock` con stile `PageTitleLabel` in cima alla pagina (stessa stringa o `ViewModel.Title`), il pulsante indietro è quello della `NavigationView`.
- Righe di una lista che si toccano e si trascinano: la riga è un `Button` (comando del modello) e il trascinamento parte da una maniglia separata (`CanDrag` + `DragStarting`), perché il `Button` cattura il puntatore. Destinazioni con `AllowDrop`, `DragOver` (`AcceptedOperation = Move`) e `Drop`.
- `KeyboardAccelerator` sulla pagina: `KeyboardAcceleratorPlacementMode="Hidden"`, altrimenti compare il tooltip del tasto sopra la pagina.
- `Slider` legato a una proprietà `int`: `Value` in OneWay con `Fn.ToDouble` e `ValueChanged` nel code-behind (`x:Bind` TwoWay non converte double → int).
- Ciclo di vita: la logica di `OnAppearing` va nel ViewModel (es. `OnAirViewModel.ActivateAsync()`), chiamata da `OnAppearing` in MAUI e da `OnNavigatedTo` in Uno.

## Controlli e layout

| MAUI | Uno/WinUI |
|---|---|
| `Shell` + `TabBar` (OnAir, SongRequest, Podcast) + flyout menu | `NavigationView` con tab in alto + `Frame`, tramite `INavigationService` (`ShellPage.xaml`); nessun layout adattivo (roadmap D9) |
| `ContentPage` | `Page` |
| `VerticalStackLayout` / `HorizontalStackLayout` | `StackPanel` (`Orientation`, `Spacing`) |
| `Grid` (`RowDefinitions="Auto,*"`) | `Grid` con `RowDefinitions` espliciti (in WinUI la sintassi abbreviata non esiste: `<RowDefinition Height="Auto" />`) |
| `Label` | `TextBlock` (`FormattedText`/`Span` → `Run` dentro `TextBlock.Inlines`) |
| `CollectionView` | `ListView` / `ItemsRepeater` |
| `CollectionView.EmptyView` | `TextBlock` con `Visibility` legata a una proprietà del ViewModel |
| `Entry` / `Editor` | `TextBox` (`AcceptsReturn` per `Editor`) |
| `SearchBar` | `TextBox` con `Text` TwoWay e `UpdateSourceTrigger=PropertyChanged` quando il ViewModel filtra mentre si scrive (`SongRequestPage`); `AutoSuggestBox` / `QuerySubmitted` → comando per una ricerca esplicita |
| `Switch` / `Slider` / `Picker` / `CheckBox` | `ToggleSwitch` / `Slider` / `ComboBox` / `CheckBox` |
| `ActivityIndicator` | `ProgressRing` |
| `ProgressBar` | `ProgressBar` (`Maximum="1"` per valori 0–1) |
| `RefreshView` | `RefreshContainer` + `KeyboardAccelerator` F5 → `RequestRefresh()` (col mouse non si trascina) |
| `SwipeView` | `MenuFlyout` contestuale (`ContextFlyout`: tasto destro, pressione lunga); lo swipe col mouse non funziona |
| `MenuFlyout` (`FlyoutBase.ContextFlyout`) | `ContextFlyout` con `MenuFlyout` |
| `DragGestureRecognizer` / `DropGestureRecognizer` | `CanDragItems` / `CanReorderItems` su `ListView` (oppure `AllowDrop` + `Drop`) |
| `TapGestureRecognizer` | `Tapped` oppure `Button` con stile senza bordo |
| `SwipeGestureRecognizer` | `ManipulationCompleted` / pulsante di chiusura |
| Toolkit `Popup` (bottom sheet) | `UserControl` mostrato da `ShellPage.ShowSheet`: pannello che sale dal basso sopra uno strato scuro (✕, clic fuori, Esc, trascinamento giù della maniglia con `ManipulationMode="TranslateY"`), altezza massima 70%. Non `ContentDialog`: centrato e, sul web, fuori dalla colonna (`ScheduleDialog.xaml`) |
| `Border` + `StrokeShape="RoundRectangle 8"` | `Border` + `CornerRadius="8"` |
| `Image` con `Source` URL | `Image` + `BitmapImage` (`UriSource`) |
| `IsVisible="{Binding B}"` | `Visibility="{x:Bind ViewModel.B, Mode=OneWay}"` (WinUI converte `bool` → `Visibility` in `x:Bind`); per l'inverso un converter o una proprietà calcolata |
| `toolkit:InvertedBoolConverter`, `IsNullConverter`, `IsNotNullConverter` | Converter in `RadioE45.Uno/Converters/` oppure funzioni in `x:Bind` (`{x:Bind local:Conv.Not(ViewModel.IsBusy)}`) |
| `AppThemeBinding` | `{ThemeResource}` + `ThemeDictionaries` Light/Dark |
| `StaticResource` colori (`Styles/Colors.xaml`) | `ThemeResource` in `RadioE45.Uno/Styles/` (stessi nomi di chiave dove possibile) |
| `FontImageSource` Font Awesome | `FontIcon FontFamily="ms-appx:///Assets/Fonts/FaSolid.ttf#Font Awesome 6 Free Solid"` |
| `GraphicsView` (`VuMeterView`) | `SKCanvasElement` (`Uno.WinUI.Graphics2DSK`, `RenderOverride`); logica condivisa nel Core (`Visuals/VuMeterLevels`) |
| `MediaElement` nascosto nella pagina | niente: l'audio Uno usa `MediaPlayer` senza elemento visuale (§4.3), il servizio non dipende dalla pagina |
| `{Binding X, StringFormat='{0:HH:mm}'}` | `{x:Bind conv:Fn.Time(X)}` (`Fn.DayDate` per `dddd d MMMM`) oppure proprietà formattata nel ViewModel (preferibile se serve a entrambe le UI) |
| `DataTrigger` su un bool (es. bordo e padding) | funzione `Fn` che restituisce il valore (`Fn.StreamerFrame(IsStreamer)`) |
| Colori `DynamicResource` grezzi (`TextSecondary`, `TextMuted`…) | il pennello semantico che segue il tema (`SecondaryTextBrush`, `MutedTextBrush`): i colori grezzi MAUI non cambiano col tema chiaro |
| `OnPlatform` / `OnIdiom` | niente `AdaptiveTrigger`: su desktop la finestra è fissa come in MAUI desktop (verticale 500×855 o orizzontale 900×450, il layout segue `width > height`); sul web sempre verticale (`UnoLayout.IsSingleColumn`). Mai rami per sistema operativo |
| `[QueryProperty]` sulla pagina | parametro di `Frame.Navigate(typeof(Page), param)` letto in `OnNavigatedTo` e inoltrato al ViewModel |
| `OnAppearing` / `OnDisappearing` | `OnNavigatedTo` / `OnNavigatedFrom` |
| `{Binding}` compilato con `x:DataType` | `{x:Bind ViewModel.X, Mode=OneWay}` (`x:Bind` è `OneTime` di default: specificare sempre `Mode`) |
| `Shell.FlyoutContent` (menu laterale) + `FlyoutIsPresented` | pannello sopra la `NavigationView` della shell (`Border` con `TranslateTransform` animata + strato scuro per chiudere con un clic fuori, Esc), aperto da `Controls/MenuButton` (`UnoNavigationService.OpenMenu`). Non `SplitView`: su Uno il suo pannello compare aperto per un frame prima dell'animazione. `Clip` sulla shell, se no sul web l'animazione si vede fuori dalla colonna |
| `Picker` con `SelectedItem` stringa (TwoWay) | `ComboBox` con `<x:String>`, `SelectedItem` `OneWay` + `SelectionChanged` nel code-behind: `x:Bind` non riconverte `object` → `string` |
| `Command` con `RelativeSource AncestorType` nei template | `Tag="{x:Bind}"` + `Click` nel code-behind che chiama `ViewModel.XCommand.Execute`: `x:Bind` in un `DataTemplate` non vede la pagina |
| `FontFamily="{Binding Nome}"` (chiave del font) | `{x:Bind conv:Fn.Font(Nome)}`: la stringa è la chiave di `Fonts.xaml` |
| `TapGestureRecognizer NumberOfTapsRequired="2"` | evento `DoubleTapped` |
| `Picker` con `ItemsSource` di oggetti (`PodcastFilterOption<T>`) e `SelectedItem` TwoWay | `ComboBox` con `DisplayMemberPath="Label"`, `SelectedItem` `OneWay` + `SelectionChanged` che riscrive nel ViewModel solo se l'elemento non è null |
| `Slider` non legato, aggiornato dal code-behind con `DragStarted`/`DragCompleted` (seek) | `Slider` con `PointerPressed`/`PointerCaptureLost` registrati con `AddHandler(..., handledEventsToo: true)` (il controllo gestisce già il puntatore); `ValueChanged` per la tastiera, ignorato mentre il code-behind imposta il valore |
| Route con parametri (`Pagina?id=…&titolo=…`) | `ShellPage.Push(typeof(Pagina), new ParametroRecord(...))`, letto in `OnNavigatedTo` |
| Pagina di una tab (istanza unica in Shell) con ViewModel che si abbona a eventi di un singleton | ViewModel singleton nella DI Uno: il `Frame` crea una pagina nuova a ogni visita |

## Ordine di porting

OnAir → RadioList / AddStation / EditStation → Settings → Schedule → SongRequest → PodcastList / PodcastEpisodes → menu app (roadmap S8.x).
