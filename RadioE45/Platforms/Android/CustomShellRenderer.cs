using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace RadioE45.Platforms.Android;

// Android's BottomNavigationView picks its label visibility mode (shown vs.
// hidden) based on how many tabs exist when the menu is first built. The
// Podcast tab starts hidden (IsVisible bound to HasPodcasts) and is added
// later once podcasts are detected for the selected station; that runtime
// item-count change leaves the newly-added tab's label unset. Forcing
// LabelVisibilityLabeled on every appearance refresh keeps all tab titles
// visible regardless of when a tab was added.
public sealed class CustomShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)
        => new LabeledBottomNavViewAppearanceTracker(this, shellItem);
}

public sealed class LabeledBottomNavViewAppearanceTracker : ShellBottomNavViewAppearanceTracker
{
    public LabeledBottomNavViewAppearanceTracker(IShellContext shellContext, ShellItem shellItem)
        : base(shellContext, shellItem)
    {
    }

    public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
    {
        base.SetAppearance(bottomView, appearance);
        bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityLabeled;
    }
}
