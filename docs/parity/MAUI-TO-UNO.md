# Conversione UI MAUI → Uno/WinUI

Tabella di riferimento per portare le pagine di `RadioE45/` su `RadioE45.Uno/` (skill `ui-parity-port`, roadmap §8).
Le pagine Uno valgono sia per desktop sia per web: le differenze stanno nei servizi, non nel XAML.

## Convenzioni obbligatorie (servono a `tools/ParityCheck`)

- Il code-behind della pagina Uno espone il ViewModel come proprietà **`ViewModel`** (risolta dalla DI) e i binding usano **`{x:Bind ViewModel.X, Mode=OneWay}`**. ParityCheck toglie il prefisso `ViewModel.` e confronta il percorso con quello di `{Binding X}` in MAUI.
- Nei template degli elementi (`x:DataType="models:AzuraStation"`) usare `{x:Bind Name}` con lo stesso percorso usato in MAUI.
- Chiamate al ViewModel dal code-behind: `ViewModel.X` in Uno, `_viewModel.X` in MAUI; anche queste vengono confrontate.
- Stringhe: `{loc:Translate Key=Tab_OnAir}` (`xmlns:loc="using:RadioE45.Services.Localization"`). Le markup extension WinUI non accettano argomenti posizionali, quindi `Key=` è obbligatorio; ParityCheck riconosce entrambe le forme. Il valore viene letto una volta al caricamento della pagina (l'app non cambia lingua a runtime).
- Un membro usato solo in MAUI per una ragione di piattaforma va in `ignore` in `parity/pages.yaml`, con `reason`.

## Controlli e layout

| MAUI | Uno/WinUI |
|---|---|
| `Shell` + `TabBar` (OnAir, SongRequest, Podcast) + flyout menu | `NavigationView` (top su desktop e web largo, `LeftCompact` su schermi stretti) + `Frame`, tramite `INavigationService` (`ShellPage.xaml`) |
| `ContentPage` | `Page` |
| `VerticalStackLayout` / `HorizontalStackLayout` | `StackPanel` (`Orientation`, `Spacing`) |
| `Grid` (`RowDefinitions="Auto,*"`) | `Grid` con `RowDefinitions` espliciti (in WinUI la sintassi abbreviata non esiste: `<RowDefinition Height="Auto" />`) |
| `Label` | `TextBlock` (`FormattedText`/`Span` → `Run` dentro `TextBlock.Inlines`) |
| `CollectionView` | `ListView` / `ItemsRepeater` |
| `CollectionView.EmptyView` | `TextBlock` con `Visibility` legata a una proprietà del ViewModel |
| `Entry` / `Editor` | `TextBox` (`AcceptsReturn` per `Editor`) |
| `SearchBar` | `AutoSuggestBox` / `TextBox` con `QuerySubmitted` → comando |
| `Switch` / `Slider` / `Picker` / `CheckBox` | `ToggleSwitch` / `Slider` / `ComboBox` / `CheckBox` |
| `ActivityIndicator` | `ProgressRing` |
| `ProgressBar` | `ProgressBar` (`Maximum="1"` per valori 0–1) |
| `RefreshView` | `RefreshContainer` (su desktop affiancare un pulsante o F5) |
| `SwipeView` | `SwipeControl`; su desktop `MenuFlyout` contestuale |
| `MenuFlyout` (`FlyoutBase.ContextFlyout`) | `ContextFlyout` con `MenuFlyout` |
| `DragGestureRecognizer` / `DropGestureRecognizer` | `CanDragItems` / `CanReorderItems` su `ListView` (oppure `AllowDrop` + `Drop`) |
| `TapGestureRecognizer` | `Tapped` oppure `Button` con stile senza bordo |
| `SwipeGestureRecognizer` | `ManipulationCompleted` / pulsante di chiusura |
| Toolkit `Popup` (bottom sheet) | `ContentDialog` / `Flyout` (`ScheduleDialog.xaml`) |
| `Border` + `StrokeShape="RoundRectangle 8"` | `Border` + `CornerRadius="8"` |
| `Image` con `Source` URL | `Image` + `BitmapImage` (`UriSource`) |
| `IsVisible="{Binding B}"` | `Visibility="{x:Bind ViewModel.B, Mode=OneWay}"` (WinUI converte `bool` → `Visibility` in `x:Bind`); per l'inverso un converter o una proprietà calcolata |
| `toolkit:InvertedBoolConverter`, `IsNullConverter`, `IsNotNullConverter` | Converter in `RadioE45.Uno/Converters/` oppure funzioni in `x:Bind` (`{x:Bind local:Conv.Not(ViewModel.IsBusy)}`) |
| `AppThemeBinding` | `{ThemeResource}` + `ThemeDictionaries` Light/Dark |
| `StaticResource` colori (`Styles/Colors.xaml`) | `ThemeResource` in `RadioE45.Uno/Styles/` (stessi nomi di chiave dove possibile) |
| `FontImageSource` Font Awesome | `FontIcon FontFamily="ms-appx:///Assets/Fonts/FaSolid.ttf#Font Awesome 6 Free Solid"` |
| `GraphicsView` (`VuMeterView`) | `SKCanvasElement` |
| `MediaElement` nascosto nella pagina | niente: l'audio Uno usa `MediaPlayer` senza elemento visuale (§4.3), il servizio non dipende dalla pagina |
| `{Binding X, StringFormat='{0:HH:mm}'}` | `{x:Bind local:Format.Time(ViewModel.X)}` oppure proprietà formattata nel ViewModel (preferibile se serve a entrambe le UI) |
| `OnPlatform` / `OnIdiom` | `VisualStateManager` con `AdaptiveTrigger` (larghezza), non per sistema operativo |
| `[QueryProperty]` sulla pagina | parametro di `Frame.Navigate(typeof(Page), param)` letto in `OnNavigatedTo` e inoltrato al ViewModel |
| `OnAppearing` / `OnDisappearing` | `OnNavigatedTo` / `OnNavigatedFrom` |
| `{Binding}` compilato con `x:DataType` | `{x:Bind ViewModel.X, Mode=OneWay}` (`x:Bind` è `OneTime` di default: specificare sempre `Mode`) |

## Ordine di porting

OnAir → RadioList / AddStation / EditStation → Settings → Schedule → SongRequest → PodcastList / PodcastEpisodes → menu app (roadmap S8.x).
