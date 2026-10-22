using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RadioE45.ParityCheck;

/// <summary>Entry of <c>parity/features.yaml</c>.</summary>
public sealed class Feature
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Since { get; set; } = "";
    public string Page { get; set; } = "";
    public string? Viewmodel { get; set; }
    public FeatureStatus Status { get; set; } = new();
    public string? Notes { get; set; }
    /// <summary>Why <c>status.tv</c> is <c>limited</c> or <c>n/a</c> (required then); free-form hints otherwise.</summary>
    public string? TvNotes { get; set; }
}

public sealed class FeatureStatus
{
    public string Maui { get; set; } = "";
    public string Desktop { get; set; } = "";
    public string Web { get; set; } = "";
    /// <summary>Android TV / Google TV views (<c>RadioE45/Views/Tv</c>), docs/ANDROID-TV-ROADMAP.md §5.</summary>
    public string Tv { get; set; } = "";

    public IEnumerable<(string Platform, string Value)> All()
    {
        yield return ("maui", Maui);
        yield return ("desktop", Desktop);
        yield return ("web", Web);
        yield return ("tv", Tv);
    }

    public IEnumerable<(string Platform, string Value)> Uno()
    {
        yield return ("desktop", Desktop);
        yield return ("web", Web);
    }
}

/// <summary>Entry of <c>parity/pages.yaml</c>: a MAUI page and its Uno counterpart.</summary>
public sealed class PageMap
{
    /// <summary>Logical page name, referenced by <see cref="Feature.Page"/>.</summary>
    public string Page { get; set; } = "";
    public string Maui { get; set; } = "";
    /// <summary>Uno file path, or <c>n/a</c> when the page has no Uno counterpart (then <see cref="Reason"/> is required).</summary>
    public string Uno { get; set; } = "";
    public string? Reason { get; set; }
    public List<string> Viewmodels { get; set; } = [];
    /// <summary>Binding members or string keys used only by MAUI, each with a reason.</summary>
    public List<IgnoreEntry> Ignore { get; set; } = [];

    /// <summary>TV page path (<c>RadioE45/Views/Tv/...</c>), or <c>n/a</c> when the page has no TV counterpart (then <see cref="TvReason"/> is required).</summary>
    public string Tv { get; set; } = "";
    public string? TvReason { get; set; }
    /// <summary>Binding members or string keys used by the phone page and deliberately absent on TV (touch-only), each with a reason.</summary>
    public List<IgnoreEntry> IgnoreTv { get; set; } = [];

    public bool UnoNotApplicable => Uno == Status.NotApplicable;
    public bool TvNotApplicable => Tv == Status.NotApplicable;
}

public sealed class IgnoreEntry
{
    public string Name { get; set; } = "";
    public string Reason { get; set; } = "";
}

public static class Status
{
    public const string Done = "done";
    public const string Todo = "todo";
    public const string Limited = "limited";
    public const string NotApplicable = "n/a";

    public static readonly IReadOnlySet<string> Valid = new HashSet<string> { Done, Todo, Limited, NotApplicable };
}

public static class ManifestLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static List<T> Load<T>(string path) =>
        Deserializer.Deserialize<List<T>?>(File.ReadAllText(path)) ?? [];
}
