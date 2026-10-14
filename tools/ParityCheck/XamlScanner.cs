using System.Text.RegularExpressions;

namespace RadioE45.ParityCheck;

/// <summary>
/// What a page uses from its ViewModels and from the shared strings, extracted with regexes from
/// the XAML and its code-behind. Paths are kept as written (<c>NowPlaying.Next.Title</c>); item
/// template bindings are included too, since both UIs bind the same model types.
/// </summary>
public sealed record PageUsage(IReadOnlySet<string> Members, IReadOnlySet<string> StringKeys);

public static partial class XamlScanner
{
    public static PageUsage Scan(string xamlPath)
    {
        var xaml = File.ReadAllText(xamlPath);
        var members = new SortedSet<string>(StringComparer.Ordinal);
        var keys = new SortedSet<string>(StringComparer.Ordinal);

        // MAUI: {Binding X}, {Binding Path=X}, {Binding ..., Path=BindingContext.X}
        foreach (Match m in BindingRegex().Matches(xaml))
        {
            var body = m.Groups[1].Value;
            var path = PathRegex().Match(body) is { Success: true } explicitPath
                ? explicitPath.Groups[1].Value
                : LeadingPathRegex().Match(body).Groups[1].Value;
            Add(members, path);
        }

        // Uno/WinUI: {x:Bind ViewModel.X}, {x:Bind X} (item templates), {x:Bind Path=...}
        foreach (Match m in XBindRegex().Matches(xaml))
            Add(members, StripViewModelPrefix(m.Groups[1].Value));

        foreach (Match m in TranslateRegex().Matches(xaml))
            keys.Add(m.Groups[1].Value);

        var codeBehind = xamlPath + ".cs";
        if (File.Exists(codeBehind))
        {
            var code = File.ReadAllText(codeBehind);
            foreach (Match m in CodeBehindMemberRegex().Matches(code))
                members.Add(m.Groups[1].Value);
        }

        return new PageUsage(members, keys);
    }

    private static void Add(SortedSet<string> set, string path)
    {
        path = path.Trim();
        if (path.StartsWith("BindingContext.", StringComparison.Ordinal))
            path = path["BindingContext.".Length..];
        if (path.Length > 0 && path != ".")
            set.Add(path);
    }

    private static string StripViewModelPrefix(string path) =>
        path.StartsWith("ViewModel.", StringComparison.Ordinal) ? path["ViewModel.".Length..] : path;

    [GeneratedRegex(@"\{Binding\b([^{}]*(?:\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}[^{}]*)*)\}")]
    private static partial Regex BindingRegex();

    [GeneratedRegex(@"\bPath=([A-Za-z_][\w.]*)")]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"^\s*([A-Za-z_][\w.]*)?")]
    private static partial Regex LeadingPathRegex();

    [GeneratedRegex(@"\{x:Bind\s+(?:Path=)?([A-Za-z_][\w.]*)")]
    private static partial Regex XBindRegex();

    [GeneratedRegex(@"\{loc:Translate\s+(?:Key=)?([A-Za-z_]\w*)")]
    private static partial Regex TranslateRegex();

    // MAUI pages keep the VM in _viewModel; Uno pages expose it as ViewModel (§8).
    [GeneratedRegex(@"\b(?:_viewModel|ViewModel)\.([A-Za-z_]\w*)")]
    private static partial Regex CodeBehindMemberRegex();
}
