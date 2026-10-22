using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RadioE45.ParityCheck;

public enum Severity { Error, Warning, Debt }

public sealed record Finding(Severity Severity, string Check, string Message);

/// <summary>
/// Checks of docs/LINUX-UNO-ROADMAP.md §6.3 (+ manifest integrity). Errors are undeclared
/// drift (exit 2); warnings and declared debt give exit 1.
/// </summary>
public sealed partial class ParityChecker(string root, Assembly coreAssembly)
{
    public const string FeaturesPath = "parity/features.yaml";
    public const string PagesPath = "parity/pages.yaml";
    public const string SurfacePath = "parity/vm-surface.txt";
    private const string MauiViewsDir = "RadioE45/Views";
    private const string MauiShell = "RadioE45/AppShell.xaml";
    private const string MauiTvViewsDir = "RadioE45/Views/Tv";

    private readonly List<Finding> _findings = [];

    /// <param name="strict">Declared 'todo' on desktop/web becomes an error (release gate of the Uno app).</param>
    /// <param name="includeTv">List 'todo' on tv feature by feature; with <paramref name="strict"/> they become errors (release gate of the TV app).</param>
    public IReadOnlyList<Finding> Run(bool strict, bool includeTv = false)
    {
        var features = Load<Feature>(FeaturesPath);
        var pages = Load<PageMap>(PagesPath);
        if (features is null || pages is null)
            return _findings;

        var vmNames = VmSurface.GetViewModelNames(coreAssembly);

        CheckManifest(features, pages, vmNames);
        CheckPageCoverage(pages, features);
        CheckPagePairs(pages, features);
        CheckVmSurface();
        ReportDebt(features, strict, includeTv);

        return _findings;
    }

    // --- 0. Manifest integrity -----------------------------------------------------------------

    private void CheckManifest(List<Feature> features, List<PageMap> pages, IReadOnlySet<string> vmNames)
    {
        const string check = "manifest";
        var pageNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in pages)
        {
            if (string.IsNullOrWhiteSpace(p.Page)) { Error(check, $"{PagesPath}: entry for '{p.Maui}' has no 'page' name"); continue; }
            if (!pageNames.Add(p.Page)) Error(check, $"{PagesPath}: duplicate page '{p.Page}'");
            if (string.IsNullOrWhiteSpace(p.Uno)) Error(check, $"{PagesPath}: page '{p.Page}' has no 'uno' path (use 'n/a' + reason)");
            if (p.UnoNotApplicable && string.IsNullOrWhiteSpace(p.Reason)) Error(check, $"{PagesPath}: page '{p.Page}' is uno: n/a without a reason");
            if (string.IsNullOrWhiteSpace(p.Tv)) Error(check, $"{PagesPath}: page '{p.Page}' has no 'tv' path (use 'n/a' + tvReason)");
            if (p.TvNotApplicable && string.IsNullOrWhiteSpace(p.TvReason)) Error(check, $"{PagesPath}: page '{p.Page}' is tv: n/a without a tvReason");
            foreach (var vm in p.Viewmodels.Where(vm => !vmNames.Contains(vm)))
                Error(check, $"{PagesPath}: page '{p.Page}' references unknown ViewModel '{vm}'");
            foreach (var i in p.Ignore.Where(i => string.IsNullOrWhiteSpace(i.Reason)))
                Error(check, $"{PagesPath}: page '{p.Page}' ignores '{i.Name}' without a reason");
            foreach (var i in p.IgnoreTv.Where(i => string.IsNullOrWhiteSpace(i.Reason)))
                Error(check, $"{PagesPath}: page '{p.Page}' ignores '{i.Name}' on tv without a reason");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in features)
        {
            var where = $"{FeaturesPath}: '{f.Id}'";
            if (!FeatureIdRegex().IsMatch(f.Id)) Error(check, $"{where}: id must look like 'page.section[.detail]' (lowercase, dots, dashes)");
            if (!ids.Add(f.Id)) Error(check, $"{where}: duplicate id");
            if (string.IsNullOrWhiteSpace(f.Title)) Error(check, $"{where}: missing title");
            if (string.IsNullOrWhiteSpace(f.Since)) Error(check, $"{where}: missing 'since'");
            if (!pageNames.Contains(f.Page)) Error(check, $"{where}: unknown page '{f.Page}' (not in {PagesPath})");
            if (f.Viewmodel is { Length: > 0 } vm && !vmNames.Contains(vm)) Error(check, $"{where}: unknown ViewModel '{vm}'");

            foreach (var (platform, value) in f.Status.All())
            {
                var (notes, notesField) = platform == "tv" ? (f.TvNotes, "tvNotes") : (f.Notes, "notes");
                if (!Status.Valid.Contains(value))
                    Error(check, $"{where}: status.{platform} '{value}' is not one of done|todo|limited|n/a");
                else if (value is Status.Limited or Status.NotApplicable && string.IsNullOrWhiteSpace(notes))
                    Error(check, $"{where}: status.{platform} is '{value}' but '{notesField}' does not say why");
            }
        }
    }

    // --- 1. Page coverage --------------------------------------------------------------------

    private void CheckPageCoverage(List<PageMap> pages, List<Feature> features)
    {
        const string check = "pages";
        var mapped = pages.Select(p => Normalize(p.Maui)).ToHashSet(StringComparer.Ordinal);

        var mauiPages = Directory.EnumerateFiles(Path.Combine(root, MauiViewsDir), "*.xaml")
            .Select(f => Normalize(Path.GetRelativePath(root, f)))
            .Append(MauiShell);
        foreach (var maui in mauiPages.Where(m => !mapped.Contains(m)))
            Error(check, $"MAUI page {maui} is not mapped in {PagesPath}");

        var tvDir = Path.Combine(root, MauiTvViewsDir);
        if (Directory.Exists(tvDir))
        {
            var mappedTv = pages.Where(p => !p.TvNotApplicable).Select(p => Normalize(p.Tv)).ToHashSet(StringComparer.Ordinal);
            foreach (var tv in Directory.EnumerateFiles(tvDir, "*.xaml").Select(f => Normalize(Path.GetRelativePath(root, f))))
                if (!mappedTv.Contains(tv))
                    Error(check, $"TV page {tv} is not mapped in {PagesPath} (field 'tv')");
        }

        foreach (var p in pages)
        {
            if (!File.Exists(Path.Combine(root, p.Maui)))
                Error(check, $"{PagesPath}: MAUI file {p.Maui} of page '{p.Page}' does not exist");

            var pageFeatures = features.Where(f => f.Page == p.Page).ToList();
            if (pageFeatures.Count == 0)
                Error(check, $"page '{p.Page}' has no feature in {FeaturesPath}");

            CheckTvPageExists(p, pageFeatures);

            if (p.UnoNotApplicable || File.Exists(Path.Combine(root, p.Uno)))
                continue;

            var claimed = pageFeatures
                .Where(f => f.Status.Uno().Any(s => s.Value is Status.Done or Status.Limited))
                .Select(f => f.Id)
                .ToList();
            if (claimed.Count > 0)
                Error(check, $"Uno page {p.Uno} does not exist but features are marked done/limited: {string.Join(", ", claimed)}");
            else
                Debt(check, $"Uno page {p.Uno} not created yet (page '{p.Page}')");
        }
    }

    /// <summary>
    /// A TV page that does not exist yet is fine for 'todo' features (debt is reported per feature);
    /// done/limited need the file. Platform features without a ViewModel (launcher, media keys) are
    /// not about a view, so they are exempt.
    /// </summary>
    private void CheckTvPageExists(PageMap p, List<Feature> pageFeatures)
    {
        if (string.IsNullOrWhiteSpace(p.Tv) || (!p.TvNotApplicable && File.Exists(Path.Combine(root, p.Tv))))
            return;

        var claimed = pageFeatures
            .Where(f => f.Status.Tv is Status.Done or Status.Limited && !string.IsNullOrWhiteSpace(f.Viewmodel))
            .Select(f => f.Id)
            .ToList();
        if (claimed.Count == 0)
            return;

        Error("pages", p.TvNotApplicable
            ? $"page '{p.Page}' is tv: n/a but features are marked done/limited on tv: {string.Join(", ", claimed)}"
            : $"TV page {p.Tv} does not exist but features are marked done/limited on tv: {string.Join(", ", claimed)}");
    }

    // --- 2-3. Bindings and strings ------------------------------------------------------------

    private void CheckPagePairs(List<PageMap> pages, List<Feature> features)
    {
        foreach (var p in pages)
        {
            var mauiPath = Path.Combine(root, p.Maui);
            if (!File.Exists(mauiPath))
                continue;

            var maui = XamlScanner.Scan(mauiPath);

            if (!p.UnoNotApplicable && File.Exists(Path.Combine(root, p.Uno)))
            {
                var uno = XamlScanner.Scan(Path.Combine(root, p.Uno));
                var ignored = p.Ignore.Select(i => i.Name).ToHashSet(StringComparer.Ordinal);

                // A page with features still 'todo' on desktop or web is a declared partial port.
                var declaredDebt = features.Any(f => f.Page == p.Page && f.Status.Uno().Any(s => s.Value == Status.Todo));

                Compare("bindings", p.Maui, p.Uno, maui.Members, uno.Members, ignored, declaredDebt, "binds");
                Compare("strings", p.Maui, p.Uno, maui.StringKeys, uno.StringKeys, ignored, declaredDebt, "uses string");
            }

            if (!p.TvNotApplicable && !string.IsNullOrWhiteSpace(p.Tv) && File.Exists(Path.Combine(root, p.Tv)))
            {
                var tv = XamlScanner.Scan(Path.Combine(root, p.Tv));
                var ignoredTv = p.IgnoreTv.Select(i => i.Name).ToHashSet(StringComparer.Ordinal);

                // A page with features still 'todo' on tv is a declared partial port.
                var declaredTvDebt = features.Any(f => f.Page == p.Page && f.Status.Tv == Status.Todo);

                Compare("bindings", p.Maui, p.Tv, maui.Members, tv.Members, ignoredTv, declaredTvDebt, "binds");
                Compare("strings", p.Maui, p.Tv, maui.StringKeys, tv.StringKeys, ignoredTv, declaredTvDebt, "uses string");
            }
        }
    }

    private void Compare(string check, string mauiPath, string targetPath, IReadOnlySet<string> maui, IReadOnlySet<string> target,
        HashSet<string> ignored, bool declaredDebt, string verb)
    {
        var missing = maui.Where(m => !target.Contains(m) && !ignored.Contains(m)).ToList();
        if (missing.Count == 0)
            return;

        var message = $"{mauiPath} {verb} {string.Join(", ", missing)} but {targetPath} does not";
        if (declaredDebt) Debt(check, message);
        else Error(check, message + " (add to 'ignore' with a reason, or mark the feature 'todo')");
    }

    // --- 4. ViewModel surface ----------------------------------------------------------------

    private void CheckVmSurface()
    {
        const string check = "vm-surface";
        var snapshotPath = Path.Combine(root, SurfacePath);
        var current = VmSurface.Generate(coreAssembly);

        if (!File.Exists(snapshotPath))
        {
            Error(check, $"{SurfacePath} does not exist: run with --update-surface");
            return;
        }

        var snapshot = VmSurface.Parse(File.ReadAllText(snapshotPath));
        var added = current.Except(snapshot, StringComparer.Ordinal).ToList();
        var removed = snapshot.Except(current, StringComparer.Ordinal).ToList();
        if (added.Count == 0 && removed.Count == 0)
            return;

        var diff = string.Join("", added.Select(a => $"\n    + {a}").Concat(removed.Select(r => $"\n    - {r}")));
        if (FeaturesChangedInWorkingTree())
            Warning(check, $"ViewModel surface changed and {FeaturesPath} was updated: run with --update-surface to refresh {SurfacePath}{diff}");
        else
            Error(check, $"ViewModel surface changed but {FeaturesPath} was not updated (new ViewModel API not tracked){diff}");
    }

    private bool FeaturesChangedInWorkingTree()
    {
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", ["status", "--porcelain", "--", FeaturesPath])
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (git is null) return false;
            var output = git.StandardOutput.ReadToEnd();
            git.WaitForExit();
            return git.ExitCode == 0 && output.Trim().Length > 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false; // git not installed: treat as unchanged
        }
    }

    // --- 5. Debt -----------------------------------------------------------------------------

    private void ReportDebt(List<Feature> features, bool strict, bool includeTv)
    {
        foreach (var f in features)
        {
            var todo = f.Status.Uno().Where(s => s.Value == Status.Todo).Select(s => s.Platform).ToList();
            if (includeTv && f.Status.Tv == Status.Todo) todo.Add("tv");
            if (todo.Count == 0) continue;

            var message = $"{f.Id} ({string.Join(", ", todo)})";
            if (strict) Error("debt", message + $" is still 'todo' (--strict{(includeTv ? " --include-tv" : "")})");
            else Debt("debt", message);
        }

        // The TV port is a long list of 'todo': without --include-tv one summary line keeps the report readable.
        if (!includeTv)
        {
            var tvTodo = features.Count(f => f.Status.Tv == Status.Todo);
            if (tvTodo > 0)
                Debt("debt", $"tv: {tvTodo} feature(s) still 'todo' (list them with --include-tv)");
        }
    }

    // --- helpers -----------------------------------------------------------------------------

    private List<T>? Load<T>(string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        if (!File.Exists(path))
        {
            Error("manifest", $"{relativePath} does not exist");
            return null;
        }

        try
        {
            return ManifestLoader.Load<T>(path);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            Error("manifest", $"{relativePath}: {ex.Message}");
            return null;
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');

    private void Error(string check, string message) => _findings.Add(new(Severity.Error, check, message));
    private void Warning(string check, string message) => _findings.Add(new(Severity.Warning, check, message));
    private void Debt(string check, string message) => _findings.Add(new(Severity.Debt, check, message));

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.[a-z0-9]+(?:-[a-z0-9]+)*)+$")]
    private static partial Regex FeatureIdRegex();
}
