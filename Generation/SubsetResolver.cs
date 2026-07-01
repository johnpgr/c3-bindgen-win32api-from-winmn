using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class SubsetResolver
{
    public SubsetResult Resolve(ApiDatabase api, SubsetSpec spec)
    {
        var neededTypes = new HashSet<string>(StringComparer.Ordinal);
        var neededFunctions = new HashSet<string>(StringComparer.Ordinal);
        var neededConstants = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var queue = new Queue<(string Kind, string Name)>();

        SeedFromNamespaces(api, spec, neededTypes, neededFunctions, neededConstants);

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
            else if (kind == "constant")
            {
                AddType(api.Constants[name].Type);
            }
        }

        ApplyExcludes(api, spec, neededTypes, neededFunctions, neededConstants);

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

    private static void SeedFromNamespaces(
        ApiDatabase api,
        SubsetSpec spec,
        HashSet<string> neededTypes,
        HashSet<string> neededFunctions,
        HashSet<string> neededConstants)
    {
        if (spec.Namespaces.Count == 0)
            return;

        foreach (var (name, function) in api.Functions)
        {
            if (Allows(spec, function.Namespace, IdentifierKind.Function, name))
                neededFunctions.Add(name);
        }

        foreach (var (name, type) in api.Types)
        {
            if (type.Kind != ApiTypeKind.Class && Allows(spec, type.Namespace, IdentifierKind.Type, name))
                neededTypes.Add(name);
        }

        foreach (var (name, constant) in api.Constants)
        {
            if (Allows(spec, constant.Namespace, IdentifierKind.Constant, name))
                neededConstants.Add(name);
        }
    }

    private static void ApplyExcludes(
        ApiDatabase api,
        SubsetSpec spec,
        HashSet<string> neededTypes,
        HashSet<string> neededFunctions,
        HashSet<string> neededConstants)
    {
        neededTypes.RemoveWhere(name =>
            api.Types.TryGetValue(name, out var type) &&
            Excludes(spec, type.Namespace, IdentifierKind.Type, name));
        neededFunctions.RemoveWhere(name =>
            api.Functions.TryGetValue(name, out var function) &&
            Excludes(spec, function.Namespace, IdentifierKind.Function, name));
        neededConstants.RemoveWhere(name =>
            api.Constants.TryGetValue(name, out var constant) &&
            Excludes(spec, constant.Namespace, IdentifierKind.Constant, name));
    }

    private static bool Allows(SubsetSpec spec, string ns, IdentifierKind kind, string name)
    {
        return MatchingNamespaceSpecs(spec, ns)
            .Any(namespaceSpec => FilterFor(namespaceSpec, kind).Allows(name));
    }

    private static bool Excludes(SubsetSpec spec, string ns, IdentifierKind kind, string name)
    {
        return MatchingNamespaceSpecs(spec, ns)
            .Any(namespaceSpec => FilterFor(namespaceSpec, kind).Excludes(name));
    }

    private static IEnumerable<NamespaceSpec> MatchingNamespaceSpecs(SubsetSpec spec, string ns)
    {
        foreach (var (namespaceName, namespaceSpec) in spec.Namespaces)
        {
            if (ns.Equals(namespaceName, StringComparison.Ordinal) ||
                ns.StartsWith(namespaceName + ".", StringComparison.Ordinal))
            {
                yield return namespaceSpec;
            }
        }
    }

    private static IdentifierFilter FilterFor(NamespaceSpec namespaceSpec, IdentifierKind kind)
    {
        return kind switch
        {
            IdentifierKind.Function => namespaceSpec.Functions,
            IdentifierKind.Type => namespaceSpec.Types,
            IdentifierKind.Constant => namespaceSpec.Constants,
            _ => IdentifierFilter.Empty
        };
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

    private enum IdentifierKind
    {
        Function,
        Type,
        Constant
    }
}
