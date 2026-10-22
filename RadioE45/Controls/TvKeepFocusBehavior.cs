namespace RadioE45.Controls;

// Android TV: un pulsante il cui comando asincrono è in esecuzione è disabilitato (CanExecute false)
// e Android toglie il focus a una vista disabilitata: il focus passa al primo elemento della pagina
// e non torna indietro. Misurato sull'emulatore (docs/ANDROID-TV-ROADMAP.md §3): dopo OK su Play il
// focus saltava su "On Air". Il comportamento ridà il focus al pulsante quando si riabilita.
// Solo input/focus, nessuna logica: il ViewModel resta quello del telefono.
public sealed class TvKeepFocusBehavior : Behavior<Button>
{
    private bool _restoreFocus;

    protected override void OnAttachedTo(Button button)
    {
        base.OnAttachedTo(button);
        button.PropertyChanged += OnPropertyChanged;
        button.Unfocused += OnUnfocused;
    }

    protected override void OnDetachingFrom(Button button)
    {
        button.PropertyChanged -= OnPropertyChanged;
        button.Unfocused -= OnUnfocused;
        base.OnDetachingFrom(button);
    }

    private void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not Button button || e.PropertyName != nameof(VisualElement.IsEnabled))
            return;

        if (!button.IsEnabled)
        {
            _restoreFocus |= button.IsFocused;
            return;
        }

        if (_restoreFocus)
        {
            _restoreFocus = false;
            button.Dispatcher.Dispatch(() => button.Focus());
        }
    }

    // Se la vista perde il focus perché si è disabilitata, la proprietà può cambiare dopo l'evento.
    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Button { IsEnabled: false })
            _restoreFocus = true;
    }
}
