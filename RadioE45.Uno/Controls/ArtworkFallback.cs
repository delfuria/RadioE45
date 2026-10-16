using Microsoft.UI.Xaml.Media.Imaging;

namespace RadioE45.Controls;

/// <summary>
/// Attached property for song artwork <see cref="Image"/>s: when the picture cannot be loaded the
/// app's own generic cover is shown. On the web the browser blocks images without CORS headers
/// (AzuraCast's /static/img/generic_song.jpg, a station logo on another site), on every platform
/// a broken link or a server error ends here too. Purely visual.
/// </summary>
public static class ArtworkFallback
{
    private static readonly Uri GenericSong = new("ms-appx:///Assets/Images/generic_song.png");

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ArtworkFallback), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        image.ImageFailed -= OnImageFailed;
        if ((bool)e.NewValue)
        {
            image.ImageFailed += OnImageFailed;
        }
    }

    private static void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // The generic cover itself failing (it ships with the app) must not loop.
        if (sender is Image { Source: BitmapImage current } image && current.UriSource != GenericSong)
        {
            image.Source = new BitmapImage(GenericSong);
        }
    }
}
