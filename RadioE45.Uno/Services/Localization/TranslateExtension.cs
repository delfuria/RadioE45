using Microsoft.UI.Xaml.Markup;

namespace RadioE45.Services.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Translate Key=Tab_OnAir}"</c>, the WinUI counterpart of
/// the MAUI TranslateExtension. WinUI markup extensions take no positional arguments, so
/// <c>Key=</c> is required (tools/ParityCheck accepts both forms). Strings come from the shared
/// .resx in RadioE45.Core through <see cref="LocalizationResourceManager"/>; the value is resolved
/// once when the page loads (the app has no runtime language switch).
/// </summary>
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public sealed class TranslateExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    protected override object ProvideValue() => LocalizationResourceManager.Instance[Key];
}
