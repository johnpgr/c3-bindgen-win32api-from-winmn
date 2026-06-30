using WinmnDump.Generation;
using WinmnDump.Model;

namespace WinmnDump.Tests;

internal static class SelfTests
{
    public static void Run()
    {
        TestNameProjection();
        TestTypeMapping();
        TestResolverAndEmitter();
        Console.Error.WriteLine("self-tests passed");
    }

    private static void TestNameProjection()
    {
        var names = new C3NameProjector();

        Equal("WndClassExW", names.TypeName("WNDCLASSEXW"));
        Equal("HWnd", names.TypeName("Windows.Win32.Foundation.HWND"));
        Equal("registerClassExW", names.FunctionName("RegisterClassExW"));
        Equal("lpWndClass", names.ParameterName("lpWndClass"));
        Equal("WS_VISIBLE", names.ConstantName("WS_VISIBLE"));
    }

    private static void TestTypeMapping()
    {
        var mapper = new C3TypeMapper(new C3NameProjector());

        Equal("uint", mapper.Map("u32"));
        Equal("HWnd*", mapper.Map("Windows.Win32.Foundation.HWND*"));
        Equal("ushort*", mapper.Map("Windows.Win32.Foundation.PCWSTR"));
        Equal("Rect*", mapper.Map("Windows.Win32.Foundation.RECT&"));
    }

    private static void TestResolverAndEmitter()
    {
        var api = new ApiDatabase();

        api.Types["BOOL"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "BOOL",
            Kind = ApiTypeKind.Alias,
            AliasTarget = "i32"
        };
        api.Types["HWND"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "HWND",
            Kind = ApiTypeKind.Handle,
            AliasTarget = "void*"
        };
        api.Types["RECT"] = new ApiType
        {
            Namespace = "Windows.Win32.Foundation",
            OriginalName = "RECT",
            Kind = ApiTypeKind.Struct
        };
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "left", Type = "i32" });
        api.Types["RECT"].Fields.Add(new ApiField { OriginalName = "top", Type = "i32" });

        var fn = new ApiFunction
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "GetWindowRect",
            ReturnType = "Windows.Win32.Foundation.BOOL",
            ImportModule = "USER32.dll"
        };
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "hWnd",
            Type = "Windows.Win32.Foundation.HWND"
        });
        fn.Parameters.Add(new ApiParameter
        {
            OriginalName = "lpRect",
            Type = "Windows.Win32.Foundation.RECT*",
            Direction = ParamDirection.Out
        });
        api.Functions["GetWindowRect"] = fn;

        api.Constants["WS_VISIBLE"] = new ApiConstant
        {
            Namespace = "Windows.Win32.UI.WindowsAndMessaging",
            OriginalName = "WS_VISIBLE",
            Type = "u32",
            Value = "268435456"
        };

        var spec = new SubsetSpec
        {
            Module = "win32",
            Functions = ["GetWindowRect"],
            Constants = ["WS_VISIBLE"]
        };

        var resolved = new SubsetResolver().Resolve(api, spec);
        True(resolved.Types.Contains("BOOL"), "return type dependency was not resolved");
        True(resolved.Types.Contains("HWND"), "parameter type dependency was not resolved");
        True(resolved.Types.Contains("RECT"), "pointer parameter dependency was not resolved");

        var names = new C3NameProjector();
        var output = new C3Emitter(names, new C3TypeMapper(names))
            .Emit(api, resolved, "win32");

        Contains(output, "module win32 @link(\"user32\");");
        Contains(output, "alias HWnd = void*;");
        Contains(output, "struct Rect");
        Contains(output, "const uint WS_VISIBLE = 268435456;");
        Contains(output, "@param [out] lpRect");
        Contains(output, "extern fn Bool getWindowRect(HWnd hWnd, Rect* lpRect)");
        Contains(output, "@cname(\"GetWindowRect\")");
    }

    private static void Equal(string expected, string actual)
    {
        if (expected != actual)
            throw new InvalidOperationException($"expected '{expected}', got '{actual}'");
    }

    private static void True(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    private static void Contains(string haystack, string needle)
    {
        if (!haystack.Contains(needle, StringComparison.Ordinal))
            throw new InvalidOperationException($"missing expected output: {needle}");
    }
}
