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

        // Uno/WinUI: {x:Bind ViewModel.X}, {x:Bind X} (item templates), {x:Bind Path=...} and the
        // paths passed to x:Bind functions: {x:Bind conv:Fn.IsNull(ViewModel.X), Mode=OneWay}
        foreach (Match m in XBindRegex().Matches(xaml))
            AddXBindPaths(members, m.Groups[1].Value);

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

    private static void AddXBindPaths(SortedSet<string> set, string body)
    {
        var expression = SplitTopLevel(body)[0].Trim();
        if (expression.StartsWith("Path=", StringComparison.Ordinal))
            expression = expression["Path=".Length..];

        int open = expression.IndexOf('(');
        if (open < 0)
        {
            if (LeadingPathRegex().Match(expression).Groups[1].Value is { Length: > 0 } path)
                Add(set, StripViewModelPrefix(path));
            return;
        }

        // Function call: the function name is not a member, its arguments are.
        int close = expression.LastIndexOf(')');
        if (close <= open)
            return;
        foreach (var argument in SplitTopLevel(expression[(open + 1)..close]))
        {
            var arg = argument.Trim();
            if (arg.Length == 0 || arg[0] is '\'' or '"' || char.IsDigit(arg[0]) || arg[0] == '-')
                continue;
            AddXBindPaths(set, arg);
        }
    }

    // Splits on commas outside parentheses and quotes.
    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        char quote = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
                continue;
            }
            switch (c)
            {
                case '\'' or '"': quote = c; break;
                case '(': depth++; break;
                case ')': depth--; break;
                case ',' when depth == 0:
                    parts.Add(text[start..i]);
                    start = i + 1;
                    break;
            }
        }
        parts.Add(text[start..]);
        return parts;
    }

    // ViewModel.X, or OnAirViewModel.X on pages that bind a second ViewModel (the shell).
    private static string StripViewModelPrefix(string path) =>
        ViewModelPrefixRegex().Match(path) is { Success: true } prefix ? path[prefix.Length..] : path;

    [GeneratedRegex(@"^(?:[A-Z]\w*)?ViewModel\.")]
    private static partial Regex ViewModelPrefixRegex();

    [GeneratedRegex(@"\{Binding\b([^{}]*(?:\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}[^{}]*)*)\}")]
    private static partial Regex BindingRegex();

    [GeneratedRegex(@"\bPath=([A-Za-z_][\w.]*)")]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"^\s*([A-Za-z_][\w.]*)?")]
    private static partial Regex LeadingPathRegex();

    [GeneratedRegex(@"\{x:Bind\s+([^{}]*)\}")]
    private static partial Regex XBindRegex();

    [GeneratedRegex(@"\{loc:Translate\s+(?:Key=)?([A-Za-z_]\w*)")]
    private static partial Regex TranslateRegex();

    // MAUI pages keep the VM in _viewModel; Uno pages expose it as ViewModel (§8).
    [GeneratedRegex(@"\b(?:_viewModel|ViewModel)\.([A-Za-z_]\w*)")]
    private static partial Regex CodeBehindMemberRegex();
}
