namespace RadioE45.Services;

internal static class ThemeService
{
    internal static void Apply(string preference)
    {
        var app = Application.Current!;
        bool isDark = preference == "Dark" ||
            (preference == "System" && app.RequestedTheme == AppTheme.Dark);

        var res = app.Resources;
        res["PageBackground"]   = Color.FromArgb(isDark ? "#0D0D0D" : "#F8F9FA");
        res["CardBackground"]   = Color.FromArgb(isDark ? "#1A1A1A" : "#FFFFFF");
        res["CardElevated"]     = Color.FromArgb(isDark ? "#2A2A2A" : "#F0F0F0");
        res["PrimaryText"]      = Color.FromArgb(isDark ? "#F1FAEE" : "#1A1A1A");
        res["SecondaryText"]    = Color.FromArgb(isDark ? "#A8DADC" : "#457B9D");
        res["MutedText"]        = Color.FromArgb(isDark ? "#6B7280" : "#9CA3AF");
        res["TabBarBackground"] = Color.FromArgb(isDark ? "#111111" : "#FFFFFF");
        res["DividerColor"]     = Color.FromArgb(isDark ? "#2A2A2A" : "#E5E7EB");
    }
}
