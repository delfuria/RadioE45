using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class SongRequestPage : ContentPage
{
    private readonly SongRequestViewModel _viewModel;

    public SongRequestPage(SongRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        SearchBox.HandlerChanged += OnSearchBoxHandlerChanged;
    }

    private void OnSearchButtonPressed(object? sender, EventArgs e) => SearchBox.Unfocus();

    // Scorrere la lista chiude la tastiera (che su iOS copre le tab).
    private void OnListScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (SearchBox.IsFocused)
            SearchBox.Unfocus();
    }

    // iOS: il tasto "Cerca" è disabilitato a campo vuoto, quindi serve una barra "Fine" sopra la tastiera.
    private void OnSearchBoxHandlerChanged(object? sender, EventArgs e)
    {
#if IOS
        if (SearchBox.Handler?.PlatformView is not UIKit.UISearchBar bar)
            return;

        var toolbar = new UIKit.UIToolbar(new CoreGraphics.CGRect(0, 0, 320, 44));
        var done = new UIKit.UIBarButtonItem(UIKit.UIBarButtonSystemItem.Done, (_, _) => bar.ResignFirstResponder());
        var space = new UIKit.UIBarButtonItem(UIKit.UIBarButtonSystemItem.FlexibleSpace);
        toolbar.Items = [space, done];
        toolbar.SizeToFit();
        bar.InputAccessoryView = toolbar;
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSongsCommand.ExecuteAsync(null);
    }
}
