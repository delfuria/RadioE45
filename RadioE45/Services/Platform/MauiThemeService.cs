namespace RadioE45.Services.Platform;

public sealed class MauiThemeService : IThemeService
{
    public void Apply(string preference) => ThemeService.Apply(preference);
}
