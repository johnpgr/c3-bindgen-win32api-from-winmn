using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class SubsetResolver
{
    public SubsetResult Resolve(ApiDatabase api, SubsetSpec spec)
    {
        var neededTypes = new HashSet<string>(spec.Types, StringComparer.Ordinal);
        var neededFunctions = new HashSet<string>(spec.Functions, StringComparer.Ordinal);
        var neededConstants = new HashSet<string>(spec.Constants, StringComparer.Ordinal);
        var warnings = new List<string>();
        var queue = new Queue<(string Kind, string Name)>();

        foreach (var fn in neededFunctions)
            queue.Enqueue(("function", fn));

        foreach (var type in neededTypes)
            queue.Enqueue(("type", type));

        foreach (var constant in neededConstants)
            queue.Enqueue(("constant", constant));

        while (queue.Count > 0)
        {
            var (kind, name) = queue.Dequeue();

            if (kind == "function")
            {
                if (!api.Functions.TryGetValue(name, out var fn))
                {
                    warnings.Add($"missing function: {name}");
                    continue;
                }

                AddType(fn.ReturnType);
                foreach (var p in fn.Parameters)
                    AddType(p.Type);
            }
            else if (kind == "type")
            {
                if (!api.Types.TryGetValue(name, out var type))
                {
                    warnings.Add($"missing type: {name}");
                    continue;
                }

                if (type.AliasTarget is not null)
                    AddType(type.AliasTarget);

                foreach (var field in type.Fields)
                    AddType(field.Type);

                if (type.DelegateSignature is not null)
                {
                    AddType(type.DelegateSignature.ReturnType);
                    foreach (var p in type.DelegateSignature.Parameters)
                        AddType(p.Type);
                }
            }
            else if (kind == "constant" && !api.Constants.ContainsKey(name))
            {
                warnings.Add($"missing constant: {name}");
            }
        }

        return new SubsetResult(
            SortByNamespaceThenName(api.Types, neededTypes),
            SortByNamespaceThenName(api.Functions, neededFunctions),
            SortByNamespaceThenName(api.Constants, neededConstants),
            warnings);

        void AddType(string rawType)
        {
            if (C3TypeMapper.IsPrimitiveBase(rawType))
                return;

            var baseType = C3TypeMapper.BaseTypeName(rawType);
            if (!api.Types.ContainsKey(baseType))
                return;

            if (neededTypes.Add(baseType))
                queue.Enqueue(("type", baseType));
        }
    }

    private static List<string> SortByNamespaceThenName<T>(Dictionary<string, T> source, HashSet<string> names)
    {
        return names
            .OrderBy(name => source.TryGetValue(name, out var value) ? NamespaceOf(value) : "")
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string NamespaceOf<T>(T value)
    {
        return value switch
        {
            ApiType type => type.Namespace,
            ApiFunction function => function.Namespace,
            ApiConstant constant => constant.Namespace,
            _ => ""
        };
    }
}
