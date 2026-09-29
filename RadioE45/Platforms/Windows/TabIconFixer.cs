using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Shell = Microsoft.Maui.Controls.Shell;

namespace RadioE45.Platforms.Windows;

// Windows tab bar: a top NavigationView fed by MenuItemsSource. Tabs that become visible at
// runtime (Request/Podcast) get their icon with the default theme Foreground (near black),
// which is invisible on the dark tab bar, until the first tab selection makes MAUI apply the
// tab bar colors. Apply() gives the unselected tabs' icons the unselected tab color, which is
// what MAUI does on the first selection.
internal static class TabIconFixer
{
    public static void Apply(Shell shell)
    {
        if (shell.Handler?.PlatformView is not DependencyObject root)
            return;

        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("MutedText", out object? value) != true
            || value is not Microsoft.Maui.Graphics.Color unselectedColor)
            return;

        var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(unselectedColor.ToWindowsColor());

        var navs = new List<NavigationView>();
        CollectNavigationViews(root, navs);

        foreach (NavigationView nav in navs)
        {
            if (nav.PaneDisplayMode != NavigationViewPaneDisplayMode.Top
                || nav.MenuItemsSource is not System.Collections.IEnumerable source)
                continue;

            foreach (object item in source)
            {
                if (nav.ContainerFromMenuItem(item) is not NavigationViewItem nvi || nvi.IsSelected)
                    continue;

                if (nvi.Icon is IconElement icon)
                    icon.Foreground = brush;
            }
        }
    }

    private static void CollectNavigationViews(DependencyObject parent, List<NavigationView> result)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is NavigationView nav)
                result.Add(nav);
            CollectNavigationViews(child, result);
        }
    }
}
