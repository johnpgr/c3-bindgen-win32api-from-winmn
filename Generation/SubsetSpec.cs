using System.Text.Json;

namespace WinmnDump.Generation;

public sealed class SubsetSpec
{
    public string Module { get; set; } = "win32";
    public Dictionary<string, NamespaceSpec> Namespaces { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> TypeNameOverrides { get; } = new(StringComparer.Ordinal);

    public static SubsetSpec Load(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        var spec = new SubsetSpec();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("module"))
            {
                spec.Module = property.Value.GetString() ?? spec.Module;
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
                continue;

            var namespaceSpec = NamespaceSpec.FromJson(property.Value);
            spec.Namespaces[property.Name] = namespaceSpec;

            foreach (var (originalName, c3Name) in namespaceSpec.TypeNameOverrides)
                spec.TypeNameOverrides[originalName] = c3Name;
        }

        return spec;
    }
}

public sealed class NamespaceSpec
{
    public IdentifierFilter Functions { get; set; } = IdentifierFilter.Empty;
    public IdentifierFilter Types { get; set; } = IdentifierFilter.Empty;
    public IdentifierFilter Constants { get; set; } = IdentifierFilter.Empty;
    public Dictionary<string, string> TypeNameOverrides { get; } = new(StringComparer.Ordinal);

    public static NamespaceSpec FromJson(JsonElement element)
    {
        var spec = new NamespaceSpec();

        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("functions"))
                spec.Functions = IdentifierFilter.FromJson(property.Value);
            else if (property.NameEquals("types"))
                spec.Types = IdentifierFilter.FromJson(property.Value);
            else if (property.NameEquals("constants"))
                spec.Constants = IdentifierFilter.FromJson(property.Value);
            else if (property.NameEquals("typeNameOverrides") && property.Value.ValueKind == JsonValueKind.Object)
                ReadTypeNameOverrides(spec, property.Value);
        }

        return spec;

        static void ReadTypeNameOverrides(NamespaceSpec spec, JsonElement value)
        {
            foreach (var overrideProperty in value.EnumerateObject())
            {
                if (overrideProperty.Value.ValueKind == JsonValueKind.String)
                    spec.TypeNameOverrides[overrideProperty.Name] = overrideProperty.Value.GetString() ?? "";
            }
        }
    }
}

public sealed class IdentifierFilter
{
    public static IdentifierFilter Empty => new();

    public List<string> Include { get; } = [];
    public List<string> Exclude { get; } = [];

    public bool HasIncludes => Include.Count > 0;

    public bool Allows(string name)
    {
        return HasIncludes &&
            Include.Any(pattern => WildcardMatch(name, pattern)) &&
            !Excludes(name);
    }

    public bool Excludes(string name)
    {
        return Exclude.Any(pattern => WildcardMatch(name, pattern));
    }

    public static IdentifierFilter FromJson(JsonElement element)
    {
        var filter = new IdentifierFilter();

        if (element.ValueKind == JsonValueKind.True)
        {
            filter.Include.Add("*");
            return filter;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            filter.Include.Add(element.GetString() ?? "");
            return filter;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            AddStrings(filter.Include, element);
            return filter;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return filter;

        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("include") || property.NameEquals("allow"))
                AddStrings(filter.Include, property.Value);
            else if (property.NameEquals("exclude") ||
                     property.NameEquals("disallow") ||
                     property.NameEquals("notInclude"))
                AddStrings(filter.Exclude, property.Value);
        }

        return filter;
    }

    internal static bool WildcardMatch(string value, string pattern)
    {
        var valueIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                valueIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = valueIndex;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }

    private static void AddStrings(List<string> target, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                target.Add(value);
            return;
        }

        if (element.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                target.Add(value);
        }
    }
}

public sealed record SubsetResult(
    List<string> Types,
    List<string> Functions,
    List<string> Constants,
    List<string> Warnings);
