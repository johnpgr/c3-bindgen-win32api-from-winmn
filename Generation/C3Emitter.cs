using System.Text;
using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class C3Emitter(C3NameProjector names, C3TypeMapper types)
{
    public string Emit(ApiDatabase api, SubsetResult subset, string moduleName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"module {moduleName}{EmitLinkAttributes(api, subset)};");
        sb.AppendLine();
        sb.AppendLine("// Generated from Windows.Win32.winmd. Original Win32 names are preserved with @cname/comments.");
        sb.AppendLine();

        foreach (var warning in subset.Warnings)
            sb.AppendLine($"// warning: {warning}");

        if (subset.Warnings.Count > 0)
            sb.AppendLine();

        EmitTypes(sb, api, subset);
        EmitConstants(sb, api, subset);
        EmitFunctions(sb, api, subset);

        return sb.ToString();
    }

    private static string EmitLinkAttributes(ApiDatabase api, SubsetResult subset)
    {
        var libraries = subset.Functions
            .Select(functionName => api.Functions.TryGetValue(functionName, out var fn) ? fn.ImportModule : null)
            .OfType<string>()
            .Where(module => !string.IsNullOrWhiteSpace(module))
            .Select(NormalizeLinkLibrary)
            .Where(library => !string.IsNullOrWhiteSpace(library))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(library => library, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return libraries.Count == 0
            ? ""
            : $" @link({string.Join(", ", libraries.Select(library => $"\"{Escape(library)}\""))})";
    }

    private static string NormalizeLinkLibrary(string module)
    {
        var name = Path.GetFileNameWithoutExtension(module.Trim());
        return name.ToLowerInvariant();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void EmitTypes(StringBuilder sb, ApiDatabase api, SubsetResult subset)
    {
        foreach (var typeName in subset.Types)
        {
            if (!api.Types.TryGetValue(typeName, out var type))
                continue;

            switch (type.Kind)
            {
                case ApiTypeKind.Handle:
                    EmitHandle(sb, type);
                    break;
                case ApiTypeKind.Alias:
                    EmitAlias(sb, type);
                    break;
                case ApiTypeKind.Struct:
                    EmitStruct(sb, type);
                    break;
                case ApiTypeKind.Enum:
                    EmitEnumAlias(sb, type);
                    break;
                case ApiTypeKind.Delegate:
                    EmitDelegate(sb, type);
                    break;
            }

            sb.AppendLine();
        }
    }

    private void EmitConstants(StringBuilder sb, ApiDatabase api, SubsetResult subset)
    {
        foreach (var constantName in subset.Constants)
        {
            if (!api.Constants.TryGetValue(constantName, out var constant))
                continue;

            sb.AppendLine($"// Win32 original: {constant.OriginalName}");
            sb.AppendLine($"const {types.Map(constant.Type)} {names.ConstantName(constant.OriginalName)} = {constant.Value};");
            sb.AppendLine();
        }
    }

    private void EmitFunctions(StringBuilder sb, ApiDatabase api, SubsetResult subset)
    {
        foreach (var functionName in subset.Functions)
        {
            if (!api.Functions.TryGetValue(functionName, out var fn))
                continue;

            EmitFunction(sb, fn);
            sb.AppendLine();
        }
    }

    private void EmitHandle(StringBuilder sb, ApiType type)
    {
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {names.TypeName(type.OriginalName)} = void*;");
    }

    private void EmitAlias(StringBuilder sb, ApiType type)
    {
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {names.TypeName(type.OriginalName)} = {types.Map(type.AliasTarget ?? type.AbiType ?? "void*")};");
    }

    private void EmitEnumAlias(StringBuilder sb, ApiType type)
    {
        var valueField = type.Fields.FirstOrDefault(f => f.OriginalName == "value__");
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {names.TypeName(type.OriginalName)} = {types.Map(valueField?.Type ?? "i32")};");
    }

    private void EmitStruct(StringBuilder sb, ApiType type)
    {
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"struct {names.TypeName(type.OriginalName)}");
        sb.AppendLine("{");

        foreach (var field in type.Fields)
        {
            if (field.OriginalName == "value__")
                continue;

            sb.AppendLine($"    {types.Map(field.Type)} {names.FieldName(field.OriginalName)};");
        }

        sb.AppendLine("}");
    }

    private void EmitDelegate(StringBuilder sb, ApiType type)
    {
        if (type.DelegateSignature is null)
            return;

        var returnType = types.Map(type.DelegateSignature.ReturnType);
        var parameters = type.DelegateSignature.Parameters
            .Select(p => $"{types.Map(p.Type)} {names.ParameterName(p.OriginalName)}");

        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {names.TypeName(type.OriginalName)} = fn {returnType}({string.Join(", ", parameters)});");
    }

    private void EmitFunction(StringBuilder sb, ApiFunction fn)
    {
        var contract = EmitContract(fn.Parameters);
        if (!string.IsNullOrWhiteSpace(contract))
            sb.Append(contract);

        var returnType = types.Map(fn.ReturnType);
        var functionName = names.FunctionName(fn.OriginalName);
        var parameters = fn.Parameters
            .Select(p => $"{types.Map(p.Type)} {names.ParameterName(p.OriginalName)}");

        sb.AppendLine($"extern fn {returnType} {functionName}({string.Join(", ", parameters)})");
        sb.AppendLine($"    @cname(\"{fn.OriginalName}\");");
    }

    private string EmitContract(List<ApiParameter> parameters)
    {
        var lines = new List<string>();

        foreach (var p in parameters)
        {
            var annotation = ParamAnnotation(p);
            if (annotation is not null)
                lines.Add($" @param {annotation} {names.ParameterName(p.OriginalName)}");
        }

        return lines.Count == 0 ? "" : "<*\n" + string.Join("\n", lines) + "\n*>\n";
    }

    private static string? ParamAnnotation(ApiParameter p)
    {
        if (!C3TypeMapper.IsPointerLike(p.Type))
            return null;

        var direction = p.Direction switch
        {
            ParamDirection.In => "in",
            ParamDirection.Out => "out",
            ParamDirection.InOut => "inout",
            _ => ""
        };

        if (direction == "")
            return null;

        return p.NonNull ? $"[&{direction}]" : $"[{direction}]";
    }
}
