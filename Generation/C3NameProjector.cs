using System.Text;

namespace WinmnDump.Generation;

public sealed class C3NameProjector
{
    private readonly Dictionary<string, string> _typeOverrides = new(StringComparer.Ordinal)
    {
        ["ATOM"] = "Atom",
        ["BOOL"] = "Bool",
        ["COLORREF"] = "ColorRef",
        ["DWORD"] = "DWord",
        ["HANDLE"] = "Handle",
        ["HBRUSH"] = "HBrush",
        ["HCURSOR"] = "HCursor",
        ["HDC"] = "HDc",
        ["HICON"] = "HIcon",
        ["HINSTANCE"] = "HInstance",
        ["HMENU"] = "HMenu",
        ["HMODULE"] = "HModule",
        ["HWND"] = "HWnd",
        ["LPARAM"] = "LParam",
        ["LPCSTR"] = "LpcStr",
        ["LPCWSTR"] = "LpcWStr",
        ["LPSTR"] = "LpStr",
        ["LPWSTR"] = "LpWStr",
        ["LRESULT"] = "LResult",
        ["MSG"] = "Msg",
        ["POINT"] = "Point",
        ["RECT"] = "Rect",
        ["UINT"] = "UInt",
        ["ULONG_PTR"] = "ULongPtr",
        ["WNDCLASS_STYLES"] = "WndClassStyles",
        ["WNDCLASSEXA"] = "WndClassExA",
        ["WNDCLASSEXW"] = "WndClassExW",
        ["WNDCLASSA"] = "WndClassA",
        ["WNDCLASSW"] = "WndClassW",
        ["WNDPROC"] = "WndProc",
        ["WPARAM"] = "WParam"
    };

    private readonly Dictionary<string, string> _originalToC3Type = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _c3TypeToOriginal = new(StringComparer.Ordinal);

    public string TypeName(string original)
    {
        original = LastNameSegment(original);

        if (_originalToC3Type.TryGetValue(original, out var existing))
            return existing;

        var projected = _typeOverrides.TryGetValue(original, out var value)
            ? value
            : FallbackTypeName(original);

        RegisterTypeName(original, projected);
        return projected;
    }

    public string FunctionName(string original) => LowerFirst(LastNameSegment(original));

    public string FieldName(string original) => LowerFirst(SanitizeIdentifier(original, "field"));

    public string ParameterName(string original) => LowerFirst(SanitizeIdentifier(original, "param"));

    public string ConstantName(string original)
    {
        var name = SanitizeIdentifier(original, "constant");
        return name.ToUpperInvariant();
    }

    private void RegisterTypeName(string original, string projected)
    {
        if (_c3TypeToOriginal.TryGetValue(projected, out var otherOriginal) && otherOriginal != original)
        {
            throw new InvalidOperationException(
                $"C3 type name collision: {original} and {otherOriginal} both project to {projected}");
        }

        _originalToC3Type[original] = projected;
        _c3TypeToOriginal[projected] = original;
    }

    private static string FallbackTypeName(string original)
    {
        var parts = original
            .Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ToPascalPart);

        var name = string.Concat(parts);
        if (string.IsNullOrEmpty(name))
            name = "Win32Type";

        if (!char.IsUpper(name[0]))
            name = char.ToUpperInvariant(name[0]) + name[1..];

        if (!name.Any(char.IsLower))
            name += "Type";

        return name;
    }

    private static string ToPascalPart(string value)
    {
        if (value.Length == 0)
            return value;

        if (value.All(char.IsUpper))
            value = value.ToLowerInvariant();

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string LowerFirst(string value)
    {
        value = SanitizeIdentifier(value, "item");
        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string SanitizeIdentifier(string value, string fallback)
    {
        value = LastNameSegment(value);
        var sb = new StringBuilder();

        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch == '_')
                sb.Append(ch);
        }

        if (sb.Length == 0)
            sb.Append(fallback);

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }

    private static string LastNameSegment(string value)
    {
        var genericIndex = value.IndexOf('<', StringComparison.Ordinal);
        if (genericIndex >= 0)
            value = value[..genericIndex];

        var index = value.LastIndexOf('.');
        return index >= 0 ? value[(index + 1)..] : value;
    }
}
