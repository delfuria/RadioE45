using System.Collections.Specialized;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace RadioE45.Platforms.Android;

// Android's BottomNavigationView picks its label visibility mode (shown vs.
// hidden) based on how many tabs exist when the menu is first built. The
// Podcast tab starts hidden (IsVisible bound to HasPodcasts) and is added
// later once podcasts are detected for the selected station. Forcing
// LabelVisibilityLabeled on every appearance refresh (below) covers the tabs
// that already exist at that point, but MAUI's own tab-count-changed handler
// (ShellItemRenderer.SetupMenu, via BottomNavigationView.SetShiftMode) resets
// the native label state again when the Podcast tab is added/removed and can
// leave that tab's label view stale until the user taps it. Re-applying the
// mode a tick later (via IShellItemController.ItemsCollectionChanged, the
// same visible-tabs-changed event the renderer reacts to) forces Android to
// relayout the label for the newly (in)visible tab too.
public sealed class CustomShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new LabeledBottomNavViewAppearanceTracker(this, shellItem);
}

public sealed class LabeledBottomNavViewAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    private readonly IShellItemController _shellItemController;
    private BottomNavigationView? _bottomView;
    private bool _disposed;

    public LabeledBottomNavViewAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
        : base(shellContext, shellItem)
    {
        _shellItemController = shellItem;
        _shellItemController.ItemsCollectionChanged += OnVisibleItemsChanged;
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        _bottomView = bottomView;
        ApplyLabelVisibility();
    }

    private void OnVisibleItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => _bottomView?.Post(ApplyLabelVisibility);

    private void ApplyLabelVisibility()
    {
        if (_bottomView is null)
            return;

        // A same-value assignment is a no-op inside NavigationBarView, so the
        // label state for a tab added/removed after the initial build never
        // gets recomputed unless the value actually changes in between.
        _bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityUnlabeled;
        _bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityLabeled;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
            _shellItemController.ItemsCollectionChanged -= OnVisibleItemsChanged;

        _disposed = true;
        base.Dispose(disposing);
    }
}
