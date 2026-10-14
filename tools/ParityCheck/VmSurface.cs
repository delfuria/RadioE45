using System.Reflection;
using System.Text;

namespace RadioE45.ParityCheck;

/// <summary>
/// Public surface of the shared ViewModels (namespace <c>RadioE45.ViewModels</c> in RadioE45.Core),
/// one sorted line per member. Read by reflection because CommunityToolkit.Mvvm generates the
/// bindable properties and commands at compile time. RadioE45.Core.Tests uses it for the
/// snapshot test of <c>parity/vm-surface.txt</c>.
/// </summary>
public static class VmSurface
{
    public const string ViewModelNamespace = "RadioE45.ViewModels";

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IReadOnlyList<string> Generate(Assembly coreAssembly)
    {
        var lines = new List<string>();
        foreach (var type in GetViewModelTypes(coreAssembly))
        {
            var owner = type.Name;
            lines.Add($"{owner} : type");

            foreach (var p in type.GetProperties(Declared))
            {
                var accessors = new List<string>();
                if (p.GetMethod is { IsPublic: true }) accessors.Add("get");
                if (p.SetMethod is { IsPublic: true }) accessors.Add("set");
                lines.Add($"{owner}.{p.Name} : {Format(p.PropertyType)} {{ {string.Join("; ", accessors)}; }}{StaticSuffix(p.GetMethod ?? p.SetMethod)}");
            }

            foreach (var e in type.GetEvents(Declared))
                lines.Add($"{owner}.{e.Name} : event {Format(e.EventHandlerType!)}");

            foreach (var m in type.GetMethods(Declared).Where(m => !m.IsSpecialName))
            {
                var parameters = string.Join(", ", m.GetParameters().Select(x => Format(x.ParameterType)));
                lines.Add($"{owner}.{m.Name}({parameters}) : {Format(m.ReturnType)}{StaticSuffix(m)}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        return lines;
    }

    /// <summary>Simple names of the ViewModel types, used to validate the parity manifests.</summary>
    public static IReadOnlySet<string> GetViewModelNames(Assembly coreAssembly) =>
        GetViewModelTypes(coreAssembly).Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

    public static string ToText(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines) sb.Append(line).Append('\n');
        return sb.ToString();
    }

    /// <summary>Snapshot lines, ignoring blank lines and <c>#</c> comments.</summary>
    public static IReadOnlyList<string> Parse(string text) =>
        text.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

    private static IEnumerable<Type> GetViewModelTypes(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types
            .OfType<Type>()
            .Where(t => t.Namespace == ViewModelNamespace && !t.IsNested)
            .OrderBy(t => t.Name, StringComparer.Ordinal);
    }

    private static string StaticSuffix(MethodInfo? m) => m is { IsStatic: true } ? " static" : "";

    private static string Format(Type t)
    {
        if (t.IsByRef) return Format(t.GetElementType()!) + "&";
        if (t.IsArray) return Format(t.GetElementType()!) + "[]";
        if (Nullable.GetUnderlyingType(t) is { } underlying) return Format(underlying) + "?";
        if (Aliases.TryGetValue(t, out var alias)) return alias;
        if (t.IsGenericParameter) return t.Name;
        if (!t.IsGenericType) return t.Name;

        var name = t.Name[..t.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Format))}>";
    }

    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte",
        [typeof(short)] = "short", [typeof(int)] = "int", [typeof(long)] = "long",
        [typeof(float)] = "float", [typeof(double)] = "double", [typeof(decimal)] = "decimal",
        [typeof(string)] = "string", [typeof(object)] = "object", [typeof(char)] = "char",
    };
}
