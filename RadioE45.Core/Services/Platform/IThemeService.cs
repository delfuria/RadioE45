namespace RadioE45.Services.Platform;

public interface IThemeService
{
    /// <summary>Applies "Dark", "Light" or "System" to the running app.</summary>
    void Apply(string preference);
}
