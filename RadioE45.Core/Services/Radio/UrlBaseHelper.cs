namespace RadioE45.Services.Radio;

public static class UrlBaseHelper
{
    public static string EnsureScheme(string urlBase) =>
        urlBase.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        urlBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? urlBase
            : $"https://{urlBase}";
}
