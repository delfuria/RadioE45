using System.Globalization;

namespace RadioE45.Services.Localization;

/// <summary>
/// XAML markup extension: <c>Text="{loc:Translate OnAir_Title}"</c>. Binds the target to
/// <see cref="LocalizationResourceManager"/>'s indexer for <see cref="Key"/>, so the string tracks
/// the current culture and updates live when the language changes.
/// </summary>
[ContentProperty(nameof(Key))]
[AcceptEmptyServiceProvider]
public sealed class TranslateExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    /// <summary>Optional .NET composite-format string, e.g. StringFormat="{}{0} %".</summary>
    public string? StringFormat { get; set; }

    public BindingBase ProvideValue(IServiceProvider serviceProvider) =>
        new Binding
        {
            Mode = BindingMode.OneWay,
            Path = $"[{Key}]",
            Source = LocalizationResourceManager.Instance,
            StringFormat = StringFormat
        };

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) =>
        ProvideValue(serviceProvider);
}
